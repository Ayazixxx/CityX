using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.UI;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// What opening and closing the inventory does to movement/camera
    /// input, and what clicking its storage and equipment slots actually
    /// does through a real Inventory component -- the same
    /// RemoveFromStorage-then-EquipItemId composition DevConsoleView.Equip
    /// uses. Built by hand rather than by loading the scene, so this tests
    /// InventoryPanelView's own behaviour and fails for one reason, the
    /// same reasoning MapPanelViewPlayTests already uses.
    /// </summary>
    public class InventoryPanelViewPlayTests
    {
        private GameObject _panelObject;
        private GameObject _playerObject;
        private GameObject _cameraObject;
        private GameObject _cloakPrefab;

        [TearDown]
        public void TearDown()
        {
            if (_panelObject != null) Object.DestroyImmediate(_panelObject);
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
            if (_cloakPrefab != null) Object.DestroyImmediate(_cloakPrefab);
        }

        /// <summary>
        /// Built INACTIVE, and stays that way until the caller activates it
        /// with ActivatePanel -- Unity only calls Awake once a GameObject is
        /// actually active, and Awake is where InventoryPanelView wires its
        /// button listeners. Adding the component to an already-active
        /// object (as MapPanelViewPlayTests does, since MapPanelView wires
        /// no listeners in Awake) would fire that wiring before this method
        /// -- or the click tests below -- had finished setting
        /// StorageButtons/EquipButtons, and it would silently wire nothing.
        /// </summary>
        private InventoryPanelView MakePanel(out PlayerInputReader inputReader,
            out ThirdPersonCamera orbitCamera, out Inventory inventory)
        {
            _playerObject = new GameObject("Player");
            _playerObject.AddComponent<CharacterController>();
            _playerObject.AddComponent<PlayerLocomotion>();
            inputReader = _playerObject.AddComponent<PlayerInputReader>();
            inventory = _playerObject.AddComponent<Inventory>();
            inventory.Storage = new Inventory.StorageSlot[Inventory.StorageSlotCount];

            _cloakPrefab = new GameObject("CloakPrefab");
            _cloakPrefab.AddComponent<HoodedCloak>();
            _cloakPrefab.SetActive(false);
            inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.HoodedCloak, Prefab = _cloakPrefab },
            };

            _cameraObject = new GameObject("Camera");
            orbitCamera = _cameraObject.AddComponent<ThirdPersonCamera>();
            orbitCamera.Target = _playerObject.transform;

            _panelObject = new GameObject("InventoryPanel");
            _panelObject.SetActive(false);
            var panel = _panelObject.AddComponent<InventoryPanelView>();
            panel.Root = new GameObject("Root");
            panel.Root.transform.SetParent(_panelObject.transform, false);
            panel.Root.SetActive(false);
            panel.InputReader = inputReader;
            panel.OrbitCamera = orbitCamera;
            panel.Inventory = inventory;
            panel.Player = _playerObject;
            return panel;
        }

        private void ActivatePanel()
        {
            _panelObject.SetActive(true);
        }

        /// <summary>A bare clickable button for the two slot tests below -- no icon, no label, just something with a real onClick to wire.</summary>
        private static Button MakeButton(GameObject parent)
        {
            var go = new GameObject("Slot");
            go.transform.SetParent(parent.transform, false);
            var image = go.AddComponent<Image>();
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        [UnityTest]
        public IEnumerator OpeningDisablesMovementAndCameraLook()
        {
            InventoryPanelView panel = MakePanel(out PlayerInputReader inputReader,
                out ThirdPersonCamera orbitCamera, out _);
            ActivatePanel();
            yield return null;

            panel.Open();
            yield return null;

            Assert.IsTrue(panel.IsOpen);
            Assert.IsFalse(inputReader.enabled, "input should be disabled while the inventory is open");
            Assert.IsFalse(orbitCamera.enabled, "the orbit camera should be disabled while the inventory is open");
        }

        [UnityTest]
        public IEnumerator ClosingRestoresMovementAndCameraLook()
        {
            InventoryPanelView panel = MakePanel(out PlayerInputReader inputReader,
                out ThirdPersonCamera orbitCamera, out _);
            ActivatePanel();
            yield return null;
            panel.Open();
            yield return null;

            panel.Close();
            yield return null;

            Assert.IsFalse(panel.IsOpen);
            Assert.IsTrue(inputReader.enabled, "input should come back once the inventory closes");
            Assert.IsTrue(orbitCamera.enabled, "the orbit camera should come back once the inventory closes");
        }

        [UnityTest]
        public IEnumerator OpeningTwiceInARowDoesNothingExtra()
        {
            InventoryPanelView panel = MakePanel(out PlayerInputReader inputReader, out _, out _);
            ActivatePanel();
            yield return null;

            panel.Open();
            yield return null;
            inputReader.enabled = true; // simulate something else re-enabling it mid-session
            panel.Open();
            yield return null;

            // The guard at the top of Open() should have refused the second
            // call rather than re-running it and stomping the flag back off
            // in a way that would mask a real bug elsewhere.
            Assert.IsTrue(inputReader.enabled);
        }

        [UnityTest]
        public IEnumerator ClickingAStorageSlotHoldingAnEquippableItemEquipsIt()
        {
            InventoryPanelView panel = MakePanel(out _, out _, out Inventory inventory);
            inventory.Storage[0] = new Inventory.StorageSlot
                { ItemId = ItemRegistry.HoodedCloak, Count = 1, InstanceData = ItemInstanceData.None };

            Button slot = MakeButton(_panelObject);
            panel.StorageButtons = new[] { slot };
            panel.StorageLabels = new Text[] { null };
            ActivatePanel();
            yield return null;

            slot.onClick.Invoke();
            yield return null;

            Assert.IsTrue(inventory.Storage[0].IsEmpty, "the cloak should have left storage");
            Assert.AreEqual(ItemRegistry.HoodedCloak, inventory.EquippedItemId("top"),
                "the cloak should now be equipped");
        }

        [UnityTest]
        public IEnumerator ClickingAFilledEquipmentSlotUnequipsItBackToStorage()
        {
            InventoryPanelView panel = MakePanel(out _, out _, out Inventory inventory);
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.HoodedCloak, _playerObject));

            int topIndex = System.Array.IndexOf(InventoryPanelView.EquipSlotKeys, "top");
            var buttons = new Button[InventoryPanelView.EquipSlotKeys.Length];
            buttons[topIndex] = MakeButton(_panelObject);
            panel.EquipButtons = buttons;
            panel.EquipLabels = new Text[InventoryPanelView.EquipSlotKeys.Length];
            ActivatePanel();
            yield return null;

            buttons[topIndex].onClick.Invoke();
            yield return null;

            Assert.AreEqual(string.Empty, inventory.EquippedItemId("top"),
                "the top slot should be empty again");
            Assert.IsTrue(inventory.HasItem(ItemRegistry.HoodedCloak),
                "the cloak should be back in storage");
        }

        /// <summary>Stands in for a real jetpack: equipping one needs only something that answers to being put on and taken off.</summary>
        private sealed class WornStandIn : MonoBehaviour, IEquippableItem
        {
            public void OnEquip(GameObject player, ItemInstanceData instanceData) { }
            public void OnUnequip(GameObject player) { Destroy(gameObject); }
            public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
        }

        [UnityTest]
        public IEnumerator AJetpackOnTheBackShowsInTheBackSlotAndClickingItTakesItOff()
        {
            // The owner's ask (2026-10-15): shoes, gloves and back slots in the inventory. The back is the jetpack's and the parachute's, and
            // the row used to have no slot for it: a jetpack worn showed nowhere.
            InventoryPanelView panel = MakePanel(out _, out _, out Inventory inventory);
            var jetpackPrefab = new GameObject("JetpackStandIn");
            jetpackPrefab.AddComponent<WornStandIn>();
            jetpackPrefab.SetActive(false);
            inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.Jetpack, Prefab = jetpackPrefab },
            };
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.Jetpack, _playerObject), "setup: could not put the jetpack on");

            int backIndex = System.Array.IndexOf(InventoryPanelView.EquipSlotKeys, EquipSlots.Back);
            Assert.GreaterOrEqual(backIndex, 0, "the inventory has no back slot");
            var buttons = new Button[InventoryPanelView.EquipSlotKeys.Length];
            var labels = new Text[InventoryPanelView.EquipSlotKeys.Length];
            buttons[backIndex] = MakeButton(_panelObject);
            labels[backIndex] = new GameObject("BackLabel").AddComponent<Text>();
            labels[backIndex].transform.SetParent(_panelObject.transform, false);
            panel.EquipButtons = buttons;
            panel.EquipLabels = labels;
            ActivatePanel();
            yield return null;

            panel.Open();
            Assert.AreEqual("Jetpack", labels[backIndex].text, "the jetpack on the back does not show in the back slot");

            buttons[backIndex].onClick.Invoke();
            yield return null;
            Assert.AreEqual(string.Empty, inventory.EquippedItemId(EquipSlots.Back), "clicking the back slot did not take the jetpack off");
            Assert.IsTrue(inventory.HasItem(ItemRegistry.Jetpack), "the jetpack should be back in storage");
            Assert.AreEqual("--", labels[backIndex].text, "the back slot still shows the jetpack");
            Object.DestroyImmediate(jetpackPrefab);
        }
    }
}
