using System.Collections.Generic;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the angel wings prefab (docs/WINGS.md), drawn in code: a stand-in until the owner's Meshy model (owner, 2026-10-28: "just start
    /// with basic angel wings ... il make better models for wings later"; plain white, no tint). Each wing is a bird's: a three-bone arm
    /// (humerus, forearm, hand) out from the shoulder blade, built OPEN in the shape WingPose.Open gives, with rows of feathers hanging off it
    /// by a bone each -- the primaries off the hand, the secondaries off the forearm, tertials off the humerus, then three rows of shorter
    /// coverts layered over their bases -- a membrane of skin under the coverts so the wing reads as one surface near the arm, and a sleeve
    /// of down along the leading edge. A feather is a tapered strip, rounded at the tip, its trailing vane the wider, drooping a little
    /// toward the tip, both faces drawn (the cel line draws each feather's edge). The left wing is the right's mirror. One skinned mesh, one
    /// white material. Sizes on the 2.6 m body: each wing about 2.8 m to the tip, the pair over 5 m across. The dark wings (<see cref="BuildDark"/>)
    /// are the same wings, the same mesh, in glossy black.
    /// </summary>
    public static class AngelWingsBuilder
    {
        public const string PrefabPath = AngelWings.PrefabPath;
        private const string MeshPath = "Assets/CityX/Weapons/AngelWingsMesh.asset";
        private const string MaterialPath = "Assets/CityX/Weapons/AngelWingFeather.mat";
        private const string DarkMaterialPath = "Assets/CityX/Weapons/DarkAngelWingFeather.mat";

        /// <summary>The two wing roots either side of the spine, metres.</summary>
        public const float RootSpacing = 0.10f;

        /// <summary>The arm bones' lengths, metres.</summary>
        public const float HumerusLength = 0.48f;

        public const float ForearmLength = 0.74f;

        public const float HandLength = 0.42f;

        /// <summary>Feathers in each row, and the rows' lengths and widths, metres.</summary>
        public const int Primaries = 12;

        public const int Secondaries = 14;

        public const int Tertials = 5;

        public const float LongestPrimary = AngelWings.LongestPrimary;

        public const float ShortestPrimary = 1.0f;

        public const float OuterSecondary = 1.0f;

        public const float InnerSecondary = 0.8f;

        public const float OuterTertial = 0.75f;

        public const float InnerTertial = 0.6f;

        public const float FlightFeatherWidth = 0.22f;

        /// <summary>The covert rows' length as a share of the flight feather under them, outermost row first.</summary>
        private static readonly float[] CovertShares = { 0.55f, 0.36f, 0.22f };

        private static readonly float[] CovertWidths = { 0.9f, 0.75f, 0.6f };

        /// <summary>How deep the membrane reaches back from each arm bone, as a share of the flight feathers' length there.</summary>
        public const float MembraneShare = 0.4f;

        private const int FlightSegments = 5;
        private const int CovertSegments = 2;

        /// <summary>How far the trailing vane droops toward the tip, as a share of the feather's length.</summary>
        public const float Droop = 0.08f;

        private static readonly Color FeatherColour = new Color(0.96f, 0.96f, 0.97f);

        /// <summary>The dark wings' feathers: near black with a little blue in it, and glossier, a raven's (pure black loses every feather to the cel lines).</summary>
        private static readonly Color DarkFeatherColour = new Color(0.055f, 0.055f, 0.07f);

        private const float FeatherSmoothness = 0.15f;
        private const float DarkFeatherSmoothness = 0.3f;

        [MenuItem("CityX/Rebuild Angel Wings")]
        public static GameObject Build()
        {
            return Build(PrefabPath, HeldModelBaker.LitMaterial(MaterialPath, FeatherColour, 0.0f, FeatherSmoothness), ItemRegistry.AngelWings, "AngelWings");
        }

        /// <summary>The dark angel wings (owner, 2026-10-29: "a dark version of the angel wings, same wings model just black").</summary>
        [MenuItem("CityX/Rebuild Dark Angel Wings")]
        public static GameObject BuildDark()
        {
            return Build(AngelWings.DarkPrefabPath, HeldModelBaker.LitMaterial(DarkMaterialPath, DarkFeatherColour, 0.0f, DarkFeatherSmoothness),
                ItemRegistry.DarkAngelWings, "DarkAngelWings");
        }

        private static GameObject Build(string prefabPath, Material material, string itemId, string name)
        {
            var root = new GameObject(name);
            try
            {
                var build = new MeshBuild();
                var bones = new List<Transform>();
                AngelWings.Wing right = BuildWing(root.transform, build, bones, false);
                AngelWings.Wing left = BuildWing(root.transform, build, bones, true);

                Mesh mesh = Save(build, bones, root.transform);
                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.bones = bones.ToArray();
                renderer.rootBone = root.transform;
                renderer.updateWhenOffscreen = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = FlightGear.InteractRange;
                var wings = root.AddComponent<AngelWings>();
                wings.Right = right;
                wings.Left = left;
                wings.SetItemId(itemId);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: {name} written to {prefabPath} -- {mesh.triangles.Length / 3} triangles, {bones.Count} bones");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>One wing's bones and geometry. The right is built in a frame with X out along the wing; the left is its mirror in X.</summary>
        private static AngelWings.Wing BuildWing(Transform root, MeshBuild build, List<Transform> bones, bool left)
        {
            float side = left ? -1.0f : 1.0f;
            var wingRoot = new GameObject(left ? "WingL" : "WingR").transform;
            wingRoot.SetParent(root, false);
            wingRoot.localPosition = new Vector3(side * RootSpacing, 0.0f, 0.0f);

            // Each arm bone's X runs along it (WingPose.Rest): a child sits at the end of its parent.
            WingPose.Arm rest = WingPose.Rest;
            Transform humerus = Bone("Humerus", wingRoot, Vector3.zero, rest.Humerus, left, bones);
            Transform forearm = Bone("Forearm", humerus, new Vector3(HumerusLength, 0.0f, 0.0f), rest.Forearm, left, bones);
            Transform hand = Bone("Hand", forearm, new Vector3(ForearmLength, 0.0f, 0.0f), rest.Hand, left, bones);

            var feathers = new List<Transform>();
            var spreads = new List<float>();
            var alongs = new List<float>();
            float wingLength = HumerusLength + ForearmLength + HandLength;
            // Rows out from the body: tertials on the humerus, secondaries on the forearm, primaries on the hand. "along" is how far out
            // along the whole wing a feather sits, 0 at the shoulder, 1 at the wingtip (the feathers' twist grows with it).
            Row(build, bones, feathers, spreads, alongs, left, humerus, HumerusLength, 0.0f, wingLength, Tertials, 0.25f, 0.95f, 100.0f, 88.0f,
                InnerTertial, OuterTertial, FlightFeatherWidth * 0.85f, 0);
            Row(build, bones, feathers, spreads, alongs, left, forearm, ForearmLength, HumerusLength, wingLength, Secondaries, 0.05f, 0.96f, 86.0f, 72.0f,
                InnerSecondary, OuterSecondary, FlightFeatherWidth * 0.95f, 1);
            Row(build, bones, feathers, spreads, alongs, left, hand, HandLength, HumerusLength + ForearmLength, wingLength, Primaries, 0.1f, 0.98f, 70.0f, 26.0f,
                ShortestPrimary, LongestPrimary, FlightFeatherWidth, 2);

            // The skin under the coverts, and the down along the leading edge: one surface near the arm.
            Membrane(build, bones.IndexOf(humerus), humerus, HumerusLength, InnerTertial * MembraneShare, OuterTertial * MembraneShare, left);
            Membrane(build, bones.IndexOf(forearm), forearm, ForearmLength, InnerSecondary * MembraneShare, OuterSecondary * MembraneShare, left);
            Membrane(build, bones.IndexOf(hand), hand, HandLength, ShortestPrimary * MembraneShare, LongestPrimary * MembraneShare * 0.4f, left);
            Sleeve(build, bones.IndexOf(humerus), humerus, HumerusLength, left);
            Sleeve(build, bones.IndexOf(forearm), forearm, ForearmLength, left);
            Sleeve(build, bones.IndexOf(hand), hand, HandLength, left);

            return new AngelWings.Wing
            {
                Humerus = humerus,
                Forearm = forearm,
                Hand = hand,
                Feathers = feathers.ToArray(),
                FeatherSpread = spreads.ToArray(),
                FeatherAlong = alongs.ToArray(),
            };
        }

        /// <summary>
        /// A row of <paramref name="count"/> flight feathers along a bone from <paramref name="fromT"/> to <paramref name="toT"/> of its length,
        /// fanned from <paramref name="innerSpread"/> (nearest the body, swept furthest back) to <paramref name="outerSpread"/> degrees, and
        /// over each its three coverts, shorter, each layered a little above and ahead of the one beneath.
        /// </summary>
        private static void Row(MeshBuild build, List<Transform> bones, List<Transform> feathers, List<float> spreads, List<float> alongs, bool left,
            Transform bone, float boneLength, float boneStart, float wingLength, int count, float fromT, float toT, float innerSpread, float outerSpread,
            float innerLength, float outerLength, float width, int row)
        {
            for (int i = 0; i < count; i++)
            {
                float u = count > 1 ? i / (float)(count - 1) : 0.5f;
                float t = Mathf.Lerp(fromT, toT, u);
                float spread = Mathf.Lerp(innerSpread, outerSpread, u);
                // The primaries are longest a little in from the tip (a rounded wing), the other rows longest furthest out.
                float length = row == 2
                    ? Mathf.Lerp(innerLength, outerLength, 1.0f - Mathf.Abs(u - 0.72f) / 0.72f)
                    : Mathf.Lerp(innerLength, outerLength, u);
                float along = (boneStart + boneLength * t) / wingLength;
                Feather(build, bones, feathers, spreads, alongs, left, bone, new Vector3(boneLength * t, 0.0f, 0.0f), spread, length, width,
                    FlightSegments, along, $"F{row}{i}");
                for (int c = 0; c < CovertShares.Length; c++)
                {
                    Vector3 at = new Vector3(boneLength * t, 0.012f * (c + 1), 0.03f * (c + 1));
                    Feather(build, bones, feathers, spreads, alongs, left, bone, at, spread + 2.0f - 3.0f * c, length * CovertShares[c],
                        width * CovertWidths[c], CovertSegments, along, $"C{c}{row}{i}");
                }
            }
        }

        /// <summary>One feather: its bone on <paramref name="parent"/> at <paramref name="at"/>, turned back by <paramref name="spread"/>, and its strip.</summary>
        private static void Feather(MeshBuild build, List<Transform> bones, List<Transform> feathers, List<float> spreads, List<float> alongs, bool left,
            Transform parent, Vector3 at, float spread, float length, float width, int segments, float along, string name)
        {
            Quaternion rest = Quaternion.AngleAxis(spread, Vector3.up);
            Transform bone = Bone(name, parent, at, rest, left, bones);
            int index = bones.Count - 1;
            feathers.Add(bone);
            spreads.Add(spread);
            alongs.Add(along);
            Strip(build, index, bone, length, width, segments, left);
        }

        /// <summary>
        /// A feather's strip in its bone's frame (X along it): a shaft line with the narrower leading vane on +Z and the wider trailing vane on
        /// -Z, the edges a hair below the shaft (a ridge the cel line can catch), drooping toward the rounded tip. Both faces.
        /// </summary>
        private static void Strip(MeshBuild build, int boneIndex, Transform bone, float length, float width, int segments, bool left)
        {
            var rings = new List<Vector3[]>();
            for (int k = 0; k <= segments; k++)
            {
                float t = k / (float)segments;
                float p = Profile(t);
                float x = length * t;
                float y = -Droop * length * t * t;
                float edge = y - 0.004f * p;
                rings.Add(new[]
                {
                    Local(bone, new Vector3(x, edge, 0.38f * width * p), left),
                    Local(bone, new Vector3(x, y, 0.0f), left),
                    Local(bone, new Vector3(x, edge, -0.62f * width * p), left),
                });
            }
            Vector3 up = bone.up;
            for (int k = 0; k < segments; k++)
            {
                Vector3[] a = rings[k], b = rings[k + 1];
                float u0 = k / (float)segments, u1 = (k + 1) / (float)segments;
                build.Quad(a[0], b[0], b[1], a[1], up, boneIndex, new Vector2(u0, 1.0f), new Vector2(u1, 1.0f), new Vector2(u1, 0.5f), new Vector2(u0, 0.5f));
                build.Quad(a[1], b[1], b[2], a[2], up, boneIndex, new Vector2(u0, 0.5f), new Vector2(u1, 0.5f), new Vector2(u1, 0.0f), new Vector2(u0, 0.0f));
                build.Quad(a[0], b[0], b[1], a[1], -up, boneIndex, new Vector2(u0, 1.0f), new Vector2(u1, 1.0f), new Vector2(u1, 0.5f), new Vector2(u0, 0.5f));
                build.Quad(a[1], b[1], b[2], a[2], -up, boneIndex, new Vector2(u0, 0.5f), new Vector2(u1, 0.5f), new Vector2(u1, 0.0f), new Vector2(u0, 0.0f));
            }
        }

        /// <summary>A feather's width along its length, as a share of its widest: a quill at the root, widest past the middle, rounded to the tip.</summary>
        public static float Profile(float t)
        {
            const float widest = 0.55f;
            if (t <= widest)
            {
                return 0.3f + 0.7f * Mathf.Pow(t / widest, 0.8f);
            }
            float s = (t - widest) / (1.0f - widest);
            return Mathf.Max(0.05f, Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - s * s)));
        }

        /// <summary>
        /// The skin under the coverts along an arm bone: a strip from the bone line back by <paramref name="nearDepth"/> at its start and
        /// <paramref name="farDepth"/> at its end, a hair under the feathers. Both faces.
        /// </summary>
        private static void Membrane(MeshBuild build, int boneIndex, Transform bone, float length, float nearDepth, float farDepth, bool left)
        {
            const int segments = 4;
            Vector3 up = bone.up;
            Vector2 uv = Vector2.zero;
            for (int k = 0; k < segments; k++)
            {
                float x0 = length * k / segments, x1 = length * (k + 1) / segments;
                float d0 = Mathf.Lerp(nearDepth, farDepth, k / (float)segments), d1 = Mathf.Lerp(nearDepth, farDepth, (k + 1) / (float)segments);
                Vector3 a = Local(bone, new Vector3(x0, -0.006f, 0.02f), left), b = Local(bone, new Vector3(x1, -0.006f, 0.02f), left);
                Vector3 c = Local(bone, new Vector3(x1, -0.02f, -d1), left), d = Local(bone, new Vector3(x0, -0.02f, -d0), left);
                build.Quad(a, b, c, d, up, boneIndex, uv, uv, uv, uv);
                build.Quad(a, b, c, d, -up, boneIndex, uv, uv, uv, uv);
            }
        }

        /// <summary>The down along an arm bone's leading edge: a flat box from the bone line forward, tapering to the bone's far end.</summary>
        private static void Sleeve(MeshBuild build, int boneIndex, Transform bone, float length, bool left)
        {
            const float ahead = 0.10f, behind = 0.03f, thick = 0.03f, thin = 0.014f;
            Vector3 P(float x, float y, float z) => Local(bone, new Vector3(x, y, z), left);
            Vector3[] near = { P(0, thick, -behind), P(0, thick, ahead), P(0, -thick, ahead), P(0, -thick, -behind) };
            Vector3[] far = { P(length, thin, -behind), P(length, thin, ahead), P(length, -thin, ahead), P(length, -thin, -behind) };
            Vector3 x = (far[0] - near[0]).normalized;
            Vector3 up = bone.up, forward = bone.forward;
            Vector2 uv = Vector2.zero;
            build.Quad(near[0], near[1], far[1], far[0], up, boneIndex, uv, uv, uv, uv);
            build.Quad(near[3], far[3], far[2], near[2], -up, boneIndex, uv, uv, uv, uv);
            build.Quad(near[1], near[2], far[2], far[1], forward, boneIndex, uv, uv, uv, uv);
            build.Quad(near[0], far[0], far[3], near[3], -forward, boneIndex, uv, uv, uv, uv);
            build.Quad(near[0], near[3], near[2], near[1], -x, boneIndex, uv, uv, uv, uv);
            build.Quad(far[0], far[1], far[2], far[3], x, boneIndex, uv, uv, uv, uv);
        }

        private static Transform Bone(string name, Transform parent, Vector3 localPosition, Quaternion localRotation, bool left, List<Transform> bones)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = left ? new Vector3(-localPosition.x, localPosition.y, localPosition.z) : localPosition;
            bone.localRotation = left ? WingPose.Mirror(localRotation) : localRotation;
            bones.Add(bone);
            return bone;
        }

        /// <summary>A point in a bone's own frame as built (the right wing's; the left's is the same point mirrored), in the prefab's frame.</summary>
        private static Vector3 Local(Transform bone, Vector3 point, bool left)
        {
            return bone.TransformPoint(left ? new Vector3(-point.x, point.y, point.z) : point);
        }

        private static Mesh Save(MeshBuild build, List<Transform> bones, Transform root)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            bool fresh = mesh == null;
            if (fresh)
            {
                mesh = new Mesh();
            }
            mesh.Clear();
            mesh.name = "AngelWingsMesh";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(build.Vertices);
            mesh.SetUVs(0, build.Uvs);
            mesh.SetTriangles(build.Triangles, 0);
            mesh.boneWeights = build.Weights.ToArray();
            var bindposes = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                bindposes[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
            }
            mesh.bindposes = bindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (fresh)
            {
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        /// <summary>The mesh as it is built: every quad its own four vertices (so each face shades flat where the cel line wants an edge), weighted to one bone.</summary>
        private sealed class MeshBuild
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
            public readonly List<BoneWeight> Weights = new List<BoneWeight>();

            /// <summary>A quad a, b, c, d (any order round it), wound so its normal leans toward <paramref name="outward"/>.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int bone, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int start = Vertices.Count;
                Vertices.Add(a);
                Vertices.Add(b);
                Vertices.Add(c);
                Vertices.Add(d);
                Uvs.Add(ua);
                Uvs.Add(ub);
                Uvs.Add(uc);
                Uvs.Add(ud);
                var weight = new BoneWeight { boneIndex0 = bone, weight0 = 1.0f };
                for (int i = 0; i < 4; i++)
                {
                    Weights.Add(weight);
                }
                bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0.0f;
                if (flip)
                {
                    Triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                }
                else
                {
                    Triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }
        }
    }
}
