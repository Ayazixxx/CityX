using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE CHAIN (docs/CHAIN.md; owner, 2026-11-22: a chain "like delsin's", one-handed, "wrap around down to around the palm when he is just
    /// chilling"). Worn, not held: wound round the right wrist and on down round the palm (<see cref="ChainWrap"/>), the hand open, the body
    /// walking and standing as an empty-handed one does. A click throws it: the hand's coil comes off, the rest is paid out through the fist,
    /// and it is swung (<see cref="ChainLash"/>, SwordStrikes' chain attacks). A while after the last swing it is drawn back in and winds itself
    /// on again.
    ///
    /// It fights through SoulsSwordCombat as a blade does (<see cref="ISwordBlade"/>): what a swing hits with is the line the attack throws the
    /// chain along, its swung length long (<see cref="BladeTipWorld"/>), swept as a sword's blade is.
    ///
    /// EVERYTHING SHOWN IS WORKED OUT ON THE BODY IT IS ON, from what that body is doing (SwordPoseDriver plays the same attack on another
    /// player's copy), so a copy's chain comes off, swings and winds on with nothing sent (<see cref="ICosmeticMountable"/>).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [DefaultExecutionOrder(500)]   // after the body's own posing (PlayerLocomotion's LateUpdate): the links hang from the hand where this frame has it
    public sealed class ChainWeapon : MonoBehaviour, IEquippableItem, IEquippableWeapon, ISwordBlade, ICosmeticMountable, IHandGripStyle
    {
        public const float InteractRange = 2.5f;
        public const float LookDotThreshold = 0.82f;

        /// <summary>The chain is wound back on this long after the last swing, seconds.</summary>
        public const float RewindAfterSeconds = 2.5f;

        /// <summary>The hand's coil comes off over this long, and the rest is paid out of the fist over this long, seconds: within a swing's wind-up.</summary>
        public const float UnwindSeconds = 0.10f;

        public const float PayOutSeconds = 0.16f;

        /// <summary>Drawn back into the fist over this long, then wound onto the hand over this long, seconds.</summary>
        public const float PayInSeconds = 0.35f;

        public const float WindSeconds = 0.45f;

        /// <summary>The rest starts out of the fist once the hand's coil is this far off (0 to 1).</summary>
        public const float PayOutOnceUnwound = 0.5f;

        /// <summary>A link meeting the ground at least this fast throws sparks, m/s, and a blow at least this fast is a strike (a flash, a ring); no more often than this, seconds.</summary>
        public const float ScrapeFromSpeed = 9.0f;

        public const float StrikeFromSpeed = 22.0f;
        public const float SparksEverySeconds = 0.07f;

        /// <summary>A chain's blow on the ground is heard this much as loud as a sword's (owner, 2026-11-22: "lower it by 30%").</summary>
        public const float GroundStrikeLoudness = 0.7f;

        /// <summary>How far below the fist the ground is looked for, metres.</summary>
        public const float GroundProbeMetres = 40.0f;

        [SerializeField] private Transform[] _links;

        [SerializeField] private SkinnedMeshRenderer _skin;

        [SerializeField] private Material _steel;

        [SerializeField] private Material _hotSteel;

        /// <summary>The links' skin, its steel, and the same steel glowing hot (ChainBuilder sets them): the last is what it wears with the Fire Enhancement on.</summary>
        public void SetLook(SkinnedMeshRenderer skin, Material steel, Material hotSteel)
        {
            _skin = skin;
            _steel = steel;
            _hotSteel = hotSteel;
        }

        /// <summary>The links' bones, the wrist coil's first (ChainBuilder sets them): each is placed in the world every frame.</summary>
        public Transform[] Links { get => _links; set => _links = value; }

        private Transform _body;
        private Transform _hand;
        private Transform _forearm;
        private SwordPoseDriver _driver;
        private Ragdoll _ragdoll;
        private FireEnhancementAbility _fire;
        private ChainLash _lash;
        private float _lashScale;
        private bool _equipped;
        private bool _mounted;
        private SphereCollider _pickupTrigger;
        private readonly GroundPickup _pickup = new GroundPickup();

        private float _wound = 1.0f;
        private float _paidOut = ChainWrap.PalmLinks;
        private float _outLeft;
        private float _sparksIn;
        private Vector3 _thrown = Vector3.down;
        private bool _swinging;

        /// <summary>How far the hand's coil is wound on: 1 at rest, 0 swung.</summary>
        public float Wound => _wound;

        /// <summary>How many of the free length's links are out of the fist: <see cref="ChainWrap.PalmLinks"/> at rest, all of them swung.</summary>
        public float PaidOut => _paidOut;

        /// <summary>The chain is off the hand, or on its way off or on.</summary>
        public bool IsOut => _wound < 1.0f;

        /// <summary>The free length (null until it is mounted on a body).</summary>
        public ChainLash Lash => _lash;

        // ---- ISwordBlade: what a swing hits with ----

        public bool IsOneHanded => true;

        public bool IsChain => true;

        /// <summary>The fist.</summary>
        public Vector3 BladeBaseWorld => _hand != null ? _hand.TransformPoint(ChainWrap.GripInHand) : transform.position;

        /// <summary>Mid-swing, the end of the line the attack throws the chain along; else the chain's own far end.</summary>
        public Vector3 BladeTipWorld
        {
            get
            {
                if (_swinging)
                {
                    return BladeBaseWorld + _thrown * (ChainRules.SwungLength * BodyScale);
                }
                return _lash != null && IsOut ? _lash.At[_lash.Links - 1] : BladeBaseWorld + Vector3.down * 0.1f;
            }
        }

        /// <summary>
        /// The light trailing a swing is drawn behind the chain's outer half, where its links really are. Hot, it has none (both ends in one
        /// place): its trail is its embers (owner, 2026-11-22: "get rid of the white trail make its a longways trail of embers").
        /// </summary>
        public Vector3 TrailBaseWorld => _lash != null && IsOut && !IsHot ? _lash.At[_lash.Links / 2] : BladeBaseWorld;

        public Vector3 TrailTipWorld => _lash != null && IsOut && !IsHot ? _lash.At[_lash.Links - 1] : BladeBaseWorld;

        public float GetMeleeDamage() => ChainRules.BaseDamage;

        // ---- IHandGripStyle: an open hand under the coil, a fist on the chain ----

        public FingerAmounts StrongHandGrip => FingerAmounts.Lerp(FingerAmounts.Wrap(RestingCurl), FingerAmounts.Wrap(1.0f), 1.0f - _wound);

        public FingerAmounts SupportHandGrip => FingerAmounts.Open;

        /// <summary>How far the wrapped hand's fingers are curled, 0 to 1: loosely, as a resting hand's are.</summary>
        public const float RestingCurl = 0.25f;

        private float BodyScale => _body != null ? Mathf.Max(0.01f, _body.lossyScale.y) : 1.0f;

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        private void Update()
        {
            if (!_equipped && !_mounted && _pickup.Player != null)
            {
                TryPickup(_pickup.Player, InteractKey.Tapped);
            }
        }

        private void OnTriggerEnter(Collider other) => _pickup.Entered(other);

        private void OnTriggerExit(Collider other) => _pickup.Left(other);

        public bool TryPickup(Transform player, bool ePressed)
        {
            return ePressed && InteractionReach.CanReach(transform.position, player, InteractRange, LookDotThreshold, out _)
                && GroundPickup.Take(player, gameObject, ItemRegistry.Chain);
        }

        // ---- IEquippableWeapon ----

        public float MeleeCooldownSeconds => SwordWeapon.MeleeCooldownSecondsConst;

        public string MovesetId => ComboChain.Sword;

        public ComboStage? HeavyStage => null;

        public bool IsEdged => false;

        public void RegisterLandedMeleeHit()
        {
        }

        public bool CanThrow() => false;

        public float ThrowSpeed => 0.0f;

        public void Throw(GameObject player, Vector3 origin, Vector3 direction)
        {
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            Mount(player);
            _equipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }
            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null)
            {
                melee.EquippedWeapon = this;
            }
            var souls = player.GetComponent<SoulsSwordCombat>();
            if (souls == null)
            {
                souls = player.AddComponent<SoulsSwordCombat>();
            }
            souls.Equip(this);
        }

        public void OnUnequip(GameObject player)
        {
            var melee = player.GetComponent<MeleeAttacker>();
            if (melee != null && ReferenceEquals(melee.EquippedWeapon, this))
            {
                melee.EquippedWeapon = null;
            }
            var souls = player.GetComponent<SoulsSwordCombat>();
            if (souls != null)
            {
                souls.Unequip(this);
            }
            Destroy(gameObject);
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        // ---- on another player's copy: the same chain on the same arm, doing what that body does ----

        public void MountCosmetic(GameObject body)
        {
            Mount(body);
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        private void Mount(GameObject body)
        {
            _body = body.transform;
            Animator animator = body.GetComponentInChildren<Animator>();
            _hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            _forearm = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
            _driver = body.GetComponent<SwordPoseDriver>();
            _ragdoll = body.GetComponent<Ragdoll>();
            _fire = body.GetComponent<FireEnhancementAbility>();
            if (_hand != null)
            {
                transform.SetParent(_hand, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            _mounted = _hand != null && _forearm != null && _links != null && _links.Length == ChainWrap.TotalLinks;
            _wound = 1.0f;
            _paidOut = ChainWrap.PalmLinks;
            _outLeft = 0.0f;
            _lash = null;
        }

        private void LateUpdate()
        {
            if (!_mounted || _hand == null || _forearm == null)
            {
                return;
            }
            if (_driver == null)
            {
                _driver = _body.GetComponent<SwordPoseDriver>();   // (added with the first attack on some bodies)
            }
            float scale = BodyScale;
            if (_lash == null || Mathf.Abs(scale - _lashScale) > 0.01f * _lashScale)
            {
                _lash = new ChainLash(ChainWrap.FreeLinks, ChainWrap.Pitch * scale, ChainWrap.BarRadius * 2.0f * scale);
                _lashScale = scale;
                _lash.Gather(BladeBaseWorld);
            }
            Step(Time.deltaTime);
            PlaceLinks(scale);
            Glow(scale, Time.deltaTime);
        }

        /// <summary>The chain off, out, in and on again as the body swings and stops, and its free length moved on. Public so a test can step it without frames.</summary>
        public void Step(float deltaTime)
        {
            SwordStrikeKind kind = _driver != null ? _driver.Kind : SwordStrikeKind.ChainLash;
            _swinging = _driver != null && _driver.IsStriking && SwordStrikes.IsChain(kind);
            bool limp = _ragdoll != null && _ragdoll.IsLimp;
            if (_swinging)
            {
                _outLeft = RewindAfterSeconds;
            }
            else
            {
                _outLeft = limp ? 0.0f : _outLeft - deltaTime;
            }

            bool wasWound = _wound >= 1.0f;
            if (_outLeft > 0.0f)
            {
                _wound = Mathf.MoveTowards(_wound, 0.0f, deltaTime / UnwindSeconds);
                if (_wound <= PayOutOnceUnwound)
                {
                    _paidOut = Mathf.MoveTowards(_paidOut, ChainWrap.FreeLinks, (ChainWrap.FreeLinks - ChainWrap.PalmLinks) * deltaTime / PayOutSeconds);
                }
            }
            else if (_paidOut > ChainWrap.PalmLinks)
            {
                _paidOut = Mathf.MoveTowards(_paidOut, ChainWrap.PalmLinks, (ChainWrap.FreeLinks - ChainWrap.PalmLinks) * deltaTime / PayInSeconds);
            }
            else
            {
                _wound = Mathf.MoveTowards(_wound, 1.0f, deltaTime / WindSeconds);
            }
            if (_wound >= 1.0f)
            {
                return;   // all wound on: nothing hangs
            }

            Vector3 fist = BladeBaseWorld;
            if (wasWound)
            {
                // Coming off: the free length starts where it was wound, the links in the fist in the fist.
                _lash.Gather(fist);
                for (int slot = 0; slot < ChainWrap.PalmLinks; slot++)
                {
                    PalmSlot(slot, out Vector3 at, out _);
                    _lash.Hold(ChainWrap.FreeLinks - ChainWrap.PalmLinks + slot, at);
                }
            }

            float pull = 0.0f;
            if (_swinging)
            {
                SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(kind);
                float ahead = Mathf.Clamp01(_driver.Progress + ChainLash.ThrownAheadSeconds / timing.Total);
                _thrown = _body.rotation * SwordStrikes.Evaluate(kind, ahead).Blade;
                pull = ChainLash.Pull(timing.WindUp, timing.Hold, timing.Strike, timing.Follow, timing.Settle, _driver.Progress * timing.Total);
            }
            _lash.Step(fist, _thrown, pull, _paidOut, GroundUnder(fist), deltaTime);

            // The links still on the hand, or on their way off it or onto it, are where the coil has them.
            for (int slot = 0; slot < ChainWrap.PalmLinks; slot++)
            {
                float on = ChainWrap.Wound(_wound, slot);
                if (on > 0.0f)
                {
                    PalmSlot(slot, out Vector3 at, out _);
                    _lash.Draw(ChainWrap.FreeLinks - ChainWrap.PalmLinks + slot, at, Mathf.SmoothStep(0.0f, 1.0f, on));
                }
            }
            ThrowSparks(deltaTime);
        }

        private void ThrowSparks(float deltaTime)
        {
            _sparksIn -= deltaTime;
            // (Not in a wind-up: coming off the hand the chain drops to the ground at the feet before it is flung back.)
            bool windingUp = _swinging && _driver.Progress < SwordStrikes.StrikeStart(_driver.Kind);
            if (windingUp || _lash.GroundBlowSpeed < ScrapeFromSpeed || _sparksIn > 0.0f)
            {
                return;
            }
            _sparksIn = SparksEverySeconds;
            if (_lash.GroundBlowSpeed >= StrikeFromSpeed && _swinging)
            {
                BlockDeflectEffect.GroundStrike(_lash.GroundBlowAt, GroundStrikeLoudness);
                _sparksIn = SparksEverySeconds * 3.0f;
            }
            else
            {
                BlockDeflectEffect.Scrape(_lash.GroundBlowAt, _thrown);
            }
        }

        // ---- hot (the Fire Enhancement) ----

        /// <summary>
        /// The glow about the hot steel, from the links out: how wide each band of it is, metres, and its colour and strength. White-hot and
        /// narrow on the steel, orange about that, red and widest outside (owner, 2026-11-22: "i like how the chain is glowing lighter and the
        /// outside of it gets redder", "look at delsin's his is like white in the center", "but make it less spread than his more around the chain").
        /// </summary>
        public static readonly float[] GlowWidths = { 0.10f, 0.22f, 0.36f };

        public static readonly Color[] GlowColours =
        {
            new Color(1.0f, 0.93f, 0.72f, 0.9f), new Color(1.0f, 0.46f, 0.1f, 0.5f), new Color(1.0f, 0.13f, 0.03f, 0.32f),
        };

        /// <summary>The glow breathes this much, and this fast.</summary>
        public const float GlowFlicker = 0.12f;

        public const float GlowFlickerPerSecond = 9.0f;

        private Vector3[] _laidFrom;
        private LineRenderer[] _glow;
        private readonly List<Vector3> _shown = new List<Vector3>();
        private float _glowClock;

        /// <summary>The chain is glowing hot this frame.</summary>
        public bool IsHot { get; private set; }

        /// <summary>The embers are laid for each stretch of this many links (FireLook.EmberTrail).</summary>
        public const int LinksToAStretch = 4;

        /// <summary>
        /// With the Fire Enhancement on the body the steel glows, wound on or not (owner, 2026-11-22: "make it like glowing a bit and leave a
        /// trail of embers that fizzle out"): its own hot material, bands of light drawn along its links where they are this frame (left in the
        /// world, a glow trailed behind a chain that moves: owner, "shouldnt it be surrounding the chains not trailling behind?"), and behind
        /// the free length as it swings, a trail of embers (FireLook.EmberTrail). On every body the enhancement shows on, a copy included.
        /// </summary>
        private void Glow(float scale, float deltaTime)
        {
            bool hot = _fire != null && _fire.IsAblaze;
            if (hot != IsHot && _skin != null && _hotSteel != null)
            {
                _skin.sharedMaterial = hot ? _hotSteel : _steel;
            }
            if (hot && !IsHot)
            {
                ChainSounds.PlayIgnite(BladeBaseWorld);   // the chain catching fire, heard on every body it shows on
            }
            DrawGlow(hot, scale, deltaTime);
            if (hot && _wound < 1.0f && _lash != null)
            {
                Vector3[] at = _lash.At;
                if (_laidFrom == null)
                {
                    _laidFrom = (Vector3[])at.Clone();
                }
                for (int i = _lash.FirstOut(_paidOut); i < at.Length - 1; i += LinksToAStretch)
                {
                    int end = Mathf.Min(i + LinksToAStretch, at.Length - 1);
                    FireScene.LayEmbers(_laidFrom[i], _laidFrom[end], at[i], at[end], scale, deltaTime);
                }
                System.Array.Copy(at, _laidFrom, at.Length);
            }
            else
            {
                _laidFrom = null;
            }
            IsHot = hot;
        }

        /// <summary>The bands of light along the links that show, in their order down the chain.</summary>
        private void DrawGlow(bool hot, float scale, float deltaTime)
        {
            if (!hot)
            {
                if (_glow != null)
                {
                    foreach (LineRenderer band in _glow)
                    {
                        band.enabled = false;
                    }
                }
                return;
            }
            if (_glow == null)
            {
                _glow = new LineRenderer[GlowWidths.Length];
                for (int i = 0; i < _glow.Length; i++)
                {
                    var band = new GameObject("Glow" + i).AddComponent<LineRenderer>();
                    band.transform.SetParent(transform, false);
                    band.useWorldSpace = true;
                    band.alignment = LineAlignment.View;
                    band.textureMode = LineTextureMode.Stretch;
                    band.numCapVertices = 3;
                    band.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    band.receiveShadows = false;
                    band.sharedMaterial = SoftGlow.MakeBand("ChainGlow" + i, GlowColours[i]);
                    _glow[i] = band;
                }
            }
            _glowClock += deltaTime;
            float breath = 1.0f + GlowFlicker * (Mathf.PerlinNoise(_glowClock * GlowFlickerPerSecond, 0.37f) - 0.5f) * 2.0f;
            for (int i = 0; i < _glow.Length; i++)
            {
                LineRenderer band = _glow[i];
                band.enabled = _shown.Count >= 2;
                band.widthMultiplier = GlowWidths[i] * scale * breath;
                band.positionCount = _shown.Count;
                for (int p = 0; p < _shown.Count; p++)
                {
                    band.SetPosition(p, _shown[p]);
                }
            }
        }

        private readonly RaycastHit[] _groundHits = new RaycastHit[8];

        /// <summary>The ground's height under the chain: the first thing under the fist that is not the body itself; far below, with nothing there.</summary>
        private float GroundUnder(Vector3 fist)
        {
            Vector3 from = fist + _thrown * (ChainRules.SwungLength * 0.5f * BodyScale);
            from.y = Mathf.Max(from.y, fist.y) + 0.5f;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, _groundHits, GroundProbeMetres, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                Transform hit = _groundHits[i].transform;
                if (hit != null && !hit.IsChildOf(_body) && _groundHits[i].point.y > best)
                {
                    best = _groundHits[i].point.y;
                }
            }
            return float.IsNegativeInfinity(best) ? _body.position.y - GroundProbeMetres : best;
        }

        /// <summary>Hand-coil link <paramref name="slot"/>'s place and turn in the world.</summary>
        private void PalmSlot(int slot, out Vector3 position, out Quaternion rotation)
        {
            ChainWrap.PalmLink(slot, out Vector3 local, out Quaternion turn);
            position = _hand.TransformPoint(local);
            rotation = _hand.rotation * turn;
        }

        /// <summary>Every link's bone where the chain is this frame.</summary>
        private void PlaceLinks(float scale)
        {
            Vector3 size = Vector3.one * scale;
            Vector3 wrist = _forearm.InverseTransformPoint(_hand.position);
            _shown.Clear();
            for (int i = 0; i < ChainWrap.WristLinks; i++)
            {
                ChainWrap.WristLink(i, wrist, out Vector3 local, out Quaternion turn);
                _links[i].SetPositionAndRotation(_forearm.TransformPoint(local), _forearm.rotation * turn);
                _links[i].localScale = LocalScale(size);
                _shown.Add(_links[i].position);
            }

            int firstPalm = ChainWrap.FreeLinks - ChainWrap.PalmLinks;
            bool hanging = _wound < 1.0f;
            int firstOut = hanging ? _lash.FirstOut(_paidOut) : firstPalm;
            Vector3 flat = _hand.up;
            for (int j = 0; j < ChainWrap.FreeLinks; j++)
            {
                Transform link = _links[ChainWrap.WristLinks + j];
                int slot = j - firstPalm;
                float on = slot >= 0 ? (hanging ? Mathf.SmoothStep(0.0f, 1.0f, ChainWrap.Wound(_wound, slot)) : 1.0f) : 0.0f;
                if (j < firstOut || (!hanging && slot < 0))
                {
                    // In the fist, unseen.
                    link.SetPositionAndRotation(BladeBaseWorld, _hand.rotation);
                    link.localScale = Vector3.zero;
                    continue;
                }
                Vector3 at;
                Quaternion turn;
                if (on >= 1.0f)
                {
                    PalmSlot(slot, out at, out turn);
                }
                else
                {
                    Vector3[] points = _lash.At;
                    at = points[j];
                    Vector3 along = points[Mathf.Min(j + 1, points.Length - 1)] - points[Mathf.Max(j - 1, firstOut)];
                    along = along.sqrMagnitude > 1e-10f ? along.normalized : Vector3.down;
                    flat -= along * Vector3.Dot(flat, along);   // carried along the chain link to link, so it does not spin about its own length
                    flat = flat.sqrMagnitude > 1e-8f ? flat.normalized : Vector3.Cross(along, Vector3.right).normalized;
                    turn = ChainWrap.LinkTurn(along, flat, j + ChainWrap.WristLinks);
                    if (on > 0.0f)
                    {
                        PalmSlot(slot, out _, out Quaternion wound);
                        turn = Quaternion.Slerp(turn, wound, on);
                    }
                }
                // The link coming out of the fist grows out of it.
                float outOfFist = hanging ? Mathf.Clamp01(_paidOut - (ChainWrap.FreeLinks - 1 - j)) : 1.0f;
                link.SetPositionAndRotation(at, turn);
                link.localScale = LocalScale(size * outOfFist);
                _shown.Add(at);
            }
        }

        /// <summary>A world size as the links' local scale (they hang under the hand, which a giant's body scales).</summary>
        private Vector3 LocalScale(Vector3 world)
        {
            Vector3 parent = transform.lossyScale;
            return new Vector3(world.x / Mathf.Max(parent.x, 1e-4f), world.y / Mathf.Max(parent.y, 1e-4f), world.z / Mathf.Max(parent.z, 1e-4f));
        }
    }
}
