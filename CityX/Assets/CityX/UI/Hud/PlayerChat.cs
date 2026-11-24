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
    /// the pause menu, the map (Escape/M), the inventory (I) and any open
    /// DialogueBox -- see PauseMenuView's own new Chat guard, added
    /// alongside this file, for the matching half of this same problem.
    /// DialogueBox is read through its static AnyOpen rather than an
    /// instance field like the others: there is one DialogueBox per NPC
    /// prefab, not a single scene singleton, so nothing here can hold one
    /// reference and ask it directly.
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

        [Tooltip("/sleep goes straight limp through this, bypassing PlayerVitals entirely -- there is no death, no HP change, just Ragdoll.Activate.")]
        public Ragdoll Ragdoll;

        [Tooltip("Enter is ignored while the C-menu is open -- see MapPanelView's own Menu field for why.")]
        public PlayerMenuView Menu;

        private readonly MenuFocus _focus = new MenuFocus();

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
                (Menu != null && Menu.IsOpen) || DialogueBox.AnyOpen)
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
            else if (Input != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame))
            {
                _recalled = keyboard.upArrowKey.wasPressedThisFrame ? History.Older(Input.text) : History.Newer(Input.text);
                _recall = true;
            }
        }

        /// <summary>What was said before, walked with Up and Down: the chat's own, apart from the console's (DevConsoleView.History).</summary>
        public readonly InputHistory History = new InputHistory();

        private bool _recall;
        private string _recalled;

        /// <summary>A line brought back with the arrows goes in after the text box has handled the same key (which moves the caret).</summary>
        private void LateUpdate()
        {
            if (_recall)
            {
                _recall = false;
                UiKit.SetTextCaretAtEnd(Input, _recalled);
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }
            _focus.Take(this, InputReader, OrbitCamera);

            if (Input != null)
            {
                Input.text = string.Empty;
            }
            History.EndWalk();
            if (Panel != null) Panel.SetActive(true);
            if (Input != null) Input.ActivateInputField();
        }

        public void Close()
        {
            if (Panel != null) Panel.SetActive(false);
            if (Input != null) Input.DeactivateInputField();

            _focus.GiveBack(this, InputReader, OrbitCamera);
        }

        public void Submit(string text)
        {
            Close();
            string trimmed = text != null ? text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }
            History.Add(trimmed);
            Dispatch(trimmed);
        }

        /// <summary>Case/whitespace-insensitive match for the claim command, pulled out so the normalization rule is directly testable.</summary>
        public static bool IsClaimCommand(string text) =>
            text != null && text.Trim().ToLowerInvariant() == "/claim";

        /// <summary>Same normalization rule as IsClaimCommand, for /dance.</summary>
        public static bool IsDanceCommand(string text) =>
            text != null && text.Trim().ToLowerInvariant() == "/dance";

        /// <summary>Same normalization rule as IsClaimCommand, for /breakdance.</summary>
        public static bool IsBreakdanceCommand(string text) =>
            text != null && text.Trim().ToLowerInvariant() == "/breakdance";

        /// <summary>Same normalization rule as IsClaimCommand, for /pushups (and /pushup).</summary>
        public static bool IsPushUpsCommand(string text)
        {
            string command = text?.Trim().ToLowerInvariant();
            return command == "/pushups" || command == "/pushup";
        }

        /// <summary>
        /// /hoodon and /hoodoff (owner, 2026-11-23: "if you type /hoodon in the text chat not the console command chat, then it turns into the hood
        /// on version"); /hoodup and /hooddown too (owner, 2026-11-24: "or /hoodup"): true for any, <paramref name="up"/> which. The same rule as
        /// IsClaimCommand.
        /// </summary>
        public static bool IsHoodCommand(string text, out bool up)
        {
            string command = text?.Trim().ToLowerInvariant();
            up = command == "/hoodon" || command == "/hoodup";
            return up || command == "/hoodoff" || command == "/hooddown";
        }

        /// <summary>
        /// Same normalization rule as IsClaimCommand, for /sleep -- a diagnostic,
        /// not a real feature: Requested, "make it so if you press enter and
        /// open text chat and type '/sleep' the character just goes limp and
        /// ragdolls immediately no animation... lets see if its something do it
        /// with it being playable." The grapple-swing distortion investigation
        /// had proven Ragdoll.cs stable on a standalone, non-playable dummy;
        /// this checks the same physics on the actual live player rig instead.
        /// </summary>
        public static bool IsSleepCommand(string text) =>
            text != null && text.Trim().ToLowerInvariant() == "/sleep";

        private void Dispatch(string text)
        {
            if (IsClaimCommand(text))
            {
                Claim?.TryClaim();
                return;
            }
            if (IsDanceCommand(text))
            {
                InputReader?.StartDance();
                return;
            }
            if (IsBreakdanceCommand(text))
            {
                InputReader?.StartBreakdance();
                return;
            }
            if (IsPushUpsCommand(text))
            {
                InputReader?.StartPushUps();
                return;
            }
            if (IsHoodCommand(text, out bool hoodUp))
            {
                if (InputReader != null && InputReader.TryGetComponent(out HoodSync hood))
                {
                    hood.SetHood(hoodUp);
                }
                return;
            }
            if (IsSleepCommand(text))
            {
                // Requested: "make it so i can press space to get up at
                // any time after /sleep is used" -- see Ragdoll.CanStandUp's
                // own doc comment for why /sleep specifically skips the
                // ordinary rest timer real knockdowns use.
                Ragdoll?.Activate(Vector3.zero, allowImmediateStandUp: true);
                return;
            }
            Bubble?.Show(text);
        }
    }
}
