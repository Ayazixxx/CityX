using System.Collections.Generic;
using System.Globalization;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Net;
using CityX.Vehicles;
using CityX.View;
using CityX.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FlightFeel = CityX.Weapons.FlightFeel;

namespace CityX.UI
{
    public sealed partial class DevConsoleView
    {
        /// <summary>The most one /spawn puts down.</summary>
        public const int MaxSpawnCount = 30;

        /// <summary>Several spawned at once stand in a row across the player's view, this far apart, metres.</summary>
        public const float SpawnSpacingMetres = 1.8f;

        /// <summary>While several are spawned at once, each one's own line is not logged (one line says how many).</summary>
        private bool _spawnQuiet;

        /// <summary>
        /// /spawn &lt;what&gt; [count] (owner, 2026-10-24: "if i add a number at the end of it after the item or mob i type out it will spawn that
        /// many"): one, or that many in a row across the player's view.
        /// </summary>
        private void Spawn(string what, string countText)
        {
            int count = 1;
            if (!string.IsNullOrEmpty(countText) && (!int.TryParse(countText, out count) || count < 1))
            {
                Log(DevCommands.UsageFor("spawn"));
                return;
            }
            count = Mathf.Min(count, MaxSpawnCount);
            Transform origin = Vitals != null ? Vitals.transform : transform;
            int made = 0;
            _spawnQuiet = count > 1;
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = origin.right * ((i - (count - 1) * 0.5f) * SpawnSpacingMetres);
                if (!SpawnOne(what, offset))
                {
                    break;
                }
                made++;
            }
            _spawnQuiet = false;
            if (made == 0)
            {
                Log(DevCommands.UsageFor("spawn"));
            }
            else if (count > 1)
            {
                Log($"spawned {made} x {what}");
            }
        }

        /// <summary>
        /// An enemy put down: in a session the server is asked to spawn it for everyone, run by this machine (EnemyNetwork); otherwise the plain
        /// one, here alone.
        /// </summary>
        private void PutDown(string kind, GameObject localPrefab, Vector3 where, Quaternion facing)
        {
            EnemyNetwork network = EnemyNetwork.Current;
            if (network == null || !network.TrySpawn(kind, where, facing))
            {
                Instantiate(localPrefab, where, facing);
            }
        }

        /// <summary>/mobs: the world's own enemies on or off (docs/MOB_SPAWNING.md), or how many there are.</summary>
        private void Mobs(string which)
        {
            var spawner = FindFirstObjectByType<CityX.View.MobSpawner>();
            if (spawner == null)
            {
                Log("this scene has no mob spawner");
                return;
            }
            switch (which)
            {
                case "on":
                    spawner.SetSpawning(true);
                    Log("the world's enemies are on: they appear as you come near them");
                    return;
                case "off":
                    spawner.SetSpawning(false);
                    Log("the world's enemies are off: taken away, and none appear");
                    return;
                case "":
                case null:
                    Log($"the world's enemies are {(spawner.Spawning ? "on" : "off")}: {spawner.AliveIn(CityX.World.MobHabitat.Outdoors)} alive out of doors round you (at most {CityX.World.MobHabitats.MostOutdoors}), {spawner.AliveIn(CityX.World.MobHabitat.Indoors)} indoors (at most {CityX.World.MobHabitats.MostIndoors})");
                    return;
                default:
                    Log(DevCommands.UsageFor("mobs"));
                    return;
            }
        }

        private void SpawnLog(string message)
        {
            if (!_spawnQuiet)
            {
                Log(message);
            }
        }

