using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The owner's Knight Helmet (2026-10-18): a helmet-slot item whose baked model is centred on the head and fits around it.</summary>
    public sealed class KnightHelmetTests
    {
        private const string PrefabPath = "Assets/CityX/Wearables/KnightHelmet.prefab";
        private const string NetworkedPlayerPath = "Assets/CityX/Character/NetworkedPlayer.prefab";

        /// <summary>The most triangles a helmet may have (the budget given to the owner on 2026-10-18).</summary>
        private const int TriangleBudget = 8000;

        [Test]
        public void TheKnightHelmetFillsTheHelmetSlot()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.KnightHelmet, out ItemDefinition helmet));
            Assert.AreEqual(EquipSlots.Helmet, helmet.EquipSlot);
            Assert.AreEqual("Knight Helmet", helmet.DisplayName);
        }

        [Test]
        public void TheHelmetIsWithinItsTriangleBudgetAndGoesAroundTheHead()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "no KnightHelmet.prefab: rebuild the scene (CityMassingSceneBuilder)");
            Assert.IsNotNull(prefab.GetComponent<KnightHelmet>());
            Mesh mesh = prefab.transform.Find(KnightHelmet.ModelName).GetComponent<MeshFilter>().sharedMesh;
            Assert.LessOrEqual(mesh.triangles.Length / 3, TriangleBudget);

            // The model's origin is the head's centre, so the head sphere has to sit inside its bounds on every side but the open bottom.
            Bounds bounds = mesh.bounds;
            float head = FusedBodyShape.HeadRadius;
            Assert.Greater(bounds.max.y, head, "the helmet's top is inside the head");
            Assert.Less(bounds.min.x, -head, "the head pokes out of the helmet's side");
            Assert.Greater(bounds.max.x, head, "the head pokes out of the helmet's side");
            Assert.Less(bounds.min.z, -head, "the head pokes out of the helmet's back");
            Assert.Greater(bounds.max.z, head, "the head pokes out of the helmet's face");
        }

        [Test]
        public void OtherPlayersCanBeShownTheHelmet()
        {
            GameObject networked = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPath);
            Assert.IsNotNull(networked, "no NetworkedPlayer.prefab");
            GameObject prefab = networked.GetComponent<Inventory>().PrefabForItem(ItemRegistry.KnightHelmet);
            Assert.IsNotNull(prefab, "a copy of another player cannot build the helmet (NetworkedPlayerWiring.ItemPrefabPaths)");
            Assert.IsNotNull(prefab.GetComponent<ICosmeticMountable>(), "the helmet cannot be mounted on a copy");
        }
    }
}
