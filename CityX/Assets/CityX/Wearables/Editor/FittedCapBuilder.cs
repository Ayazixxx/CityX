using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the fitted caps (docs/FITTED_CAP.md; owner, 2026-11-24: "make a basic fitted cap model we can make a bunch of colorways for use this
    /// links as guide to help you build it but make it fitted to our playermodel's round head like the red circle image i attached"): one model,
    /// made here from its measures about the head's centre (the Head bone's pivot), and one prefab a colourway (FittedCapColourways), its three
    /// parts (crown and brim, the brim's underside, the button) each in its colourway's colour.
    ///
    /// The model, as a fitted cap is: a ROUND crown (the head is a ball, and seen from above the crown is a circle, as the owner drew it) of six
    /// panels, each puffed a little so the outline draws the seams between them, hugging the head <see cref="CrownGap"/> off it, its band low at
    /// the back and above the eyes in front; a stiff brim out in front, as wide as the crown where it meets it, its sides running straight
    /// forward and round at the front, sloping down a little and its sides a little more; a button on top.
    /// </summary>
    public static class FittedCapBuilder
    {
        public const string Folder = "Assets/CityX/Wearables/FittedCaps";
        private const string MeshPath = Folder + "/FittedCapMesh.asset";

        /// <summary>The cloth this far off the head, metres.</summary>
        public const float CrownGap = 0.012f;

        /// <summary>The band's height above the head's centre in front (above the eyes, on the forehead) and behind (low on the back of the head).</summary>
        public const float BandFront = 0.10f, BandBack = -0.05f;

        /// <summary>How far each of the six panels puffs out at its middle, metres: the seams between them show as creases.</summary>
        public const float PanelPuff = 0.007f;

        public const int Panels = 6;

        /// <summary>How far the brim reaches out in front of the crown at its middle, metres.</summary>
        public const float BrimReach = 0.27f;

        /// <summary>How fast the brim's front comes round at its sides (smaller is squarer).</summary>
        public const float BrimRound = 0.35f;

        /// <summary>How far the brim slopes down over its reach, and its sides down more, metres.</summary>
        public const float BrimSlope = 0.035f, BrimDroop = 0.04f;

        public const float BrimThickness = 0.009f;

        public const float ButtonRadius = 0.024f;

        private const int CrownSegmentsPerPanel = 8, CrownRows = 14, BrimAcross = 32, BrimOut = 6, ButtonSegments = 12;

        private const float Smoothness = 0.15f;

        /// <summary>The model's three parts, its submeshes in order.</summary>
        public enum Part { Crown, UnderBrim, Button }

        [MenuItem("CityX/Rebuild Fitted Caps")]
        public static void BuildAllFromMenu() => BuildAll();

        public static Inventory.ItemPrefabEntry[] BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Wearables", "FittedCaps");
            }
            Mesh mesh = SaveMesh(BuildMesh());
            var entries = new List<Inventory.ItemPrefabEntry>();
            foreach (FittedCapColourways.Colourway colourway in FittedCapColourways.All)
            {
                GameObject prefab = Save(colourway, mesh);
                if (prefab != null)
                {
                    entries.Add(new Inventory.ItemPrefabEntry { ItemId = colourway.ItemId, Prefab = prefab });
                }
            }
            AssetDatabase.SaveAssets();
            return entries.ToArray();
        }

        private static GameObject Save(FittedCapColourways.Colourway colourway, Mesh mesh)
        {
            var materials = new[]
            {
                BlenderGarmentMesh.PlainMaterial($"{Folder}/{colourway.ItemId}_crown.mat", colourway.Crown, Smoothness),
                BlenderGarmentMesh.PlainMaterial($"{Folder}/{colourway.ItemId}_underbrim.mat", colourway.UnderBrim, Smoothness),
                BlenderGarmentMesh.PlainMaterial($"{Folder}/{colourway.ItemId}_button.mat", colourway.Button, Smoothness),
            };
            var root = new GameObject(colourway.ItemId);
            try
            {
                var model = new GameObject(FittedCap.ModelName);
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>().sharedMaterials = materials;
                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GroundWearable.InteractRange;
                root.AddComponent<FittedCap>().SetItemId(colourway.ItemId);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, FittedCapColourways.PrefabPath(colourway.ItemId));
                Debug.Log($"CityX: {colourway.DisplayName} written to {FittedCapColourways.PrefabPath(colourway.ItemId)} -- {mesh.triangles.Length / 3} triangles");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh SaveMesh(Mesh mesh)
        {
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mesh, MeshPath);
                return mesh;
            }
            saved.Clear();
            EditorUtility.CopySerialized(mesh, saved);
            Object.DestroyImmediate(mesh);
            return saved;
        }

        /// <summary>The crown's radius: the head's, and the gap.</summary>
        public static float CrownRadius => FusedBodyShape.HeadRadius + CrownGap;

        /// <summary>The band's height round the head, <paramref name="around"/> radians from straight ahead (+z) toward the right (+x).</summary>
        public static float BandHeight(float around) => (BandFront + BandBack) * 0.5f + (BandFront - BandBack) * 0.5f * Mathf.Cos(around);

        /// <summary>A point of the band, on the crown's sphere, <paramref name="around"/> radians from straight ahead.</summary>
        public static Vector3 BandPoint(float around)
        {
            float y = BandHeight(around);
            float flat = Mathf.Sqrt(CrownRadius * CrownRadius - y * y);
            return new Vector3(flat * Mathf.Sin(around), y, flat * Mathf.Cos(around));
        }

        /// <summary>The model, about the head's centre (+y up, +z ahead), its submeshes the <see cref="Part"/>s.</summary>
        public static Mesh BuildMesh()
        {
            var points = new List<Vector3>();
            var parts = new[] { new List<int>(), new List<int>(), new List<int>() };

            // The crown: six panels from the band up to the top, each its own points, so the seams between them stay sharp.
            int columns = CrownSegmentsPerPanel;
            for (int panel = 0; panel < Panels; panel++)
            {
                int start = points.Count;
                for (int c = 0; c <= columns; c++)
                {
                    float across = (float)c / columns;
                    float around = (panel + across) * Mathf.PI * 2.0f / Panels;
                    float bandAngle = Mathf.Acos(BandHeight(around) / CrownRadius);   // from the top down to the band
                    for (int r = 0; r <= CrownRows; r++)
                    {
                        float up = (float)r / CrownRows;   // 0 at the band, 1 at the top
                        float fromTop = bandAngle * (1.0f - up);
                        float puff = PanelPuff * Mathf.Sin(Mathf.PI * across) * Mathf.Sin(Mathf.PI * Mathf.Min(1.0f, (1.0f - up) * 1.6f) * 0.5f);
                        float radius = CrownRadius + puff;
                        points.Add(new Vector3(radius * Mathf.Sin(fromTop) * Mathf.Sin(around), radius * Mathf.Cos(fromTop),
                            radius * Mathf.Sin(fromTop) * Mathf.Cos(around)));
                    }
                }
                int rows = CrownRows + 1;
                for (int c = 0; c < columns; c++)
                {
                    for (int r = 0; r < CrownRows; r++)
                    {
                        int a = start + c * rows + r, b = a + 1, d = a + rows, e = d + 1;
                        Facing(points, parts[(int)Part.Crown], a, d, b, Vector3.zero, true);
                        Facing(points, parts[(int)Part.Crown], d, e, b, Vector3.zero, true);
                    }
                }
            }

            // The brim: from the band's front half, out ahead; its top in the crown's colour, its underside in its own, a rim between.
            int top = points.Count;
            for (int i = 0; i <= BrimAcross; i++)
            {
                float side = Mathf.Lerp(-1.0f, 1.0f, (float)i / BrimAcross);
                Vector3 inner = BandPoint(side * Mathf.PI * 0.5f);
                float reach = BrimReach * Mathf.Pow(Mathf.Max(0.0f, 1.0f - side * side), BrimRound);
                for (int o = 0; o <= BrimOut; o++)
                {
                    // Down as far as it reaches out: where it meets the crown's sides it hardly reaches, and must not hang there.
                    float t = (float)o / BrimOut;
                    points.Add(inner + new Vector3(0.0f, -(BrimSlope + BrimDroop * side * side) * reach * t / BrimReach, reach * t));
                }
            }
            int count = points.Count - top;
            for (int k = 0; k < count; k++)
            {
                points.Add(points[top + k] + Vector3.down * BrimThickness);
            }
            int bottom = top + count, step = BrimOut + 1;
            for (int i = 0; i < BrimAcross; i++)
            {
                for (int o = 0; o < BrimOut; o++)
                {
                    int a = i * step + o, b = a + 1, d = a + step, e = d + 1;
                    Facing(points, parts[(int)Part.Crown], top + a, top + d, top + b, Vector3.up, false);
                    Facing(points, parts[(int)Part.Crown], top + d, top + e, top + b, Vector3.up, false);
                    Facing(points, parts[(int)Part.UnderBrim], bottom + a, bottom + b, bottom + d, Vector3.down, false);
                    Facing(points, parts[(int)Part.UnderBrim], bottom + d, bottom + b, bottom + e, Vector3.down, false);
                }
                // The rim along the brim's outer edge: its own points, so the edge stays crisp.
                int rim = points.Count;
                Vector3 p0 = points[top + i * step + BrimOut], p1 = points[top + (i + 1) * step + BrimOut];
                points.Add(p0);
                points.Add(p1);
                points.Add(p0 + Vector3.down * BrimThickness);
                points.Add(p1 + Vector3.down * BrimThickness);
                Vector3 outward = Vector3.Cross(p1 - p0, Vector3.up).normalized;
                outward = Vector3.Dot(outward, (p0 + p1) * 0.5f) < 0.0f ? -outward : outward;
                Facing(points, parts[(int)Part.Crown], rim, rim + 1, rim + 2, outward, false);
                Facing(points, parts[(int)Part.Crown], rim + 1, rim + 3, rim + 2, outward, false);
            }

            // The button on top: a little dome.
            Vector3 crownTop = new Vector3(0.0f, CrownRadius, 0.0f);
            int button = points.Count;
            for (int ring = 0; ring <= 3; ring++)
            {
                float lift = ring / 3.0f * Mathf.PI * 0.5f;
                for (int s = 0; s <= ButtonSegments; s++)
                {
                    float around = s * Mathf.PI * 2.0f / ButtonSegments;
                    points.Add(crownTop + new Vector3(ButtonRadius * Mathf.Cos(lift) * Mathf.Sin(around), ButtonRadius * 0.6f * Mathf.Sin(lift),
                        ButtonRadius * Mathf.Cos(lift) * Mathf.Cos(around)));
                }
            }
            for (int ring = 0; ring < 3; ring++)
            {
                for (int s = 0; s < ButtonSegments; s++)
                {
                    int a = button + ring * (ButtonSegments + 1) + s, b = a + 1, d = a + ButtonSegments + 1, e = d + 1;
                    Facing(points, parts[(int)Part.Button], a, b, d, crownTop - Vector3.up * ButtonRadius, true);
                    Facing(points, parts[(int)Part.Button], b, e, d, crownTop - Vector3.up * ButtonRadius, true);
                }
            }

            var mesh = new Mesh { name = "FittedCapMesh" };
            mesh.SetVertices(points);
            mesh.SetUVs(0, new List<Vector2>(new Vector2[points.Count]));
            mesh.subMeshCount = parts.Length;
            for (int i = 0; i < parts.Length; i++)
            {
                mesh.SetTriangles(parts[i], i);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The triangle a, b, c into <paramref name="triangles"/>, wound to face away from <paramref name="from"/> (a point, when
        /// <paramref name="isPoint"/>) or along it (a direction).
        /// </summary>
        private static void Facing(List<Vector3> points, List<int> triangles, int a, int b, int c, Vector3 from, bool isPoint)
        {
            Vector3 normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
            Vector3 wanted = isPoint ? (points[a] + points[b] + points[c]) / 3.0f - from : from;
            if (Vector3.Dot(normal, wanted) < 0.0f)
            {
                (b, c) = (c, b);
            }
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }
}
