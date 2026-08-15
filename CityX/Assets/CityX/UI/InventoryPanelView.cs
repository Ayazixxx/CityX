using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The storage grid and equipment slots, toggled with I. Ported from
    /// ui/inventory_panel.gd's own Inventory tab -- the Stats and Skills
    /// tabs it also carries are not: nothing in this port tracks level/
    /// stats or a skill tree yet (see CharacterIdentity, which has neither),
    /// so those tabs would be UI for systems that don't exist. Skills was
    /// already just a "Coming soon" placeholder even in the Godot original.
    ///
    /// CLICK-TO-EQUIP ONLY, NOT DRAG-AND-DROP. Godot's item_slot.gd supports
    /// both; drag-and-drop needs its own IBeginDragHandler/IDropHandler pair
    /// and a drag-ghost visual, real scope on top of the storage/equipment
    /// wiring this pass is actually about. Click already reaches every
    /// state a slot can be in (empty, holding a plain item, equipped) --
    /// see Refresh.
    ///
    /// NO ICONS. items/item_icons.gd renders a live photograph of each
    /// item's own 3D scene into a cached texture; nothing in this port has
    /// that render-to-texture pipeline, and ItemDefinition carries no
    /// sprite/texture field to point one at yet. Slots fall back to text --
    /// exactly the path item_slot.gd itself takes when its own icon cache
    /// comes back empty (its comment names a headless run; here it is
    /// simply the normal path) -- so nothing here is left silently broken,
    /// just plainer than Godot's until an icon pipeline exists to build on.
    ///
    /// POLLED, NOT EVENT-DRIVEN. Inventory raises no change notification of
    /// any kind (see its own file) -- neither does player/inventory.gd,
    /// which inventory_panel.gd's own _refresh() reads by direct dictionary
    /// indexing exactly like this does. Refresh runs once on Open and again
    /// after every click this view itself causes, which is every way the
    /// data can change while a single player is looking at their own panel.
    ///
    /// ALL LISTENERS WIRED IN AWAKE -- see PauseMenuView's doc comment for
    /// why an editor-time AddListener never survives the scene being saved.
    /// </summary>
    public sealed class InventoryPanelView : MonoBehaviour
    {
        /// <summary>
        /// player/inventory.gd's own equipment keys, in the fixed order the
        /// row of equipment slots is drawn in. Only "top", "legs" and
        /// "hand_r" have anything registered to fill them today (see
        /// ItemRegistry) -- the rest sit empty and ready for later items,
        /// the same forward-compatible reasoning the weapons/wearables plan
        /// used for keeping the full slot set rather than only what is
        /// currently reachable.
        /// </summary>
        public static readonly string[] EquipSlotKeys =
        {
            "hat", "mask", "top", "hands", "legs", "feet", "hand_l", "hand_r",
        };

        [Tooltip("The whole overlay, hidden until I is pressed.")]
        public GameObject Root;

        [Tooltip("The body this panel reads and equips items onto.")]
        public Inventory Inventory;

        [Tooltip("Inventory.EquipItemId/UnequipToSlot take the player GameObject itself, not the Inventory component's own.")]
        public GameObject Player;

        [Tooltip("One button+label per storage slot, StorageSlotCount long.")]
        public Button[] StorageButtons = new Button[0];

        public Text[] StorageLabels = new Text[0];

        [Tooltip("One button+label per EquipSlotKeys entry, same order.")]
        public Button[] EquipButtons = new Button[0];

        public Text[] EquipLabels = new Text[0];

        [Tooltip("Movement/combat input. Disabled while the panel is open.")]
        public PlayerInputReader InputReader;

        [Tooltip("Mouse-look. Disabled while the panel is open.")]
        public ThirdPersonCamera OrbitCamera;

        [Tooltip("I is ignored while the console is open -- typing a command should not also open the inventory.")]
        public DevConsoleView Console;

        [Tooltip("I is ignored while the game is paused.")]
        public PauseMenuView Pause;

        [Tooltip("I is ignored while the map is open -- both cover the same part of the screen.")]
        public MapPanelView Map;

        private bool _wasCursorCaptured;

        public bool IsOpen => Root != null && Root.activeSelf;

        private void Awake()
        {
            for (int i = 0; i < StorageButtons.Length; i++)
            {
                int index = i;   // captured per-iteration, not the loop variable itself
                if (StorageButtons[i] != null)
                {
                    StorageButtons[i].onClick.AddListener(() => OnStorageClicked(index));
                }
            }
            for (int i = 0; i < EquipButtons.Length && i < EquipSlotKeys.Length; i++)
            {
                string slotKey = EquipSlotKeys[i];
                if (EquipButtons[i] != null)
                {
                    EquipButtons[i].onClick.AddListener(() => OnEquipSlotClicked(slotKey));
                }
            }
            Close();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if ((Console != null && Console.IsOpen) || (Pause != null && Pause.IsOpen) ||
                (Map != null && Map.IsOpen))
            {
                return;
            }

            if (keyboard.iKey.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }
            _wasCursorCaptured = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (InputReader != null) InputReader.enabled = false;
            if (OrbitCamera != null) OrbitCamera.enabled = false;

            if (Root != null) Root.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (Root != null) Root.SetActive(false);

            if (InputReader != null) InputReader.enabled = true;
            if (OrbitCamera != null) OrbitCamera.enabled = true;

            if (_wasCursorCaptured)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Equips the clicked storage item, exactly the RemoveFromStorage-
        /// then-EquipItemId composition DevConsoleView.Equip already uses --
        /// Inventory has no single "equip from this slot" call of its own
        /// (unlike Godot's equip_from_slot(index)), so this is that same
        /// two-step with the same rollback if the second step fails.
        /// Non-equippable items (no EquipSlot registered) are left alone --
        /// clicking a slot that can't go anywhere does nothing rather than
        /// erroring, since a bare storage-only item is a real, unremarkable
        /// state, not a broken one.
        /// </summary>
        private void OnStorageClicked(int index)
        {
            if (Inventory == null || Player == null || index >= Inventory.Storage.Length)
            {
                return;
            }

            Inventory.StorageSlot slot = Inventory.Storage[index];
            if (slot.IsEmpty)
            {
                return;
            }
            if (!ItemRegistry.TryGet(slot.ItemId, out ItemDefinition definition) ||
                string.IsNullOrEmpty(definition.EquipSlot))
            {
                return;
            }

            if (!Inventory.RemoveFromStorage(slot.ItemId, 1, out ItemInstanceData instanceData))
            {
                return;
            }
            if (!Inventory.EquipItemId(slot.ItemId, Player, instanceData))
            {
                // Put it back rather than leaving it removed from storage
                // and nowhere else -- see DevConsoleView.Equip's own copy
                // of this same rollback.
                Inventory.AddItem(slot.ItemId, 1, instanceData);
            }
            Refresh();
        }

        private void OnEquipSlotClicked(string slotKey)
        {
            if (Inventory == null || Player == null)
            {
                return;
            }
            if (string.IsNullOrEmpty(Inventory.EquippedItemId(slotKey)))
            {
                return;
            }
            Inventory.UnequipToSlot(slotKey, Player);
            Refresh();
        }

        private void Refresh()
        {
            if (Inventory == null)
            {
                return;
            }

            for (int i = 0; i < StorageLabels.Length && i < Inventory.Storage.Length; i++)
            {
                if (StorageLabels[i] == null)
                {
                    continue;
                }
                Inventory.StorageSlot slot = Inventory.Storage[i];
                StorageLabels[i].text = slot.IsEmpty
                    ? string.Empty
                    : SlotText(slot.ItemId, slot.Count);
            }

            for (int i = 0; i < EquipLabels.Length && i < EquipSlotKeys.Length; i++)
            {
                if (EquipLabels[i] == null)
                {
                    continue;
                }
                string itemId = Inventory.EquippedItemId(EquipSlotKeys[i]);
                EquipLabels[i].text = string.IsNullOrEmpty(itemId) ? "--" : DisplayNameFor(itemId);
            }
        }

        private static string SlotText(string itemId, int count)
        {
            string name = DisplayNameFor(itemId);
            return count > 1 ? name + " x" + count : name;
        }

        private static string DisplayNameFor(string itemId) =>
            ItemRegistry.TryGet(itemId, out ItemDefinition definition) ? definition.DisplayName : itemId;
    }
}
