using System.Collections.Generic;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the boomerangs (docs/BOOMERANG.md), stand-ins drawn in code until the owner's models, each a V lofted along its length:
    ///  * the Wooden Boomerang: a flat carved V, its tips painted so its spin reads. Boomerang.prefab is the one in the hand (BoomerangItem), ThrownBoomerang.prefab
    ///    the one in the air. 70 cm tip to tip, well over the city's 1.3 times life, so that it reads in the air at its range;
    ///  * the Giant Boomerang: a wide, thick, bone-white blade as tall as the player (2.6 m tip to tip), leather wrapped at its grip near one end, round its elbow
    ///    and near its other tip. GiantBoomerang.prefab is the one carried (GiantBoomerangItem: its origin is the grip, so the hand and the back hold it there),
    ///    ThrownGiantBoomerang.prefab the one in the air.
    /// The thrown ones lie flat with their middle at the origin, which is what they spin about.
    /// </summary>
    public static class BoomerangBuilder
    {
        private const string Folder = "Assets/CityX/Weapons/Boomerang";
        public const string PrefabPath = "Assets/CityX/Weapons/Boomerang.prefab";
        public const string ThrownPrefabPath = "Assets/CityX/Weapons/ThrownBoomerang.prefab";
        public const string GiantPrefabPath = "Assets/CityX/Weapons/GiantBoomerang.prefab";
        public const string ThrownGiantPrefabPath = "Assets/CityX/Weapons/ThrownGiantBoomerang.prefab";

        private static readonly Color Wood = new Color(0.55f, 0.34f, 0.17f);
        private static readonly Color Paint = new Color(0.72f, 0.16f, 0.1f);
        private static readonly Color Bone = new Color(0.84f, 0.80f, 0.68f);
        private static readonly Color Leather = new Color(0.33f, 0.20f, 0.11f);

        /// <summary>
        /// A boomerang's shape: each arm's length and how far it opens from straight down the middle, how round the elbow is (a share of an arm), its width at
        /// the elbow and at the tips, its thickness, and the sides of each slice.
        /// </summary>
        private readonly struct Shape
        {
            public readonly float Arm, OpenDegrees, Roundness, WidthAtElbow, WidthAtTip, Thickness;
            public readonly int Sides;

            public Shape(float arm, float openDegrees, float roundness, float widthAtElbow, float widthAtTip, float thickness, int sides)
            {
                Arm = arm;
                OpenDegrees = openDegrees;
                Roundness = roundness;
                WidthAtElbow = widthAtElbow;
                WidthAtTip = widthAtTip;
                Thickness = thickness;
                Sides = sides;
            }
        }

        private static readonly Shape WoodenShape = new Shape(0.45f, 50.0f, 0.12f, 0.115f, 0.075f, 0.03f, 6);

        /// <summary>2.6 m tip to tip: two arms of 1.4 m opened wide.</summary>
        private static readonly Shape GiantShape = new Shape(1.4f, 68.0f, 0.1f, 0.42f, 0.3f, 0.11f, 8);

        /// <summary>Slices along it, and where along each arm (0 elbow, 1 tip) the wooden one's paint starts.</summary>
        private const int Slices = 24;

        private const int GiantSlices = 40;

        private const float PaintFrom = 0.72f;

        /// <summary>The paint and the wraps stand this proud of the body.</summary>
        private const float PaintProud = 0.0015f;

        private const float WrapProud = 0.008f;

        /// <summary>Where the giant one is held, along it (-1 one tip, 1 the other), and its leather wraps (from, to).</summary>
        public const float GiantGripAt = 0.7f;

        private static readonly Vector2[] GiantWraps =
        {
            new Vector2(0.6f, 0.8f), new Vector2(-0.07f, -0.02f), new Vector2(0.02f, 0.07f), new Vector2(-0.86f, -0.81f),
        };

        [MenuItem("CityX/Rebuild Boomerang")]
        public static GameObject Build()
        {
            EnsureFolder();
            Material wood = HeldModelBaker.LitMaterial($"{Folder}/BoomerangWood.mat", Wood, 0.0f, 0.35f);
            Material paint = HeldModelBaker.LitMaterial($"{Folder}/BoomerangPaint.mat", Paint, 0.0f, 0.45f);
            Vector3 middle = MiddleOf(WoodenShape);
            Mesh body = Loft($"{Folder}/BoomerangBody.asset", "BoomerangBody", WoodenShape, middle, Slices, new[] { new Vector2(-1.0f, 1.0f) }, 0.0f);
            Mesh tips = Loft($"{Folder}/BoomerangTips.asset", "BoomerangTips", WoodenShape, middle, 6,
                new[] { new Vector2(-1.0f, -PaintFrom), new Vector2(PaintFrom, 1.0f) }, PaintProud);

            var thrown = new GameObject("ThrownBoomerang");
            var held = new GameObject("Boomerang");
            try
            {
                var spinner = new GameObject("Spinner");
                spinner.transform.SetParent(thrown.transform, false);
                AddModel(spinner.transform, body, tips, wood, paint);
                thrown.AddComponent<ThrownBoomerang>().Spinner = spinner.transform;
                PrefabUtility.SaveAsPrefabAsset(thrown, ThrownPrefabPath);

                AddModel(held.transform, body, tips, wood, paint);
                var box = held.AddComponent<BoxCollider>();
                box.size = new Vector3(0.7f, WoodenShape.Thickness * 2.0f, 0.4f);
                held.AddComponent<BoomerangItem>();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(held, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(thrown);
                Object.DestroyImmediate(held);
            }
        }

        [MenuItem("CityX/Rebuild Giant Boomerang")]
        public static GameObject BuildGiant()
        {
            EnsureFolder();
            Material bone = HeldModelBaker.LitMaterial($"{Folder}/GiantBoomerangBone.mat", Bone, 0.0f, 0.3f);
            Material leather = HeldModelBaker.LitMaterial($"{Folder}/GiantBoomerangLeather.mat", Leather, 0.0f, 0.25f);
            Vector3 middle = MiddleOf(GiantShape);
            Mesh body = Loft($"{Folder}/GiantBoomerangBody.asset", "GiantBoomerangBody", GiantShape, middle, GiantSlices, new[] { new Vector2(-1.0f, 1.0f) }, 0.0f);
            Mesh wraps = Loft($"{Folder}/GiantBoomerangWraps.asset", "GiantBoomerangWraps", GiantShape, middle, 4, GiantWraps, WrapProud);

            var thrown = new GameObject("ThrownGiantBoomerang");
            var held = new GameObject("GiantBoomerang");
            try
            {
                var spinner = new GameObject("Spinner");
                spinner.transform.SetParent(thrown.transform, false);
                AddGiantModel(spinner.transform, body, wraps, bone, leather);
                var flying = thrown.AddComponent<ThrownBoomerang>();
                flying.Spinner = spinner.transform;
                flying.Giant = true;
                PrefabUtility.SaveAsPrefabAsset(thrown, ThrownGiantPrefabPath);

                // Carried by its grip: the model moved so the grip is the origin, its length from there toward the elbow along +z, its flat face +y.
                var model = new GameObject(GiantBoomerangItem.ModelName);
                model.transform.SetParent(held.transform, false);
                Vector3 grip = AlongTheV(GiantShape, GiantGripAt) - middle;
                Vector3 towardElbow = (AlongTheV(GiantShape, 0.0f) - AlongTheV(GiantShape, GiantGripAt)).normalized;
                Quaternion turn = Quaternion.Inverse(Quaternion.LookRotation(towardElbow, Vector3.up));
                model.transform.localRotation = turn;
                model.transform.localPosition = -(turn * grip);
                AddGiantModel(model.transform, body, wraps, bone, leather);
                var box = held.AddComponent<BoxCollider>();
                box.center = new Vector3(0.0f, 0.0f, 1.0f);
                box.size = new Vector3(0.6f, GiantShape.Thickness * 2.0f, 2.4f);
                var item = held.AddComponent<GiantBoomerangItem>();
                // Its outline in the carried frame, for the hair it lies over on the back (Items/BackClip): a V from tip to elbow to tip, as wide as its middle.
                Vector3 OnIt(float s) => turn * (AlongTheV(GiantShape, s) - AlongTheV(GiantShape, GiantGripAt));
                Vector3 tipA = OnIt(-1.0f), elbow = OnIt(0.0f), tipB = OnIt(1.0f);
                item.OutlineTipAndElbow = new Vector4(tipA.x, tipA.z, elbow.x, elbow.z);
                item.OutlineTipWidthDepth = new Vector4(tipB.x, tipB.z, GiantShape.WidthAtElbow * 0.5f, GiantShape.Thickness * 0.5f);
                item.OutlineTipHalfWidth = GiantShape.WidthAtTip * 0.5f;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(held, GiantPrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(thrown);
                Object.DestroyImmediate(held);
            }
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Boomerang");
            }
        }

        private static void AddModel(Transform parent, Mesh body, Mesh tips, Material wood, Material paint)
        {
            HeldModelBaker.Part(parent, "Body", body, wood);
            HeldModelBaker.Part(parent, "Tips", tips, paint);
        }

        private static void AddGiantModel(Transform parent, Mesh body, Mesh wraps, Material bone, Material leather)
        {
            HeldModelBaker.Part(parent, "Blade", body, bone);
            HeldModelBaker.Part(parent, "Wraps", wraps, leather);
        }

        /// <summary>Where along the V, s from -1 (one tip) to 1 (the other): flat in XZ, the elbow rounded by a soft absolute value.</summary>
        private static Vector3 AlongTheV(in Shape shape, float s)
        {
            float softAbs = Mathf.Sqrt(s * s + shape.Roundness * shape.Roundness) - shape.Roundness;
            float open = shape.OpenDegrees * Mathf.Deg2Rad;
            return new Vector3(s * shape.Arm * Mathf.Sin(open), 0.0f, -softAbs * shape.Arm * Mathf.Cos(open));
        }

        /// <summary>The middle of the whole V: what it spins about.</summary>
        private static Vector3 MiddleOf(in Shape shape)
        {
            Vector3 middle = Vector3.zero;
            for (int i = 0; i <= Slices; i++)
            {
                middle += AlongTheV(shape, i * 2.0f / Slices - 1.0f);
            }
            return middle / (Slices + 1);
        }

        /// <summary>The V's stretches <paramref name="spans"/> (each from, to along it) lofted, <paramref name="proud"/> proud of its surface, about <paramref name="middle"/>.</summary>
        private static Mesh Loft(string path, string name, in Shape shape, Vector3 middle, int slices, Vector2[] spans, float proud)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (Vector2 span in spans)
            {
                AddLoft(vertices, triangles, shape, span.x, span.y, slices, middle, proud);
            }

            var worked = new Mesh();
            worked.SetVertices(vertices);
            worked.SetTriangles(triangles, 0);
            worked.RecalculateNormals();
            Vector3[] normals = worked.normals;
            Object.DestroyImmediate(worked);
            return HeldModelBaker.Save(path, name, vertices.ToArray(), normals, triangles.ToArray());
        }

        private static void AddLoft(List<Vector3> vertices, List<int> triangles, in Shape shape, float from, float to, int slices, Vector3 middle, float proud)
        {
            int sides = shape.Sides;
            int start = vertices.Count;
            for (int i = 0; i <= slices; i++)
            {
                float s = Mathf.Lerp(from, to, i / (float)slices);
                Vector3 centre = AlongTheV(shape, s) - middle;
                Vector3 ahead = AlongTheV(shape, Mathf.Min(1.0f, s + 0.01f)) - AlongTheV(shape, Mathf.Max(-1.0f, s - 0.01f));
                Vector3 across = Vector3.Cross(Vector3.up, ahead.normalized);

                // Rounded to a point over the last of each arm.
                float edge = Mathf.Abs(s);
                float round = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - Mathf.Pow(Mathf.InverseLerp(0.88f, 1.0f, edge), 2.0f)));
                float half = Mathf.Lerp(shape.WidthAtElbow, shape.WidthAtTip, edge) * 0.5f * Mathf.Max(0.05f, round) + proud;
                float high = shape.Thickness * 0.5f * Mathf.Max(0.05f, round) + proud;
                if (sides == 6)
                {
                    // The wooden one's six-sided slice, as it always was.
                    vertices.Add(centre - across * half);
                    vertices.Add(centre - across * (half * 0.5f) + Vector3.up * high);
                    vertices.Add(centre + across * (half * 0.5f) + Vector3.up * high);
                    vertices.Add(centre + across * half);
                    vertices.Add(centre + across * (half * 0.5f) - Vector3.up * high);
                    vertices.Add(centre - across * (half * 0.5f) - Vector3.up * high);
                }
                else
                {
                    // A rounder slice: an ellipse, the blade's edges thinned to an edge.
                    for (int k = 0; k < sides; k++)
                    {
                        float a = Mathf.PI - k * 2.0f * Mathf.PI / sides;
                        float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                        float thin = Mathf.Lerp(1.0f, 0.55f, Mathf.Abs(c));
                        vertices.Add(centre + across * (c * half) + Vector3.up * (sn * high * thin));
                    }
                }
            }
            for (int i = 0; i < slices; i++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    int a = start + i * sides + side, b = start + i * sides + next;
                    int c = start + (i + 1) * sides + side, d = start + (i + 1) * sides + next;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }
            for (int side = 1; side < sides - 1; side++)
            {
                triangles.Add(start); triangles.Add(start + side); triangles.Add(start + side + 1);
                int end = start + slices * sides;
                triangles.Add(end); triangles.Add(end + side + 1); triangles.Add(end + side);
            }
        }
    }
}
