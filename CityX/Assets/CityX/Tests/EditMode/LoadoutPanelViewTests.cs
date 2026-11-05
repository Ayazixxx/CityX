using CityX.Character;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// LoadoutPanelView driven through its own public Open/Close, the same
    /// reasoning PlayerMenuViewTests already uses. Awake wires its slot
    /// button listeners against whatever SlotButtons array is assigned AT
    /// THE MOMENT AddComponent runs -- the field's own default (empty),
    /// here, since the real buttons are assigned afterward -- so a
    /// simulated Button click would silently reach no listener at all.
    /// Unequip-on-click is exercised at the Inventory level instead (see
    /// InventoryTests.UnequipToSlotReturnsTheItemToStorageAndCallsOnUnequip)
    /// -- the same boundary this project already draws for every other
    /// Awake-wired panel (there is no InventoryPanelViewTests.cs either,
    /// for the same reason). What IS tested here is Open/Close's own state
    /// transitions and the Refresh() read path Open() drives.
    /// </summary>
    public sealed class LoadoutPanelViewTests
    {
        private sealed class TestEquippableItem : MonoBehaviour, IEquippableItem
        {
            public void OnEquip(GameObject player, ItemInstanceData instanceData) { }
            public void OnUnequip(GameObject player) { }
            public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
        }

        private GameObject _playerObject;
        private GameObject _bottlePrefab;
        private GameObject _clothingPrefab;
        private Inventory _inventory;
        private PlayerInputReader _inputReader;
        private GameObject _cameraObject;
        private ThirdPersonCamera _orbitCamera;
        private GameObject _panelRoot;
        private LoadoutPanelView _loadout;

        [SetUp]
        public void SetUp()
        {
            _playerObject = new GameObject("Player", typeof(CharacterController));
            _inputReader = _playerObject.AddComponent<PlayerInputReader>();
            _inventory = _playerObject.AddComponent<Inventory>();
            _inventory.Storage = new Inventory.StorageSlot[Inventory.StorageSlotCount];

            _bottlePrefab = new GameObject("BottlePrefab");
            _bottlePrefab.AddComponent<TestEquippableItem>();
            _bottlePrefab.SetActive(false);

            _clothingPrefab = new GameObject("ClothingPrefab");
            _clothingPrefab.AddComponent<TestEquippableItem>();
            _clothingPrefab.SetActive(false);

            _inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.GlassBottle, Prefab = _bottlePrefab },
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.HoodedCloak, Prefab = _clothingPrefab },
            };

            _cameraObject = new GameObject("Camera");
            _orbitCamera = _cameraObject.AddComponent<ThirdPersonCamera>();
            _orbitCamera.Target = _playerObject.transform;

            _panelRoot = new GameObject("LoadoutRoot", typeof(RectTransform));
            // Same reasoning as PlayerMenuViewTests' own menuRoot -- a
            // freshly created GameObject defaults to active, and Awake's
            // own Close() call is a no-op here (Root is still null at the
            // moment AddComponent runs it), so this stands in for it.
            _panelRoot.SetActive(false);
            _loadout = _panelRoot.AddComponent<LoadoutPanelView>();
            _loadout.Root = _panelRoot;
            _loadout.Inventory = _inventory;
            _loadout.Player = _playerObject;
            _loadout.InputReader = _inputReader;
            _loadout.OrbitCamera = _orbitCamera;

            string[] keys = LoadoutPanelView.SlotKeys;
            var buttons = new Button[keys.Length];
            var labels = new Text[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                var go = new GameObject("Slot_" + keys[i], typeof(RectTransform));
                go.transform.SetParent(_panelRoot.transform, false);
                buttons[i] = go.AddComponent<Button>();
                labels[i] = go.AddComponent<Text>();
            }
            _loadout.SlotButtons = buttons;
            _loadout.SlotLabels = labels;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_bottlePrefab);
            Object.DestroyImmediate(_clothingPrefab);
            Object.DestroyImmediate(_cameraObject);
            Object.DestroyImmediate(_panelRoot);
        }

        [Test]
        public void OpenShowsTheRootAndDisablesInputAndOrbitCamera()
        {
            _loadout.Open();

            Assert.IsTrue(_loadout.IsOpen);
            Assert.IsFalse(_inputReader.TakesKeys, "movement/combat input should be disabled while the panel is up");
            Assert.IsFalse(_orbitCamera.enabled, "mouse-look should be disabled while the panel is up");
        }

        [Test]
        public void CloseHidesTheRootAndReenablesInputAndOrbitCamera()
        {
            _loadout.Open();
            _loadout.Close();

            Assert.IsFalse(_loadout.IsOpen);
            Assert.IsTrue(_inputReader.TakesKeys);
            Assert.IsTrue(_orbitCamera.enabled);
        }

        [Test]
        public void OpeningTwiceInARowIsHarmlessAndDoesNotReenableInputEarly()
        {
            _loadout.Open();
            _inputReader.enabled = false; // simulate whatever Open() already set
            _loadout.Open();

            Assert.IsTrue(_loadout.IsOpen);
            Assert.IsFalse(_inputReader.TakesKeys, "a redundant Open() must not disturb the already-disabled input");
        }

        [Test]
        public void OpenRefreshesEmptySlotsToADash()
        {
            _loadout.Open();

            int legsIndex = System.Array.IndexOf(LoadoutPanelView.SlotKeys, "legs");
            Assert.AreEqual("--", _loadout.SlotLabels[legsIndex].text);
        }

        [Test]
        public void OpenRefreshesAnEquippedSlotToTheItemsDisplayName()
        {
            _inventory.EquipItemId(ItemRegistry.HoodedCloak, _playerObject);

            _loadout.Open();

            int topIndex = System.Array.IndexOf(LoadoutPanelView.SlotKeys, "top");
            Assert.AreEqual("Hooded Cloak", _loadout.SlotLabels[topIndex].text);
        }
    }
}
