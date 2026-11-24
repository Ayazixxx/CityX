using System.Collections.Generic;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A shirt under a coat is drawn only in the coat's open front (UnderCoat; owner, 2026-11-21: "im seeing the shirt clipping through around the
    /// shoulders. make the shirt sleeves invisible underneath the parka").
    /// </summary>
    public sealed class UnderCoatTests
    {
        [Test]
        public void OnlyTheTrianglesInTheOpenFrontStay()
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-0.05f, 1.5f, 0.2f), new Vector3(0.05f, 1.5f, 0.2f), new Vector3(0.0f, 1.6f, 0.2f),     // the chest, in the opening
                new Vector3(0.30f, 1.9f, 0.05f), new Vector3(0.40f, 1.9f, 0.05f), new Vector3(0.35f, 1.95f, 0.0f),  // the top of a shoulder
                new Vector3(-0.05f, 1.5f, -0.2f), new Vector3(0.05f, 1.5f, -0.2f), new Vector3(0.0f, 1.6f, -0.2f),  // the back
            };
            List<int> kept = UnderCoat.InTheOpening(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, vertices);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, kept, "a shoulder or the back is still drawn under the coat, or the front is not");
        }

        [Test]
        public void ShirtsAreTheTopsThatAreNotCoats()
        {
            Assert.IsTrue(UnderCoat.IsShirt(ItemRegistry.LongSleeveTShirt));
            Assert.IsTrue(UnderCoat.IsShirt(ItemRegistry.RedStripedTShirt));
            Assert.IsFalse(UnderCoat.IsShirt(ItemRegistry.GreenFurHoodedParka));
            Assert.IsFalse(UnderCoat.IsShirt(ItemRegistry.BlackJeans));
        }

        /// <summary>Under a coat, nothing of a shirt is drawn round the shoulders or on the arms, and its chest still is.</summary>
        [TestCase("LongSleeveTShirt")]
        [TestCase("TShirt")]
        public void AShirtUnderACoatShowsItsChestAndNothingRoundTheShoulders(string prefab)
        {
            Mesh shirt = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{prefab}.prefab").GetComponent<SkinnedGarment>().GarmentMesh;
            Mesh under = UnderCoat.FrontOnly(shirt);
            Assert.Greater(under.triangles.Length, 0, "nothing of the shirt shows in the coat's open front");
            Assert.Less(under.triangles.Length, shirt.triangles.Length / 2, "most of the shirt is still drawn under the coat");
            Vector3[] vertices = shirt.vertices;
            foreach (int v in under.triangles)
            {
                Assert.Less(Mathf.Abs(vertices[v].x), Character.BodyRig.ShoulderX, "a point by the shoulders or on an arm is still drawn under the coat");
            }
            Assert.AreSame(under, UnderCoat.FrontOnly(shirt), "the shirt as it shows under a coat is made again every time");
        }

        /// <summary>
        /// Under the Brown Coat a shirt hugs the body from the chest down (owner, 2026-11-24: "make the t shirt fit a bit tigher when brown coat is
        /// equipped so it doesnt overlap like that", "the top part looks good its just the stomach"), and goes under the coat's front panels whole, never
        /// cut round them or out in front of them; under the parka it is left as it is.
        /// </summary>
        [TestCase("LongSleeveTShirt")]
        [TestCase("TShirt")]
        public void UnderTheBrownCoatAShirtIsWholeItsStomachDrawnInAndItGoesUnderThePanels(string prefab)
        {
            Assert.IsTrue(UnderCoat.FitsClose(ItemRegistry.BrownCoat));
            Assert.IsFalse(UnderCoat.FitsClose(ItemRegistry.GreenFurHoodedParka), "the parka's shirt is drawn in too");
            Mesh shirt = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{prefab}.prefab").GetComponent<SkinnedGarment>().GarmentMesh;
            Mesh coat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/BrownCoat.prefab").GetComponent<SkinnedGarment>().GarmentMesh;
            Mesh close = UnderCoat.FrontOnlyClose(shirt, coat);
            // Whole: every triangle in the opening, none cut away round the panels (owner, 2026-11-24: "there is a bunch of "shards" missing
            // from the t shirt").
            Assert.AreEqual(UnderCoat.FrontOnly(shirt).triangles.Length, close.triangles.Length, "triangles of the shirt are cut away under the coat");
            Dictionary<(int, int), float> panels = UnderCoat.PanelsInFront(coat);
            Assert.Greater(panels.Count, 1000, "the coat covers nothing of the front");
            Vector3[] drawn = close.vertices;
            float loosest = 0.0f;
            int behind = 0;
            foreach (int v in close.triangles)
            {
                if (drawn[v].y < UnderCoat.CloseBelow)
                {
                    loosest = Mathf.Max(loosest, new Vector2(drawn[v].x, drawn[v].z).magnitude);
                }
                if (panels.TryGetValue((Mathf.FloorToInt(drawn[v].x / UnderCoat.CoverCell), Mathf.FloorToInt(drawn[v].y / UnderCoat.CoverCell)), out float panel))
                {
                    Assert.LessOrEqual(drawn[v].z, panel - UnderCoat.BehindThePanels + 1e-4f, $"the shirt comes out in front of the coat at {drawn[v]}");
                    behind++;
                }
            }
            TestContext.WriteLine($"{prefab}: {behind} corners under the panels, the stomach {loosest * 100.0f:0.0} cm from the middle line at the most");
            Assert.Greater(behind, 0, "none of the shirt goes under the panels");
            // Hung from the hips, not the thighs: a knee coming up swung its corners out through the panels ("the white shirt going through the
            // coat a tiny bit").
            int firstLeg = System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.LeftUpperLeg);
            foreach (BoneWeight w in close.boneWeights)
            {
                foreach ((int bone, float share) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                {
                    Assert.IsFalse(share > 0.0f && bone >= firstLeg && bone < SkinnedGarment.Bones.Length, "the shirt under the coat still follows a leg");
                }
            }
            Assert.LessOrEqual(loosest, UnderCoat.TrunkRadius + UnderCoat.CloseOffTheTrunk + 1e-4f, "the stomach still hangs off the body");
            Assert.AreSame(close, UnderCoat.FrontOnlyClose(shirt, coat));
        }

        /// <summary>
        /// Every pair of trousers under the Brown Coat (owner, 2026-11-24: "fix the jeans clipping through the brown coat"): wherever the coat lies
        /// over them, from in front or behind, they are behind its cloth; below its hem they are as they were.
        /// </summary>
        [Test]
        public void UnderTheBrownCoatTrousersStayBehindItsCloth()
        {
            Mesh coat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/BrownCoat.prefab").GetComponent<SkinnedGarment>().GarmentMesh;
            Dictionary<(int, int), float> front = UnderCoat.PanelsInFront(coat), back = UnderCoat.PanelsBehind(coat);
            Assert.Greater(back.Count, 1000, "the coat covers nothing of the back");
            int checkedTrousers = 0;
            foreach (Character.Inventory.ItemPrefabEntry entry in CityX.CharacterEditor.NetworkedPlayerWiring.ItemPrefabEntries(definition =>
                         definition.EquipSlot == EquipSlots.Legs))
            {
                var garment = entry.Prefab != null ? entry.Prefab.GetComponent<SkinnedGarment>() : null;
                if (garment == null || garment.GarmentMesh == null)
                {
                    continue;
                }
                Assert.IsTrue(UnderCoat.IsTrousers(entry.ItemId), entry.ItemId);
                Mesh own = garment.GarmentMesh, under = UnderCoat.TrousersUnderClose(own, coat);
                Vector3[] before = own.vertices, after = under.vertices;
                Assert.AreEqual(before.Length, after.Length);
                int moved = 0;
                for (int i = 0; i < after.Length; i++)
                {
                    var cell = (Mathf.FloorToInt(after[i].x / UnderCoat.CoverCell), Mathf.FloorToInt(after[i].y / UnderCoat.CoverCell));
                    if (after[i].z >= 0.0f && front.TryGetValue(cell, out float panel))
                    {
                        Assert.LessOrEqual(after[i].z, panel - UnderCoat.TrousersBehindTheCoat + 1e-4f, $"{entry.ItemId} come out in front of the coat at {after[i]}");
                    }
                    if (after[i].z < 0.0f && back.TryGetValue(cell, out float cloth))
                    {
                        Assert.GreaterOrEqual(after[i].z, cloth + UnderCoat.TrousersBehindTheCoat - 1e-4f, $"{entry.ItemId} come out through the coat's back at {after[i]}");
                    }
                    if (after[i] != before[i])
                    {
                        moved++;
                    }
                }
                for (int i = 0; i < after.Length; i++)
                {
                    if (before[i].y < coat.bounds.min.y - 0.05f)
                    {
                        Assert.AreEqual(before[i], after[i], $"{entry.ItemId} moved below the coat's hem at {before[i]}");
                    }
                }
                TestContext.WriteLine($"{entry.ItemId}: {moved} points drawn in under the coat");
                checkedTrousers++;
            }
            Assert.Greater(checkedTrousers, 0, "no trousers to check");
        }
    }
}
