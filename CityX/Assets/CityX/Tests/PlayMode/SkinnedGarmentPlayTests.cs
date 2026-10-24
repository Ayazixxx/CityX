using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Real-frame behavior for skinned garments -- Destroy() defers to end of
    /// frame, so verifying anything it does needs a real yielded frame, not
    /// an EditMode test (see SkinnedGarmentTests for the plain-logic half).
    /// </summary>
    public sealed class SkinnedGarmentPlayTests
    {
        private GameObject _playerObject;
        private Inventory _inventory;
        private GameObject _helmetPrefab;
        private GameObject _cloakPrefab;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player");
            _inventory = _playerObject.AddComponent<Inventory>();
            _inventory.Storage = new Inventory.StorageSlot[Inventory.StorageSlotCount];

            _helmetPrefab = new GameObject("HelmetPrefab");
            _helmetPrefab.AddComponent<KnightHelmet>();
            _helmetPrefab.SetActive(false);

            _cloakPrefab = new GameObject("CloakPrefab");
            _cloakPrefab.AddComponent<HoodedCloak>();
            _cloakPrefab.SetActive(false);

            _inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.KnightHelmet, Prefab = _helmetPrefab },
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.HoodedCloak, Prefab = _cloakPrefab },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_helmetPrefab);
            Object.DestroyImmediate(_cloakPrefab);
        }

        /// <summary>
        /// A bare Animator with no avatar reproduces exactly the gap this
        /// test caught: Animator.GetBoneTransform THROWS rather than
        /// returning null when there is no avatar at all, unlike a missing
        /// individual bone, which it tolerates. Building an animator with a
        /// full working Humanoid avatar needs the real editor-time rig
        /// builder, which is out of reach for a play-mode test assembly --
        /// the actual skin-building path against a real rig is instead
        /// verified visually (see the plan's own screenshot checks), and
        /// what belongs here is the two things a real frame can prove:
        /// OnEquip degrades safely rather than throwing, and OnUnequip's
        /// deferred Destroy() still cleans up the root even when no skin
        /// was ever built.
        /// </summary>
        [UnityTest]
        public IEnumerator OnEquipWithNoAvatarDoesNotThrowAndOnUnequipStillDestroysTheRoot()
        {
            var player = new GameObject("PlayerWithBareAnimator");
            player.AddComponent<Animator>();

            var itemObject = new GameObject("Cloak");
            var cloak = itemObject.AddComponent<HoodedCloak>();
            cloak.GarmentMesh = new Mesh();

            Assert.DoesNotThrow(() => cloak.OnEquip(player, ItemInstanceData.None));
            Assert.IsNull(player.transform.Find(cloak.ItemId + "Skin"),
                "no avatar means no bones to bind to, so no skin should have been built");

            cloak.OnUnequip(player);
            yield return null; // Destroy() is deferred to end of frame

            Assert.IsTrue(itemObject == null, "the root must not survive OnUnequip");

            Object.DestroyImmediate(player);
        }

        [UnityTest]
        public IEnumerator TheHelmetAndTheCloakFillDifferentSlotsSoBothCanBeWornAtOnce()
        {
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.KnightHelmet, _playerObject));
            yield return null;
            Assert.IsTrue(_inventory.EquipItemId(ItemRegistry.HoodedCloak, _playerObject));
            yield return null;

            Assert.AreEqual(ItemRegistry.KnightHelmet, _inventory.EquippedItemId("helmet"),
                "the helmet should still be worn -- the cloak fills a different slot");
            Assert.AreEqual(ItemRegistry.HoodedCloak, _inventory.EquippedItemId("top"));
        }

        /// <summary>
        /// THE ACTUAL POINT OF SkinnedGarmentCombiner, proven on a real rig
        /// rather than assumed from the EditMode logic tests alone -- with
        /// many players on screen at once, a real GPU skin evaluation runs
        /// once per SkinnedMeshRenderer, so wearing two skinned
        /// garments has to cost one renderer, not two, regardless of how
        /// large the item catalog ever grows.
        /// </summary>
        [UnityTest]
        public IEnumerator WearingTwoSkinnedGarmentsSharesOneCombinedRendererNotTwo()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");
            GameObject shirtPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Wearables/TShirt.prefab");
            Assert.IsNotNull(shirtPrefab, "TShirt.prefab is missing -- rebuild the scene");
            GameObject cloakPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Wearables/HoodedCloak.prefab");
            Assert.IsNotNull(cloakPrefab, "HoodedCloak.prefab is missing -- run its own Rebuild menu item first");

            var realPlayer = Object.Instantiate(playerPrefab);
            int bodyOnlyRendererCount = realPlayer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;

            var shirt = Object.Instantiate(shirtPrefab).GetComponent<TShirt>();
            shirt.OnEquip(realPlayer, ItemInstanceData.None);
            yield return null;

            var cloak = Object.Instantiate(cloakPrefab).GetComponent<HoodedCloak>();
            cloak.OnEquip(realPlayer, ItemInstanceData.None);
            yield return null;

            var combiner = realPlayer.GetComponent<SkinnedGarmentCombiner>();
            Assert.IsNotNull(combiner, "equipping a skinned garment on a real rig should build the combiner");
            Assert.AreEqual(2, combiner.CombinedMesh.subMeshCount, "both garments should share one combined mesh");

            int totalRendererCount = realPlayer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            Assert.AreEqual(bodyOnlyRendererCount + 1, totalRendererCount,
                "wearing two garments should add exactly ONE renderer (the shared combiner's), not one per item");

            Object.Destroy(realPlayer);
            Object.Destroy(shirt.gameObject);
            Object.Destroy(cloak.gameObject);
#else
            yield break;
#endif
        }
    }
}
