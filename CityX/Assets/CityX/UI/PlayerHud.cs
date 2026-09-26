using System.Collections;
using System.Collections.Generic;
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

        // The portrait is framed from the character's own measurements (see Frame): the head, the hair or
        // hat above it, and the top of the chest, from the front and level. hud.gd's fixed camera
        // distance was carried over first and then pulled back once ("too tight a crop"), but a fixed
        // distance cannot follow a hairstyle or hat, and showed a bare head with no shoulders.

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
        private bool _wasCursorCaptured;

        private void Awake()
        {
            if (Target == null)
            {
                Target = GetComponentInParent<PlayerVitals>();
            }

            // The reticle's shooting reactions (hit marker, blocked state,
            // reload dim) live in their own component, added here so no HUD
            // prefab has to be rebuilt for them.
            if (GetComponent<GunReticleView>() == null)
            {
                gameObject.AddComponent<GunReticleView>();
            }
        }

        private void OnEnable()
        {
            Bind(Target);
            HookButton(RespawnButton);
            ShowDead(_bound != null && _bound.IsDead);
            CharacterIdentity.LookApplied += OnLookApplied;
            StartCoroutine(CapturePortrait());
        }

        private void OnDisable()
        {
            CharacterIdentity.LookApplied -= OnLookApplied;
            _refreshPending = false;
            Bind(null);
            HookButton(null);
        }

        private bool _refreshPending;

        /// <summary>The character just put on a new look (a redraw, a load): retake the portrait so it matches.</summary>
        private void OnLookApplied(CharacterIdentity identity)
        {
            if (Target != null && identity != null && identity.GetComponent<PlayerVitals>() == Target)
            {
                RefreshPortrait();
            }
        }

        /// <summary>Retakes the portrait a frame from now (once, however many changes arrive at once).</summary>
        public void RefreshPortrait()
        {
            if (!_refreshPending && isActiveAndEnabled)
            {
                StartCoroutine(RefreshNextFrame());
            }
        }

        private IEnumerator RefreshNextFrame()
        {
            _refreshPending = true;
            yield return null;
            _refreshPending = false;
            CaptureNow();
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
            CaptureNow();
        }

        /// <summary>
        /// Takes the portrait now: the head, its hair or hat, and the top of the chest, from the front.
        ///
        /// WHAT THE CAMERA SEES IS DECIDED HERE, AT THE MOMENT OF THE SHOT, not when the scene was
        /// built. The portrait camera only draws one dedicated layer, and PlayerHudBuilder used to put
        /// the renderers that existed at BUILD time on it -- so anything made while the game runs (the
        /// hair, a worn hat or coat) stayed on the ordinary layer and simply never appeared: the
        /// portrait was a bare head. Now every renderer on the body is moved onto the layer for the
        /// length of one synchronous Render call and put straight back, so whatever the character is
        /// wearing when this runs is what is shown.
        ///
        /// Anything held in a hand (a sword, a pistol) is left out: the portrait is of the person.
        /// </summary>
        /// <returns>False when there is nothing to photograph yet (no target, camera or head).</returns>
        public bool CaptureNow()
        {
            if (Target == null || PortraitCamera == null || PortraitImage == null)
            {
                return false;
            }

            Renderer headRenderer = FindHeadRenderer(Target.transform);
            int layer = SingleLayerOf(PortraitCamera.cullingMask);
            if (headRenderer == null || layer < 0)
            {
                return false;
            }

            Transform[] hands = HeldItemRoots(Target.transform);
            var moved = new List<KeyValuePair<Renderer, int>>();
            float top = headRenderer.bounds.max.y;
            Bounds headBounds = headRenderer.bounds;
            foreach (Renderer renderer in Target.GetComponentsInChildren<Renderer>(false))
            {
                if (!renderer.enabled || IsHeld(renderer.transform, hands) || renderer.transform.IsChildOf(PortraitCamera.transform))
                {
                    continue;
                }
                moved.Add(new KeyValuePair<Renderer, int>(renderer, renderer.gameObject.layer));
                renderer.gameObject.layer = layer;

                // Hair and hats sit at head height or above: they set how much room the top needs.
                if (renderer.bounds.center.y >= headBounds.center.y - headBounds.extents.y * 0.2f)
                {
                    top = Mathf.Max(top, renderer.bounds.max.y);
                }
            }

            try
            {
                Frame(PortraitCamera, headBounds, top, Target.transform.forward);
                // The whole GameObject, not just the camera -- PlayerHudBuilder hangs a dedicated light
                // off this same object for the shot, so it has to be on for the shot and off afterwards
                // (leaving it lit would cost render time near the player forever, for a texture
                // nothing writes to again).
                PortraitCamera.gameObject.SetActive(true);
                PortraitCamera.Render();
            }
            finally
            {
                foreach (KeyValuePair<Renderer, int> entry in moved)
                {
                    if (entry.Key != null)
                    {
                        entry.Key.gameObject.layer = entry.Value;
                    }
                }
                PortraitCamera.gameObject.SetActive(false);
            }
            return true;
        }

        /// <summary>How many head-radii below the head's centre the picture reaches: the shoulders and the top of the chest.</summary>
        public const float PortraitChestDropHeadRadii = 2.3f;

        /// <summary>Extra room around the framed area, because the picture is shown in a circle and its corners are cut off.</summary>
        public const float PortraitMargin = 1.25f;

        /// <summary>
        /// Puts the camera in front of the character, looking level, so the frame runs from the top of
        /// the hair (<paramref name="top"/>) down to the chest, centred on that stretch.
        /// </summary>
        public static void Frame(Camera camera, Bounds head, float top, Vector3 facing)
        {
            float radius = Mathf.Max(head.extents.x, head.extents.y, head.extents.z);
            float bottom = head.center.y - radius * PortraitChestDropHeadRadii;
            float centreY = (top + bottom) * 0.5f;
            float halfHeight = (top - bottom) * 0.5f * PortraitMargin;
            float distance = halfHeight / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            facing.y = 0.0f;
            facing = facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector3.forward;
            var look = new Vector3(head.center.x, centreY, head.center.z);
            camera.transform.position = look + facing * distance;
            camera.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, distance + radius * 4.0f);
        }

        /// <summary>The layer a culling mask names, when it names exactly one (-1 otherwise).</summary>
        private static int SingleLayerOf(int mask)
        {
            if (mask == 0 || (mask & (mask - 1)) != 0)
            {
                return -1;
            }
            int layer = 0;
            while ((mask >>= 1) != 0)
            {
                layer++;
            }
            return layer;
        }

        /// <summary>The hand bones: whatever hangs off them is being held, not worn.</summary>
        private static Transform[] HeldItemRoots(Transform body)
        {
            Animator animator = body.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return new Transform[0];
            }
            return new[]
            {
                animator.GetBoneTransform(HumanBodyBones.RightHand),
                animator.GetBoneTransform(HumanBodyBones.LeftHand),
            };
        }

        private static bool IsHeld(Transform renderer, Transform[] hands)
        {
            foreach (Transform hand in hands)
            {
                if (hand != null && renderer != hand && renderer.IsChildOf(hand))
                {
                    return true;
                }
            }
            return false;
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
        ///
        /// THE CURSOR HAS TO COME BACK TOO, or the respawn button is a real
        /// reported bug: it renders and its onClick is genuinely wired (see
        /// HookButton), but third-person play keeps the cursor locked and
        /// invisible the entire time, so there is no pointer on screen to
        /// click it with. Same "remember, then restore on the way back out"
        /// idiom every other panel in this project already uses (Pause,
        /// Inventory, Map, PlayerChat and the rest) -- captured here instead
        /// of assumed Locked, since a player could in principle die while
        /// one of those panels already has it free.
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

            if (dead)
            {
                _wasCursorCaptured = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (_wasCursorCaptured)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
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