        private bool SpawnOne(string what, Vector3 offset)
        {
            Transform origin = Vitals != null ? Vitals.transform : transform;
            Vector3 where = origin.position + origin.forward * SpawnDistance + offset;
            Quaternion facing = Quaternion.LookRotation(-origin.forward, Vector3.up);

            switch (what)
            {
                case "civilian":
                    if (CivilianPrefab == null)
                    {
                        Log("no civilian prefab wired");
                        return false;
                    }
                    Instantiate(CivilianPrefab, where, facing);
                    SpawnLog("spawned civilian");
                    return true;

                case "flyer":
                    if (FlyingNpcPrefab == null)
                    {
                        Log("no flying npc prefab wired");
                        return false;
                    }
                    // Airborne, not at ground level like every other /spawn
                    // case -- it is a flier, and dropping it on the street
                    // would just mean watching it climb to altitude before
                    // it does anything interesting.
                    Instantiate(FlyingNpcPrefab, where + Vector3.up * 30.0f, facing);
                    SpawnLog("spawned a flying npc");
                    return true;

                case "crimsonclaw":
                    if (CrimsonClawPrefab == null)
                    {
                        Log("no crimson claw prefab wired");
                        return false;
                    }
                    // A few metres up, like the flyer -- it hovers rather
                    // than falls, and HoverCorrect eases it down to its own
                    // ground-relative height the moment it ticks.
                    PutDown("crimsonclaw", CrimsonClawPrefab, where + Vector3.up * 3.0f, facing);
                    SpawnLog("spawned a crimson claw -- it will chase and attack you");
                    return true;

                case "wraith":
                    if (WraithPrefab == null)
                    {
                        Log("no wraith prefab wired");
                        return false;
                    }
                    // At its hover height already: it eases onto it over the real ground from there.
                    PutDown("wraith", WraithPrefab, where + Vector3.up * WraithDrift.HoverHeight, facing);
                    SpawnLog("spawned a Wraith -- it floats about, and sneaks up behind whoever turns their back");
                    return true;

                case "shoota":
                    if (ShootaPrefab == null)
                    {
                        Log("no shoota prefab wired");
                        return false;
                    }
                    // Further off than the rest: he opens fire the moment he sees you, so you get a moment to react.
                    PutDown("shoota", ShootaPrefab, origin.position + origin.forward * ShootaSpawnDistance + offset, facing);
                    SpawnLog("spawned a Shoota -- he shoots on sight, takes cover when hurt or reloading");
                    return true;

                case "swordsman":
                    if (SwordsmanPrefab == null)
                    {
                        Log("no swordsman prefab wired");
                        return false;
                    }
                    PutDown("swordsman", SwordsmanPrefab, origin.position + origin.forward * SwordsmanSpawnDistance + offset, facing);
                    SpawnLog("spawned a swordsman -- draw your sword: he blocks, rolls and punishes a blade that bounces off his guard");
                    return true;

                case "crazyguy":
                    if (CrazyGuyPrefab == null)
                    {
                        Log("no crazy guy prefab wired");
                        return false;
                    }
                    PutDown("crazyguy", CrazyGuyPrefab, origin.position + origin.forward * SwordsmanSpawnDistance + offset, facing);
                    SpawnLog("spawned Crazy Guy -- a kitchen knife and no guard: he comes in fast and works round to your back. Don't turn it to him");
                    return true;

                case "giant":
                    if (GiantPrefab == null)
                    {
                        Log("no giant prefab wired");
                        return false;
                    }
                    // Spaced by half its size: giants a man's width apart would stand in each other.
                    PutDown("giant", GiantPrefab, origin.position + origin.forward * GiantSpawnDistance + offset * BodySize.GrowScale * 0.5f, facing);
                    SpawnLog("spawned a giant -- it strides at you; when it crouches, run: it leaps and the ground heaves where it lands");
                    return true;

                case "radio":
                    if (RadioPrefab == null)
                    {
                        Log("no radio prefab wired");
                        return false;
                    }
                    // Dropped a little above the ground so it settles onto it
                    // rather than starting intersecting -- it is a real body.
                    Instantiate(RadioPrefab, where + Vector3.up * 0.5f, facing);
                    SpawnLog("spawned radio -- press E to switch it on");
                    return true;

                case "mustang":
                    if (MustangPrefab == null)
                    {
                        Log("no mustang prefab wired");
                        return false;
                    }
                    // A bigger distance and drop height than every other
                    // /spawn target (6m and 1.2m, not SpawnDistance's own
                    // 3m at ground level) -- matches car.gd's own
                    // _spawn_mustang: a much bigger object needs more room,
                    // and it settles onto the ground via gravity rather
                    // than being placed exactly on it. Facing the SAME
                    // direction the player already faces, not back toward
                    // them like every other spawn -- a car is walked up to
                    // and entered from the side, not looked at like a
                    // picked-up prop.
                    Vector3 mustangWhere = origin.position + origin.forward * 6.0f + Vector3.up * 1.2f + offset;
                    Quaternion mustangFacing = Quaternion.Euler(0.0f, origin.eulerAngles.y, 0.0f);
                    Instantiate(MustangPrefab, mustangWhere, mustangFacing);
                    SpawnLog("spawned a Mustang in front of you -- WASD to drive, Space to handbrake");
                    return true;

                case "dummy":
                    // BUILT HERE RATHER THAN INSTANTIATED, because a sparring
                    // dummy has no prefab -- the scene builder makes its own out
                    // of a capsule for the same reason. It is a thing to punch,
                    // not a character.
                    GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    dummy.name = "SparringDummy";
                    dummy.transform.SetPositionAndRotation(
                        where + Vector3.up * 1.0f, facing);
                    dummy.AddComponent<SparringDummy>();
                    SpawnLog("spawned dummy");
                    return true;

                case "book":
                    // BUILT HERE TOO, same reason as dummy -- Book has no
                    // prefab, CityStreamer.AddBooks builds one procedurally
                    // the same way, and Streamer is already wired here for
                    // /goto so its own BookMaterial needs no new field.
                    if (Streamer == null || Streamer.BookMaterial == null)
                    {
                        Log("no book material wired");
                        return false;
                    }
                    GameObject book = new GameObject("Book");
                    book.transform.SetPositionAndRotation(where, facing);
                    book.AddComponent<Book>().Build(Streamer.BookMaterial);
                    SpawnLog("spawned a book -- press E to read it");
                    return true;

                default:
                    if (!SpawnItem(what, where))
                    {
                        return false;
                    }
                    return true;
            }
        }

