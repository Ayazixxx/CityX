using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The stat-points panel reached from the Player submenu's Stats
    /// button -- Level, an XP bar, how many points are left to spend, and
    /// a "+" row each for Vitality/Endurance/Strength. Reads/writes
    /// PlayerVitals directly (AddXp/SpendStatPoint, ported from player.gd
    /// there -- see that file's own doc comments) rather than owning any
    /// progression state of its own.
    ///
    /// NO DEDICATED HOTKEY -- reached only through PlayerSubmenuView's
    /// Stats button, which has already closed the C-menu by the time
    /// Open() runs. Same shape as LoadoutPanelView: only watches for
    /// Escape while open.
    ///
    /// POLLED, NOT EVENT-DRIVEN -- PlayerVitals raises no change
    /// notification for Level/Experience/StatPoints (same reasoning
    /// InventoryPanelView's own doc comment gives for Inventory). Refresh
    /// runs once on Open and again after every "+" click this view itself
    /// causes, which is every way the data can change while a single
    /// player is looking at their own panel.
    /// </summary>
    public sealed class StatsPanelView : MonoBehaviour
    {
        [Tooltip("The whole overlay, hidden until Open() is called.")]
        public GameObject Root;

        public PlayerVitals Vitals;

        public Text LevelLabel;
        public Text ExperienceLabel;

        [Tooltip("Scaled by moving its right anchor, the same technique PlayerHud's own health/stamina bars use.")]
        public RectTransform ExperienceBarFill;

        public Text PointsLabel;

        public Text VitalityLabel;
        public Text EnduranceLabel;
        public Text StrengthLabel;

        public Button VitalityButton;
        public Button EnduranceButton;
        public Button StrengthButton;

        public Button CloseButton;

        [Tooltip("Movement/combat input. Disabled while the panel is open.")]
        public PlayerInputReader InputReader;

        [Tooltip("Mouse-look. Disabled while the panel is open.")]
        public ThirdPersonCamera OrbitCamera;

        private bool _wasCursorCaptured;

        public bool IsOpen => Root != null && Root.activeSelf;

        private void Awake()
        {
            if (VitalityButton != null) VitalityButton.onClick.AddListener(() => OnSpendClicked("vitality"));
            if (EnduranceButton != null) EnduranceButton.onClick.AddListener(() => OnSpendClicked("endurance"));
            if (StrengthButton != null) StrengthButton.onClick.AddListener(() => OnSpendClicked("strength"));
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

        private void OnSpendClicked(string statName)
        {
            if (Vitals == null)
            {
                return;
            }
            Vitals.SpendStatPoint(statName);
            Refresh();
        }

        private void Refresh()
        {
            if (Vitals == null)
            {
                return;
            }

            if (LevelLabel != null) LevelLabel.text = "LEVEL " + Vitals.Level;
            if (ExperienceLabel != null)
            {
                ExperienceLabel.text = Vitals.Experience + " / " + Vitals.ExperienceToNextLevel + " XP";
            }
            if (ExperienceBarFill != null)
            {
                float toNext = Vitals.ExperienceToNextLevel;
                float fraction = toNext > 0.0f ? Mathf.Clamp01((float)Vitals.Experience / toNext) : 0.0f;
                // Moves the bar's right anchor rather than setting a pixel
                // width, the same technique PlayerHud's own health/stamina
                // bars use -- an anchor fraction stays correct when the
                // canvas scaler resizes everything for a different
                // resolution, a width in pixels would not.
                ExperienceBarFill.anchorMin = new Vector2(0.0f, 0.0f);
                ExperienceBarFill.anchorMax = new Vector2(fraction, 1.0f);
                ExperienceBarFill.offsetMin = Vector2.zero;
                ExperienceBarFill.offsetMax = Vector2.zero;
            }
            if (PointsLabel != null)
            {
                PointsLabel.text = Vitals.StatPoints == 1
                    ? "1 point to spend"
                    : Vitals.StatPoints + " points to spend";
            }

            if (VitalityLabel != null) VitalityLabel.text = "VITALITY  " + Vitals.Vitality;
            if (EnduranceLabel != null) EnduranceLabel.text = "ENDURANCE  " + Vitals.Endurance;
            if (StrengthLabel != null) StrengthLabel.text = "STRENGTH  " + Vitals.Strength;

            bool canSpend = Vitals.StatPoints > 0;
            if (VitalityButton != null) VitalityButton.interactable = canSpend;
            if (EnduranceButton != null) EnduranceButton.interactable = canSpend;
            if (StrengthButton != null) StrengthButton.interactable = canSpend;
        }
    }
}
