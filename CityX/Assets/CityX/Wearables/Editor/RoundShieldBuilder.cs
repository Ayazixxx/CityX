using System.Collections.Generic;
using CityX.Wearables;
using CityX.WeaponsEditor;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds RoundShield.prefab (owner, 2026-10-27: "add a basic round, 1 handed shield il do a better model later just do something that
    /// functions for testing now"; docs/SHIELD.md), drawn in code: a wooden disc, its face a shallow dome, an iron band round its edge and an
    /// iron boss in the middle. Its face looks along +z, its top along +y; the body places it (PlayerLocomotion.Shield.cs). Sized for this
    /// body (2.6 m: a real round shield's 70-80 cm is about a metre here).
    /// </summary>
    public static class RoundShieldBuilder
    {
        public const string PrefabPath = "Assets/CityX/Wearables/RoundShield.prefab";
        private const string WoodMeshPath = "Assets/CityX/Wearables/RoundShieldWood.asset";
        private const string IronMeshPath = "Assets/CityX/Wearables/RoundShieldIron.asset";
        private const string WoodMaterialPath = "Assets/CityX/Wearables/RoundShieldWood.mat";
        private const string IronMaterialPath = "Assets/CityX/Wearables/RoundShieldIron.mat";

        public const float Radius = 0.48f;
        public const float Thickness = 0.04f;
        public const float Dome = 0.05f;
        public const float BossRadius = 0.12f;
        public const float BandWidth = 0.035f;
        private const int Segments = 32;
        private const int Rings = 6;

        [MenuItem("CityX/Rebuild Round Shield")]
        public static GameObject Build()
        {
            Mesh wood = Save(WoodMeshPath, "RoundShieldWood", Wood());
            Mesh iron = Save(IronMeshPath, "RoundShieldIron", Iron());
            Material woodMaterial = HeldModelBaker.LitMaterial(WoodMaterialPath, new Color(0.47f, 0.31f, 0.18f), 0.0f, 0.2f);
            Material ironMaterial = HeldModelBaker.LitMaterial(IronMaterialPath, new Color(0.36f, 0.37f, 0.4f), 0.8f, 0.45f);

            var root = new GameObject("RoundShield");
            try
            {
                var model = new GameObject(RoundShield.ModelName);
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = wood;
                model.AddComponent<MeshRenderer>().sharedMaterial = woodMaterial;
                var band = new GameObject("Iron");
                band.transform.SetParent(root.transform, false);
                band.AddComponent<MeshFilter>().sharedMesh = iron;
                band.AddComponent<MeshRenderer>().sharedMaterial = ironMaterial;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GroundWearable.InteractRange;
                root.AddComponent<RoundShield>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: RoundShield written to {PrefabPath} -- {(wood.triangles.Length + iron.triangles.Length) / 3} triangles");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>How far the face stands out at a distance <paramref name="r"/> from the middle: the dome, 0 at the edge.</summary>
        public static float FaceAt(float r) => Dome * (1.0f - (r / Radius) * (r / Radius));

        private sealed class Shape
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            public int Add(Vector3 v)
            {
                Vertices.Add(v);
                return Vertices.Count - 1;
            }

            public void Quad(int a, int b, int c, int d)
            {
                Triangles.AddRange(new[] { a, b, c, a, c, d });
            }
        }

        private static Vector3 Round(float r, int segment, float z)
        {
            float angle = segment * Mathf.PI * 2.0f / Segments;
            return new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, z);
        }

        /// <summary>The wooden disc: the domed face (rings out to the edge), the flat back, and the edge between them.</summary>
        private static Shape Wood()
        {
            var shape = new Shape();
            int centre = shape.Add(new Vector3(0.0f, 0.0f, FaceAt(0.0f)));
            int[] previous = null;
            for (int ring = 1; ring <= Rings; ring++)
            {
                float r = Radius * ring / Rings;
                var row = new int[Segments];
                for (int s = 0; s < Segments; s++)
                {
                    row[s] = shape.Add(Round(r, s, FaceAt(r)));
                }
                for (int s = 0; s < Segments; s++)
                {
                    int next = (s + 1) % Segments;
                    if (previous == null)
                    {
                        shape.Triangles.AddRange(new[] { centre, row[s], row[next] });
                    }
                    else
                    {
                        shape.Quad(previous[s], row[s], row[next], previous[next]);
                    }
                }
                previous = row;
            }
            int backCentre = shape.Add(new Vector3(0.0f, 0.0f, -Thickness));
            var back = new int[Segments];
            var edgeFront = new int[Segments];
            for (int s = 0; s < Segments; s++)
            {
                back[s] = shape.Add(Round(Radius, s, -Thickness));
                edgeFront[s] = shape.Add(Round(Radius, s, 0.0f));
            }
            for (int s = 0; s < Segments; s++)
            {
                int next = (s + 1) % Segments;
                shape.Triangles.AddRange(new[] { backCentre, back[next], back[s] });
                shape.Quad(edgeFront[s], back[s], back[next], edgeFront[next]);
            }
            return shape;
        }

        /// <summary>The iron: a band round the rim (its face and its outside) and the boss, a low dome in the middle.</summary>
        private static Shape Iron()
        {
            var shape = new Shape();
            float inner = Radius - BandWidth, outer = Radius + 0.008f;
            var innerFront = new int[Segments];
            var outerFront = new int[Segments];
            var outerBack = new int[Segments];
            for (int s = 0; s < Segments; s++)
            {
                innerFront[s] = shape.Add(Round(inner, s, FaceAt(inner) + 0.006f));
                outerFront[s] = shape.Add(Round(outer, s, 0.01f));
                outerBack[s] = shape.Add(Round(outer, s, -Thickness - 0.006f));
            }
            for (int s = 0; s < Segments; s++)
            {
                int next = (s + 1) % Segments;
                shape.Quad(innerFront[s], outerFront[s], outerFront[next], innerFront[next]);
                shape.Quad(outerFront[s], outerBack[s], outerBack[next], outerFront[next]);
            }

            float baseZ = FaceAt(0.0f) - 0.01f;
            int top = shape.Add(new Vector3(0.0f, 0.0f, baseZ + BossRadius * 0.6f));
            int[] previous = null;
            const int bossRings = 4;
            for (int ring = bossRings; ring >= 1; ring--)
            {
                float t = ring / (float)bossRings;
                float r = BossRadius * t;
                float z = baseZ + BossRadius * 0.6f * Mathf.Sqrt(1.0f - t * t);
                var row = new int[Segments];
                for (int s = 0; s < Segments; s++)
                {
                    row[s] = shape.Add(Round(r, s, z));
                }
                if (previous != null)
                {
                    for (int s = 0; s < Segments; s++)
                    {
                        int next = (s + 1) % Segments;
                        shape.Quad(previous[s], previous[next], row[next], row[s]);
                    }
                }
                previous = row;
            }
            for (int s = 0; s < Segments; s++)
            {
                shape.Triangles.AddRange(new[] { top, previous[s], previous[(s + 1) % Segments] });
            }
            return shape;
        }

        private static Mesh Save(string path, string name, Shape shape)
        {
            var scratch = new Mesh { vertices = shape.Vertices.ToArray(), triangles = shape.Triangles.ToArray() };
            scratch.RecalculateNormals();
            Mesh saved = HeldModelBaker.Save(path, name, scratch.vertices, scratch.normals, scratch.triangles);
            Object.DestroyImmediate(scratch);
            return saved;
        }
    }
}
