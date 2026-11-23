using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A drink from the hotbar (owner, 2026-11-23: "drinks can be equipped in the hotbar ant it appears and does the drinking animation immediately"):
    /// with a bottle (a weapon) in the hand and two cans in the bag, the key puts the bottle away, takes a can out and drinks it at once; drunk, the hand
    /// has the bottle back and one can is left in the bag.
    /// </summary>
    public sealed class QuickDrinkPlayTests
    {
        private static GameObject Load(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

        [UnityTest]
        public IEnumerator TheKeyDrinksOneFromTheBagAtOnceAndGivesTheHandBackWhatItHeld()
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);
            GameObject body = Object.Instantiate(Load("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            try
            {
                body.GetComponent<PlayerInputReader>().enabled = false;
                if (body.GetComponent<Drinker>() == null)
                {
                    body.AddComponent<Drinker>();
                }
                var quick = body.GetComponent<QuickDrink>() ?? body.AddComponent<QuickDrink>();
                var inventory = body.GetComponent<Inventory>() ?? body.AddComponent<Inventory>();
                inventory.ItemPrefabs = new[]
                {
                    new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.PhizzCan, Prefab = Load("Assets/CityX/Weapons/Cans/phizz_can.prefab") },
                    new Inventory.ItemPrefabEntry { ItemId = GlassBottleKinds.Beer, Prefab = Load(GlassBottleKinds.PrefabPath(GlassBottleKinds.Beer)) },
                };
                Assert.IsTrue(inventory.EquipItemId(GlassBottleKinds.Beer, body));
                Assert.IsTrue(inventory.AddItem(ItemRegistry.PhizzCan, 2));
                yield return null;

                Assert.IsTrue(quick.Use(ItemRegistry.PhizzCan), "the key did not drink");
                string slot = inventory.ActiveWeaponSlot;
                Assert.AreEqual(ItemRegistry.PhizzCan, inventory.EquippedItemId(slot), "the can is not in the hand");
                Assert.IsTrue(body.GetComponent<Drinker>().Drinking, "the can is not drunk at once");
                Assert.AreEqual(1, inventory.CountInStorage(GlassBottleKinds.Beer), "the bottle was not put away in the bag");
                Assert.IsFalse(quick.Use(ItemRegistry.PhizzCan), "a second press began a second drink");

                float seconds = 0.0f;
                while (seconds < Drinker.Seconds + 0.3f)
                {
                    yield return null;
                    seconds += 1.0f / 60.0f;
                }
                Assert.AreEqual(GlassBottleKinds.Beer, inventory.EquippedItemId(slot), "the hand did not get the bottle back");
                Assert.AreEqual(1, inventory.CountInStorage(ItemRegistry.PhizzCan), "not one can left in the bag");
                Assert.AreEqual(0, inventory.CountInStorage(GlassBottleKinds.Beer));
            }
            finally
            {
                Object.Destroy(body);
                Object.Destroy(ground);
                foreach (DroppedCan dropped in Object.FindObjectsByType<DroppedCan>(FindObjectsSortMode.None))
                {
                    Object.Destroy(dropped.gameObject);
                }
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
        }
    }
}
