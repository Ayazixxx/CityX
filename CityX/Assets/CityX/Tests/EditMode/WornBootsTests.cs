using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using CityX.WearablesEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Worn Boots (docs/WORN_BOOTS.md; owner, 2026-11-23: "use the sneakers we built as a base for sizing but these can be a bit bulkier than the
    /// sneakers ... not too bulky", "just make a boot a homeless person would wear"): the item, the pair on the feet as the sneakers are, and the
    /// model's size against the sneakers'.
    /// </summary>
    public sealed class WornBootsTests
    {
        /// <summary>A pair of shoes' triangles at most for "fine" (tools/model_qa.py --kind shoes).</summary>
        private const int TriangleBudget = 8000;

        private static Shoes Boots()
        {
            GameObject networked = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            GameObject prefab = networked.GetComponent<Inventory>().PrefabForItem(ItemRegistry.WornBoots);
            Assert.IsNotNull(prefab, "a copy of another player cannot build the Worn Boots (NetworkedPlayerWiring.ItemPrefabPaths)");
            return prefab.GetComponent<Shoes>();
        }

        /// <summary>A shoe's box in the body's frame (the pair's root), standing as worn.</summary>
        private static Bounds InBody(Transform shoe)
        {
            Mesh mesh = shoe.GetComponent<MeshFilter>().sharedMesh;
            Matrix4x4 place = Matrix4x4.TRS(shoe.localPosition, shoe.localRotation, shoe.localScale);
            Vector3[] points = mesh.vertices;
            var box = new Bounds(place.MultiplyPoint3x4(points[0]), Vector3.zero);
            foreach (Vector3 p in points)
            {
                box.Encapsulate(place.MultiplyPoint3x4(p));
            }
            return box;
        }

        [Test]
        public void TheyAreCommonShoesOfTheirOwn()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.WornBoots, out ItemDefinition item));
            Assert.AreEqual("Worn Boots", item.DisplayName);
            Assert.AreEqual(EquipSlots.Shoes, item.EquipSlot);
            Assert.AreEqual(Rarity.Common, ItemRarity.Of(ItemRegistry.WornBoots));
            Shoes boots = Boots();
            Assert.IsNotNull(boots.Left, "no left boot");
            Assert.IsNotNull(boots.Right, "no right boot");
            int triangles = 0;
            foreach (Transform shoe in new[] { boots.Left, boots.Right })
            {
                triangles += shoe.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
                var map = shoe.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap");
                Assert.IsNotNull(map, "the boots have no paint");
                Assert.LessOrEqual(map.width, 1024);
            }
            TestContext.WriteLine($"{triangles} triangles for the pair");
            Assert.Less(triangles, TriangleBudget, "over a pair of shoes' budget");
            Assert.Greater(boots.HiddenFromBodyTriangles, 0, "the feet would show through the boots");
        }

        [Test]
        public void TheyStandOnTheGroundEachOnItsFootABitBulkierThanTheSneakers()
        {
            Shoes boots = Boots();
            Shoes sneakers = AssetDatabase.LoadAssetAtPath<GameObject>(WhiteSneakerBuilder.PrefabPath).GetComponent<Shoes>();
            Bounds left = InBody(boots.Left), right = InBody(boots.Right), sneaker = InBody(sneakers.Right);
            Assert.Less(left.max.x, 0.0f, "the left boot is not on the left foot");
            Assert.Greater(right.min.x, 0.0f, "the right boot is not on the right foot");
            Assert.AreEqual(0.0f, right.min.y, 0.005f, "the sole is not on the ground");
            Assert.AreEqual(0.0f, left.min.y, 0.005f, "the sole is not on the ground");
            Assert.Greater(right.max.z, -right.min.z, "the toe does not point forward");
            float wider = right.size.x / sneaker.size.x, longer = right.size.z / sneaker.size.z, taller = right.size.y / sneaker.size.y;
            TestContext.WriteLine($"against the sneaker: {wider:0.00} as wide, {longer:0.00} as long, {taller:0.00} as tall");
            Assert.That(wider, Is.InRange(1.1f, 1.4f), "not a bit bulkier than the sneakers (or too bulky)");
            Assert.That(longer, Is.InRange(1.0f, 1.25f), "not the sneakers' length, a little more");
            Assert.Greater(taller, 1.5f, "not a boot: its shaft stands no higher than a sneaker's collar");
        }
    }
}
