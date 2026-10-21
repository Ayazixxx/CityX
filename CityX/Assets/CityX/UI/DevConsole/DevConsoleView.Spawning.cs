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
        private void Spawn(string what)
        {
            Transform origin = Vitals != null ? Vitals.transform : transform;
            Vector3 where = origin.position + origin.forward * SpawnDistance;
            Quaternion facing = Quaternion.LookRotation(-origin.forward, Vector3.up);

            switch (what)
            {
                case "civilian":
                    if (CivilianPrefab == null)
                    {
                        Log("no civilian prefab wired");
                        return;
                    }
                    Instantiate(CivilianPrefab, where, facing);
                    Log("spawned civilian");
                    return;

                case "flyer":
                    if (FlyingNpcPrefab == null)
                    {
                        Log("no flying npc prefab wired");
                        return;
                    }
                    // Airborne, not at ground level like every other /spawn
                    // case -- it is a flier, and dropping it on the street
                    // would just mean watching it climb to altitude before
                    // it does anything interesting.
                    Instantiate(FlyingNpcPrefab, where + Vector3.up * 30.0f, facing);
                    Log("spawned a flying npc");
                    return;

                case "crimsonclaw":
                    if (CrimsonClawPrefab == null)
                    {
                        Log("no crimson claw prefab wired");
                        return;
                    }
                    // A few metres up, like the flyer -- it hovers rather
                    // than falls, and HoverCorrect eases it down to its own
                    // ground-relative height the moment it ticks.
                    Instantiate(CrimsonClawPrefab, where + Vector3.up * 3.0f, facing);
                    Log("spawned a crimson claw -- it will chase and attack you");
                    return;

                case "shoota":
                    if (ShootaPrefab == null)
                    {
                        Log("no shoota prefab wired");
                        return;
                    }
                    // Further off than the rest: he opens fire the moment he sees you, so you get a moment to react.
                    Instantiate(ShootaPrefab, origin.position + origin.forward * ShootaSpawnDistance, facing);
                    Log("spawned a Shoota -- he shoots on sight, takes cover when hurt or reloading");
                    return;

                case "swordsman":
                    if (SwordsmanPrefab == null)
                    {
                        Log("no swordsman prefab wired");
                        return;
                    }
                    Instantiate(SwordsmanPrefab, origin.position + origin.forward * SwordsmanSpawnDistance, facing);
                    Log("spawned a swordsman -- draw your sword: he blocks, rolls and punishes a blade that bounces off his guard");
                    return;

                case "radio":
                    if (RadioPrefab == null)
                    {
                        Log("no radio prefab wired");
                        return;
                    }
                    // Dropped a little above the ground so it settles onto it
                    // rather than starting intersecting -- it is a real body.
                    Instantiate(RadioPrefab, where + Vector3.up * 0.5f, facing);
                    Log("spawned radio -- press E to switch it on");
                    return;

                case "mustang":
                    if (MustangPrefab == null)
                    {
                        Log("no mustang prefab wired");
                        return;
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
                    Vector3 mustangWhere = origin.position + origin.forward * 6.0f + Vector3.up * 1.2f;
                    Quaternion mustangFacing = Quaternion.Euler(0.0f, origin.eulerAngles.y, 0.0f);
                    Instantiate(MustangPrefab, mustangWhere, mustangFacing);
                    Log("spawned a Mustang in front of you -- WASD to drive, Space to handbrake");
                    return;

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
                    Log("spawned dummy");
                    return;

                case "book":
                    // BUILT HERE TOO, same reason as dummy -- Book has no
                    // prefab, CityStreamer.AddBooks builds one procedurally
                    // the same way, and Streamer is already wired here for
                    // /goto so its own BookMaterial needs no new field.
                    if (Streamer == null || Streamer.BookMaterial == null)
                    {
                        Log("no book material wired");
                        return;
                    }
                    GameObject book = new GameObject("Book");
                    book.transform.SetPositionAndRotation(where, facing);
                    book.AddComponent<Book>().Build(Streamer.BookMaterial);
                    Log("spawned a book -- press E to read it");
                    return;

                default:
                    if (!SpawnItem(what, where))
                    {
                        Log(DevCommands.UsageFor("spawn"));
                    }
                    return;
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
            float lowest = float.MaxValue;
            foreach (Renderer part in item.GetComponentsInChildren<Renderer>())
            {
                lowest = Mathf.Min(lowest, part.bounds.min.y);
            }
            if (lowest < float.MaxValue)
            {
                item.transform.position += Vector3.up * (where.y - lowest + 0.02f);
            }
            Log($"spawned a {(ItemRegistry.TryGet(itemId, out ItemDefinition definition) ? definition.DisplayName : itemId)} -- press E nearby to pick it up");
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
                case "bottle": return ItemRegistry.GlassBottle;
                case "jeans": return ItemRegistry.SkelliJeans;
                case "cloak": return ItemRegistry.HoodedCloak;
                case "coat":
                case "midnight_coat":
                case "midnightcoat": return ItemRegistry.MidnightCoat;
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
                case "jetpack": return ItemRegistry.Jetpack;
                case "parachute": return ItemRegistry.Parachute;
                case "grapple": return ItemRegistry.GrapplingHook;
                case "pistol": return ItemRegistry.Pistol;
                case "spikesword":
                case "spike_sword": return ItemRegistry.SpikeSword;
                case "combatsword":
                case "combat_sword":
                case "1h": return ItemRegistry.CombatSword;
                case "crowbar":
                case "metalcrowbar":
                case "metal_crowbar": return ItemRegistry.MetalCrowbar;
                case "bat":
                case "baseballbat":
                case "baseball_bat": return ItemRegistry.BaseballBat;
                case "flamethrower": return ItemRegistry.Flamethrower;
                case "spray":
                case "spraycan":
                case "spray_can":
                case "graffiti": return ItemRegistry.SprayCan;
                case "ak":
                case "ak47":
                case "ak-47":
                case "rifle": return ItemRegistry.AK47;
                case "glock": return ItemRegistry.Pistol;
                case "top_hat":
                case "tophat": return ItemRegistry.TopHat;
                case "knight_helmet":
                case "knighthelmet":
                case "helmet": return ItemRegistry.KnightHelmet;
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
                inventory.AddItem(itemId, 1);
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
