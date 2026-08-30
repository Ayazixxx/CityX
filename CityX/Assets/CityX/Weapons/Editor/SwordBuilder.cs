using System.Collections.Generic;
using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the sword prefab -- a real, faithful port of
    /// weapons/greatsword.gd's own procedural model, not an invented
    /// replacement. An earlier version of this file built a plain straight
    /// three-segment silver blade with a bronze guard, on the reasoning
    /// that "no Godot source [existed] to port numbers from" -- that
    /// reasoning was wrong: greatsword.gd (and its shared base,
    /// two_handed_sword.gd) has a complete, fully-specified procedural
    /// model (a lofted, curved kopis-style blade with a gold spine inlay,
    /// gold grip rings, a gold pommel, and a gold guard), and this ports
    /// its actual geometry, colours and dimensions directly rather than
    /// guessing a new shape.
    ///
    /// GODOT-SPACE GEOMETRY, THEN ONE FLIP -- every position/mesh number in
    /// this file is copied straight from greatsword.gd/two_handed_sword.gd
    /// with no manual sign changes, built under a child "Model" transform
    /// whose own localScale is (MODEL_SCALE, MODEL_SCALE, -MODEL_SCALE).
    /// The single negative Z axis handles Godot's own -Z-forward vs this
    /// project's +Z-forward convention in one place, and Unity's renderer
    /// automatically corrects triangle winding/normals for a transform
    /// with a negative-determinant scale, so no per-triangle winding or
    /// per-vertex sign-flip logic is needed anywhere in the mesh-building
    /// code below -- it is a literal transcription of the .gd source.
    /// </summary>
    public static class SwordBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/Sword.prefab";
        private const string BladeMeshPath = "Assets/CityX/Weapons/SwordBladeMesh.asset";

        // --- two_handed_sword.gd's own shared constants ---
        private const float GripLen = 0.20f;
        private const float ModelScale = 2.0f;
        private const float GripCenterT = 0.5f;

        // --- greatsword.gd's own constants ---
        private const float GripRadius = 0.025f;
        private const float PommelRadius = 0.032f;
        private const float BladeLength = 1.05f;
        private const float GuardArmLen = 0.11f;
        private const float GuardArmRadius = 0.013f;
        private const float GuardBossRadius = 0.045f;

        private static readonly Color BladeColour = new Color(0.04f, 0.035f, 0.04f);
        private static readonly Color GoldColour = new Color(0.62f, 0.47f, 0.16f);
        private static readonly Color GripColour = new Color(0.05f, 0.03f, 0.02f);

        /// <summary>(t, edge_x, width, thickness) keyframes, t=0 at the guard, t=1 at the tip -- greatsword.gd's own BLADE_PROFILE, verbatim.</summary>
        private static readonly float[][] BladeProfile =
        {
            new[] { 0.0f, 0.0f, 0.06f, 0.022f },
            new[] { 0.3f, -0.025f, 0.075f, 0.020f },
            new[] { 0.6f, -0.09f, 0.125f, 0.015f },
            new[] { 0.85f, -0.17f, 0.16f, 0.008f },
            new[] { 1.0f, -0.24f, 0.0f, 0.0f },
        };
        private const int BladeSlices = 28;

        /// <summary>Sides per procedural tube -- greatsword.gd's own tubes are Godot CylinderMeshes, which default to 12; matched here for the same silhouette.</summary>
        private const int TubeSides = 12;

        [MenuItem("CityX/Rebuild Sword")]
        public static GameObject Build()
        {
            var root = new GameObject("Sword");
            try
            {
                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                // See this file's own class comment for why this single
                // negative-Z scale is the entire Godot-to-Unity coordinate
                // fix needed.
                model.transform.localScale = new Vector3(ModelScale, ModelScale, -ModelScale);

                Material bladeMat = CharacterSkin.Ensure("SwordBlade", BladeColour);
                bladeMat.SetFloat("_Metallic", 0.55f);
                bladeMat.SetFloat("_Smoothness", 0.4f);

                Material goldMat = CharacterSkin.Ensure("SwordGold", GoldColour);
                goldMat.SetFloat("_Metallic", 0.9f);
                goldMat.SetFloat("_Smoothness", 0.3f);

                Material gripMat = CharacterSkin.Ensure("SwordGrip", GripColour);
                gripMat.SetFloat("_Metallic", 0.1f);
                gripMat.SetFloat("_Smoothness", 0.85f);

                BladeSlice[] slices = BuildBladeObject(model.transform, bladeMat);
                BuildSpineInlay(model.transform, goldMat, slices);
                BuildGripAndPommel(model.transform, gripMat, goldMat);
                BuildGuard(model.transform, goldMat);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = SwordWeapon.InteractRange;
                // Centred on roughly the middle of the visible blade, in the
                // ROOT's own (unscaled) space -- BladeLength/2 in Model
                // space becomes BladeLength/2*ModelScale once the Model
                // child's own scale is applied.
                pickupTrigger.center = new Vector3(0.0f, BladeLength * ModelScale * 0.5f, 0.0f);

                root.AddComponent<SwordWeapon>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: sword written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>One blade cross-section -- greatsword.gd's own _blade_slice, verbatim.</summary>
        private readonly struct BladeSlice
        {
            public readonly Vector3 Edge;
            public readonly Vector3 Front;
            public readonly Vector3 Back;

            public BladeSlice(Vector3 edge, Vector3 front, Vector3 back)
            {
                Edge = edge;
                Front = front;
                Back = back;
            }
        }

        /// <summary>two_handed_sword.gd's own _blade_profile_at, verbatim.</summary>
        private static (float edgeX, float width, float thickness) ProfileAt(float[][] profile, float t)
        {
            float[] lo = profile[0];
            float[] hi = profile[profile.Length - 1];
            for (int i = 0; i < profile.Length - 1; i++)
            {
                if (t >= profile[i][0] && t <= profile[i + 1][0])
                {
                    lo = profile[i];
                    hi = profile[i + 1];
                    break;
                }
            }
            float span = hi[0] - lo[0];
            float f = span > 0.0001f ? (t - lo[0]) / span : 0.0f;
            return (
                Mathf.Lerp(lo[1], hi[1], f),
                Mathf.Lerp(lo[2], hi[2], f),
                Mathf.Lerp(lo[3], hi[3], f));
        }

        /// <summary>two_handed_sword.gd's own _blade_slice, verbatim.</summary>
        private static BladeSlice SliceAt(float[][] profile, float bladeLength, float t)
        {
            (float edgeX, float width, float thickness) = ProfileAt(profile, t);
            float y = t * bladeLength;
            float halfWidth = width * 0.5f;
            return new BladeSlice(
                new Vector3(edgeX - halfWidth, y, 0.0f),
                new Vector3(edgeX + halfWidth, y, thickness * 0.5f),
                new Vector3(edgeX + halfWidth, y, -thickness * 0.5f));
        }

        /// <summary>
        /// two_handed_sword.gd's own _build_blade, verbatim -- a real wedge-
        /// profile blade (edge/front/back per slice, ribboned between
        /// consecutive slices), flat-shaded (each triangle gets its own
        /// three vertices and one normal, no sharing/smoothing), matching
        /// greatsword.gd's own low-poly stylised look. Returns the computed
        /// slices for the spine inlay to reuse, same as the .gd original.
        /// </summary>
        private static BladeSlice[] BuildBladeObject(Transform parent, Material material)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();

            var slices = new BladeSlice[BladeSlices + 1];
            for (int i = 0; i <= BladeSlices; i++)
            {
                slices[i] = SliceAt(BladeProfile, BladeLength, (float)i / BladeSlices);
            }

            AddTri(verts, normals, tris, slices[0].Edge, slices[0].Back, slices[0].Front);

            for (int i = 0; i < BladeSlices; i++)
            {
                BladeSlice s0 = slices[i];
                BladeSlice s1 = slices[i + 1];
                AddTri(verts, normals, tris, s0.Edge, s0.Front, s1.Front);
                AddTri(verts, normals, tris, s0.Edge, s1.Front, s1.Edge);
                AddTri(verts, normals, tris, s0.Edge, s1.Back, s0.Back);
                AddTri(verts, normals, tris, s0.Edge, s1.Edge, s1.Back);
                AddTri(verts, normals, tris, s0.Front, s0.Back, s1.Back);
                AddTri(verts, normals, tris, s0.Front, s1.Back, s1.Front);
            }

            var mesh = new Mesh { name = "SwordBladeMesh" };
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(BladeMeshPath);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(BladeMeshPath);
            }
            AssetDatabase.CreateAsset(mesh, BladeMeshPath);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Blade");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;

            return slices;
        }

        /// <summary>
        /// greatsword.gd's own gold fuller inlay, tracing the blade's own
        /// spine centreline (front+back midpoint per slice) -- echoes the
        /// reference image's engraved gold line without trying to
        /// reproduce its actual filigree.
        /// </summary>
        private static void BuildSpineInlay(Transform parent, Material goldMat, BladeSlice[] slices)
        {
            var spinePoints = new Vector3[slices.Length];
            for (int i = 0; i < slices.Length; i++)
            {
                spinePoints[i] = (slices[i].Front + slices[i].Back) * 0.5f;
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < spinePoints.Length - 1; i++)
            {
                AddTube(verts, normals, tris, spinePoints[i], spinePoints[i + 1], 0.006f, 0.006f);
            }
            AddMeshChild(parent, "SpineInlay", verts, normals, tris, goldMat);
        }

        /// <summary>
        /// greatsword.gd's own grip tube (tapered, GRIP_RADIUS to
        /// GRIP_RADIUS*1.15), 3 gold rings spaced along it, and a gold
        /// pommel sphere at the butt end -- all verbatim positions.
        /// </summary>
        private static void BuildGripAndPommel(Transform parent, Material gripMat, Material goldMat)
        {
            var gripVerts = new List<Vector3>();
            var gripNormals = new List<Vector3>();
            var gripTris = new List<int>();
            AddTube(gripVerts, gripNormals, gripTris,
                new Vector3(0.0f, -GripLen, 0.0f), new Vector3(0.0f, -0.015f, 0.0f),
                GripRadius, GripRadius * 1.15f);
            AddMeshChild(parent, "Grip", gripVerts, gripNormals, gripTris, gripMat);

            var ringVerts = new List<Vector3>();
            var ringNormals = new List<Vector3>();
            var ringTris = new List<int>();
            for (int i = 1; i < 4; i++)
            {
                float ringY = -GripLen * (i / 4.0f);
                AddTube(ringVerts, ringNormals, ringTris,
                    new Vector3(0.0f, ringY - 0.008f, 0.0f), new Vector3(0.0f, ringY + 0.008f, 0.0f),
                    GripRadius * 1.2f, GripRadius * 1.2f);
            }
            AddMeshChild(parent, "GripRings", ringVerts, ringNormals, ringTris, goldMat);

            var pommel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pommel.name = "Pommel";
            Object.DestroyImmediate(pommel.GetComponent<Collider>());
            pommel.transform.SetParent(parent, false);
            pommel.transform.localPosition = new Vector3(0.0f, -GripLen - PommelRadius * 0.6f, 0.0f);
            pommel.transform.localScale = Vector3.one * (PommelRadius * 2.0f);
            pommel.GetComponent<MeshRenderer>().sharedMaterial = goldMat;
        }

        /// <summary>greatsword.gd's own two tapering guard arms plus the round gold boss at the centre.</summary>
        private static void BuildGuard(Transform parent, Material goldMat)
        {
            var armVerts = new List<Vector3>();
            var armNormals = new List<Vector3>();
            var armTris = new List<int>();
            AddTube(armVerts, armNormals, armTris,
                Vector3.zero, new Vector3(-GuardArmLen, 0.02f, 0.02f), GuardArmRadius, GuardArmRadius * 0.5f);
            AddTube(armVerts, armNormals, armTris,
                Vector3.zero, new Vector3(GuardArmLen, 0.02f, -0.02f), GuardArmRadius, GuardArmRadius * 0.5f);
            AddMeshChild(parent, "GuardArms", armVerts, armNormals, armTris, goldMat);

            var boss = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            boss.name = "GuardBoss";
            Object.DestroyImmediate(boss.GetComponent<Collider>());
            boss.transform.SetParent(parent, false);
            boss.transform.localPosition = new Vector3(0.0f, 0.0f, 0.01f);
            // Godot's own SphereMesh height=GUARD_BOSS_RADIUS*1.6 (a slightly
            // flattened sphere, not a perfect ball) -- approximated here with
            // a non-uniform scale on Unity's own primitive sphere.
            boss.transform.localScale = new Vector3(
                GuardBossRadius * 2.0f, GuardBossRadius * 1.6f, GuardBossRadius * 2.0f);
            boss.GetComponent<MeshRenderer>().sharedMaterial = goldMat;
        }

        /// <summary>
        /// A flat-shaded triangle -- one normal computed fresh per triangle
        /// and three UNIQUE vertices (never shared with any other
        /// triangle), matching two_handed_sword.gd's own _tri (SurfaceTool
        /// set_normal-per-corner) exactly, which is what gives the blade
        /// its faceted, low-poly look rather than a smoothed one.
        /// </summary>
        private static void AddTri(List<Vector3> verts, List<Vector3> normals, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int start = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            tris.Add(start);
            tris.Add(start + 1);
            tris.Add(start + 2);
        }

        /// <summary>
        /// A tapered tube (frustum) between two arbitrary points, with
        /// independent end radii and its own end caps -- two_handed_sword.gd's
        /// own _add_tube, minus the part where it builds a real Godot
        /// CylinderMesh (Unity's own built-in cylinder is uniform-radius
        /// only, so this generates the ring geometry directly instead). The
        /// basis computation (an arbitrary perpendicular pair around the
        /// tube's own axis) is copied verbatim from the .gd source.
        /// </summary>
        private static void AddTube(List<Vector3> verts, List<Vector3> normals, List<int> tris,
            Vector3 a, Vector3 b, float radiusA, float radiusB)
        {
            float length = Vector3.Distance(a, b);
            Vector3 yAxis = length > 0.001f ? (b - a).normalized : Vector3.up;
            Vector3 helper = Mathf.Abs(Vector3.Dot(yAxis, Vector3.forward)) < 0.95f ? Vector3.forward : Vector3.right;
            Vector3 xAxis = Vector3.Cross(yAxis, helper).normalized;
            Vector3 zAxis = Vector3.Cross(xAxis, yAxis).normalized;

            var ringA = new Vector3[TubeSides];
            var ringB = new Vector3[TubeSides];
            for (int i = 0; i < TubeSides; i++)
            {
                float angle = (float)i / TubeSides * Mathf.PI * 2.0f;
                Vector3 dir = xAxis * Mathf.Cos(angle) + zAxis * Mathf.Sin(angle);
                ringA[i] = a + dir * radiusA;
                ringB[i] = b + dir * radiusB;
            }

            for (int i = 0; i < TubeSides; i++)
            {
                int next = (i + 1) % TubeSides;
                AddTri(verts, normals, tris, ringA[i], ringB[i], ringB[next]);
                AddTri(verts, normals, tris, ringA[i], ringB[next], ringA[next]);
            }

            if (radiusA > 0.0001f)
            {
                for (int i = 0; i < TubeSides; i++)
                {
                    int next = (i + 1) % TubeSides;
                    AddTri(verts, normals, tris, a, ringA[next], ringA[i]);
                }
            }
            if (radiusB > 0.0001f)
            {
                for (int i = 0; i < TubeSides; i++)
                {
                    int next = (i + 1) % TubeSides;
                    AddTri(verts, normals, tris, b, ringB[i], ringB[next]);
                }
            }
        }

        /// <summary>
        /// Every generated mesh MUST be saved as a real asset -- an
        /// in-memory-only Mesh assigned to sharedMesh silently comes back
        /// null after PrefabUtility.SaveAsPrefabAsset/AssetDatabase.SaveAssets
        /// (confirmed directly: only Blade, which already called
        /// AssetDatabase.CreateAsset itself, survived a save; SpineInlay,
        /// Grip, GripRings and GuardArms all came back with a null
        /// sharedMesh on the saved prefab until this used the same
        /// save-to-disk step for them too).
        /// </summary>
        private static void AddMeshChild(Transform parent, string name,
            List<Vector3> verts, List<Vector3> normals, List<int> tris, Material material)
        {
            if (verts.Count == 0)
            {
                return;
            }
            var mesh = new Mesh { name = name };
            mesh.indexFormat = verts.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            string path = $"Assets/CityX/Weapons/Sword{name}Mesh.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
