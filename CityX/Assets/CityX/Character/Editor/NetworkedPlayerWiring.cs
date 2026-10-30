using CityX.Character;
using CityX.Items;
using FishNet.Component.Transforming;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// What turns a body into a fully shared player: called for BOTH kinds of body (the scene's baked player and NetworkedPlayer.prefab), next to
    /// <see cref="PlayerAbilityWiring"/>. A new thing that must be visible to other players is added HERE, once.
    ///
    ///  * smooth movement: the NetworkTransform interpolates over three ticks (100 ms at the default 30 Hz) and sends position and rotation unpacked, so
    ///    there is no stepping from rounding (the sword spin's fast turn used to arrive in visible steps);
    ///  * what the body holds or wears on its back (<see cref="NetworkedEquipmentSync"/>);
    ///  * damage between players (<see cref="NetworkedDamage"/>);
    ///  * the equipment list a copy builds its item models from (an Inventory with the item prefabs, which also lets a joining player equip things).
    /// </summary>
    public static class NetworkedPlayerWiring
    {
        private static readonly (string id, string path)[] ItemPrefabPaths =
        {
            (ItemRegistry.GlassBottle, "Assets/CityX/Weapons/GlassBottle.prefab"),
            (ItemRegistry.GrapplingHook, "Assets/CityX/Weapons/GrapplingHook.prefab"),
            (ItemRegistry.Pistol, "Assets/CityX/Weapons/Pistol.prefab"),
            (ItemRegistry.Jetpack, "Assets/CityX/Weapons/Jetpack.prefab"),
            (ItemRegistry.Parachute, "Assets/CityX/Weapons/Parachute.prefab"),
            (ItemRegistry.AngelWings, "Assets/CityX/Weapons/AngelWings.prefab"),
            (ItemRegistry.DarkAngelWings, "Assets/CityX/Weapons/DarkAngelWings.prefab"),
            (ItemRegistry.SpikeSword, "Assets/CityX/Weapons/SpikeSword.prefab"),
            (ItemRegistry.CombatSword, "Assets/CityX/Weapons/CombatSword.prefab"),
            (ItemRegistry.MetalCrowbar, "Assets/CityX/Weapons/MetalCrowbar.prefab"),
            (ItemRegistry.BaseballBat, "Assets/CityX/Weapons/BaseballBat.prefab"),
            (ItemRegistry.Flamethrower, "Assets/CityX/Weapons/Flamethrower.prefab"),
            (ItemRegistry.AK47, "Assets/CityX/Weapons/AK47.prefab"),
            (ItemRegistry.SprayCan, "Assets/CityX/Weapons/SprayCan.prefab"),
            (ItemRegistry.ArcaneStaff, "Assets/CityX/Weapons/ArcaneStaff.prefab"),
            (ItemRegistry.FireballStaff, "Assets/CityX/Weapons/FireballStaff.prefab"),
            (ItemRegistry.IceShardStaff, "Assets/CityX/Weapons/IceShardStaff.prefab"),
            (ItemRegistry.WaterStaff, "Assets/CityX/Weapons/WaterStaff.prefab"),
            (ItemRegistry.FlameStaff, "Assets/CityX/Weapons/FlameStaff.prefab"),
            (ItemRegistry.ArcaneBeamStaff, "Assets/CityX/Weapons/ArcaneBeamStaff.prefab"),
            (ItemRegistry.HealingStaff, "Assets/CityX/Weapons/HealingStaff.prefab"),
            (ItemRegistry.Flashlight, "Assets/CityX/Weapons/Flashlight.prefab"),
            (ItemRegistry.FrostStaff, "Assets/CityX/Weapons/FrostStaff.prefab"),
            (ItemRegistry.Grenade, "Assets/CityX/Weapons/Grenade.prefab"),
            (ItemRegistry.JerryCan, "Assets/CityX/Weapons/JerryCan.prefab"),
            (ItemRegistry.Bow, "Assets/CityX/Weapons/Bow.prefab"),
            (ItemRegistry.Crossbow, "Assets/CityX/Weapons/Crossbow.prefab"),
            (ItemRegistry.KnightHelmet, "Assets/CityX/Wearables/KnightHelmet.prefab"),
            (ItemRegistry.FarmerHat, "Assets/CityX/Wearables/FarmerHat.prefab"),
            (ItemRegistry.RoundShield, "Assets/CityX/Wearables/RoundShield.prefab"),
            (ItemRegistry.HoodedCloak, "Assets/CityX/Wearables/HoodedCloak.prefab"),
            (ItemRegistry.MidnightCoat, "Assets/CityX/Wearables/MidnightCoat.prefab"),
            (ItemRegistry.TShirt, "Assets/CityX/Wearables/TShirt.prefab"),
            (ItemRegistry.BloodRavenTShirt, "Assets/CityX/Wearables/BloodRavenTShirt.prefab"),
            (ItemRegistry.RedCamoTShirt, "Assets/CityX/Wearables/RedCamoTShirt.prefab"),
            (ItemRegistry.BlackTShirt, "Assets/CityX/Wearables/BlackTShirt.prefab"),
            (ItemRegistry.LongSleeveTShirt, "Assets/CityX/Wearables/LongSleeveTShirt.prefab"),
            (ItemRegistry.BlackLongSleeveTShirt, "Assets/CityX/Wearables/BlackLongSleeveTShirt.prefab"),
            (ItemRegistry.BlackJeans, "Assets/CityX/Wearables/BlackJeans.prefab"),
            (ItemRegistry.BlueJeans, "Assets/CityX/Wearables/BlueJeans.prefab"),
            (ItemRegistry.BredJeans, "Assets/CityX/Wearables/BredJeans.prefab"),
            (ItemRegistry.MaroonJeans, "Assets/CityX/Wearables/MaroonJeans.prefab"),
            (ItemRegistry.Khakis, "Assets/CityX/Wearables/Khakis.prefab"),
            (ItemRegistry.GreyJeans, "Assets/CityX/Wearables/GreyJeans.prefab"),
            (ItemRegistry.WhiteSneaker, "Assets/CityX/Wearables/WhiteSneaker.prefab"),
            (ItemRegistry.BWSneakers, "Assets/CityX/Wearables/BWSneakers.prefab"),
        };

        /// <summary>
        /// The item prefabs, as an Inventory lists them: every item, or those <paramref name="include"/> keeps (the select screen shows the
        /// clothes only).
        /// </summary>
        public static Inventory.ItemPrefabEntry[] ItemPrefabEntries(System.Func<ItemDefinition, bool> include = null)
        {
            var entries = new System.Collections.Generic.List<Inventory.ItemPrefabEntry>();
            foreach ((string id, string path) in ItemPrefabPaths)
            {
                if (include != null && !(ItemRegistry.TryGet(id, out ItemDefinition definition) && include(definition)))
                {
                    continue;
                }
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    entries.Add(new Inventory.ItemPrefabEntry { ItemId = id, Prefab = prefab });
                }
                else
                {
                    Debug.LogWarning($"CityX: no item prefab at {path}: a body cannot show '{id}'.");
                }
            }
            return entries.ToArray();
        }

        public static void AddSharedState(GameObject body, bool addInventory)
        {
            TuneTransform(body.GetComponent<NetworkTransform>());
            if (body.GetComponent<NetworkedEquipmentSync>() == null)
            {
                body.AddComponent<NetworkedEquipmentSync>();
            }
            if (body.GetComponent<NetworkedDamage>() == null)
            {
                body.AddComponent<NetworkedDamage>();
            }
            AddRagdollSync(body);
            if (addInventory && body.GetComponent<Inventory>() == null)
            {
                body.AddComponent<Inventory>().ItemPrefabs = ItemPrefabEntries();
            }
        }

        /// <summary>
        /// A body's limp pose, sent by its owner and laid on every copy (2026-10-29, docs/MULTIPLAYER_STATE.md): a player's or an enemy's body,
        /// whatever threw it limp.
        /// </summary>
        public static void AddRagdollSync(GameObject body)
        {
            if (body.GetComponent<Ragdoll>() == null)
            {
                return;
            }
            if (body.GetComponent<RagdollMirror>() == null)
            {
                body.AddComponent<RagdollMirror>();
            }
            if (body.GetComponent<NetworkedRagdollSync>() == null)
            {
                body.AddComponent<NetworkedRagdollSync>();
            }
        }

        /// <summary>Interpolate three ticks and send position and rotation unpacked: smooth, not stepped.</summary>
        public static void TuneTransform(NetworkTransform transform)
        {
            if (transform == null)
            {
                return;
            }
            var so = new SerializedObject(transform);
            SerializedProperty interpolation = so.FindProperty("_interpolation");
            if (interpolation != null)
            {
                interpolation.intValue = 3;
            }
            SerializedProperty position = so.FindProperty("_packing.Position");
            if (position != null)
            {
                position.enumValueIndex = 0;   // Unpacked
            }
            SerializedProperty rotation = so.FindProperty("_packing.Rotation");
            if (rotation != null)
            {
                rotation.enumValueIndex = 0;   // Unpacked
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
