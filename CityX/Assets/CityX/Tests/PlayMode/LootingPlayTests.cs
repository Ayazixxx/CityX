using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using House = CityX.World.OldGuysHouse;

namespace CityX.Tests
{
    /// <summary>
    /// Looking into Old Guy's furniture for real (docs/LOOTING.md): the real body opens the wardrobe and takes the clothes out of it, a cupboard
    /// on the far side of a wall is never in reach however it is looked at, the same cupboard is from its own side, and the bat is in the tree
    /// house's crate.
    /// </summary>
    public sealed class LootingPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        private static LootContainer Holding(string itemId)
        {
            foreach (LootContainer c in LootContainer.All) if (c.Items.Contains(itemId)) return c;
            return null;
        }

        /// <summary>
        /// The floor <paramref name="metres"/> in front of the first piece of <paramref name="kind"/> (behind it when negative), on its floor; only the
        /// pieces standing at <paramref name="floorY"/> when it is given.
        /// </summary>
        private static Vector3 InFrontOf(House.FurnitureKind kind, float metres, float floorY = float.NaN)
        {
            foreach (House.Furniture piece in House.FurnitureList)
            {
                if (piece.Kind != kind || !float.IsNaN(floorY) && !Mathf.Approximately(piece.Position.y, floorY)) continue;
                Vector3 feet = piece.Position + Quaternion.Euler(0, piece.Yaw, 0) * Vector3.forward * metres;
                return new Vector3(feet.x, piece.Position.y, feet.z);
            }
            Assert.Fail($"the house has no {kind}");
            return Vector3.zero;
        }

        /// <summary>The real player at <paramref name="feet"/> in the house's frame, looking from its eyes at <paramref name="lookAt"/>.</summary>
        private GameObject PlayerLookingAt(Vector3 feet, Vector3 lookAt)
        {
#if UNITY_EDITOR
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), feet + Vector3.up * 0.05f, Quaternion.identity);
            _made.Add(player);
            if (player.GetComponent<Inventory>() == null) player.AddComponent<Inventory>();
            var eye = new GameObject("Eye");
            _made.Add(eye);
            eye.transform.position = feet + Vector3.up * (2.6f * LootSearcher.EyeShare);
            eye.transform.rotation = Quaternion.LookRotation(lookAt - eye.transform.position);
            var reader = player.GetComponent<PlayerInputReader>();
            reader.YawSource = eye.transform;
            Vector3 flat = lookAt - feet;
            flat.y = 0;
            player.transform.rotation = Quaternion.LookRotation(flat);
            reader.enabled = false;
            return player;
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator TheRealBodyOpensTheWardrobeAndTakesTheClothes()
        {
            OldGuysHousePlayTests.MakeHouse(_made, false, false);
            yield return null;
            LootContainer wardrobe = Holding(ItemRegistry.TShirt);
            Assert.IsNotNull(wardrobe, "no wardrobe holds the T-shirt");
            Assert.AreEqual("Old Guy's bedroom", House.RoomAt(2, wardrobe.transform.position.x, wardrobe.transform.position.z)?.Name);
            GameObject player = PlayerLookingAt(InFrontOf(House.FurnitureKind.Wardrobe, 2.0f), wardrobe.Centre);
            var searcher = player.GetComponent<LootSearcher>();
            yield return null;
            Assert.AreEqual(wardrobe, searcher.InReach(out _), "looking at the wardrobe's door from in front of it, it is not the one in reach");

            Quaternion closed = wardrobe.transform.rotation;
            searcher.Search(wardrobe);
            for (int i = 0; i < 40; i++) yield return null;
            Assert.AreEqual(1.0f, wardrobe.Open01, 1e-3f, "the wardrobe's door did not open");
            Assert.Greater(Quaternion.Angle(closed, wardrobe.transform.rotation), 80.0f, "the door did not swing open");
            CollectionAssert.AreEquivalent(new[] { ItemRegistry.TShirt, ItemRegistry.BlueJeans }, searcher.List.Items);
            Assert.AreEqual(2, searcher.TakeAll());
            var inventory = player.GetComponent<Inventory>();
            Assert.IsTrue(inventory.HasItem(ItemRegistry.TShirt) && inventory.HasItem(ItemRegistry.BlueJeans), "the clothes did not go into the inventory");
            Assert.IsEmpty(wardrobe.Items, "the clothes are still in the wardrobe");
            searcher.CloseWindow();
            Assert.IsNull(searcher.Current);
            Assert.IsTrue(wardrobe.IsOpen, "a searched wardrobe should stay open");
        }

        [UnityTest]
        public IEnumerator ACupboardBehindAWallIsNeverInReachButIsFromItsOwnSide()
        {
            OldGuysHousePlayTests.MakeHouse(_made, false, false);
            yield return null;
            LootContainer cabinet = null;
            foreach (LootContainer c in LootContainer.All) if (c.Label == "Bathroom cabinet" && c.transform.position.y < House.LevelY(2)) cabinet = c;
            Assert.IsNotNull(cabinet, "no bathroom cabinet in the WC");
            // From the coats room next door, through the wall the basin hangs on.
            GameObject behind = PlayerLookingAt(InFrontOf(House.FurnitureKind.Basin, -1.6f, House.LevelY(1)), cabinet.Centre);
            yield return null;
            Assert.AreNotEqual(cabinet, behind.GetComponent<LootSearcher>().InReach(out _), "a cabinet on the far side of a wall was in reach");
            Object.Destroy(behind);
            yield return null;
            GameObject inFront = PlayerLookingAt(InFrontOf(House.FurnitureKind.Basin, 1.9f, House.LevelY(1)), cabinet.Centre);
            yield return null;
            Assert.AreEqual(cabinet, inFront.GetComponent<LootSearcher>().InReach(out _), "the cabinet is not in reach from in front of it");
        }

        [UnityTest]
        public IEnumerator TheBatIsHiddenInACrateInTheCellar()
        {
            OldGuysHousePlayTests.MakeHouse(_made, false, false);
            yield return null;
            LootContainer crate = Holding(ItemRegistry.BaseballBat);
            Assert.IsNotNull(crate, "nothing holds the bat");
            Assert.AreEqual("Crate", crate.Label);
            Assert.AreEqual("Cellar", House.RoomAt(0, crate.transform.position.x, crate.transform.position.z)?.Name);
            Assert.AreEqual(ContainerKind.Lid, crate.Kind);
        }
    }
}
