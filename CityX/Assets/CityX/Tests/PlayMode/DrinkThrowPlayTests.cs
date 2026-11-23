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
    /// Drinks thrown and broken (docs/PHIZZ_CAN.md; owner, 2026-11-23: "hold right click to aim and click to throw when aiming", "a yellow liquid splash effect
    /// when the bottle is broken (when shot or thrown)"): a full bottle thrown at a wall bursts into its pieces with a yellow splash and the next comes out of
    /// the bag into the hand; a thrown can splashes and is left lying there empty; a bottle lying on the ground, shot, bursts and lets the bullet on through.
    /// </summary>
    public sealed class DrinkThrowPlayTests
    {
        private static GameObject Load(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static int Splashes() => BloodBurst.Splashed;

        private static void Tidy()
        {
            foreach (var kind in new System.Type[] { typeof(BottleShatter), typeof(DroppedCan), typeof(ThrownBottle) })
            {
                foreach (Object found in Object.FindObjectsByType(kind, FindObjectsSortMode.None))
                {
                    Object.Destroy(((Component)found).gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator AThrownDrinkSplashesYellowWhereItLandsAndTheNextComesIntoTheHand(
            [Values(ItemRegistry.PhizzBottle, ItemRegistry.PhizzCan)] string itemId)
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0.0f, 2.0f, 5.0f);
            wall.transform.localScale = new Vector3(8.0f, 4.0f, 0.5f);
            GameObject body = Object.Instantiate(Load("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            try
            {
                body.GetComponent<PlayerInputReader>().enabled = false;
                var inventory = body.GetComponent<Inventory>() ?? body.AddComponent<Inventory>();
                string prefab = itemId == ItemRegistry.PhizzBottle ? "Assets/CityX/Weapons/Cans/phizz_bottle.prefab" : "Assets/CityX/Weapons/Cans/phizz_can.prefab";
                inventory.ItemPrefabs = new[] { new Inventory.ItemPrefabEntry { ItemId = itemId, Prefab = Load(prefab) } };
                Assert.IsTrue(inventory.AddItem(itemId, 2));
                Assert.IsTrue(inventory.RemoveFromStorage(itemId, 1, out ItemInstanceData taken));
                Assert.IsTrue(inventory.EquipItemId(itemId, body, taken));
                GameObject held = inventory.EquippedInstance(inventory.ActiveWeaponSlot);
                var drink = held.GetComponent<HeldDrink>();
                yield return null;
                int splashesBefore = Splashes();

                Assert.IsTrue(drink.CanThrow, "a full drink in the hand cannot be thrown");
                drink.Throw(body, body.transform.position + Vector3.up * 1.8f, Vector3.forward);
                yield return null;
                Assert.IsNotNull(Object.FindAnyObjectByType<ThrownBottle>(), "nothing flew");
                GameObject next = inventory.EquippedInstance(inventory.ActiveWeaponSlot);
                Assert.IsNotNull(next, "the next of the stack did not come into the hand");
                Assert.AreNotSame(held, next, "the thrown drink is still in the hand");
                Assert.AreEqual(0, inventory.CountInStorage(itemId), "the next was not taken out of the bag");

                float waited = 0.0f;
                while (Object.FindAnyObjectByType<ThrownBottle>() != null && waited < 2.0f)
                {
                    yield return null;
                    waited += 1.0f / 60.0f;
                }
                Assert.IsNull(Object.FindAnyObjectByType<ThrownBottle>(), "the drink never landed");
                Assert.Greater(Splashes(), splashesBefore, "it landed without a splash");
                if (itemId == ItemRegistry.PhizzBottle)
                {
                    Assert.IsNotNull(Object.FindAnyObjectByType<BottleShatter>(), "the bottle did not burst into its pieces");
                }
                else
                {
                    Assert.IsNotNull(Object.FindAnyObjectByType<DroppedCan>(), "the empty can is not left lying there");
                }
            }
            finally
            {
                Object.Destroy(body);
                Object.Destroy(ground);
                Object.Destroy(wall);
                Tidy();
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
        }

        [UnityTest]
        public IEnumerator AFullBottleLyingThereShotBurstsSplashesAndLetsTheBulletThrough()
        {
            var bottle = Object.Instantiate(Load("Assets/CityX/Weapons/Cans/phizz_bottle.prefab"), new Vector3(30.0f, 0.0f, 30.0f), Quaternion.identity)
                .GetComponent<FullBottle>();
            bottle.GetComponent<Rigidbody>().isKinematic = true;
            try
            {
                yield return null;
                int splashesBefore = Splashes();
                bool through = bottle.TakeBlow(bottle.transform.position + Vector3.up * 0.3f, Vector3.forward, CityX.World.BlowStrength.Bullet);
                yield return null;
                Assert.IsTrue(through, "the bullet stopped on the bottle");
                Assert.IsTrue(bottle == null, "the bottle is still there");
                Assert.Greater(Splashes(), splashesBefore, "no splash");
                Assert.IsNotNull(Object.FindAnyObjectByType<BottleShatter>(), "no pieces");
            }
            finally
            {
                if (bottle != null)
                {
                    Object.Destroy(bottle.gameObject);
                }
                Tidy();
            }
        }
    }
}
