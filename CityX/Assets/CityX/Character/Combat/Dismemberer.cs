using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A body taken apart (docs/DISMEMBERMENT.md): by the blow that killed it, when it did way more than the body had left, and by blows on
    /// its corpse, each part coming off once the blows on it add up to what it can take. <see cref="DismemberRules"/> decides; this shows it:
    /// a cut part is cut out of every skinned mesh on the body (<see cref="MeshSevering"/>) and flies off as a piece of its own with what rides
    /// its bones; a burst part breaks into pieces of itself with what was inside it, and a burst torso takes all that is left (Dismemberer.Burst.cs).
    /// The body keeps a bleeding stump. Revived, it is whole again.
    ///
    /// Decided where the body is (the owner's machine, or the only one; a blow on another player's copy of a corpse is forwarded there as any
    /// blow is); other players' copies are told what went, where and which way, and that it is whole again on revival
    /// (<see cref="IReplicatedAbility"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class Dismemberer : MonoBehaviour, IReplicatedAbility
    {
        public const string Id = "dismemberment";

        /// <summary>How fast a cut-off part leaves, along the blow and up, m/s, and how fast it tumbles, radians a second.</summary>
        public const float PieceSpeed = 3.5f;

        public const float PieceUpSpeed = 2.5f;
        public const float PieceSpin = 7.0f;

        /// <summary>A cut-off part's weight, kg: a head, an arm, a leg.</summary>
        public const float HeadMass = 4.0f;

        public const float ArmMass = 3.0f;
        public const float LegMass = 8.0f;

        /// <summary>
        /// A stump's radius when the cut has no edge to measure, m: the head (a mesh of its own, sitting on the shoulders, so the skin is not
        /// cut at all) and a limb of a body without a skin of its own.
        /// </summary>
        public const float HeadStumpRadius = 0.12f;

        public const float FallbackStumpRadius = 0.09f;

        /// <summary>
        /// A cut-off bone's ragdoll body still hangs on its joint (it must be whole again on revival), this light and with no collider, so the
        /// invisible limb neither props the body up nor drags it. It is always the end of a chain, where a light link is stable.
        /// </summary>
        public const float CutBodyMass = 0.1f;

        /// <summary>
        /// And held from spinning, rad/s and its damping: weightless and with nothing to collide with, a cut-off arm's bones spun at 46 turns a second, sweeping
        /// whatever light rode the hand round like a strobe (owner, 2026-11-05: "flashing lights ... like a flashlight type brightness").
        /// </summary>
        public const float CutBodyMostSpin = 20.0f;

        public const float CutBodyAngularDamping = 5.0f;

        string IReplicatedAbility.AbilityId => Id;

        public event System.Action<AbilityEvent> Fired;

        private PlayerVitals _vitals;
        private Animator _animator;
        private NetworkObject _network;
        private readonly List<(SkinnedMeshRenderer renderer, Mesh whole, Mesh kept)> _cut = new List<(SkinnedMeshRenderer, Mesh, Mesh)>();
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private readonly List<Collider> _collidersOff = new List<Collider>();
        private readonly List<(Rigidbody body, float mass, float damping, float mostSpin)> _lightened = new List<(Rigidbody, float, float, float)>();

        /// <summary>Lights riding a part that is gone (a flashlight's beam, a burning blade's glow), off until the body is whole again.</summary>
        private readonly List<Light> _lightsOff = new List<Light>();
        private readonly List<GameObject> _stumps = new List<GameObject>();
        private const int Parts = (int)BodyPart.RightLowerLeg + 1;
        private readonly bool[] _gone = new bool[Parts];
        private readonly float[] _corpseDamage = new float[Parts];
        private int _goneCount;

        /// <summary>What the last dismemberment was, and of which part (tests and the console look at them).</summary>
        public Dismemberment Last { get; private set; }

        public BodyPart LastPart { get; private set; }

        /// <summary>The cut-off part flying off after the last cut (null after a burst), and how many pieces bursts have thrown.</summary>
        public GameObject LastPiece { get; private set; }

        public int GibsThrown { get; private set; }

        /// <summary>Nothing taken off.</summary>
        public bool IsWhole => _goneCount == 0;

        /// <summary>Whether <paramref name="part"/> is still on the body.</summary>
        public bool Has(BodyPart part) => !_gone[(int)part];

        /// <summary>
        /// Whether blows on <paramref name="target"/>, a corpse, can still take something off it: what lets a swing or a shot at a dead body
        /// reach it (owner: "if you just keep hitting or shooting the dead body then it will occur then as well").
        /// </summary>
        public static bool TakesCorpseHits(IDamageable target)
        {
            return target is Component body && body != null && body.TryGetComponent(out Dismemberer dismemberer) && dismemberer.Has(BodyPart.Torso);
        }

        private void Awake()
        {
            Bind();
        }

        /// <summary>Public and idempotent, for tests where Awake never runs.</summary>
        public void Bind()
        {
            _vitals = GetComponent<PlayerVitals>();
            _animator = GetComponentInChildren<Animator>();
            _network = GetComponent<NetworkObject>();
        }

        private void OnEnable()
        {
            if (_vitals != null)
            {
                _vitals.Died += OnDied;
                _vitals.Revived += OnRevived;
                _vitals.CorpseStruck += OnCorpseStruck;
            }
        }

        private void OnDisable()
        {
            if (_vitals != null)
            {
                _vitals.Died -= OnDied;
                _vitals.Revived -= OnRevived;
                _vitals.CorpseStruck -= OnCorpseStruck;
            }
        }

        private void OnCorpseStruck(Blow blow)
        {
            StrikeCorpse(blow, _vitals.MaxHealth);
        }

        /// <summary>Another player's copy: it never decides, it is told.</summary>
        private bool IsCopy => _network != null && _network.IsSpawned && !_network.IsOwner;

        private void OnDied()
        {
            if (!IsCopy)
            {
                TakeApart(_vitals.LastKillingBlow, _vitals.MaxHealth);
            }
        }

        private void OnRevived()
        {
            if (IsWhole)
            {
                return;
            }
            Restore();
            Fired?.Invoke(new AbilityEvent((int)Dismemberment.None, transform.position, Vector3.zero));
        }

        /// <summary>
        /// The blow that killed this body: takes off the part it hit if it did way more than the body had left, shows it, and tells every
        /// other player; a blow that did less leaves its excess on the corpse's part. Public for a body without PlayerVitals (a civilian) and
        /// for tests.
        /// </summary>
        public void TakeApart(Blow blow, float maxHealth)
        {
            if (blow.Bloodless || _animator == null || !_animator.isHuman)
            {
                return;
            }
            BodyPart part = PartHit(blow, out Vector3 point);
            Dismemberment what = DismemberRules.Decide(blow, maxHealth, ArmourOn(part));
            if (what == Dismemberment.None)
            {
                _corpseDamage[(int)part] += Mathf.Max(0.0f, blow.Overkill);
                return;
            }
            Take(what == Dismemberment.Sever && part == BodyPart.Torso ? Dismemberment.Burst : what, part, point, blow.From);
        }

        /// <summary>
        /// A blow on this body's corpse: added to what the part it lands on has taken, and that part off (cut by an edge, burst by anything
        /// else) once it is as much as the part can take. Nothing from a bloodless blow, or on another player's copy (its owner decides, sent
        /// the blow as every blow on a copy is). Public for a body without PlayerVitals (a civilian) and for tests.
        /// </summary>
        public void StrikeCorpse(Blow blow, float maxHealth)
        {
            if (blow.Bloodless || IsCopy || !Has(BodyPart.Torso) || _animator == null || !_animator.isHuman || float.IsNaN(blow.Damage))
            {
                return;
            }
            BodyPart part = PartHit(blow, out Vector3 point);
            _corpseDamage[(int)part] += Mathf.Max(0.0f, blow.Damage);
            if (_corpseDamage[(int)part] >= DismemberRules.Toughness(part, maxHealth, ArmourOn(part)))
            {
                Take(blow.Edged && part != BodyPart.Torso ? Dismemberment.Sever : Dismemberment.Burst, part, point, blow.From);
            }
        }

        /// <summary>The part of those still on that a blow lands on: the nearest to where it met the body; an edge on the torso, the joint nearest.</summary>
        private BodyPart PartHit(Blow blow, out Vector3 point)
        {
            point = blow.Contact ?? Chest();
            BodyPart part = DismemberRules.PartAt(point, Bone, Has);
            return blow.Edged ? DismemberRules.CutAt(part, point, Bone, Has) : part;
        }

        /// <summary>Takes <paramref name="part"/> off here and tells every other player, thrown away from <paramref name="from"/>.</summary>
        private void Take(Dismemberment what, BodyPart part, Vector3 point, Vector3 from)
        {
            Vector3 away = point - from;
            away.y = 0.0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;
            int seed = Random.Range(1, SeedRange);
            if (Show(what, part, away, seed))
            {
                // The seed goes with it, so every screen throws the same pieces.
                Fired?.Invoke(new AbilityEvent((int)what, point, away, seed, 0.0f, (int)part));
            }
        }

        /// <summary>The owner's machine took its body apart (or made it whole): this copy follows. A packet off the wire: only what it may be.</summary>
        public void PlayRemote(AbilityEvent evt)
        {
            if (evt.Kind == (int)Dismemberment.None)
            {
                Restore();
                return;
            }
            if ((evt.Kind != (int)Dismemberment.Sever && evt.Kind != (int)Dismemberment.Burst)
                || evt.N < (int)BodyPart.Torso || evt.N > (int)BodyPart.RightLowerLeg)
            {
                return;
            }
            Vector3 away = evt.Direction.sqrMagnitude > 1e-6f ? evt.Direction.normalized : -transform.forward;
            Show((Dismemberment)evt.Kind, (BodyPart)evt.N, away, SeedOf(evt.A));
        }

        private Vector3? Bone(HumanBodyBones bone)
        {
            Transform found = _animator != null ? _animator.GetBoneTransform(bone) : null;
            return found != null ? found.position : (Vector3?)null;
        }

        private Vector3 Chest()
        {
            return Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.Spine) ?? transform.position + Vector3.up * 1.5f;
        }

        /// <summary>
        /// Takes <paramref name="part"/> off this body, cut clean or burst (the torso: all that is left), thrown along <paramref name="away"/>;
        /// the same <paramref name="seed"/> throws the same pieces. False when it is already gone.
        /// </summary>
        public bool Show(Dismemberment what, BodyPart part, Vector3 away, int seed = 0)
        {
            if (what == Dismemberment.None || !Has(part) || _animator == null)
            {
                return false;
            }
            if (part == BodyPart.Torso)
            {
                PopWornOff(transform, away);
                Gone(part, Dismemberment.Burst);
                BurstWhole(away, seed);
                return true;
            }
            Transform root = _animator.GetBoneTransform(DismemberRules.RootOf(part));
            if (root == null || root.parent == null)
            {
                return false;
            }
            PopWornOff(root, away);
            Gone(part, what);
            var going = new HashSet<Transform>(root.GetComponentsInChildren<Transform>(true));
            GameObject piece = what == Dismemberment.Sever ? new GameObject($"{part} (cut off)") : null;
            var made = new List<Mesh>();
            var pieceBounds = new Bounds(root.position, Vector3.zero);
            var sources = new List<Gore.Source>();

            // The skin and the clothes skinned on the body: the triangles those bones carry come out (and, for a cut, fly off as the piece).
            Vector3 cutCentre = root.position;
            float cutRadius = 0.0f;
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.enabled || skin.sharedMesh == null || IsUnder(skin.transform, going))
                {
                    continue;
                }
                MeshSevering.Cut cut = CutFrom(skin, going);
                if (cut == null)
                {
                    continue;
                }
                _cut.Add((skin, skin.sharedMesh, cut.Kept));
                skin.sharedMesh = cut.Kept;
                if (skin.name == BodySurface.SkinName && cut.Radius > 0.0f)
                {
                    cutCentre = skin.transform.position + skin.transform.rotation * cut.Centre;
                    cutRadius = cut.Radius;
                }
                if (piece != null)
                {
                    made.Add(cut.Piece);
                    pieceBounds.Encapsulate(AddPart(piece.transform, cut.Piece, skin, skin.transform.position, skin.transform.rotation, Vector3.one));
                }
                else
                {
                    sources.Add(SourceOf(skin, cut.Piece, skin.transform.position, skin.transform.rotation, Vector3.one, LayerOf(skin)));
                    Destroy(cut.Piece);
                }
            }

            // What rides those bones (the skull, hair, a hat): a still copy goes with a cut-off part; a burst breaks the skull and hair with the
            // part and knocks anything else off whole. The original is hidden. Something held in a cut-off hand stays: the death drops it.
            int knocked = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !IsSolid(renderer) || HeldInHand(renderer.transform))
                {
                    continue;
                }
                if (piece != null)
                {
                    pieceBounds.Encapsulate(Freeze(renderer, piece.transform, made));
                }
                else
                {
                    knocked += BreakOrKnockOff(renderer, sources, away);
                }
                renderer.enabled = false;
                _hidden.Add(renderer);
            }

            LetGoOfBones(root);
            Vector3 outward = (root.position - root.parent.position).normalized;
            if (cutRadius <= 0.0f)
            {
                // The skin was not cut (the head sits on the shoulders as a mesh of its own): the stump goes on the skin where it met the part.
                cutCentre = NearestSkinPoint(root.position) ?? root.position;
                cutRadius = part == BodyPart.Head ? HeadStumpRadius : FallbackStumpRadius;
            }
            _stumps.Add(Gore.Stump(root.parent, cutCentre, outward, cutRadius, broken: piece == null && part != BodyPart.Head));
            Gore.Blood(cutCentre, away, part == BodyPart.Head ? 2.0f : 1.4f, transform);

            if (piece != null)
            {
                Gore.Stump(piece.transform, cutCentre, -outward, cutRadius);
                Launch(piece, pieceBounds, MassOf(part), away, Vector3.zero, made);
                LastPiece = piece;
                return true;
            }
            LastPiece = null;
            Vector3 middle = DismemberRules.Segment(part, Bone, out Vector3 start, out Vector3 end) ? (start + end) * 0.5f : cutCentre;
            GibsThrown += knocked + Gore.Burst(sources, GoreAnatomy.BitsOf(part), middle, away, BodyScale, part == BodyPart.Head ? HeadChunks : LimbChunks,
                seed, Gore.PartSmears, BodyColliders(), Carried());
            return true;
        }

        /// <summary><paramref name="part"/> and what it takes with it marked gone, and the last dismemberment noted.</summary>
        private void Gone(BodyPart part, Dismemberment what)
        {
            for (var other = BodyPart.Torso; other <= BodyPart.RightLowerLeg; other++)
            {
                if (DismemberRules.Takes(part, other) && !_gone[(int)other])
                {
                    _gone[(int)other] = true;
                    _goneCount++;
                }
            }
            Last = what;
            LastPart = part;
        }

        /// <summary>
        /// The body whole again (revived): every mesh, every hidden part, every collider and weight as it was; the stumps gone. What flew off
        /// stays where it fell, for a while. Undone newest first, so a mesh cut twice gets its first back; a mesh something else has put on a
        /// renderer since (a garment changed) is left as it is.
        /// </summary>
        public void Restore()
        {
            for (int i = _cut.Count - 1; i >= 0; i--)
            {
                (SkinnedMeshRenderer renderer, Mesh whole, Mesh kept) = _cut[i];
                if (renderer != null && renderer.sharedMesh == kept)
                {
                    renderer.sharedMesh = whole;
                }
                Destroy(kept);
            }
            _cut.Clear();
            foreach (Renderer renderer in _hidden)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            _hidden.Clear();
            foreach (Collider collider in _collidersOff)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                }
            }
            _collidersOff.Clear();
            for (int i = _lightened.Count - 1; i >= 0; i--)
            {
                (Rigidbody body, float mass, float damping, float mostSpin) = _lightened[i];
                if (body != null)
                {
                    body.mass = mass;
                    body.angularDamping = damping;
                    body.maxAngularVelocity = mostSpin;
                }
            }
            _lightened.Clear();
            foreach (Light light in _lightsOff)
            {
                if (light != null)
                {
                    light.enabled = true;
                }
            }
            _lightsOff.Clear();
            System.Array.Clear(_gone, 0, _gone.Length);
            System.Array.Clear(_corpseDamage, 0, _corpseDamage.Length);
            _goneCount = 0;
            foreach (GameObject stump in _stumps)
            {
                if (stump != null)
                {
                    Destroy(stump);
                }
            }
            _stumps.Clear();
            Last = Dismemberment.None;
            LastPiece = null;
        }

        private static MeshSevering.Cut CutFrom(SkinnedMeshRenderer skin, HashSet<Transform> going)
        {
            Transform[] bones = skin.bones;
            var cutOff = new HashSet<int>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && going.Contains(bones[i]))
                {
                    cutOff.Add(i);
                }
            }
            if (cutOff.Count == 0)
            {
                return null;
            }
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            if (weights == null || weights.Length != skin.sharedMesh.vertexCount)
            {
                return null;
            }
            var baked = new Mesh();
            skin.BakeMesh(baked, true);
            MeshSevering.Cut cut = MeshSevering.Split(skin.sharedMesh, baked, MeshSevering.Going(weights, cutOff));
            Destroy(baked);
            return cut;
        }

        /// <summary>
        /// A still mesh that looks like <paramref name="source"/>, under <paramref name="parent"/> at <paramref name="position"/>,
        /// <paramref name="rotation"/> and world <paramref name="scale"/>; its bounds.
        /// </summary>
        private static Bounds AddPart(Transform parent, Mesh mesh, Renderer source, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(source.name);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.transform.SetParent(parent, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = FrozenLook.WithoutSheen(source.sharedMaterials);
            var block = new MaterialPropertyBlock();
            source.GetPropertyBlock(block);
            renderer.SetPropertyBlock(block);
            renderer.shadowCastingMode = source.shadowCastingMode;
            return renderer.bounds;
        }

        /// <summary>
        /// A still copy of a renderer as it looks now (baked, if it is skinned), under <paramref name="parent"/>; its bounds (empty at the part's
        /// root when it could not be copied). A baked mesh is added to <paramref name="made"/>: the piece owns it.
        /// </summary>
        private static Bounds Freeze(Renderer source, Transform parent, List<Mesh> made)
        {
            Transform at = source.transform;
            if (source is SkinnedMeshRenderer skinned)
            {
                if (skinned.sharedMesh == null)
                {
                    return new Bounds(parent.position, Vector3.zero);
                }
                var baked = new Mesh { name = skinned.sharedMesh.name + " (still)" };
                skinned.BakeMesh(baked, true);
                made.Add(baked);
                return AddPart(parent, baked, source, at.position, at.rotation, Vector3.one);
            }
            var filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return new Bounds(parent.position, Vector3.zero);
            }
            return AddPart(parent, filter.sharedMesh, source, at.position, at.rotation, at.lossyScale);
        }

        /// <summary>The point of the body's own skin nearest <paramref name="point"/>, as it is posed now; null on a body without one.</summary>
        private Vector3? NearestSkinPoint(Vector3 point)
        {
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.name != BodySurface.SkinName || skin.sharedMesh == null)
                {
                    continue;
                }
                var baked = new Mesh();
                skin.BakeMesh(baked, true);
                Vector3[] vertices = baked.vertices;
                Destroy(baked);
                Transform at = skin.transform;
                Vector3 local = Quaternion.Inverse(at.rotation) * (point - at.position);
                int best = -1;
                float nearest = float.MaxValue;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float distance = (vertices[i] - local).sqrMagnitude;
                    if (distance < nearest)
                    {
                        nearest = distance;
                        best = i;
                    }
                }
                return best >= 0 ? at.position + at.rotation * vertices[best] : (Vector3?)null;
            }
            return null;
        }

        /// <summary>A renderer of something solid (not a spray, a trail or a rope).</summary>
        private static bool IsSolid(Renderer renderer)
        {
            return renderer is MeshRenderer || renderer is SkinnedMeshRenderer;
        }

        /// <summary>Something held in a hand (a sword, a gun, a shield): it hangs under a hand bone.</summary>
        private bool HeldInHand(Transform part)
        {
            Transform left = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform right = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            for (Transform at = part.parent; at != null && at != transform; at = at.parent)
            {
                if (at == left || at == right)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsUnder(Transform part, HashSet<Transform> bones)
        {
            for (Transform at = part; at != null; at = at.parent)
            {
                if (bones.Contains(at))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The cut-off bones' ragdoll bodies: no collider and almost no weight while they are gone (<see cref="CutBodyMass"/>).</summary>
        private void LetGoOfBones(Transform root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider.enabled)
                {
                    collider.enabled = false;
                    _collidersOff.Add(collider);
                }
            }
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Calm(body, CutBodyMass);
            }
            SwitchOffLights(root);
        }

        /// <summary>Held from spinning (and given <paramref name="mass"/>) while its part is gone; as it was again on revival.</summary>
        private void Calm(Rigidbody body, float mass)
        {
            _lightened.Add((body, body.mass, body.angularDamping, body.maxAngularVelocity));
            body.mass = mass;
            body.angularDamping = CutBodyAngularDamping;
            body.maxAngularVelocity = CutBodyMostSpin;
        }

        /// <summary>Every light under <paramref name="root"/> off with the part it rides.</summary>
        private void SwitchOffLights(Transform root)
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.enabled)
                {
                    light.enabled = false;
                    _lightsOff.Add(light);
                }
            }
        }

        private Collider[] BodyColliders() => GetComponentsInChildren<Collider>(true);

        /// <summary>
        /// A part leaves (cut off, blown off, knocked off): a body of <paramref name="mass"/> with a box round it, the blow's push and
        /// <paramref name="push"/> on top of the body's own speed, a tumble; it never knocks into the body it came off, and smears the ground
        /// where it lands. It lies a while, then shrinks away with the meshes made for it.
        /// </summary>
        private void Launch(GameObject piece, Bounds bounds, float mass, Vector3 away, Vector3 push, List<Mesh> made)
        {
            Transform pieceTransform = piece.transform;
            var children = new List<Transform>();
            foreach (Transform child in pieceTransform)
            {
                children.Add(child);
            }
            foreach (Transform child in children)
            {
                child.SetParent(null, true);
            }
            pieceTransform.position = bounds.center;
            foreach (Transform child in children)
            {
                child.SetParent(pieceTransform, true);
            }
            var box = piece.AddComponent<BoxCollider>();
            box.size = Vector3.Max(bounds.size, Vector3.one * 0.08f);
            var body = piece.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Carried() + away * PieceSpeed + Vector3.up * PieceUpSpeed + push;
            body.angularVelocity = Random.onUnitSphere * PieceSpin;
            foreach (Collider collider in BodyColliders())
            {
                Physics.IgnoreCollision(box, collider, true);
            }
            piece.AddComponent<GoreChunk>().Bind(new GoreChunk.Smears(1), BodyScale);
            piece.AddComponent<Debris>().Own(made);
        }
    }
}
