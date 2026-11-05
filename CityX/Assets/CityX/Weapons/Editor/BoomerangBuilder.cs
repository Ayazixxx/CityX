using System.Collections.Generic;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the Wooden Boomerang (docs/BOOMERANG.md), a stand-in drawn in code until the owner's model: a flat carved V, its tips painted so its spin
    /// reads. Boomerang.prefab is the one in the hand (BoomerangItem), ThrownBoomerang.prefab the one in the air. It lies flat with its middle at the
    /// origin, which is what it spins about. 70 cm tip to tip, well over the city's 1.3 times life, so that it reads in the air at its range.
    /// </summary>
    public static class BoomerangBuilder
    {
        private const string Folder = "Assets/CityX/Weapons/Boomerang";
        public const string PrefabPath = "Assets/CityX/Weapons/Boomerang.prefab";
        public const string ThrownPrefabPath = "Assets/CityX/Weapons/ThrownBoomerang.prefab";

        private static readonly Color Wood = new Color(0.55f, 0.34f, 0.17f);
        private static readonly Color Paint = new Color(0.72f, 0.16f, 0.1f);

        /// <summary>Each arm's length and how far it opens from straight down the middle, and how round the elbow is (a share of an arm).</summary>
        private const float ArmMetres = 0.45f;

        private const float ArmOpenDegrees = 50.0f;
        private const float ElbowRoundness = 0.12f;

        private const float WidthAtElbow = 0.115f;
        private const float WidthAtTip = 0.075f;
        private const float Thickness = 0.03f;

        /// <summary>Slices along it, and where along each arm (0 elbow, 1 tip) the paint starts.</summary>
        private const int Slices = 24;

        private const float PaintFrom = 0.72f;

        /// <summary>The paint stands this proud of the wood.</summary>
        private const float PaintProud = 0.0015f;

        [MenuItem("CityX/Rebuild Boomerang")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Boomerang");
            }
            Material wood = HeldModelBaker.LitMaterial($"{Folder}/BoomerangWood.mat", Wood, 0.0f, 0.35f);
            Material paint = HeldModelBaker.LitMaterial($"{Folder}/BoomerangPaint.mat", Paint, 0.0f, 0.45f);
            Mesh body = Loft($"{Folder}/BoomerangBody.asset", "BoomerangBody", 0.0f);
            Mesh tips = Loft($"{Folder}/BoomerangTips.asset", "BoomerangTips", PaintFrom);

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
                box.size = new Vector3(0.7f, Thickness * 2.0f, 0.4f);
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

        private static void AddModel(Transform parent, Mesh body, Mesh tips, Material wood, Material paint)
        {
            HeldModelBaker.Part(parent, "Body", body, wood);
            HeldModelBaker.Part(parent, "Tips", tips, paint);
        }

        /// <summary>Where along the V, s from -1 (one tip) to 1 (the other): flat in XZ, the elbow rounded by a soft absolute value.</summary>
        private static Vector3 AlongTheV(float s)
        {
            float softAbs = Mathf.Sqrt(s * s + ElbowRoundness * ElbowRoundness) - ElbowRoundness;
            float open = ArmOpenDegrees * Mathf.Deg2Rad;
            return new Vector3(s * ArmMetres * Mathf.Sin(open), 0.0f, -softAbs * ArmMetres * Mathf.Cos(open));
        }

        /// <summary>
        /// The V as a loft of six-sided slices, or with <paramref name="paintFrom"/> above 0 only its two painted ends. Its middle is put at the origin
        /// using the whole V either way, so the tips sit on the body.
        /// </summary>
        private static Mesh Loft(string path, string name, float paintFrom)
        {
            Vector3 middle = Vector3.zero;
            for (int i = 0; i <= Slices; i++)
            {
                middle += AlongTheV(i * 2.0f / Slices - 1.0f);
            }
            middle /= Slices + 1;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            if (paintFrom <= 0.0f)
            {
                AddLoft(vertices, triangles, -1.0f, 1.0f, Slices, middle, 0.0f);
            }
            else
            {
                const int ends = 6;
                AddLoft(vertices, triangles, -1.0f, -paintFrom, ends, middle, PaintProud);
                AddLoft(vertices, triangles, paintFrom, 1.0f, ends, middle, PaintProud);
            }

            var worked = new Mesh();
            worked.SetVertices(vertices);
            worked.SetTriangles(triangles, 0);
            worked.RecalculateNormals();
            Vector3[] normals = worked.normals;
            Object.DestroyImmediate(worked);
            return HeldModelBaker.Save(path, name, vertices.ToArray(), normals, triangles.ToArray());
        }

        private static void AddLoft(List<Vector3> vertices, List<int> triangles, float from, float to, int slices, Vector3 middle, float proud)
        {
            const int sides = 6;
            int start = vertices.Count;
            for (int i = 0; i <= slices; i++)
            {
                float s = Mathf.Lerp(from, to, i / (float)slices);
                Vector3 centre = AlongTheV(s) - middle;
                Vector3 ahead = AlongTheV(Mathf.Min(1.0f, s + 0.01f)) - AlongTheV(Mathf.Max(-1.0f, s - 0.01f));
                Vector3 across = Vector3.Cross(Vector3.up, ahead.normalized);

                // Rounded to a point over the last of each arm.
                float edge = Mathf.Abs(s);
                float round = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - Mathf.Pow(Mathf.InverseLerp(0.88f, 1.0f, edge), 2.0f)));
                float half = Mathf.Lerp(WidthAtElbow, WidthAtTip, edge) * 0.5f * Mathf.Max(0.05f, round) + proud;
                float high = Thickness * 0.5f * Mathf.Max(0.05f, round) + proud;
                vertices.Add(centre - across * half);
                vertices.Add(centre - across * (half * 0.5f) + Vector3.up * high);
                vertices.Add(centre + across * (half * 0.5f) + Vector3.up * high);
                vertices.Add(centre + across * half);
                vertices.Add(centre + across * (half * 0.5f) - Vector3.up * high);
                vertices.Add(centre - across * (half * 0.5f) - Vector3.up * high);
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
