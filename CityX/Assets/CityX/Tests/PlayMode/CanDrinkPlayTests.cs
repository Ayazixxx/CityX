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

        private static (GameObject body, PhizzCan can, Drinker drinker) Body(Vector3 at)
        {
            GameObject body = Object.Instantiate(Load("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            var drinker = body.GetComponent<Drinker>() ?? body.AddComponent<Drinker>();
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
            (GameObject body, PhizzCan can, Drinker drinker) = Body(new Vector3(0.0f, 0.05f, 0.0f));
            (GameObject copy, PhizzCan copiedCan, Drinker copyDrinker) = Body(new Vector3(6.0f, 0.05f, 0.0f));
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
                while (seconds < Drinker.Seconds + 0.3f)
                {
                    yield return null;
                    seconds += 1.0f / 60.0f;
                    if (can != null && can.Opened >= 1.0f && seconds < Drinker.TabToSeconds + 0.1f)
                    {
                        opened = can.Hole.activeSelf;
                    }
                    if (healed && !healedInTime)
                    {
                        healedInTime = true;
                        Assert.That(seconds, Is.InRange(Drinker.HealFromSeconds - 0.1f, Drinker.HealFromSeconds + 0.1f), "the heal began away from the first sip");
                        Assert.AreEqual(PhizzCan.Heal, vitals.HealingToCome, 0.01f, "the drink does not heal 80");
                    }
                    if (!droppedInTime && Object.FindObjectsByType<DroppedCan>(FindObjectsSortMode.None).Length >= 2)
                    {
                        droppedInTime = true;
                        Assert.That(seconds, Is.InRange(Drinker.DropSeconds - 0.1f, Drinker.Seconds), "the can was dropped at the wrong moment");
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
                vitals.TickHealthRegen(PhizzCan.Seconds * 0.5f);
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

        /// <summary>
        /// Opening the tab (owner: "the hand his turned knuckles upward and the finger is pinching the tab. no clipping into the can maybe a very slightly
        /// amount at the fingertips"): through the pinch the left hand is no more than a centimetre into the can, and its index fingertip is at the tab.
        /// </summary>
        [UnityTest]
        public IEnumerator OpeningTheTabTheLeftHandPinchesItWithoutGoingIntoTheCan()
        {
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(50.0f, 1.0f, 50.0f);
            (GameObject body, PhizzCan can, Drinker drinker) = Body(new Vector3(0.0f, 0.05f, 0.0f));
            try
            {
                can.OnEquip(body, ItemInstanceData.None);
                var locomotion = body.GetComponent<PlayerLocomotion>();
                for (int i = 0; i < 40; i++)
                {
                    locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                }
                Assert.IsTrue(can.Drink());
                var skin = CityX.Core.TransformSearch.FindDeep(body.transform, BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
                float seconds = 0.0f, deepest = 0.0f, farthestTip = 0.0f, deepestAt = 0.0f;
                string deepestPart = string.Empty;
                string report = string.Empty;
                while (seconds < Drinker.TabToSeconds)
                {
                    locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                    seconds += 1.0f / 60.0f;
                    if (seconds < Drinker.TabFromSeconds)
                    {
                        continue;
                    }
                    var posed = new Mesh();
                    skin.BakeMesh(posed, true);
                    Vector3[] points = posed.vertices;
                    BoneWeight[] weights = skin.sharedMesh.boneWeights;
                    Transform[] bones = skin.bones;
                    float lid = CanMesh.Height * BottleShape.LifeScale;
                    Vector3 tab = can.TabHinge.TransformPoint(CanMesh.TabEndFromHinge * BottleShape.LifeScale);
                    float deep = 0.0f, nearestTip = float.MaxValue;
                    string deepPart = string.Empty;
                    for (int i = 0; i < points.Length; i++)
                    {
                        Transform bone = bones[weights[i].boneIndex0];
                        string name = bone != null ? bone.name : string.Empty;
                        if (!name.EndsWith("_l") || !(name.StartsWith("hand") || name.StartsWith("index") || name.StartsWith("middle")
                            || name.StartsWith("ring") || name.StartsWith("little") || name.StartsWith("thumb")))
                        {
                            continue;
                        }
                        Vector3 world = skin.transform.TransformPoint(points[i]);
                        Vector3 inCan = can.transform.InverseTransformPoint(world);
                        float height = inCan.y / BottleShape.LifeScale;
                        float radius = height < 0.0f || height > CanMesh.Height ? 0.0f : BottleMesh.RadiusAt(CanMesh.Outline, height, 0.004f) * BottleShape.LifeScale;
                        float inside = Mathf.Min(radius - new Vector2(inCan.x, inCan.z).magnitude, lid - inCan.y);
                        if (inside > deep)
                        {
                            deep = inside;
                            deepPart = $"{name} at ({inCan.x:F2}, {inCan.y:F2}, {inCan.z:F2}) in the can's frame";
                        }
                        if (name.StartsWith("index2"))
                        {
                            nearestTip = Mathf.Min(nearestTip, Vector3.Distance(world, tab));
                        }
                    }
                    Object.Destroy(posed);
                    if (deep > deepest)
                    {
                        deepest = deep;
                        deepestAt = seconds;
                        deepestPart = deepPart;
                    }
                    farthestTip = Mathf.Max(farthestTip, nearestTip);
                }
                report = $"the left hand {deepest * 100.0f:F1} cm into the can at {deepestAt:F2} s ({deepestPart}); the index fingertip at most {farthestTip * 100.0f:F1} cm from the tab";
                Debug.Log("TAB PINCH: " + report);
                Assert.Less(deepest, 0.012f, "the left hand goes into the can. " + report);
                Assert.Less(farthestTip, 0.05f, "the index finger is not at the tab. " + report);
            }
            finally
            {
                Object.Destroy(body);
                Object.Destroy(ground);
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
        }

        /// <summary>
        /// The full PHIZZ bottle (owner, 2026-11-23: "full of piss yellow liquid and when you drink its, it visually lowers until empty", "100 health over 10
        /// seconds", drunk empty it "stays as a weapon", drinks "stack infinitely"): two in the bag, one taken in hand; the first drunk goes down to nothing
        /// and heals 100, the second comes into the hand and the empty one goes in the bag; the second drunk, the hand holds the empty soda bottle.
        /// </summary>
        [UnityTest]
        public IEnumerator AFullBottleIsDrunkDownHealsAHundredAndTheLastLeavesTheSodaBottleInTheHand()
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
                var inventory = body.GetComponent<Inventory>() ?? body.AddComponent<Inventory>();
                inventory.ItemPrefabs = new[]
                {
                    new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.PhizzBottle, Prefab = Load("Assets/CityX/Weapons/Cans/phizz_bottle.prefab") },
                    new Inventory.ItemPrefabEntry { ItemId = GlassBottleKinds.Soda, Prefab = Load(GlassBottleKinds.PrefabPath(GlassBottleKinds.Soda)) },
                };
                Assert.IsTrue(inventory.AddItem(ItemRegistry.PhizzBottle, 2));
                Assert.IsTrue(inventory.RemoveFromStorage(ItemRegistry.PhizzBottle, 1, out ItemInstanceData taken));
                Assert.IsTrue(inventory.EquipItemId(ItemRegistry.PhizzBottle, body, taken));
                var vitals = body.GetComponent<PlayerVitals>();
                FullBottle Held() => inventory.EquippedInstance(inventory.ActiveWeaponSlot)?.GetComponent<FullBottle>();

                for (int drink = 0; drink < 2; drink++)
                {
                    FullBottle bottle = Held();
                    Assert.IsNotNull(bottle, $"drink {drink + 1}: no full bottle in the hand");
                    yield return null;
                    float fullLevel = bottle.Level;
                    Assert.IsTrue(bottle.Drink());
                    float seconds = 0.0f, lowest = float.MaxValue;
                    bool capOff = false;
                    while (seconds < Drinker.Seconds + 0.3f)
                    {
                        yield return null;
                        seconds += 1.0f / 60.0f;
                        if (bottle != null)
                        {
                            capOff |= !bottle.Cap.activeSelf;
                            if (seconds > Drinker.HealFromSeconds && seconds < Drinker.DrunkBySeconds)
                            {
                                lowest = Mathf.Min(lowest, bottle.Left);
                            }
                        }
                    }
                    Assert.IsTrue(capOff, "the cap never came off");
                    Assert.Less(lowest, 0.3f, "the drink did not go down as it was drunk");
                    Assert.AreEqual(FullBottle.Heal * (drink + 1), vitals.HealingToCome, 0.01f, "a full bottle does not heal 100");
                    Assert.IsFalse(float.IsInfinity(fullLevel), "a full bottle shows no drink");
                }
                GameObject inHand = inventory.EquippedInstance(inventory.ActiveWeaponSlot);
                Assert.IsNotNull(inHand, "the hand is empty after the last bottle");
                Assert.IsNotNull(inHand.GetComponent<BottleWeapon>(), "the last bottle drunk is not the soda bottle, a weapon, in the hand");
                Assert.AreEqual(GlassBottleKinds.Soda, inHand.GetComponent<BottleWeapon>().ItemId);
                Assert.AreEqual(1, inventory.CountInStorage(GlassBottleKinds.Soda), "the first empty bottle did not go in the bag");
                Assert.AreEqual(0, inventory.CountInStorage(ItemRegistry.PhizzBottle));
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