        /// <summary>
        /// Any item the player's inventory has a prefab for, put down to be picked up with E, without a prefab field of its own here. Lifted
        /// so its lowest point rests on the ground (a helmet's root is the head's centre, half its height up).
        /// </summary>
        private bool SpawnItem(string what, Vector3 where)
        {
            string itemId = AliasToItemId(what);
            Inventory inventory = Vitals != null ? Vitals.GetComponent<Inventory>() : null;
            GameObject prefab = itemId != null && inventory != null ? inventory.PrefabForItem(itemId) : null;
            if (prefab == null)
            {
                return false;
            }
            GameObject item = Instantiate(prefab, where, Quaternion.identity);
            // The floor under the spot, not the player's feet: the body stands a little above the floor (its controller's skin), and a
            // thing lying flat, a folded shirt, showed the gap as a hover.
            float floor = Physics.Raycast(where + Vector3.up * SpawnFloorSearch, Vector3.down, out RaycastHit hit, 2.0f * SpawnFloorSearch, ~0,
                QueryTriggerInteraction.Ignore) ? hit.point.y : where.y;
            float lowest = float.MaxValue;
            foreach (Renderer part in item.GetComponentsInChildren<Renderer>())
            {
                lowest = Mathf.Min(lowest, part.bounds.min.y);
            }
            if (lowest < float.MaxValue)
            {
                item.transform.position += Vector3.up * (floor - lowest + SpawnFloorClearance);
            }
            SpawnLog($"spawned a {(ItemRegistry.TryGet(itemId, out ItemDefinition definition) ? definition.DisplayName : itemId)} -- press E nearby to pick it up");
            return true;
        }

