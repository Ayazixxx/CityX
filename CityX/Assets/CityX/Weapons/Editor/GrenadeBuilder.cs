using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the frag grenade (docs/GRENADE.md), drawn in code as the Godot game drew its own (weapons/grenade.gd): an olive egg turned in
    /// code, a grey fuse cap on top with the spoon down its side, and on the thrown one the fuse light. Grenade.prefab is the one in the hand
    /// (GrenadeItem), ThrownGrenade.prefab the one in flight (a rigid body, ThrownGrenade). Sizes 1.3 times life, as the city is.
    /// </summary>
    public static class GrenadeBuilder
    {
        private const string Folder = "Assets/CityX/Weapons/Grenade";
        public const string PrefabPath = "Assets/CityX/Weapons/Grenade.prefab";
        public const string ThrownPrefabPath = "Assets/CityX/Weapons/ThrownGrenade.prefab";

        private const int Sides = 14;

        private static readonly Color Olive = new Color(0.2f, 0.25f, 0.16f);
        private static readonly Color Steel = new Color(0.46f, 0.47f, 0.49f);
        private static readonly Color Ember = new Color(0.55f, 0.08f, 0.03f);

        /// <summary>The body: an egg, a little fuller below (radius, height), metres.</summary>
        private static readonly Vector2[] BodyProfile =
        {
            new Vector2(0.0f, -0.075f), new Vector2(0.035f, -0.068f), new Vector2(0.052f, -0.045f), new Vector2(0.058f, -0.01f),
            new Vector2(0.055f, 0.025f), new Vector2(0.045f, 0.05f), new Vector2(0.03f, 0.065f), new Vector2(0.022f, 0.07f),
        };

        /// <summary>The fuse cap on top.</summary>
        private static readonly Vector2[] CapProfile =
        {
            new Vector2(0.024f, 0.064f), new Vector2(0.026f, 0.07f), new Vector2(0.026f, 0.085f), new Vector2(0.02f, 0.09f), new Vector2(0.0f, 0.092f),
        };

        /// <summary>The fuse light: a little bead on the cap.</summary>
        private static readonly Vector2[] BeadProfile =
        {
            new Vector2(0.0f, 0.089f), new Vector2(0.009f, 0.092f), new Vector2(0.011f, 0.098f), new Vector2(0.009f, 0.104f), new Vector2(0.0f, 0.107f),
        };

        public const float Radius = 0.058f;
        public const float Height = 0.15f;

        [MenuItem("CityX/Rebuild Grenade")]
        public static GameObject Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Grenade");
            }
            Material olive = HeldModelBaker.LitMaterial($"{Folder}/GrenadeBody.mat", Olive, 0.3f, 0.3f);
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/GrenadeSteel.mat", Steel, 0.8f, 0.45f);
            Material ember = HeldModelBaker.LitMaterial($"{Folder}/GrenadeFuse.mat", Ember, 0.0f, 0.6f);
            Mesh body = HeldModelBaker.Turned($"{Folder}/GrenadeBody.asset", BodyProfile, "GrenadeBody", Sides);
            Mesh cap = HeldModelBaker.Turned($"{Folder}/GrenadeCap.asset", CapProfile, "GrenadeCap", Sides);
            Mesh bead = HeldModelBaker.Turned($"{Folder}/GrenadeBead.asset", BeadProfile, "GrenadeBead", 8);

            GameObject thrown = Model("ThrownGrenade", body, cap, olive, steel);
            GameObject held = Model("Grenade", body, cap, olive, steel);
            try
            {
                Transform blinker = HeldModelBaker.Part(thrown.transform, "FuseLight", bead, ember);
                var rigidbody = thrown.AddComponent<Rigidbody>();
                rigidbody.mass = GrenadeRules.Mass;
                thrown.AddComponent<ThrownGrenade>().Blinker = blinker.GetComponent<Renderer>();
                GameObject savedThrown = PrefabUtility.SaveAsPrefabAsset(thrown, ThrownPrefabPath);

                held.AddComponent<GrenadeItem>().ThrownPrefab = savedThrown;
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

        /// <summary>The egg, its cap and its spoon, and a capsule to bounce on.</summary>
        private static GameObject Model(string name, Mesh body, Mesh cap, Material olive, Material steel)
        {
            var root = new GameObject(name);
            HeldModelBaker.Part(root.transform, "Body", body, olive);
            HeldModelBaker.Part(root.transform, "Cap", cap, steel);
            GameObject lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lever.name = "Spoon";
            Object.DestroyImmediate(lever.GetComponent<Collider>());
            lever.transform.SetParent(root.transform, false);
            lever.transform.localPosition = new Vector3(Radius * 0.98f, 0.012f, 0.0f);
            lever.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, -6.0f);
            lever.transform.localScale = new Vector3(0.012f, 0.12f, 0.024f);
            lever.GetComponent<MeshRenderer>().sharedMaterial = steel;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = Radius;
            collider.height = Height + 0.03f;
            collider.center = new Vector3(0.0f, 0.008f, 0.0f);
            return root;
        }
    }
}
