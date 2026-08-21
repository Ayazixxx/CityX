using System.Collections;
using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Health and stamina bars, a level badge, a circular character
    /// portrait, and the death screen. Ported from ui/hud.gd.
    ///
    /// POLLS THE CONTINUOUS VALUES, LISTENS FOR THE DISCRETE ONES. Health and
    /// stamina change every single frame while sprinting or regenerating, so an
    /// event per change would be an event per frame carrying a number the HUD
    /// could simply have read. Dying and respawning happen once and have to be
    /// caught exactly when they happen, so those are real events. The Godot
    /// original drew the same line for the same reason.
    ///
    /// THE PORTRAIT IS A ONE-TIME SNAPSHOT, NOT A LIVE FEED -- matching
    /// hud.gd's own reasoning exactly: a portrait that kept following the
    /// character's current facing would visibly swing around every time the
    /// player turned, reading as the portrait itself "moving" rather than a
    /// still headshot. See CapturePortrait.
    ///
    /// THE AMMO COUNTER READS PlayerInputReader.EquippedGun, NOT A CACHED
    /// REFERENCE -- a gun can be equipped, dropped and re-equipped over a
    /// HUD's own lifetime, and polling fresh every frame (same "target can
    /// be reassigned" reasoning Bind's own caller already applies) means
    /// this never has to be told when that happens.
    /// </summary>
    public sealed class PlayerHud : MonoBehaviour
    {
        // --- thresholds and colours, ported from hud.gd ---

        public const float HealthYellowFraction = 0.75f;
        public const float HealthRedFraction = 0.25f;

        public static readonly Color HealthGreen = new Color(0.2f, 0.85f, 0.2f);
        public static readonly Color HealthYellow = new Color(0.95f, 0.85f, 0.15f);
        public static readonly Color HealthRed = new Color(0.9f, 0.15f, 0.15f);

        /// <summary>
        /// In front of the character's own face, close and level -- a
        /// head-and-a-little-shoulder crop, not a full-body shot and not
        /// tilted up/down. hud.gd's own PORTRAIT_CAMERA_DISTANCE was
        /// carried straight over first, on the theory that a near-identical
        /// head-sphere size meant the same distance would frame the same
        /// crop -- reported too tight a crop live in-game, so pulled back
        /// further than that carried-over number.
        /// </summary>
        public const float PortraitCameraDistance = 1.75f;

        /// <summary>A flat, darker shade of the gold ring around the portrait -- hud.gd's own PORTRAIT_BG_COLOR.</summary>
        public static readonly Color PortraitBackgroundColour = new Color(0.26f, 0.21f, 0.07f);

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

        [Tooltip("Displays the baked RenderTexture PortraitCamera writes once, then never again.")]
        public RawImage PortraitImage;

        [Tooltip("Hidden unless a gun (see PlayerInputReader.EquippedGun) is actually equipped.")]
        public Text AmmoLabel;

        [Tooltip("A scene camera, cullingMask-restricted to the player's own dedicated portrait layer (see PlayerHudBuilder.PortraitLayerName), that never renders anywhere else.")]
        public Camera PortraitCamera;

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
            StartCoroutine(CapturePortrait());
        }

        private void OnDisable()
        {
            Bind(null);
            HookButton(null);
        }

        /// <summary>
        /// Bakes one still image of the character into PortraitCamera's own
        /// RenderTexture, then switches the camera off for good -- see this
        /// class's own doc comment for why a live feed is wrong here.
        ///
        /// WAITS TWO FRAMES FIRST, before ever looking for Target -- matches
        /// hud.gd's own _capture_portrait() exactly, and for the same
        /// ordering reason: AddToScene sets Target on this component right
        /// after Instantiate, which runs Awake/OnEnable (and so starts this
        /// coroutine) before that assignment happens. Coroutines only begin
        /// running after the calling method returns, so by the time this
        /// resumes past the first yield the field is already set -- but the
        /// wait, and the null guard after it, cost nothing and mean this
        /// never depends on that ordering staying exactly as it is today.
        /// </summary>
        private IEnumerator CapturePortrait()
        {
            yield return null;
            yield return null;

            if (Target == null || PortraitCamera == null || PortraitImage == null)
            {
                yield break;
            }

            Renderer headRenderer = FindHeadRenderer(Target.transform);
            if (headRenderer == null)
            {
                yield break;
            }

            Vector3 headPos = headRenderer.bounds.center;
            Vector3 facing = Target.transform.forward;
            PortraitCamera.transform.position = headPos + facing * PortraitCameraDistance;
            PortraitCamera.transform.LookAt(headPos, Vector3.up);

            PortraitCamera.Render();
            // The whole GameObject, not just the camera -- PlayerHudBuilder
            // hangs a dedicated light off this same object for the shot, and
            // leaving that behind still-enabled would keep it lit and
            // costing render time near the player forever, for a texture
            // nothing ever writes to again.
            PortraitCamera.gameObject.SetActive(false);
        }

        /// <summary>
        /// Ported from character.gd's get_head_center_world(), but reading
        /// the actual visual mesh's own world bounds rather than a bone
        /// offset -- FusedBodyBuilder.AttachHead parents the head mesh onto
        /// the head bone once and lets it ride along, so the mesh's own
        /// current bounds ARE its current world position with nothing else
        /// to derive.
        /// </summary>
        private static Renderer FindHeadRenderer(Transform root)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name == "Head")
                {
                    return renderer;
                }
            }
            return null;
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

            if (AmmoLabel != null)
            {
                IEquippableGun gun = _bound.GetComponent<PlayerInputReader>()?.EquippedGun;
                AmmoLabel.gameObject.SetActive(gun != null);
                if (gun != null)
                {
                    AmmoLabel.text = gun.GetAmmoText();
                }
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
