using System.Collections.Generic;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// THE BOW, THE ARROW AND THE CROSSBOW, drawn in code (owner, 2026-10-28: "make basic models for both of them i will make better ones later
    /// when needed"; docs/ARCHERY.md). The bow: a wrapped grip with a recurved limb above and below it (tubes along a curve, tapering to the
    /// tips) and a string between the tips, its +Y up the limbs and +Z the way it shoots, its origin the grip's middle. The arrow: a shaft up +Z
    /// from its nock with a steel head and three vanes, <see cref="ArcheryRules.ArrowLength"/> long. The crossbow: a stock with a rail, a pistol
    /// grip, a bow across the front and the string back at the latch, laid out as a gun (PistolWeapon, FirearmProfile.Crossbow: +Z the barrel),
    /// a bolt on the rail while it is loaded. Meshes saved under Weapons/Archery.
    /// </summary>
    public static class ArcheryBuilder
    {
        public const string BowPrefabPath = "Assets/CityX/Weapons/Bow.prefab";
        public const string ArrowPrefabPath = "Assets/CityX/Weapons/Arrow.prefab";
        public const string CrossbowPrefabPath = "Assets/CityX/Weapons/Crossbow.prefab";
        private const string Folder = "Assets/CityX/Weapons/Archery";

        private static readonly Color WoodColour = new Color(0.36f, 0.22f, 0.12f);
        private static readonly Color DarkWoodColour = new Color(0.2f, 0.13f, 0.08f);
        private static readonly Color SteelColour = new Color(0.55f, 0.56f, 0.58f);
        private static readonly Color StringColour = new Color(0.85f, 0.8f, 0.68f);
        private static readonly Color VaneColour = new Color(0.8f, 0.2f, 0.15f);
        private static readonly Color ShaftColour = new Color(0.72f, 0.6f, 0.4f);

        /// <summary>The bow's limbs: from the grip's end up to the tip, curving forward then recurving back behind the string's line.</summary>
        private const float GripHalf = 0.12f;
        private const float LimbTip = 0.85f;
        private const float StringBehind = 0.08f;

        /// <summary>The crossbow, in its gun frame: the stock from the butt to the front, the bow across it near the front, the string back at the latch.</summary>
        private const float StockBack = -0.45f;
        private const float StockFront = 0.35f;
        private const float CrossbowSpan = 0.42f;
        private const float BowAt = 0.3f;
        private const float LatchAt = -0.05f;

        [MenuItem("CityX/Rebuild Archery")]
        public static void BuildAll()
        {
            GameObject arrow = BuildArrow();
            BuildBow(arrow);
            BuildCrossbow(arrow);
        }

        public static GameObject BuildArrow()
        {
            EnsureFolder();
            var root = new GameObject("Arrow");
            try
            {
                AddArrowParts(root.transform);
                var trail = root.AddComponent<TrailRenderer>();
                trail.time = 0.12f;
                trail.minVertexDistance = 0.1f;
                trail.widthMultiplier = 0.015f;
                trail.sharedMaterial = HeldModelBaker.LitMaterial($"{Folder}/ArrowTrail.mat", new Color(0.9f, 0.9f, 0.85f), 0.0f, 0.2f);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;
                root.AddComponent<ArrowProjectile>();
                return Save(root, ArrowPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static GameObject BuildBow(GameObject arrowPrefab)
        {
            EnsureFolder();
            Material wood = HeldModelBaker.LitMaterial($"{Folder}/BowWood.mat", WoodColour, 0.0f, 0.35f);
            Material wrap = HeldModelBaker.LitMaterial($"{Folder}/BowWrap.mat", DarkWoodColour, 0.0f, 0.2f);
            var root = new GameObject("Bow");
            try
            {
                Part(root.transform, "Grip", Lathe(GripProfile(), "BowGrip", 12), wrap, Quaternion.identity);
                Part(root.transform, "UpperLimb", LimbMesh("BowUpperLimb", 1.0f), wood, Quaternion.identity);
                Part(root.transform, "LowerLimb", LimbMesh("BowLowerLimb", -1.0f), wood, Quaternion.identity);
                Transform top = Point(root.transform, "TopTip", new Vector3(0.0f, LimbTip, -StringBehind));
                Transform bottom = Point(root.transform, "BottomTip", new Vector3(0.0f, -LimbTip, -StringBehind));
                LineRenderer line = StringLine(root.transform, new[] { top.localPosition, BowWeapon.StringRest, bottom.localPosition });

                var nocked = new GameObject("NockedArrow");
                nocked.transform.SetParent(root.transform, false);
                nocked.transform.localPosition = BowWeapon.StringRest;
                AddArrowParts(nocked.transform);
                nocked.SetActive(false);

                var bow = root.AddComponent<BowWeapon>();
                bow.ArrowPrefab = arrowPrefab;
                bow.NockedArrow = nocked.transform;
                bow.String = line;
                bow.TopTip = top;
                bow.BottomTip = bottom;
                return Save(root, BowPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static GameObject BuildCrossbow(GameObject arrowPrefab)
        {
            EnsureFolder();
            Material wood = HeldModelBaker.LitMaterial($"{Folder}/CrossbowWood.mat", DarkWoodColour, 0.0f, 0.3f);
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/CrossbowSteel.mat", SteelColour, 0.8f, 0.5f);
            var root = new GameObject("Crossbow");
            try
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                Box(visual.transform, "Stock", new Vector3(0.0f, 0.0f, (StockBack + StockFront) * 0.5f), new Vector3(0.05f, 0.06f, StockFront - StockBack), wood);
                Box(visual.transform, "Rail", new Vector3(0.0f, 0.045f, (LatchAt + StockFront) * 0.5f), new Vector3(0.03f, 0.03f, StockFront - LatchAt), steel);
                Box(visual.transform, "Grip", new Vector3(0.0f, -0.08f, -0.08f), new Vector3(0.035f, 0.12f, 0.045f), wood, Quaternion.Euler(-15.0f, 0.0f, 0.0f));
                Box(visual.transform, "Latch", new Vector3(0.0f, 0.05f, LatchAt), new Vector3(0.05f, 0.03f, 0.03f), steel);
                Part(visual.transform, "RightLimb", CrossbowLimbMesh("CrossbowRightLimb", 1.0f), steel, Quaternion.identity);
                Part(visual.transform, "LeftLimb", CrossbowLimbMesh("CrossbowLeftLimb", -1.0f), steel, Quaternion.identity);
                Vector3 rightTip = new Vector3(CrossbowSpan, 0.04f, BowAt - 0.12f), leftTip = new Vector3(-CrossbowSpan, 0.04f, BowAt - 0.12f);
                Vector3 latch = new Vector3(0.0f, 0.05f, LatchAt);
                LineRenderer line = StringLine(visual.transform, new[] { rightTip, latch, leftTip });

                // The bolt on the rail: an arrow's parts, shortened to a bolt's length (the loosed one is the full arrow, for its flight maths).
                var bolt = new GameObject("Bolt");
                bolt.transform.SetParent(visual.transform, false);
                bolt.transform.localPosition = new Vector3(0.0f, 0.065f, LatchAt);
                bolt.transform.localScale = new Vector3(1.0f, 1.0f, 0.55f);
                AddArrowParts(bolt.transform);

                root.AddComponent<SphereCollider>().isTrigger = true;
                root.GetComponent<SphereCollider>().radius = PistolWeapon.InteractRange;
                var weapon = root.AddComponent<PistolWeapon>();
                weapon.Profile = FirearmProfile.Crossbow();
                weapon.VisualRoot = visual.transform;
                weapon.ArrowPrefab = arrowPrefab;
                var loaded = bolt.AddComponent<CrossbowBolt>();
                loaded.Weapon = weapon;
                // The spanning animation (docs/ARCHERY.md): the string hauled from the bow back to the latch, the bolt laid from over the shoulder.
                var span = root.AddComponent<CrossbowSpan>();
                span.Weapon = weapon;
                span.String = line;
                span.RightTip = rightTip;
                span.LeftTip = leftTip;
                span.Latch = latch;
                span.StringForward = new Vector3(0.0f, 0.05f, BowAt - 0.02f);
                span.ForeStock = weapon.Profile.SupportHandLocal;
                return Save(root, CrossbowPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // --- the arrow's parts, for the loose arrow, the nocked one and the crossbow's bolt alike ---

        private static void AddArrowParts(Transform parent)
        {
            Material shaft = HeldModelBaker.LitMaterial($"{Folder}/ArrowShaft.mat", ShaftColour, 0.0f, 0.3f);
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/ArrowHead.mat", SteelColour, 0.8f, 0.55f);
            Material vane = HeldModelBaker.LitMaterial($"{Folder}/ArrowVane.mat", VaneColour, 0.0f, 0.2f);
            float length = ArcheryRules.ArrowLength;
            // Lathed about Y, turned to run up +Z.
            Quaternion alongZ = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            Part(parent, "Shaft", Lathe(new[] { new Vector2(0.0f, 0.0f), new Vector2(0.007f, 0.0f), new Vector2(0.007f, length - 0.05f), new Vector2(0.0f, length - 0.05f) }, "ArrowShaft", 8), shaft, alongZ);
            Part(parent, "Head", Lathe(new[] { new Vector2(0.0f, length - 0.06f), new Vector2(0.014f, length - 0.05f), new Vector2(0.0f, length) }, "ArrowHead", 6), steel, alongZ);
            Part(parent, "Vanes", VanesMesh("ArrowVanes", 0.05f, 0.13f, 0.03f), vane, Quaternion.identity);
        }

        // --- meshes ---

        private static Vector2[] GripProfile() => new[]
        {
            new Vector2(0.0f, -GripHalf), new Vector2(0.02f, -GripHalf), new Vector2(0.024f, 0.0f), new Vector2(0.02f, GripHalf), new Vector2(0.0f, GripHalf),
        };

        /// <summary>A limb from the grip's end to the tip, bowing forward and recurving back to the string line, tapering; <paramref name="sign"/> up or down.</summary>
        private static Mesh LimbMesh(string name, float sign)
        {
            var points = new List<Vector3>();
            var radii = new List<float>();
            const int segments = 12;
            Vector3 p0 = new Vector3(0.0f, sign * GripHalf, 0.0f), p1 = new Vector3(0.0f, sign * 0.5f, 0.2f), p2 = new Vector3(0.0f, sign * LimbTip, -StringBehind);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                points.Add(Bezier(p0, p1, p2, t));
                radii.Add(Mathf.Lerp(0.02f, 0.008f, t));
            }
            return Tube(name, points, radii, 8);
        }

        /// <summary>The crossbow's limb from the stock out to its tip, swept back; <paramref name="sign"/> right or left.</summary>
        private static Mesh CrossbowLimbMesh(string name, float sign)
        {
            var points = new List<Vector3>();
            var radii = new List<float>();
            const int segments = 10;
            Vector3 p0 = new Vector3(0.0f, 0.04f, BowAt), p1 = new Vector3(sign * 0.22f, 0.04f, BowAt + 0.02f), p2 = new Vector3(sign * CrossbowSpan, 0.04f, BowAt - 0.12f);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                points.Add(Bezier(p0, p1, p2, t));
                radii.Add(Mathf.Lerp(0.016f, 0.007f, t));
            }
            return Tube(name, points, radii, 8);
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) => (1.0f - t) * (1.0f - t) * a + 2.0f * (1.0f - t) * t * b + t * t * c;

        /// <summary>A tube along <paramref name="points"/> with <paramref name="radii"/> at each, closed at both ends.</summary>
        private static Mesh Tube(string name, List<Vector3> points, List<float> radii, int sides)
        {
            int n = points.Count;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < n; i++)
            {
                Vector3 tangent = (i + 1 < n ? points[i + 1] - points[i] : points[i] - points[i - 1]).normalized;
                Vector3 side = Vector3.Cross(tangent, Vector3.right);
                if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(tangent, Vector3.up);
                side.Normalize();
                Vector3 other = Vector3.Cross(tangent, side);
                for (int s = 0; s < sides; s++)
                {
                    float a = s * 2.0f * Mathf.PI / sides;
                    Vector3 normal = side * Mathf.Cos(a) + other * Mathf.Sin(a);
                    vertices.Add(points[i] + normal * radii[i]);
                    normals.Add(normal);
                }
            }
            for (int i = 0; i + 1 < n; i++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = i * sides + s, b = i * sides + (s + 1) % sides, c = a + sides, d = b + sides;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }
            Cap(points[0], (points[0] - points[1]).normalized, 0, sides, vertices, normals, triangles);
            Cap(points[n - 1], (points[n - 1] - points[n - 2]).normalized, (n - 1) * sides, sides, vertices, normals, triangles);
            return HeldModelBaker.Save($"{Folder}/{name}.asset", name, vertices.ToArray(), normals.ToArray(), triangles.ToArray());
        }

        private static void Cap(Vector3 centre, Vector3 outward, int ring, int sides, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            int middle = vertices.Count;
            vertices.Add(centre);
            normals.Add(outward);
            int first = vertices.Count;
            for (int s = 0; s < sides; s++)
            {
                vertices.Add(vertices[ring + s]);
                normals.Add(outward);
            }
            bool flip = Vector3.Dot(Vector3.Cross(vertices[first + 1] - vertices[first], vertices[middle] - vertices[first]), outward) < 0.0f;
            for (int s = 0; s < sides; s++)
            {
                int a = first + s, b = first + (s + 1) % sides;
                triangles.Add(middle);
                triangles.Add(flip ? b : a);
                triangles.Add(flip ? a : b);
            }
        }

        /// <summary>Three vanes round the shaft near the nock: thin double-sided fins, <paramref name="from"/> to <paramref name="to"/> up the arrow, <paramref name="height"/> out.</summary>
        private static Mesh VanesMesh(string name, float from, float to, float height)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int v = 0; v < 3; v++)
            {
                float a = v * 2.0f * Mathf.PI / 3.0f;
                Vector3 out3 = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0.0f);
                Vector3 n = Vector3.Cross(out3, Vector3.forward);
                int i = vertices.Count;
                vertices.Add(new Vector3(0.0f, 0.0f, from));
                vertices.Add(new Vector3(0.0f, 0.0f, to));
                vertices.Add(out3 * height + Vector3.forward * (to - 0.02f));
                vertices.Add(out3 * (height * 0.6f) + Vector3.forward * (from + 0.01f));
                for (int k = 0; k < 4; k++) normals.Add(n);
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3, i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            return HeldModelBaker.Save($"{Folder}/{name}.asset", name, vertices.ToArray(), normals.ToArray(), triangles.ToArray());
        }

        private static Mesh Lathe(Vector2[] profile, string name, int sides)
        {
            return HeldModelBaker.Turned($"{Folder}/{name}.asset", profile, name, sides);
        }

        // --- parts ---

        private static void Part(Transform parent, string name, Mesh mesh, Material material, Quaternion rotation)
        {
            HeldModelBaker.Part(parent, name, mesh, material).localRotation = rotation;
        }

        private static void Box(Transform parent, string name, Vector3 centre, Vector3 size, Material material, Quaternion? rotation = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = centre;
            box.transform.localScale = size;
            box.transform.localRotation = rotation ?? Quaternion.identity;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Transform Point(Transform parent, string name, Vector3 local)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent, false);
            point.transform.localPosition = local;
            return point.transform;
        }

        private static LineRenderer StringLine(Transform parent, Vector3[] localPoints)
        {
            var go = new GameObject("String");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = localPoints.Length;
            line.SetPositions(localPoints);
            line.widthMultiplier = 0.006f;
            line.numCornerVertices = 2;
            line.sharedMaterial = HeldModelBaker.LitMaterial($"{Folder}/BowString.mat", StringColour, 0.0f, 0.3f);
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Archery");
            }
        }

        private static GameObject Save(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: {root.name} written to {path}");
            return saved;
        }
    }
}
