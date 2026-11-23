using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using CityX.WeaponsEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The PHIZZ can (docs/PHIZZ_CAN.md): a real can's shape at the bottles' size, its parts (gold body, silver ends, the tab, the opening, the
    /// label), within a small prop's budget, the tab and the opening on the lid inside its rim and clear of each other.
    /// </summary>
    public sealed class PhizzCanTests
    {
        [Test]
        public void TheCanIsBuiltWithItsPartsAtTheBottlesSize()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PhizzCanBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "run CityX/Rebuild PHIZZ Can");
            int triangles = 0;
            Bounds bounds = default;
            bool first = true;
            foreach (string part in new[] { "Body", "Foot", "Top", "Rivet", "TabHinge/Tab", "Opening", "Hole", "Print" })
            {
                Transform child = prefab.transform.Find(part);
                Assert.IsNotNull(child, $"the can has no {part}");
                Mesh mesh = child.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsNotNull(mesh, $"the {part} lost its mesh");
                Assert.Greater(mesh.triangles.Length, 0, $"the {part} is empty");
                triangles += mesh.triangles.Length / 3;
                if (first)
                {
                    bounds = mesh.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(mesh.bounds);
                }
            }
            Assert.Less(triangles, 3500, "over a small prop's budget (the bottles are allowed 5000)");
            Assert.AreEqual(CanMesh.Height * BottleShape.LifeScale, bounds.max.y, 0.004f, "it stands on its base at the origin, a real can's height");
            Assert.AreEqual(0.0f, bounds.min.y, 0.002f);
            Assert.AreEqual(CanMesh.Radius * BottleShape.LifeScale, bounds.max.x, 0.004f, "a real can's width");
            Assert.IsNotNull(prefab.transform.Find("Print").GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap"), "the label is not printed");
            Assert.IsNotNull(prefab.GetComponent<PhizzCan>().OpenSound, "the tab opens without its sound");
        }

        [Test]
        public void ACanHealsEightyOverTenSecondsEvenlyAndTwoHealSideBySide()
        {
            var heal = new HealOverTime();
            heal.Add(PhizzCan.Heal, PhizzCan.Seconds);
            Assert.AreEqual(80.0f, PhizzCan.Heal, "the owner's 80");
            Assert.AreEqual(10.0f, PhizzCan.Seconds, "the owner's 10 seconds");
            Assert.AreEqual(8.0f, heal.Take(1.0f), 1e-4f, "not even over the ten seconds");
            heal.Add(PhizzCan.Heal, PhizzCan.Seconds);
            Assert.AreEqual(16.0f, heal.Take(1.0f), 1e-4f, "a second drink does not heal beside the first");
            float given = 24.0f;
            for (int i = 0; i < 20; i++)
            {
                given += heal.Take(1.0f);
            }
            Assert.AreEqual(160.0f, given, 1e-3f, "two cans did not give 160 in all");
            Assert.IsFalse(heal.Running);
        }

        [Test]
        public void TheDrinkClipIsThreeAndAHalfSecondsAndItsMomentsComeInOrder()
        {
            BakedMotion motion = BakedMotion.Load(Drinker.MotionPath);
            Assert.IsNotNull(motion, "the drink clip is not in Resources: run can_drink_build.py --export");
            Assert.IsTrue(motion.HasSegment(PhizzCan.DrinkClip));
            Assert.AreEqual(Drinker.Seconds, (motion.SegmentFrames(PhizzCan.DrinkClip) - 1) / (float)motion.FramesPerSecond, 0.02f, "the owner's 3.5 seconds");
            Assert.IsTrue(motion.HasSegment(FullBottle.DrinkClip), "the bottle has no drink clip");
            Assert.AreEqual(Drinker.Seconds, (motion.SegmentFrames(FullBottle.DrinkClip) - 1) / (float)motion.FramesPerSecond, 0.02f, "the bottle's drink is not 3.5 seconds");
            Assert.Less(Drinker.HealFromSeconds, Drinker.DrunkBySeconds);
            Assert.Less(Drinker.DrunkBySeconds, Drinker.Seconds);
            Assert.Less(Drinker.TabFromSeconds, Drinker.TabToSeconds);
            Assert.Less(Drinker.TabToSeconds, Drinker.HealFromSeconds);
            Assert.Less(Drinker.HealFromSeconds, Drinker.DropSeconds);
            Assert.Less(Drinker.DropSeconds, Drinker.Seconds);
        }

        [Test]
        public void AFullBottleHealsAHundredStacksWithoutEndAndShowsItsDrink()
        {
            Assert.AreEqual(100.0f, FullBottle.Heal, "the owner's 100");
            Assert.AreEqual(10.0f, FullBottle.Seconds, "the owner's 10 seconds");
            Assert.AreEqual(GlassBottleKinds.Soda, FullBottle.EmptyItemId, "drunk empty it is not the soda bottle");
            foreach (string drink in new[] { CityX.Items.ItemRegistry.PhizzCan, CityX.Items.ItemRegistry.PhizzBottle })
            {
                Assert.IsTrue(CityX.Items.ItemRegistry.TryGet(drink, out CityX.Items.ItemDefinition definition), drink);
                Assert.AreEqual(CityX.Items.ItemRegistry.EndlessStack, definition.StackSize, drink + " does not stack without end");
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FullBottleBuilder.PrefabPath);
            Assert.IsNotNull(prefab, "run CityX/Rebuild PHIZZ Bottle");
            var bottle = prefab.GetComponent<FullBottle>();
            Assert.IsNotNull(bottle.Cap, "no cap to pull off");
            Assert.IsNotNull(bottle.CapPrefab, "the cap does not fall");
            Assert.AreEqual(1, bottle.Glass.Length);
            Assert.IsTrue(bottle.Glass[0].sharedMaterial.IsKeywordEnabled("_CITYX_LIQUID"), "the glass shows no drink");
        }

        [Test]
        public void TheTabAndTheOpeningLieOnTheLidInsideItsRimAndApart()
        {
            BottleMesh.Model tab = CanMesh.Tab(), opening = CanMesh.Opening();
            float tabNearest = float.MaxValue, openingFurthest = float.MinValue;
            foreach (Vector3 v in tab.Vertices)
            {
                Assert.Less(new Vector2(v.x, v.z).magnitude, CanMesh.LidRadius - 0.002f, "the tab reaches over the rim");
                Assert.GreaterOrEqual(v.y, CanMesh.LidHeight, "the tab is under the lid");
                tabNearest = Mathf.Min(tabNearest, v.x);
            }
            foreach (Vector3 v in opening.Vertices)
            {
                Assert.Less(new Vector2(v.x, v.z).magnitude, CanMesh.LidRadius - 0.002f, "the opening runs into the rim");
                openingFurthest = Mathf.Max(openingFurthest, v.x);
            }
            Assert.Less(openingFurthest, 0.0f, "the opening is on the other side of the rivet from the tab's finger end");
            Assert.Less(tabNearest, 0.0f, "the tab's nose does not reach back over the rivet, where it presses the opening");
        }
    }
}
