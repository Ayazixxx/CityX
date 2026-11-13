using CityX.Character;
using CityX.Items;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// What turns a body into a fully shared player: called for BOTH kinds of body (the scene's baked player and NetworkedPlayer.prefab), next to
    /// <see cref="PlayerAbilityWiring"/>. A new thing that must be visible to other players is added HERE, once.
    ///
    ///  * where the body is (<see cref="NetworkedBodyMotion"/>, added by each builder next to the NetworkObject);
    ///  * what the body holds or wears on its back (<see cref="NetworkedEquipmentSync"/>);
    ///  * damage between players (<see cref="NetworkedDamage"/>);
    ///  * proximity voice (<see cref="NetworkedVoice"/>, played by <see cref="VoicePlayback"/>);
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
            (ItemRegistry.Sledgehammer, "Assets/CityX/Weapons/Sledgehammer.prefab"),
            (ItemRegistry.KitchenKnife, "Assets/CityX/Weapons/KitchenKnife.prefab"),
            (ItemRegistry.ButterKnife, "Assets/CityX/Weapons/ButterKnife.prefab"),
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
            (ItemRegistry.MagicWand, "Assets/CityX/Weapons/MagicWand.prefab"),
            (ItemRegistry.MagicStaff, "Assets/CityX/Weapons/MagicStaff.prefab"),
            (ItemRegistry.WraithStaff, "Assets/CityX/Weapons/WraithStaff.prefab"),
            (ItemRegistry.Grenade, "Assets/CityX/Weapons/Grenade.prefab"),
            (ItemRegistry.Semtex, "Assets/CityX/Weapons/Semtex.prefab"),
            (ItemRegistry.WoodenBoomerang, "Assets/CityX/Weapons/Boomerang.prefab"),
            (ItemRegistry.GiantBoomerang, "Assets/CityX/Weapons/GiantBoomerang.prefab"),
            (ItemRegistry.JerryCan, "Assets/CityX/Weapons/JerryCan.prefab"),
            (ItemRegistry.Bow, "Assets/CityX/Weapons/Bow.prefab"),
            (ItemRegistry.Crossbow, "Assets/CityX/Weapons/Crossbow.prefab"),
            (ItemRegistry.Slingshot, "Assets/CityX/Weapons/Slingshot.prefab"),
            (ItemRegistry.KnightHelmet, "Assets/CityX/Wearables/KnightHelmet.prefab"),
            (ItemRegistry.FarmerHat, "Assets/CityX/Wearables/FarmerHat.prefab"),
            (ItemRegistry.ClawSkull, "Assets/CityX/Wearables/ClawSkull.prefab"),
            (ItemRegistry.RoundShield, "Assets/CityX/Wearables/RoundShield.prefab"),
            (ItemRegistry.HoodedCloak, "Assets/CityX/Wearables/HoodedCloak.prefab"),
            (ItemRegistry.MidnightCoat, "Assets/CityX/Wearables/MidnightCoat.prefab"),
            (ItemRegistry.TShirt, "Assets/CityX/Wearables/TShirt.prefab"),
            (ItemRegistry.BloodRavenTShirt, "Assets/CityX/Wearables/BloodRavenTShirt.prefab"),
            (ItemRegistry.RedCamoTShirt, "Assets/CityX/Wearables/RedCamoTShirt.prefab"),
            (ItemRegistry.BlackTShirt, "Assets/CityX/Wearables/BlackTShirt.prefab"),
            (ItemRegistry.CityXShirt, "Assets/CityX/Wearables/CityXTShirt.prefab"),
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

        /// <summary>
        /// What every player's body needs in a session. <paramref name="addOwnParts"/>: also what the scene builder gives the baked player by
        /// hand (the inventory, hold-E carry), for the clone a joining player plays.
        /// </summary>
        public static void AddSharedState(GameObject body, bool addOwnParts)
        {
            if (body.GetComponent<NetworkedEquipmentSync>() == null)
            {
                body.AddComponent<NetworkedEquipmentSync>();
            }
            if (body.GetComponent<NetworkedDamage>() == null)
            {
                body.AddComponent<NetworkedDamage>();
            }
            AddRagdollSync(body);
            // Proximity voice (docs/VOICE.md): the owner's microphone to the players near enough, played from this body's head on theirs.
            if (body.GetComponent<VoicePlayback>() == null)
            {
                body.AddComponent<VoicePlayback>();
            }
            if (body.GetComponent<NetworkedVoice>() == null)
            {
                body.AddComponent<NetworkedVoice>();
            }
            if (addOwnParts && body.GetComponent<Inventory>() == null)
            {
                body.AddComponent<Inventory>().ItemPrefabs = ItemPrefabEntries();
            }
            // Hold E to carry, as the baked player can (the scene builder gives it that one): the camera it looks from is given when this
            // machine claims the body (LocalBodyBinder).
            if (addOwnParts && body.GetComponent<GrabCarry>() == null)
            {
                body.AddComponent<GrabCarry>().Vitals = body.GetComponent<PlayerVitals>();
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
    }
}
