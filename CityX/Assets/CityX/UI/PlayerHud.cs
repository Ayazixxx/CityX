using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Health and stamina bars, a level badge, and the death screen. Ported
    /// from ui/hud.gd.
    ///
    /// POLLS THE CONTINUOUS VALUES, LISTENS FOR THE DISCRETE ONES. Health and
    /// stamina change every single frame while sprinting or regenerating, so an
    /// event per change would be an event per frame carrying a number the HUD
    /// could simply have read. Dying and respawning happen once and have to be
    /// caught exactly when they happen, so those are real events. The Godot
    /// original drew the same line for the same reason.
    ///
    /// Deliberately not ported yet: the character portrait and the ammo
    /// counter. The portrait needs a head bone, a per-character render layer
    /// and a paint job, none of which exist here yet, and there are no guns.
    /// An empty frame in the corner would look like a bug rather than an
    /// absence.
    /// </summary>
    public sealed class PlayerHud : MonoBehaviour
    {
        // --- thresholds and colours, ported from hud.gd ---

        public const float HealthYellowFraction = 0.75f;
        public const float HealthRedFraction = 0.25f;

        public static readonly Color HealthGreen = new Color(0.2f, 0.85f, 0.2f);
        public static readonly Color HealthYellow = new Color(0.95f, 0.85f, 0.15f);
        public static readonly Color HealthRed = new Color(0.9f, 0.15f, 0.15f);

        [Tooltip("Whose vitals are shown. Found on the parent hierarchy if left empty.")]
        public PlayerVitals Target;

        [Header("Wired by PlayerHudBuilder")]
        public RectTransform HealthFill;

        public RectTransform StaminaFill;
        public Image HealthFillImage;
        public Text LevelLabel;
        public GameObject Crosshair;
        public GameObject DeathScreen;
        public Button RespawnButton;

        private PlayerVitals _bound;
        private Button _hookedButton;
        private int _shownLevel = -1;

        private void Awake()
        {
            if (Target == null)
            {
                Target = GetComponentInParent<PlayerVitals>();
            }
        }

        private void OnEnable()
        {
            Bind(Target);
            HookButton(RespawnButton);
            ShowDead(_bound != null && _bound.IsDead);
        }

        private void OnDisable()
        {
            Bind(null);
            HookButton(null);
        }

        /// <summary>
        /// Attaches the click handler to whichever button is currently set.
        ///
        /// IDEMPOTENT AND RE-CHECKED EVERY FRAME, rather than hooked once in
        /// OnEnable. Adding a component and then filling in its fields is the
        /// normal way to build one of these in code, and OnEnable runs at
        /// AddComponent -- before the field holds anything. The result was a
        /// respawn button that rendered, highlighted, depressed on click and
        /// did nothing at all, with no error anywhere.
        /// </summary>
        private void HookButton(Button button)
        {
            if (_hookedButton == button)
            {
                return;
            }

            if (_hookedButton != null)
            {
                _hookedButton.onClick.RemoveListener(OnRespawnPressed);
            }

            _hookedButton = button;

            if (_hookedButton != null)
            {
                _hookedButton.onClick.AddListener(OnRespawnPressed);
            }
        }

        /// <summary>
        /// Points the HUD at a different body, unsubscribing from the old one.
        ///
        /// Exists because the alternative -- subscribing in OnEnable against
        /// whatever Target happened to be then -- leaks a handler the moment
        /// anything reassigns Target, and a HUD listening to a dead player's
        /// Died event is the sort of thing that only shows up much later as a
        /// death screen that will not go away.
        /// </summary>
        public void Bind(PlayerVitals vitals)
        {
            if (_bound == vitals)
            {
                return;
            }

            if (_bound != null)
            {
                _bound.Died -= OnDied;
                _bound.Revived -= OnRevived;
            }

            _bound = vitals;
            Target = vitals;

            if (_bound != null)
            {
                _bound.Died += OnDied;
                _bound.Revived += OnRevived;
            }
        }

        private void Update()
        {
            // Target can be reassigned from the inspector or by whatever spawns
            // the player, so keep the subscription following it.
            if (Target != _bound)
            {
                Bind(Target);
            }
            HookButton(RespawnButton);

            if (_bound == null)
            {
                return;
            }

            SetFill(HealthFill, Fraction(_bound.Health, _bound.MaxHealth));
            SetFill(StaminaFill, Fraction(_bound.Stamina, _bound.MaxStamina));

            if (HealthFillImage != null)
            {
                HealthFillImage.color = ColourForHealth(Fraction(_bound.Health, _bound.MaxHealth));
            }

            // Rebuilding a string every frame to say "1" is wasteful, and text
            // assignment dirties the whole canvas even when the value is
            // unchanged.
            if (LevelLabel != null && _bound.Level != _shownLevel)
            {
                _shownLevel = _bound.Level;
                LevelLabel.text = _shownLevel.ToString();
            }
        }

        /// <summary>
        /// The colour the health bar takes at a given fraction of full. Static
        /// and pure so the thresholds can be checked without a canvas.
        /// </summary>
        public static Color ColourForHealth(float fraction)
        {
            if (fraction <= HealthRedFraction)
            {
                return HealthRed;
            }
            return fraction <= HealthYellowFraction ? HealthYellow : HealthGreen;
        }

        private static float Fraction(float value, float max)
        {
            return max <= 0.0f ? 0.0f : Mathf.Clamp01(value / max);
        }

        /// <summary>
        /// Scales a bar by moving its right anchor rather than by setting a
        /// width. Anchors are a fraction of the parent, so the bar stays
        /// correct when the canvas scaler resizes everything for a different
        /// resolution -- a width in pixels would not.
        /// </summary>
        private static void SetFill(RectTransform fill, float fraction)
        {
            if (fill == null)
            {
                return;
            }
            fill.anchorMin = new Vector2(0.0f, 0.0f);
            fill.anchorMax = new Vector2(fraction, 1.0f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        private void OnDied()
        {
            ShowDead(true);
        }

        private void OnRevived()
        {
            ShowDead(false);
        }

        /// <summary>
        /// The crosshair goes with the death screen. A corpse is not aiming at
        /// anything, and left up it lands between YOU DIED and the respawn
        /// button looking like a rendering fault rather than a reticle.
        /// </summary>
        private void ShowDead(bool dead)
        {
            if (DeathScreen != null)
            {
                DeathScreen.SetActive(dead);
            }
            if (Crosshair != null)
            {
                Crosshair.SetActive(!dead);
            }
        }

        private void OnRespawnPressed()
        {
            // Restores the body where it stands. The Godot version also moved
            // the player back to a spawn point, which needs a spawn point to
            // exist -- there is not one here yet.
            if (_bound != null)
            {
                _bound.Revive();
            }
        }
    }
}
