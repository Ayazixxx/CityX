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
    /// Drinking a can of PHIZZ (docs/PHIZZ_CAN.md; owner, 2026-11-23): a click, the tab lifted and the hole black, 80 health over 10 seconds from the
    /// first sip, the empty can dropped and the hand empty, all in 3.5 seconds; another player's copy of the body drinks its copy of the can the
    /// same way and heals no one.
    /// </summary>
    public sealed class CanDrinkPlayTests
    {
        private static GameObject Load(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static (GameObject body, PhizzCan can, CanDrinker drinker) Body(Vector3 at)
        {
            GameObject body = Object.Instantiate(Load("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            var drinker = body.GetComponent<CanDrinker>() ?? body.AddComponent<CanDrinker>();
            var can = Object.Instantiate(Load("Assets/CityX/Weapons/Cans/phizz_can.prefab"), at + Vector3.up * 5.0f, Quaternion.identity).GetComponent<PhizzCan>();
            return (body, can, drinker);
        }

        [UnityTest]
        public IEnumerator AClickDrinksTheCanOpeningItHealingAndDroppingItEmptyInThreeAndAHalfSeconds()
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);
            (GameObject body, PhizzCan can, CanDrinker drinker) = Body(new Vector3(0.0f, 0.05f, 0.0f));
            (GameObject copy, PhizzCan copiedCan, CanDrinker copyDrinker) = Body(new Vector3(6.0f, 0.05f, 0.0f));
            try
            {
                var vitals = body.GetComponent<PlayerVitals>();
                var copyVitals = copy.GetComponent<PlayerVitals>();
                can.OnEquip(body, ItemInstanceData.None);
                copiedCan.MountCosmetic(copy);
                Assert.AreSame(can, body.GetComponent<PlayerInputReader>().EquippedDrink, "a click would not reach the can");
                // The owner's drink is sent; the copy plays what is sent.
                drinker.Fired += evt => copyDrinker.PlayRemote(evt);
                bool healed = false;
                drinker.Healed += () => healed = true;
                yield return null;

                Assert.IsTrue(can.Drink(), "the click did not begin a drink");
                Assert.IsFalse(can.Drink(), "a second click began a second drink");
                Assert.IsTrue(copyDrinker.Drinking, "the other player does not see the drink begin");
                Assert.IsFalse(can.Hole.activeSelf, "the hole is there before the tab is lifted");

                float seconds = 0.0f;
                bool opened = false, healedInTime = false, droppedInTime = false;
                while (seconds < CanDrinker.Seconds + 0.3f)
                {
                    yield return null;
                    seconds += 1.0f / 60.0f;
                    if (can != null && can.Opened >= 1.0f && seconds < CanDrinker.TabToSeconds + 0.1f)
                    {
                        opened = can.Hole.activeSelf;
                    }
                    if (healed && !healedInTime)
                    {
                        healedInTime = true;
                        Assert.That(seconds, Is.InRange(CanDrinker.HealFromSeconds - 0.1f, CanDrinker.HealFromSeconds + 0.1f), "the heal began away from the first sip");
                        Assert.AreEqual(PhizzCan.HealAmount, vitals.HealingToCome, 0.01f, "the drink does not heal 80");
                    }
                    if (!droppedInTime && Object.FindObjectsByType<DroppedCan>(FindObjectsSortMode.None).Length >= 2)
                    {
                        droppedInTime = true;
                        Assert.That(seconds, Is.InRange(CanDrinker.DropSeconds - 0.1f, CanDrinker.Seconds), "the can was dropped at the wrong moment");
                    }
                }
                Assert.IsTrue(opened, "the tab did not lift and show the hole early in the drink");
                Assert.IsTrue(healedInTime, "the drink never healed");
                Assert.IsTrue(droppedInTime, "the empty can was not dropped, on both machines");
                Assert.IsFalse(drinker.Drinking, "the drink is still going after 3.5 seconds");
                Assert.IsTrue(can == null, "the can is still in the hand");
                Assert.IsNull(body.GetComponent<PlayerInputReader>().EquippedDrink, "the hand still thinks it holds a drink");
                Assert.AreEqual(0.0f, copyVitals.HealingToCome, "the other player's copy healed");
                Assert.IsFalse(copiedCan.gameObject.activeSelf, "the copy's can is still in its hand after it was dropped");

                // 80 health over the 10 seconds, at an even rate.
                vitals.TakeDamage(vitals.MaxHealth * 0.9f);
                float before = vitals.Health;
                float healing = vitals.HealingToCome;
                vitals.TickHealthRegen(PhizzCan.HealSeconds * 0.5f);
                Assert.Greater(vitals.Health - before, healing * 0.45f, "the heal does not come over the ten seconds");
            }
            finally
            {
                Object.Destroy(body);
                Object.Destroy(copy);
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
