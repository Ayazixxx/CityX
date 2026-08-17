using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Enter opens and sends, Escape cancels. Ported from ui/chat_box.gd.
    ///
    /// DOES NOT TOUCH Time.timeScale -- chat_box.gd is explicitly
    /// PROCESS_MODE_ALWAYS, staying usable mid-fight. PauseMenuView is the
    /// only overlay in this port that pauses; this one only takes the
    /// controls, the same technique MapPanelView already uses.
    ///
    /// READS Keyboard.current DIRECTLY IN Update(), NOT InputField.onEndEdit
    /// -- onEndEdit fires on Escape/blur too, which would conflate
    /// "cancelled" with "submitted." Matches DevConsoleView's own reasoning.
    ///
    /// GUARDED AGAINST EVERY OTHER OVERLAY THAT ALSO READS ENTER OR ESCAPE,
    /// OR COVERS THE SAME PART OF THE SCREEN: the dev console (backtick),
    /// the pause menu, the map (Escape/M) and the inventory (I) -- see
    /// PauseMenuView's own new Chat guard, added alongside this file, for
    /// the matching half of this same problem.
    ///
    /// ALL LISTENERS WIRED IN AWAKE -- see PauseMenuView's doc comment for
    /// why an editor-time AddListener never survives the scene being saved.
    /// </summary>
    public sealed class PlayerChat : MonoBehaviour
    {
        public const int MaxMessageLength = 120;

        [Tooltip("The whole overlay, hidden until Enter is pressed.")]
        public GameObject Panel;

        public InputField Input;
        public SpeechBubble Bubble;
        public ClaimSystem Claim;
        public ClaimBanner Banner;

        [Tooltip("Movement/combat input. Disabled while chat is open.")]
        public PlayerInputReader InputReader;

        [Tooltip("Mouse-look. Disabled while chat is open.")]
        public ThirdPersonCamera OrbitCamera;

        public DevConsoleView Console;
        public PauseMenuView Pause;
        public MapPanelView Map;
        public InventoryPanelView Inventory;

        [Tooltip("Enter is ignored while the C-menu is open -- see MapPanelView's own Menu field for why.")]
        public PlayerMenuView Menu;

        private bool _wasCursorCaptured;

        public bool IsOpen => Panel != null && Panel.activeSelf;

        private void Awake()
        {
            if (Input != null) Input.characterLimit = MaxMessageLength;
            if (Claim != null) Claim.BannerMessage += OnClaimBannerMessage;
            Close();
        }

        private void OnDestroy()
        {
            if (Claim != null) Claim.BannerMessage -= OnClaimBannerMessage;
        }

        private void OnClaimBannerMessage(string text)
        {
            Banner?.ShowMessage(text);
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
                (Menu != null && Menu.IsOpen))
            {
                return;
            }

            if (!IsOpen)
            {
                if (keyboard.enterKey.wasPressedThisFrame)
                {
                    Open();
                }
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
            if (keyboard.enterKey.wasPressedThisFrame)
            {
                Submit(Input != null ? Input.text : string.Empty);
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

            if (Input != null)
            {
                Input.text = string.Empty;
            }
            if (Panel != null) Panel.SetActive(true);
            if (Input != null) Input.ActivateInputField();
        }

        public void Close()
        {
            if (Panel != null) Panel.SetActive(false);
            if (Input != null) Input.DeactivateInputField();

            if (InputReader != null) InputReader.enabled = true;
            if (OrbitCamera != null) OrbitCamera.enabled = true;

            if (_wasCursorCaptured)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void Submit(string text)
        {
            Close();
            string trimmed = text != null ? text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }
            Dispatch(trimmed);
        }

        /// <summary>Case/whitespace-insensitive match for the claim command, pulled out so the normalization rule is directly testable.</summary>
        public static bool IsClaimCommand(string text) =>
            text != null && text.Trim().ToLowerInvariant() == "/claim";

        private void Dispatch(string text)
        {
            if (IsClaimCommand(text))
            {
                Claim?.TryClaim();
                return;
            }
            Bubble?.Show(text);
        }
    }
}
