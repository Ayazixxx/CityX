using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The bursts (docs/DISMEMBERMENT.md): what a blunt blow leaves of a part, made of the part itself. A burst part breaks into pieces of its
    /// own skin, skull, clothes and hair with meat behind them (<see cref="Gore.Burst"/>), what was inside it flies with them (bone, eyes, the
    /// jaw), and what it wore is knocked off whole (a hat, a shoe). A whole body bursting is what Fallout and Team Fortress throw: the head and
    /// two to four ends of limbs fly off as themselves with a stump each, the trunk breaks into pieces with the ribs, spine and organs.
    /// </summary>
    public sealed partial class Dismemberer
    {
        /// <summary>How many pieces of itself a burst part breaks into: the head, a limb part, the trunk of a whole body.</summary>
        public const int HeadChunks = 7;

        public const int LimbChunks = 5;
        public const int TorsoChunks = 12;

        /// <summary>A trunk takes this many more pieces for the upper half of every limb that broke with it.</summary>
        public const int BrokenLimbChunks = 2;

        /// <summary>A whole body bursting: the odds a limb flies off whole; otherwise it breaks at the elbow or knee and its lower half flies.</summary>
        public const float WholeLimbShare = 0.5f;

        /// <summary>How fast what a whole body throws off flies out from the chest, on top of the blow, m/s.</summary>
        public const float BlownSpeed = 3.0f;

        /// <summary>Something worn knocked off whole: its weight, kg, and how fast it pops up, m/s.</summary>
        public const float WornMass = 0.5f;

        public const float KnockOffLift = 2.0f;

        /// <summary>Seeds go up to this: sent in an event's number, which the server refuses past this (NetworkedAbilitySync.IsPlausible), and arrives whole.</summary>
        public const int SeedRange = (int)NetworkedAbilitySync.MaxSpeedOrDuration;

        private static readonly (BodyPart upper, BodyPart lower)[] Limbs =
        {
            (BodyPart.LeftUpperArm, BodyPart.LeftLowerArm),
            (BodyPart.RightUpperArm, BodyPart.RightLowerArm),
            (BodyPart.LeftUpperLeg, BodyPart.LeftLowerLeg),
            (BodyPart.RightUpperLeg, BodyPart.RightLowerLeg),
        };

        /// <summary>A seed carried in a replicated event's number: whole, never negative, never more than <see cref="SeedRange"/>.</summary>
        public static int SeedOf(float number)
        {
            return float.IsNaN(number) || float.IsInfinity(number) ? 0 : Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(number)), 0, SeedRange);
        }

        /// <summary>The body's size against the player's 2.6 m (a giant's pieces are a giant's).</summary>
        private float BodyScale => Mathf.Clamp(transform.lossyScale.y, 0.25f, 4.0f);

        /// <summary>How fast the body itself was moving, which what flies off it keeps.</summary>
        private Vector3 Carried()
        {
            Ragdoll ragdoll = GetComponent<Ragdoll>();
            return ragdoll != null && ragdoll.Torso != null && !ragdoll.Torso.isKinematic ? ragdoll.Torso.linearVelocity : Vector3.zero;
        }

        private static float MassOf(BodyPart part)
        {
            return part == BodyPart.Head ? HeadMass : part >= BodyPart.LeftUpperLeg ? LegMass : ArmMass;
        }

        /// <summary>The body's own skin is flesh; whatever else is skinned on the body is clothes.</summary>
        private static GoreShatter.Layer LayerOf(Renderer renderer)
        {
            return renderer.name == BodySurface.SkinName || renderer.name == BodySurface.HeadName ? GoreShatter.Layer.Flesh : GoreShatter.Layer.Cloth;
        }

        /// <summary>A rider that bursts with its part (the skull, and the hair on it); anything else worn is knocked off whole.</summary>
        private bool Shatters(Renderer renderer)
        {
            return renderer.name == BodySurface.HeadName || IsHair(renderer.transform);
        }

        private bool IsHair(Transform part)
        {
            for (Transform at = part; at != null && at != transform; at = at.parent)
            {
                if (at.name == CharacterHair.WornName)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A burst part's riders: the skull and hair go into <paramref name="sources"/> to break with it, anything else worn is knocked off
        /// whole (as is a skull or hair whose mesh cannot be read). Returns how many were knocked off.
        /// </summary>
        private int BreakOrKnockOff(Renderer renderer, List<Gore.Source> sources, Vector3 away)
        {
            if (Shatters(renderer) && AddRider(sources, renderer, renderer.name == BodySurface.HeadName ? GoreShatter.Layer.Flesh : GoreShatter.Layer.Hair))
            {
                return 0;
            }
            KnockOff(renderer, away);
            return 1;
        }

        private static bool AddRider(List<Gore.Source> sources, Renderer renderer, GoreShatter.Layer layer)
        {
            Transform at = renderer.transform;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                if (skinned.sharedMesh == null || !skinned.sharedMesh.isReadable)
                {
                    return false;
                }
                var baked = new Mesh();
                skinned.BakeMesh(baked, true);
                sources.Add(SourceOf(renderer, baked, at.position, at.rotation, Vector3.one, layer));
                Destroy(baked);
                return true;
            }
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
            {
                return false;
            }
            sources.Add(SourceOf(renderer, filter.sharedMesh, at.position, at.rotation, at.lossyScale, layer));
            return true;
        }

        /// <summary>A surface to break up: <paramref name="mesh"/> as <paramref name="renderer"/> draws it, put into world space.</summary>
        private static Gore.Source SourceOf(Renderer renderer, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, GoreShatter.Layer layer)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            var inverse = new Vector3(Inverse(scale.x), Inverse(scale.y), Inverse(scale.z));
            var world = new Vector3[vertices.Length];
            var worldNormals = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                world[i] = position + rotation * Vector3.Scale(vertices[i], scale);
                worldNormals[i] = normals.Length > i ? (rotation * Vector3.Scale(normals[i], inverse)).normalized : Vector3.up;
            }
            var triangles = new int[mesh.subMeshCount][];
            for (int s = 0; s < triangles.Length; s++)
            {
                triangles[s] = mesh.GetTriangles(s);
            }
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Material[] materials = FrozenLook.WithoutSheen(renderer.sharedMaterials);
            return new Gore.Source
            {
                Surface = new GoreShatter.Surface { Positions = world, Normals = worldNormals, Uvs = mesh.uv, Triangles = triangles, Layer = layer },
                Name = renderer.name,
                Materials = materials,
                Block = block,
                Blocks = MaterialBlocks.Of(renderer, materials.Length),
                Shadows = renderer.shadowCastingMode,
            };
        }

        private static float Inverse(float value) => Mathf.Abs(value) > 1e-6f ? 1.0f / value : 1.0f;

        /// <summary>Something worn knocked off whole by a burst (a hat, a shoe): a still copy pops up and away.</summary>
        private void KnockOff(Renderer renderer, Vector3 away)
        {
            var worn = new GameObject(renderer.name + " (knocked off)");
            var made = new List<Mesh>();
            Bounds bounds = Freeze(renderer, worn.transform, made);
            Launch(worn, bounds, WornMass, away, Vector3.up * KnockOffLift, made);
        }

        /// <summary>Every transform under the hips and the part it belongs to: the deepest part whose root bone it hangs under.</summary>
        private Dictionary<Transform, BodyPart> PartsOfBones()
        {
            var parts = new Dictionary<Transform, BodyPart>();
            Transform hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null)
            {
                return parts;
            }
            var roots = new Dictionary<Transform, BodyPart>();
            for (var part = BodyPart.Head; part <= BodyPart.RightLowerLeg; part++)
            {
                Transform root = _animator.GetBoneTransform(DismemberRules.RootOf(part));
                if (root != null)
                {
                    roots[root] = part;
                }
            }
            foreach (Transform bone in hips.GetComponentsInChildren<Transform>(true))
            {
                BodyPart part = BodyPart.Torso;
                for (Transform at = bone; at != null && at != hips.parent; at = at.parent)
                {
                    if (roots.TryGetValue(at, out BodyPart found))
                    {
                        part = found;
                        break;
                    }
                }
                parts[bone] = part;
            }
            return parts;
        }

        /// <summary>
        /// The whole body bursts out of the chest: the head flies off as itself, and each limb either whole or, broken at the elbow or knee, its
        /// lower half, each with a stump; the trunk (with the upper half of a broken limb) breaks into pieces of itself, with the ribs, spine and
        /// organs. Every renderer of the body is hidden; its ragdoll keeps lying on the floor (what follows the body, the camera, finds it there).
        /// </summary>
        private void BurstWhole(Vector3 away, int seed)
        {
            var random = new System.Random(seed);
            Vector3 chest = Chest();

            // Group 0 bursts; groups 1 and up fly off, each a piece of its own.
            var flying = new List<BodyPart> { BodyPart.Head };
            var groupOfPart = new int[Parts];
            groupOfPart[(int)BodyPart.Head] = 1;
            var bits = new List<GoreAnatomy.Bit>(GoreAnatomy.BitsOf(BodyPart.Torso));
            int broken = 0;
            foreach ((BodyPart upper, BodyPart lower) in Limbs)
            {
                if (random.NextDouble() < WholeLimbShare)
                {
                    flying.Add(upper);
                    groupOfPart[(int)upper] = groupOfPart[(int)lower] = flying.Count;
                }
                else
                {
                    flying.Add(lower);
                    groupOfPart[(int)lower] = flying.Count;
                    bits.Add(GoreAnatomy.BoneOf(upper));
                    broken++;
                }
            }
            int groups = flying.Count + 1;
            Dictionary<Transform, BodyPart> partOfBone = PartsOfBones();
            var pieces = new GameObject[groups];
            var bounds = new Bounds[groups];
            var made = new List<Mesh>[groups];
            var cutCentre = new Vector3[groups];
            var cutRadius = new float[groups];
            var outwardOf = new Vector3[groups];   // the way each piece comes off (zero: it cannot)
            for (int g = 1; g < groups; g++)
            {
                pieces[g] = new GameObject($"{flying[g - 1]} (blown off)");
                Transform root = _animator.GetBoneTransform(DismemberRules.RootOf(flying[g - 1]));
                bounds[g] = new Bounds(root != null ? root.position : chest, Vector3.zero);
                made[g] = new List<Mesh>();
                outwardOf[g] = root != null && root.parent != null ? (root.position - root.parent.position).normalized : Vector3.zero;
            }
            var axes = new Vector3[groups];

            // The skin and the clothes: carved once into the groups, in the pose the body is in.
            var sources = new List<Gore.Source>();
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.enabled || skin.sharedMesh == null || !skin.sharedMesh.isReadable || partOfBone.ContainsKey(skin.transform))
                {
                    continue;
                }
                BoneWeight[] weights = skin.sharedMesh.boneWeights;
                Transform[] bones = skin.bones;
                if (weights == null || weights.Length != skin.sharedMesh.vertexCount)
                {
                    continue;
                }
                var groupOfBone = new int[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    groupOfBone[i] = bones[i] != null && partOfBone.TryGetValue(bones[i], out BodyPart part) ? groupOfPart[(int)part] : 0;
                }
                var groupOf = new int[weights.Length];
                for (int v = 0; v < weights.Length; v++)
                {
                    int bone = MeshSevering.Heaviest(weights[v]);
                    groupOf[v] = bone >= 0 && bone < groupOfBone.Length ? groupOfBone[bone] : 0;
                }
                Quaternion intoSkin = Quaternion.Inverse(skin.transform.rotation);
                for (int g = 1; g < groups; g++)
                {
                    axes[g] = intoSkin * outwardOf[g];
                }
                var baked = new Mesh();
                skin.BakeMesh(baked, true);
                MeshSevering.Carving carving = MeshSevering.Carve(skin.sharedMesh, baked, groupOf, groups, axes);
                Destroy(baked);
                Transform at = skin.transform;
                for (int g = 0; g < groups; g++)
                {
                    Mesh piece = carving.Pieces[g];
                    if (piece == null)
                    {
                        continue;
                    }
                    if (g == 0)
                    {
                        sources.Add(SourceOf(skin, piece, at.position, at.rotation, Vector3.one, LayerOf(skin)));
                        Destroy(piece);
                        continue;
                    }
                    made[g].Add(piece);
                    bounds[g].Encapsulate(AddPart(pieces[g].transform, piece, skin, at.position, at.rotation, Vector3.one));
                    if (skin.name == BodySurface.SkinName && carving.Radii[g] > 0.0f)
                    {
                        cutCentre[g] = at.position + at.rotation * carving.Centres[g];
                        cutRadius[g] = carving.Radii[g];
                    }
                }
            }

            // What rides the bones goes with its bone's piece; on the bursting trunk, something worn is knocked off whole. All of it hidden.
            int thrown = 0;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !IsSolid(renderer) || HeldInHand(renderer.transform))
                {
                    continue;
                }
                if (IsDrawn(renderer) && partOfBone.TryGetValue(renderer.transform, out BodyPart on))
                {
                    int g = groupOfPart[(int)on];
                    if (g > 0)
                    {
                        bounds[g].Encapsulate(Freeze(renderer, pieces[g].transform, made[g]));
                    }
                    else
                    {
                        KnockOff(renderer, away);
                        thrown++;
                    }
                }
                renderer.enabled = false;
                _hidden.Add(renderer);
            }
            SwitchOffLights(transform);
            // The hidden ragdoll left lying (the camera follows it) at its own weight, but not flailing from the blast.
            foreach (Rigidbody bone in GetComponentsInChildren<Rigidbody>(true))
            {
                Calm(bone, bone.mass);
            }

            for (int g = 1; g < groups; g++)
            {
                BodyPart part = flying[g - 1];
                Transform root = _animator.GetBoneTransform(DismemberRules.RootOf(part));
                if (pieces[g].transform.childCount == 0 || root == null || root.parent == null)
                {
                    Destroy(pieces[g]);
                    continue;
                }
                if (cutRadius[g] <= 0.0f)
                {
                    cutCentre[g] = NearestSkinPoint(root.position) ?? root.position;
                    cutRadius[g] = part == BodyPart.Head ? HeadStumpRadius : FallbackStumpRadius;
                }
                Gore.Stump(pieces[g].transform, cutCentre[g], -outwardOf[g], cutRadius[g]);
                Vector3 push = bounds[g].center - chest;
                Launch(pieces[g], bounds[g], MassOf(part), away, (push.sqrMagnitude > 1e-6f ? push.normalized : Vector3.up) * BlownSpeed, made[g]);
                thrown++;
            }

            LastPiece = null;
            GibsThrown += thrown + Gore.Burst(sources, bits, chest, away, BodyScale, TorsoChunks + BrokenLimbChunks * broken, seed, Gore.BodySmears,
                BodyColliders(), Carried());
            Gore.Blood(chest, away, 3.0f, transform);
            Gore.Blood(chest + Vector3.down * 0.6f, -away, 2.0f, transform);
        }
    }
}
