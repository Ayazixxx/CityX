using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The 8-slot equip panel reached from the Player submenu's Loadout
    /// button -- Helmet, Torso, Legs, Shoes, Gloves, Back, Primary Weapon,
    /// Secondary Weapon. Uses the same Inventory.EquippedItemId/
    /// UnequipToSlot mechanism InventoryPanelView's own equip row already
    /// uses, with a wider slot vocabulary of its own.
    ///
    /// EQUIP SLOTS ONLY, NO STORAGE GRID -- unlike InventoryPanelView, this
    /// panel has nowhere to click a plain item into a slot from. Equipping
    /// happens by clicking a storage item in Items (each item's own
    /// registered EquipSlot decides which of these 8 slots it lands in);
    /// this panel is where you see and unequip what is already worn --
    /// exactly the "click a filled slot to send it back to storage" half
    /// InventoryPanelView.OnEquipSlotClicked already implements, just
    /// against a different, longer slot list.
    ///
    /// SlotKeys[1] IS "top" AND SlotKeys[6]/[7] ARE "hand_r"/"hand_l" --
    /// the same underlying Inventory slot keys InventoryPanelView's own
    /// EquipSlotKeys already use, so items already registered against them
    /// (HoodedCloak -> top, SkelliJeans -> legs, GlassBottle/GrapplingHook
    /// -> hand_r) show up here unchanged, just under the friendlier label
    /// "Torso"/"Primary Weapon". The keys are EquipSlots', the same ones the
    /// inventory's own equipped row uses since 2026-10-15; of them only
    /// Shoes and Gloves have nothing registered against them yet.
    ///
    /// NO DEDICATED HOTKEY -- reached only through PlayerSubmenuView's
    /// Loadout button, which has already closed the C-menu by the time
    /// Open() runs. Same shape as PlaceholderPanelView: only watches for
    /// Escape while open, no gate list of its own to check on the way in.
    /// </summary>
    public sealed class LoadoutPanelView : MonoBehaviour
    {
        public static readonly string[] SlotKeys =
        {
            EquipSlots.Helmet, EquipSlots.Torso, EquipSlots.Legs, EquipSlots.Shoes, EquipSlots.Gloves, EquipSlots.Back,
            EquipSlots.Primary, EquipSlots.Secondary,
        };

        public static readonly string[] SlotLabelText =
        {
            "HELMET", "TORSO", "LEGS", "SHOES", "GLOVES", "BACK", "PRIMARY WEAPON", "SECONDARY WEAPON",
        };

        [Tooltip("The whole overlay, hidden until Open() is called.")]
        public GameObject Root;

        [Tooltip("The body this panel reads and unequips items from.")]
        public Inventory Inventory;

        [Tooltip("Inventory.UnequipToSlot takes the player GameObject itself, not the Inventory component's own.")]
        public GameObject Player;

        [Tooltip("One button+label per SlotKeys entry, same order.")]
        public Button[] SlotButtons = new Button[0];

        public Text[] SlotLabels = new Text[0];

        public Button CloseButton;

        [Tooltip("Movement/combat input. Disabled while the panel is open.")]
        public PlayerInputReader InputReader;

        [Tooltip("Mouse-look. Disabled while the panel is open.")]
        public ThirdPersonCamera OrbitCamera;

        private bool _wasCursorCaptured;

        public bool IsOpen => Root != null && Root.activeSelf;

        private void Awake()
        {
            for (int i = 0; i < SlotButtons.Length && i < SlotKeys.Length; i++)
            {
                string slotKey = SlotKeys[i];
                if (SlotButtons[i] != null)
                {
                    SlotButtons[i].onClick.AddListener(() => OnSlotClicked(slotKey));
                }
            }
            if (CloseButton != null) CloseButton.onClick.AddListener(Close);
            Close();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
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

        private void OnSlotClicked(string slotKey)
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
            for (int i = 0; i < SlotLabels.Length && i < SlotKeys.Length; i++)
            {
                if (SlotLabels[i] == null)
                {
                    continue;
                }
                string itemId = Inventory.EquippedItemId(SlotKeys[i]);
                SlotLabels[i].text = string.IsNullOrEmpty(itemId) ? "--" : DisplayNameFor(itemId);
            }
        }

        private static string DisplayNameFor(string itemId) =>
            ItemRegistry.TryGet(itemId, out ItemDefinition definition) ? definition.DisplayName : itemId;
    }
}
