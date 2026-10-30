using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the three staffs' prefabs (docs/STAFFS.md), drawn in code as stand-ins until the owner's own models ("just have it be a basic model
    /// for now"): turned shapes (HeldModelBaker.Lathe) for a shaft tapering to its foot, an iron or brass ferrule, a leather wrap where the hand
    /// holds, a cup at the head and the crystal it holds, glowing in the spell's colour. The Arcane Staff's crystal is a magenta orb in brass, the
    /// Fireball Staff's an ember in an iron brazier, the Ice Shard Staff's a faceted shard of ice in silver. The grip is the origin and the shaft
    /// runs up +Y (StaffItem holds it there); <see cref="StaffItem.Tip"/> is the crystal's middle. Sized for this body: about 2.4 m, the head
    /// above the 2.6 m body's own when carried.
    /// </summary>
    public static class StaffBuilder
    {
        private const string Folder = "Assets/CityX/Weapons/Staffs";
        public const string ArcanePrefabPath = "Assets/CityX/Weapons/ArcaneStaff.prefab";
        public const string FireballPrefabPath = "Assets/CityX/Weapons/FireballStaff.prefab";
        public const string IceShardPrefabPath = "Assets/CityX/Weapons/IceShardStaff.prefab";
        public const string WaterPrefabPath = "Assets/CityX/Weapons/WaterStaff.prefab";
        public const string FlamePrefabPath = "Assets/CityX/Weapons/FlameStaff.prefab";
        public const string ArcaneBeamPrefabPath = "Assets/CityX/Weapons/ArcaneBeamStaff.prefab";
        public const string HealingPrefabPath = "Assets/CityX/Weapons/HealingStaff.prefab";
        public const string FrostPrefabPath = "Assets/CityX/Weapons/FrostStaff.prefab";

        private const int Sides = 14;

        /// <summary>The head's middle, metres up the shaft from the grip: where the crystal sits and shots leave.</summary>
        public const float OrbHeight = 1.29f;

        public const float OrbRadius = 0.09f;

        public const float ShardHeight = 1.34f;

        private static readonly Vector2[] FerruleProfile =
        {
            new Vector2(0.0f, -0.98f), new Vector2(0.026f, -0.98f), new Vector2(0.03f, -0.95f), new Vector2(0.03f, -0.86f), new Vector2(0.022f, -0.84f),
        };

        private static readonly Vector2[] ShaftProfile =
        {
            new Vector2(0.022f, -0.88f), new Vector2(0.026f, 0.0f), new Vector2(0.03f, 1.1f),
        };

        private static readonly Vector2[] WrapProfile =
        {
            new Vector2(0.03f, -0.14f), new Vector2(0.036f, -0.12f), new Vector2(0.036f, 0.14f), new Vector2(0.03f, 0.16f),
        };

        private static readonly Vector2[] CupProfile =
        {
            new Vector2(0.028f, 1.02f), new Vector2(0.042f, 1.06f), new Vector2(0.04f, 1.12f), new Vector2(0.06f, 1.17f),
            new Vector2(0.092f, 1.23f), new Vector2(0.098f, 1.26f), new Vector2(0.088f, 1.27f),
        };

        private static readonly Vector2[] BrazierProfile =
        {
            new Vector2(0.028f, 1.0f), new Vector2(0.045f, 1.05f), new Vector2(0.045f, 1.1f), new Vector2(0.07f, 1.15f),
            new Vector2(0.11f, 1.24f), new Vector2(0.116f, 1.27f), new Vector2(0.1f, 1.28f),
        };

        private static readonly Vector2[] ShardProfile =
        {
            new Vector2(0.0f, ShardHeight - 0.16f), new Vector2(0.07f, ShardHeight - 0.02f), new Vector2(0.0f, ShardHeight + 0.2f),
        };

        private sealed class Look
        {
            public string Name;
            public Color Wood;
            public Color Metal;
            public Color Crystal;
            public float CrystalGlow;
            public Vector2[] Cup;
            public bool Shard;
        }

        [MenuItem("CityX/Rebuild Staffs")]
        public static void BuildAll()
        {
            BuildArcane();
            BuildFireball();
            BuildIceShard();
            BuildWater();
            BuildFlame();
            BuildArcaneBeam();
            BuildHealing();
            BuildFrost();
        }

        /// <summary>The Frost Staff (2026-10-29): pale ash wood bleached by the cold, steel, an ice-blue shard.</summary>
        public static GameObject BuildFrost() => Build<FrostStaff>(FrostPrefabPath, new Look
        {
            Name = "FrostStaff", Wood = new Color(0.78f, 0.82f, 0.86f), Metal = new Color(0.62f, 0.68f, 0.74f),
            Crystal = new Color(0.55f, 0.88f, 1.0f), CrystalGlow = 1.2f, Cup = CupProfile, Shard = true,
        });

        /// <summary>The Arcane Beam Staff (2026-10-29): near-black plum wood, silver, a magenta crystal shard (the Arcane Staff's is an orb).</summary>
        public static GameObject BuildArcaneBeam() => Build<ArcaneBeamStaff>(ArcaneBeamPrefabPath, new Look
        {
            Name = "ArcaneBeamStaff", Wood = new Color(0.12f, 0.06f, 0.11f), Metal = new Color(0.78f, 0.79f, 0.84f),
            Crystal = new Color(1.0f, 0.16f, 0.82f), CrystalGlow = 1.4f, Cup = CupProfile, Shard = true,
        });

        /// <summary>The Staff of Healing (2026-10-29): white wood, gold, a warm yellow orb (Mercy's staff is white and gold).</summary>
        public static GameObject BuildHealing() => Build<HealingStaff>(HealingPrefabPath, new Look
        {
            Name = "HealingStaff", Wood = new Color(0.9f, 0.88f, 0.82f), Metal = new Color(0.86f, 0.68f, 0.26f),
            Crystal = new Color(1.0f, 0.84f, 0.38f), CrystalGlow = 1.1f, Cup = CupProfile,
        });

        /// <summary>The flame staff (2026-10-28): charred black wood, iron, a brazier holding a red-orange ember.</summary>
        public static GameObject BuildFlame() => Build<FlameStaff>(FlamePrefabPath, new Look
        {
            Name = "FlameStaff", Wood = new Color(0.1f, 0.07f, 0.06f), Metal = new Color(0.28f, 0.24f, 0.22f),
            Crystal = new Color(1.0f, 0.42f, 0.08f), CrystalGlow = 1.3f, Cup = BrazierProfile,
        });

        /// <summary>The water staff (2026-10-28): sea-green wood, steel, a pale blue orb of water.</summary>
        public static GameObject BuildWater() => Build<WaterStaff>(WaterPrefabPath, new Look
        {
            Name = "WaterStaff", Wood = new Color(0.16f, 0.3f, 0.34f), Metal = new Color(0.6f, 0.68f, 0.74f),
            Crystal = new Color(0.45f, 0.78f, 1.0f), CrystalGlow = 0.6f, Cup = CupProfile,
        });

        public static GameObject BuildArcane() => Build<ArcaneStaff>(ArcanePrefabPath, new Look
        {
            Name = "ArcaneStaff", Wood = new Color(0.2f, 0.12f, 0.17f), Metal = new Color(0.78f, 0.62f, 0.3f),
            Crystal = new Color(1.0f, 0.15f, 0.8f), CrystalGlow = 1.0f, Cup = CupProfile,
        });

        public static GameObject BuildFireball() => Build<FireballStaff>(FireballPrefabPath, new Look
        {
            Name = "FireballStaff", Wood = new Color(0.28f, 0.12f, 0.07f), Metal = new Color(0.22f, 0.2f, 0.2f),
            Crystal = new Color(1.0f, 0.32f, 0.04f), CrystalGlow = 1.0f, Cup = BrazierProfile,
        });

        public static GameObject BuildIceShard() => Build<IceShardStaff>(IceShardPrefabPath, new Look
        {
            Name = "IceShardStaff", Wood = new Color(0.72f, 0.69f, 0.63f), Metal = new Color(0.74f, 0.79f, 0.84f),
            Crystal = new Color(0.55f, 0.85f, 1.0f), CrystalGlow = 0.8f, Cup = CupProfile, Shard = true,
        });

        private static GameObject Build<T>(string prefabPath, Look look) where T : StaffItem
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Staffs");
            }
            Material wood = HeldModelBaker.LitMaterial($"{Folder}/{look.Name}Wood.mat", look.Wood, 0.0f, 0.25f);
            Material metal = HeldModelBaker.LitMaterial($"{Folder}/{look.Name}Metal.mat", look.Metal, 0.85f, 0.5f);
            Material leather = HeldModelBaker.LitMaterial($"{Folder}/StaffLeather.mat", new Color(0.12f, 0.08f, 0.06f), 0.0f, 0.15f);
            Material crystal = HeldModelBaker.LitMaterial($"{Folder}/{look.Name}Crystal.mat", look.Crystal, 0.0f, 0.85f);
            crystal.EnableKeyword("_EMISSION");
            crystal.SetColor("_EmissionColor", look.Crystal * look.CrystalGlow);
            crystal.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(crystal);

            var root = new GameObject(look.Name);
            try
            {
                Part(root, "Shaft", Turned(ShaftProfile, "StaffShaft", Sides), wood);
                Part(root, "Ferrule", Turned(FerruleProfile, "StaffFerrule", Sides), metal);
                Part(root, "Wrap", Turned(WrapProfile, "StaffWrap", Sides), leather);
                Part(root, "Cup", Turned(look.Cup, look.Cup == BrazierProfile ? "StaffBrazier" : "StaffCup", Sides), metal);
                Mesh head = look.Shard ? Turned(ShardProfile, "StaffShard", 6) : Turned(OrbProfile(), "StaffOrb", 16);
                Part(root, "Crystal", head, crystal);

                var tip = new GameObject(StaffItem.TipName);
                tip.transform.SetParent(root.transform, false);
                tip.transform.localPosition = new Vector3(0.0f, look.Shard ? ShardHeight : OrbHeight, 0.0f);

                T staff = root.AddComponent<T>();
                staff.Tip = tip.transform;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: {look.Name} written to {prefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>A half circle up the axis: the orb at the staff's head, turned.</summary>
        private static Vector2[] OrbProfile()
        {
            const int steps = 10;
            var profile = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float angle = i * Mathf.PI / steps;
                profile[i] = new Vector2(Mathf.Sin(angle) * OrbRadius, OrbHeight - Mathf.Cos(angle) * OrbRadius);
            }
            return profile;
        }

        /// <summary>A turned shape saved as a mesh asset in the staffs' folder (shared by every staff that uses it).</summary>
        private static Mesh Turned(Vector2[] profile, string name, int sides)
        {
            return HeldModelBaker.Turned($"{Folder}/{name}.asset", profile, name, sides);
        }

        private static void Part(GameObject root, string name, Mesh mesh, Material material)
        {
            HeldModelBaker.Part(root.transform, name, mesh, material);
        }
    }
}