        /// <summary>
        /// Friendly console spellings for the item ids in ItemRegistry.
        /// Null for anything not recognised.
        /// </summary>
        private static string AliasToItemId(string which)
        {
            switch (which)
            {
                case "shield":
                case "round_shield": return ItemRegistry.RoundShield;
                case "riotshield":
                case "riot_shield": return ItemRegistry.RiotShield;
                case "bottle": return ItemRegistry.GlassBottle;
                case "cloak": return ItemRegistry.HoodedCloak;
                case "jeans":
                case "black_jeans":
                case "blackjeans": return ItemRegistry.BlackJeans;
                case "blue_jeans":
                case "bluejeans": return ItemRegistry.BlueJeans;
                case "bred_jeans":
                case "bredjeans": return ItemRegistry.BredJeans;
                case "maroon_jeans":
                case "maroonjeans": return ItemRegistry.MaroonJeans;
                case "khakis":
                case "khaki": return ItemRegistry.Khakis;
                case "grey_jeans":
                case "greyjeans": return ItemRegistry.GreyJeans;
                case "sneaker":
                case "sneakers":
                case "white_sneaker":
                case "whitesneaker": return ItemRegistry.WhiteSneaker;
                case "bw_sneakers":
                case "bwsneakers":
                case "bwsneaker": return ItemRegistry.BWSneakers;
                case "coat":
                case "midnight_coat":
                case "midnightcoat": return ItemRegistry.MidnightCoat;
                case "parka":
                case "greenparka":
                case "green_parka":
                case "green_fur_hooded_parka": return ItemRegistry.GreenFurHoodedParka;
                case "maroonparka":
                case "maroon_parka":
                case "maroon_fur_hooded_parka": return ItemRegistry.MaroonFurHoodedParka;
                case "tshirt":
                case "t_shirt":
                case "shirt": return ItemRegistry.TShirt;
                case "bloodraven":
                case "blood_raven":
                case "bloodraventshirt":
                case "blood_raven_t_shirt": return ItemRegistry.BloodRavenTShirt;
                case "redcamo":
                case "red_camo":
                case "camo":
                case "red_camo_t_shirt": return ItemRegistry.RedCamoTShirt;
                case "black":
                case "blacktshirt":
                case "blackshirt":
                case "black_t_shirt": return ItemRegistry.BlackTShirt;
                case "cityxshirt":
                case "cityx_shirt":
                case "cityx": return ItemRegistry.CityXShirt;
                case "stripes":
                case "striped":
                case "redstriped":
                case "red_striped_t_shirt": return ItemRegistry.RedStripedTShirt;
                case "longsleeve":
                case "long_sleeve":
                case "longsleeveshirt":
                case "long_sleeve_t_shirt": return ItemRegistry.LongSleeveTShirt;
                case "blacklongsleeve":
                case "black_long_sleeve":
                case "longsleeveblack":
                case "black_long_sleeve_t_shirt": return ItemRegistry.BlackLongSleeveTShirt;
                case "jetpack": return ItemRegistry.Jetpack;
                case "wings":
                case "angelwings":
                case "angel_wings": return ItemRegistry.AngelWings;
                case "darkwings":
                case "dark_wings":
                case "blackwings":
                case "black_wings":
                case "darkangelwings":
                case "dark_angel_wings": return ItemRegistry.DarkAngelWings;
                case "parachute": return ItemRegistry.Parachute;
                case "grapple": return ItemRegistry.GrapplingHook;
                case "pistol": return ItemRegistry.Pistol;
                case "spikesword":
                case "spike_sword": return ItemRegistry.SpikeSword;
                case "combatsword":
                case "combat_sword":
                case "1h": return ItemRegistry.CombatSword;
                case "knife":
                case "kitchenknife":
                case "kitchen_knife": return ItemRegistry.KitchenKnife;
                case "butter":
                case "butterknife":
                case "butter_knife": return ItemRegistry.ButterKnife;
                case "crowbar":
                case "metalcrowbar":
                case "metal_crowbar": return ItemRegistry.MetalCrowbar;
                case "bat":
                case "baseballbat":
                case "baseball_bat": return ItemRegistry.BaseballBat;
                case "sledge":
                case "hammer":
                case "sledgehammer": return ItemRegistry.Sledgehammer;
                case "spear": return ItemRegistry.Spear;
                case "bostaff":
                case "bo_staff":
                case "bo":
                case "pole":
                case "metalpole": return ItemRegistry.BoStaff;
                case "chain":
                case "chains": return ItemRegistry.Chain;
                case "flamethrower": return ItemRegistry.Flamethrower;
                case "staff":
                case "arcane":
                case "arcanestaff":
                case "arcane_staff": return ItemRegistry.ArcaneStaff;
                case "wand":
                case "magicwand":
                case "magic_wand": return ItemRegistry.MagicWand;
                case "magicstaff":
                case "magic_staff": return ItemRegistry.MagicStaff;
                case "wraithstaff":
                case "wraith_staff": return ItemRegistry.WraithStaff;
                case "waterstaff":
                case "water_staff":
                case "hose":
                case "water": return ItemRegistry.WaterStaff;
                case "bow": return ItemRegistry.Bow;
                case "crossbow":
                case "xbow": return ItemRegistry.Crossbow;
                case "slingshot":
                case "sling": return ItemRegistry.Slingshot;
                case "flamestaff":
                case "flame_staff":
                case "flame":
                case "torch": return ItemRegistry.FlameStaff;
                case "beamstaff":
                case "beam_staff":
                case "arcanebeam":
                case "arcane_beam":
                case "arcane_beam_staff":
                case "beam": return ItemRegistry.ArcaneBeamStaff;
                case "healstaff":
                case "heal_staff":
                case "healingstaff":
                case "healing_staff":
                case "healing":
                case "heal": return ItemRegistry.HealingStaff;
                case "flashlight":
                case "light": return ItemRegistry.Flashlight;
                case "jerrycan":
                case "jerry_can":
                case "gascan":
                case "gas_can":
                case "gas":
                case "petrol":
                case "can": return ItemRegistry.JerryCan;
                case "firestaff":
                case "fire_staff":
                case "fireballstaff":
                case "fireball_staff": return ItemRegistry.FireballStaff;
                case "icestaff":
                case "ice_staff":
                case "iceshardstaff":
                case "ice_shard_staff": return ItemRegistry.IceShardStaff;
                case "froststaff":
                case "frost_staff":
                case "frost":
                case "freeze": return ItemRegistry.FrostStaff;
                case "ammo":
                case "pistolammo":
                case "pistol_ammo":
                case "9mm": return ItemRegistry.PistolAmmo;
                case "rifleammo":
                case "rifle_ammo":
                case "akammo":
                case "ak_ammo": return ItemRegistry.RifleAmmo;
                case "shells":
                case "shotgunshells":
                case "shotgun_shells":
                case "shotgunammo": return ItemRegistry.ShotgunShells;
                case "rockets":
                case "rpgammo":
                case "rpg_rockets": return ItemRegistry.RpgRockets;
                case "grenade":
                case "grenades":
                case "frag":
                case "nade": return ItemRegistry.Grenade;
                case "semtex":
                case "sticky":
                case "stickybomb":
                case "sticky_grenade": return ItemRegistry.Semtex;
                case "boomerang":
                case "woodenboomerang":
                case "wooden_boomerang":
                case "rang": return ItemRegistry.WoodenBoomerang;
                case "giantboomerang":
                case "giant_boomerang":
                case "bigboomerang":
                case "big_boomerang":
                case "giantrang": return ItemRegistry.GiantBoomerang;
                case "ironspikedgiantboomerang":
                case "iron_spiked_giant_boomerang":
                case "ironspikedboomerang":
                case "ironspiked":
                case "spikedboomerang":
                case "ironboomerang":
                case "ironrang": return ItemRegistry.IronSpikedGiantBoomerang;
                case "spray":
                case "spraycan":
                case "spray_can":
                case "graffiti": return ItemRegistry.SprayCan;
                case "ak":
                case "ak47":
                case "ak-47":
                case "rifle": return ItemRegistry.AK47;
                case "shotgun":
                case "pump": return ItemRegistry.Shotgun;
                case "rpg":
                case "rocket":
                case "rocketlauncher": return ItemRegistry.RPG;
                case "glock": return ItemRegistry.Pistol;
                case "knight_helmet":
                case "knighthelmet":
                case "helmet": return ItemRegistry.KnightHelmet;
                case "farmer_hat":
                case "farmerhat":
                case "strawhat": return ItemRegistry.FarmerHat;
                case "claw_skull":
                case "clawskull": return ItemRegistry.ClawSkull;
                case "3d_glasses":
                case "3dglasses":
                case "glasses": return ItemRegistry.ThreeDGlasses;
                default: return null;
            }
        }

