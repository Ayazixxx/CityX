using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds JerryCan.prefab (docs/FIRE.md; owner: "doesnt need to look exactly like the jerry can with all the details just make it red and
    /// in the same shape for now"): GTA V's can -- a flat red steel box with an X pressed into each broad side, three handle bars along the
    /// top and a spout at one top corner -- at a real can's size times the city's 1.3, out of scaled primitives (as the flamethrower is), no
    /// textures. The origin is the middle handle bar, which is what the hand holds.
    /// </summary>
    public static class JerryCanBuilder
    {
        public const string PrefabPath = "Assets/CityX/Weapons/JerryCan.prefab";
        private const string RedMaterialPath = "Assets/CityX/Weapons/JerryCanRed.mat";
        private const string MetalMaterialPath = "Assets/CityX/Weapons/JerryCanMetal.mat";

        private static readonly Color Red = new Color(0.68f, 0.07f, 0.05f);
        private static readonly Color Metal = new Color(0.14f, 0.14f, 0.15f);

        /// <summary>A real jerry can is 34 x 47 x 17 cm; the city's 1.3 makes it this. Width along X, height along Y, depth along Z.</summary>
        private const float Width = 0.44f;

        private const float Height = 0.61f;
        private const float Depth = 0.22f;

        /// <summary>The handle bars sit this far over the can's top, the middle one at the origin.</summary>
        private const float HandleRise = 0.07f;

        private const float BarRadius = 0.016f;
        private const float BarLength = 0.26f;
        private const float RidgeThickness = 0.012f;

        [MenuItem("CityX/Rebuild Jerry Can")]
        public static GameObject Build()
        {
            Material red = HeldModelBaker.LitMaterial(RedMaterialPath, Red, 0.35f, 0.5f);
            Material metal = HeldModelBaker.LitMaterial(MetalMaterialPath, Metal, 0.7f, 0.35f);
            var root = new GameObject("JerryCan");
            try
            {
                float top = -HandleRise;
                Vector3 bodyCentre = new Vector3(0.0f, top - Height * 0.5f, 0.0f);
                Box(root.transform, "Body", bodyCentre, Quaternion.identity, new Vector3(Width, Height, Depth), red);
                // The X pressed into each broad side: two raised ridges across it, corner to corner.
                float diagonal = Mathf.Sqrt(Width * Width * 0.8f + Height * Height * 0.8f);
                float angle = Mathf.Atan2(Height, Width) * Mathf.Rad2Deg;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 face = bodyCentre + Vector3.forward * (side * (Depth * 0.5f + RidgeThickness * 0.4f));
                    Box(root.transform, "Ridge", face, Quaternion.Euler(0.0f, 0.0f, angle), new Vector3(diagonal, 0.03f, RidgeThickness), red);
                    Box(root.transform, "Ridge", face, Quaternion.Euler(0.0f, 0.0f, -angle), new Vector3(diagonal, 0.03f, RidgeThickness), red);
                }
                // Three handle bars along the top, the middle one at the origin, on two end posts.
                for (int bar = -1; bar <= 1; bar++)
                {
                    Cylinder(root.transform, "Handle", new Vector3(0.0f, 0.0f, bar * 0.05f), Quaternion.Euler(0.0f, 0.0f, 90.0f), BarRadius, BarLength, red);
                }
                for (int end = -1; end <= 1; end += 2)
                {
                    Box(root.transform, "HandlePost", new Vector3(end * BarLength * 0.5f, top * 0.5f, 0.0f), Quaternion.identity, new Vector3(0.03f, HandleRise + 0.01f, 0.13f), red);
                }
                // The spout: a short tube at the top right corner, leaning out, its mouth where the gasoline comes from.
                var spoutRoot = new GameObject("SpoutRoot");
                spoutRoot.transform.SetParent(root.transform, false);
                spoutRoot.transform.localPosition = new Vector3(Width * 0.5f - 0.05f, top, 0.0f);
                spoutRoot.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, -35.0f);
                Cylinder(spoutRoot.transform, "SpoutTube", new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity, 0.026f, 0.1f, metal);
                Cylinder(spoutRoot.transform, "SpoutCap", new Vector3(0.0f, 0.105f, 0.0f), Quaternion.identity, 0.03f, 0.014f, metal);
                var spout = new GameObject("Spout").transform;
                spout.SetParent(spoutRoot.transform, false);
                spout.localPosition = new Vector3(0.0f, 0.115f, 0.0f);

                var collider = root.AddComponent<BoxCollider>();
                collider.center = bodyCentre;
                collider.size = new Vector3(Width, Height + HandleRise, Depth);

                var item = root.AddComponent<JerryCanItem>();
                item.Spout = spout;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Box(Transform parent, string name, Vector3 centre, Quaternion rotation, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Place(box, parent, name, centre, rotation, size, material);
        }

        /// <summary>Unity's primitive cylinder is 2 units tall with radius 1, along Y.</summary>
        private static void Cylinder(Transform parent, string name, Vector3 centre, Quaternion rotation, float radius, float length, Material material)
        {
            GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Place(tube, parent, name, centre, rotation, new Vector3(radius * 2.0f, length * 0.5f, radius * 2.0f), material);
        }

        private static void Place(GameObject primitive, Transform parent, string name, Vector3 centre, Quaternion rotation, Vector3 scale, Material material)
        {
            primitive.name = name;
            Object.DestroyImmediate(primitive.GetComponent<Collider>());
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = centre;
            primitive.transform.localRotation = rotation;
            primitive.transform.localScale = scale;
            primitive.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
