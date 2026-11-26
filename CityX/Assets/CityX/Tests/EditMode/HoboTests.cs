using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.View;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The hobos (docs/HOBO.md; owner, 2026-11-24): either of the owner's two hobo characters with a metal pole, a broken glass bottle, a chain or a
    /// machete, at random; calm until hit; with the bottle, three whole ones to throw and to be found on him. Their makes, the roll that picks
    /// one, the lob of a throw, where they appear, and the prefabs as built.
    /// </summary>
    public sealed class HoboTests
    {
        [Test]
        public void EveryLookComesWithEveryWeapon()
        {
            Assert.AreEqual(15, HoboKinds.All.Length);
            CollectionAssert.AllItemsAreUnique(HoboKinds.All);
            CollectionAssert.Contains(HoboKinds.All, "hobo_1_pole");
            CollectionAssert.Contains(HoboKinds.All, "hobo_2_plank");
            Assert.AreEqual("Hobo_2_Bottle", HoboKinds.PrefabNameOf(1, 1));
        }

        [Test]
        public void TheRollPicksEveryHoboAsOftenAsAnyOther()
        {
            var counts = new Dictionary<string, int>();
            const int rolls = 15000;
            for (int i = 0; i < rolls; i++)
            {
                string kind = HoboKinds.Pick((i + 0.5f) / rolls);
                counts.TryGetValue(kind, out int count);
                counts[kind] = count + 1;
            }
            foreach (string kind in HoboKinds.All)
            {
                Assert.AreEqual(rolls / HoboKinds.All.Length, counts[kind], kind);
            }
            Assert.AreEqual(HoboKinds.All[HoboKinds.All.Length - 1], HoboKinds.Pick(1.0f), "a roll of 1 is still a hobo");
            StringAssert.EndsWith("_chain", HoboKinds.PickWith("chain", 0.7f));
            Assert.IsNull(HoboKinds.PickWith("spoon", 0.5f));
            Assert.IsTrue(HoboKinds.IsHobo("hobo") && HoboKinds.IsHobo("hobo_1_bottle") && !HoboKinds.IsHobo("swordsman"));
        }

        [Test]
        public void AFallenHoboIsCalledHobo()
        {
            var body = new GameObject("NetworkedHobo_2_Chain(Clone)");
            try
            {
                Assert.AreEqual("Hobo", CorpseLoot.NameOf(body));
                body.name = "Swordsman(Clone)";
                Assert.AreEqual("Swordsman", CorpseLoot.NameOf(body));
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        /// <summary>The throw comes down on its mark, near or far, above or below the hand.</summary>
        [TestCase(8.0f, 0.0f)]
        [TestCase(20.0f, -1.0f)]
        [TestCase(14.0f, 3.0f)]
        public void ALobbedBottleLandsOnItsMark(float across, float up)
        {
            const float speed = BottleWeapon.ThrowSpeedConst, gravity = 9.81f;
            var from = new Vector3(1.0f, 2.0f, -3.0f);
            Vector3 to = from + new Vector3(across * 0.6f, up, across * 0.8f);
            Vector3 velocity = BottleLob.Velocity(from, to, speed, gravity);
            Assert.AreEqual(speed, velocity.magnitude, 1e-3f);
            var flat = new Vector3(velocity.x, 0.0f, velocity.z);
            float seconds = across / flat.magnitude;
            Vector3 landed = from + velocity * seconds + Vector3.down * (0.5f * gravity * seconds * seconds);
            Assert.Less(Vector3.Distance(landed, to), 0.05f, $"landed at {landed}, aimed at {to}");
            Assert.Less(velocity.y / flat.magnitude, 1.0f, "the flatter arc, not the high one");
        }

        [Test]
        public void HobosAppearOutOfDoors()
        {
            Assert.AreEqual(MobFamily.Monster, MobHabitats.FamilyFor(MobHabitat.Outdoors, MobHabitats.MonsterShare - 0.01f));
            Assert.AreEqual(MobFamily.Hobo, MobHabitats.FamilyFor(MobHabitat.Outdoors, MobHabitats.MonsterShare + 0.01f));
            Assert.AreEqual(MobFamily.Ghost, MobHabitats.FamilyFor(MobHabitat.Outdoors, MobHabitats.MonsterShare + MobHabitats.HoboShare + 0.01f));
            Assert.AreEqual(MobFamily.Humanoid, MobHabitats.FamilyFor(MobHabitat.Indoors, 0.5f), "indoors stays the humanoids'");
            for (int i = 0; i < 20; i++)
            {
                CollectionAssert.Contains(HoboKinds.All, MobSpawnRules.KindFor(MobFamily.Hobo, i / 20.0f));
            }
        }

#if UNITY_EDITOR
        private static GameObject Prefab(int look, int weapon)
        {
            string path = CityX.WeaponsEditor.HoboBuilder.PrefabPath(look, weapon);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"{path} is missing -- rebuild the scene");
            return prefab;
        }

        /// <summary>Each is calm until hurt, with its weapon; with the bottle, the bottle broken, three whole ones carried and the throw shared.</summary>
        [Test]
        public void EachHoboCarriesItsWeapon()
        {
            var weapons = new Dictionary<string, string>
            {
                ["pole"] = ItemRegistry.BoStaff, ["bottle"] = GlassBottleKinds.Classic, ["chain"] = ItemRegistry.Chain, ["machete"] = ItemRegistry.Machete, ["plank"] = ItemRegistry.WoodPlank,
            };
            for (int look = 0; look < HoboKinds.Looks.Length; look++)
            {
                for (int weapon = 0; weapon < HoboKinds.Weapons.Length; weapon++)
                {
                    GameObject prefab = Prefab(look, weapon);
                    var brain = prefab.GetComponent<SwordsmanBrain>();
                    Assert.IsNotNull(brain, prefab.name);
                    Assert.IsTrue(brain.PeacefulUntilHurt, $"{prefab.name} is hostile from the start");
                    Assert.AreEqual(HoboKinds.CalmArmedShare, brain.CalmArmedShare, $"{prefab.name} does not keep his weapon put away like most hobos");
                    Assert.AreEqual(look == 2, brain.HoodUp, $"{prefab.name}'s hood");
                    Assert.AreEqual(weapons[HoboKinds.Weapons[weapon]], brain.BladeItemId, prefab.name);
                    Assert.IsNotNull(brain.SwordPrefab, prefab.name);
                    Assert.IsNotNull(brain.SwordPrefab.GetComponent<ISwordBlade>(), $"{prefab.name}'s weapon is not swung");
                    bool bottle = HoboKinds.Weapons[weapon] == "bottle";
                    if (bottle)
                    {
                        Assert.AreEqual((int)BottleWeapon.DurabilityState.Broken, brain.BladeDurability, "the bottle in his hand is not broken");
                    }
                    // Every hobo carries empty bottles, one to three rolled at spawn, and throws them (owner, 2026-11-26); none dashes.
                    var carried = prefab.GetComponent<CarriedLoot>();
                    Assert.IsNotNull(carried, $"{prefab.name} carries no bottles");
                    Assert.AreEqual(3, carried.Count(GlassBottleKinds.Classic), $"{prefab.name}'s make does not carry three whole bottles to roll from");
                    Assert.IsTrue(brain.RollsHowManyBottles, $"{prefab.name} does not roll how many bottles he carries");
                    var thrower = prefab.GetComponent<BottleThrower>();
                    Assert.IsNotNull(thrower, $"{prefab.name}'s throws are not shown to other players");
                    int index = System.Array.IndexOf(BottleThrower.Throwables, GlassBottleKinds.Classic);
                    Assert.IsNotNull(thrower.ThrownPrefabs[index], $"{prefab.name} has nothing to throw");
                    Assert.IsFalse(brain.CanDash, $"{prefab.name} dashes (owner, 2026-11-26: \"make it so hobos dont have dash\")");
                    string networked = $"{CityX.WeaponsEditor.HoboBuilder.Folder}/Networked{prefab.name}.prefab";
                    Assert.IsNotNull(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(networked), $"{networked} is missing: not in sessions");
                }
            }
        }

        /// <summary>Each wears the beard of the owner's character (owner, 2026-11-24: "the hobos dont have their beards").</summary>
        [Test]
        public void EachLookWearsItsBeard()
        {
            string[] beards = { CharacterHair.SpikyBeard, CharacterHair.FullBeard, CharacterHair.SpikyBeard };
            for (int look = 0; look < HoboKinds.Looks.Length; look++)
            {
                for (int weapon = 0; weapon < HoboKinds.Weapons.Length; weapon++)
                {
                    var hair = Prefab(look, weapon).GetComponent<CharacterHair>();
                    Assert.IsNotNull(hair, "no hair on the hobo");
                    Assert.AreEqual(beards[look], hair.Beard, $"{Prefab(look, weapon).name} has the wrong beard");
                    Assert.IsNull(hair.BeardWorn, "a beard built into the prefab would stay on beside the one grown at spawn");
                }
            }
        }

        /// <summary>Dressed as the owner's characters: hobo2 in the parka and khakis; Hobo in the parka and black jeans; brack guy in the Brown Coat, hood up, over a black T-shirt and grey jeans.</summary>
        [Test]
        public void EachLookWearsItsOutfit()
        {
            string[][] outfits =
            {
                new[] { "green_fur_hooded_parka", "black_jeans", "bw_sneakers", "black_fingerless_gloves" },
                new[] { "green_fur_hooded_parka", "khakis", "black_sneakers", "black_fingerless_gloves", "t_shirt" },
                new[] { "brown_coat", "black_fingerless_gloves", "black_sneakers", "black_t_shirt", "grey_jeans" },   // brack guy (owner, 2026-11-24)
            };
            Inventory.ItemPrefabEntry[] clothes = CityX.CharacterEditor.NetworkedPlayerWiring.ItemPrefabEntries(definition => definition.IsWearable);
            for (int look = 0; look < HoboKinds.Looks.Length; look++)
            {
                var worn = new List<string>();
                foreach (GameObject piece in Prefab(look, 0).GetComponent<SwordsmanBrain>().Wearing)
                {
                    worn.Add(System.Array.Find(clothes, entry => entry.Prefab == piece).ItemId);
                }
                foreach (string id in outfits[look])
                {
                    CollectionAssert.Contains(worn, id, $"look {HoboKinds.Looks[look]} does not wear {id}");
                }
            }
        }
#endif
    }
}