        /// <summary>
        /// TOPS UP STORAGE IF THE ITEM ISN'T ALREADY THERE -- a deliberate
        /// dev-console convenience so /equip bottle works standalone,
        /// without first /spawn-ing one and walking over to pick it up.
        /// </summary>
        private void Equip(string which)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            Inventory inventory = Vitals.GetComponent<Inventory>();
            if (inventory == null)
            {
                Log("no inventory on this body");
                return;
            }

            string itemId = AliasToItemId(which);
            if (itemId == null)
            {
                Log(DevCommands.UsageFor("equip"));
                return;
            }

            if (!inventory.HasItem(itemId))
            {
                // A whole stack of what comes in stacks (grenades: spent as thrown, the next one into the hand), one of anything else.
                inventory.AddItem(itemId, ItemRegistry.TryGet(itemId, out ItemDefinition definition) ? Mathf.Max(1, definition.StackSize) : 1);
            }

            if (!inventory.RemoveFromStorage(itemId, 1, out ItemInstanceData instanceData))
            {
                Log("no " + which + " in storage");
                return;
            }

            if (!inventory.EquipItemId(itemId, Vitals.gameObject, instanceData))
            {
                // Put it back rather than leaving it removed from storage
                // and nowhere else -- a prefab wired wrong shouldn't also
                // eat the item.
                inventory.AddItem(itemId, 1, instanceData);
                Log("couldn't equip " + which + " -- check its prefab is wired");
                return;
            }

            PlaySession.AdminEdited = true;   // made by the console: the next save says so, so a server lets it into the bag (docs/SERVER_SAVING.md, step 3b)
            Log("equipped " + which);
        }

        private void Unequip(string slot)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            Inventory inventory = Vitals.GetComponent<Inventory>();
            if (inventory == null)
            {
                Log("no inventory on this body");
                return;
            }
            if (string.IsNullOrEmpty(slot))
            {
                Log(DevCommands.UsageFor("unequip"));
                return;
            }

            if (!inventory.UnequipToSlot(slot, Vitals.gameObject))
            {
                Log("nothing equipped in " + slot + " (or storage is full)");
                return;
            }

            Log("unequipped " + slot);
        }
    }
}
