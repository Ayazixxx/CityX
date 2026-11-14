using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The C-menu: cuts the camera to a front-on shot of the player and
    /// shows a 4-icon rail -- Quests, Player, Social, Map.
    ///
    /// Quests/Social/Map/Skills/Loadout/Stats/Items ALL CLOSE THIS MENU AND
    /// OPEN THEIR OWN INDEPENDENT OVERLAY, the same way every other panel in
    /// this project already works (MapPanelView, InventoryPanelView) rather
    /// than nesting inside this one -- the only exception is Player, whose
    /// 5-button submenu is a genuine fly-out that stays inside this menu's
    /// own session (see PlayerSubmenuView), matching the two reference
    /// mockups: one shows just the rail, the other shows the rail with a
    /// fly-out beside it.
    ///
    /// SAME GATE/OPEN/CLOSE SHAPE AS EVERY OTHER PANEL -- see MapPanelView's
    /// own doc comment on why this only disables PlayerInputReader and
    /// ThirdPersonCamera rather than pausing time, and InventoryPanelView's
    /// on why every listener is wired here in Awake rather than by the
    /// editor-time builder.
    /// </summary>
    public sealed class PlayerMenuView : MonoBehaviour
    {
        [Tooltip("The whole overlay, hidden until C is pressed.")]
        public GameObject Root;

        public Button QuestsButton;
        public Button PlayerButton;
        public Button SocialButton;
        public Button MapButton;

        [Tooltip("The Player button's own 5-item fly-out, built alongside this and hidden until Player is clicked.")]
        public PlayerSubmenuView Submenu;

        [Tooltip("Positions the shared camera in front of the player while this menu is open.")]
        public PlayerMenuCamera MenuCamera;

        public Transform CameraTransform;
        public Transform Player;

        public PlayerInputReader InputReader;
        public ThirdPersonCamera OrbitCamera;

        [Tooltip("C is ignored while any of these are open.")]
        public DevConsoleView Console;
        public PauseMenuView Pause;
        public MapPanelView Map;
        public InventoryPanelView Inventory;
        public PlayerChat Chat;

        [Tooltip("Opened (after this menu closes) by the Quests and Social buttons.")]
        public PlaceholderPanelView Placeholder;

        private readonly MenuFocus _focus = new MenuFocus();

        public bool IsOpen => Root != null && Root.activeSelf;

        private void Awake()
        {
            if (QuestsButton != null) QuestsButton.onClick.AddListener(OnQuestsClicked);
            if (PlayerButton != null) PlayerButton.onClick.AddListener(OnPlayerClicked);
            if (SocialButton != null) SocialButton.onClick.AddListener(OnSocialClicked);
            if (MapButton != null) MapButton.onClick.AddListener(OnMapClicked);
            // Every icon drawn now, as the city loads, the fly-out's hidden ones too: drawn on the first press of C, they held the frame.
            foreach (MenuIconImage icon in GetComponentsInChildren<MenuIconImage>(true))
            {
                icon.Draw();
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
                (Map != null && Map.IsOpen) || (Inventory != null && Inventory.IsOpen) ||
                (Chat != null && Chat.IsOpen) || SkillMenuView.IsAnyOpen || SkillMenuView.ClosedOnFrame == Time.frameCount ||
                GraffitiMenuView.IsAnyOpen || GraffitiMenuView.ClosedOnFrame == Time.frameCount)
            {
                // (The skill menu closes on C too: the press that closed it does not also open this.)
                return;
            }

            if (keyboard.cKey.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
                return;
            }

            if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
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
            _focus.Take(this, InputReader, OrbitCamera);
            if (MenuCamera != null && CameraTransform != null && Player != null)
            {
                MenuCamera.Begin(CameraTransform, Player);
            }

            Submenu?.Hide();
            if (Root != null) Root.SetActive(true);
        }

        public void Close()
        {
            if (Root != null) Root.SetActive(false);
            Submenu?.Hide();

            // Before re-enabling OrbitCamera, not after -- otherwise a menu
            // closed mid-ease leaves this component and ThirdPersonCamera
            // both writing to the camera transform on the very same frame.
            MenuCamera?.Stop();
            _focus.GiveBack(this, InputReader, OrbitCamera);
        }

        private void OnQuestsClicked()
        {
            Close();
            Placeholder?.Show("QUESTS", "Coming soon.");
        }

        private void OnSocialClicked()
        {
            Close();
            Placeholder?.Show("SOCIAL", "Coming soon.");
        }

        private void OnPlayerClicked()
        {
            if (Submenu == null)
            {
                return;
            }
            if (Submenu.IsVisible)
            {
                Submenu.Hide();
            }
            else
            {
                Submenu.Show();
            }
        }

        private void OnMapClicked()
        {
            Close();
            Map?.Open();
        }
    }
}
