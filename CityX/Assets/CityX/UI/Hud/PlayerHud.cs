using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
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
        /// <summary>Under 75%: a warm yellow, a step toward orange (owner, 2026-10-14: orange, then "more of a yellow actually").</summary>
        public static readonly Color HealthYellow = new Color(1.0f, 0.78f, 0.12f);
        public static readonly Color HealthRed = new Color(0.9f, 0.15f, 0.15f);

        // The portrait is framed from the character's own measurements (see Frame): the head, the hair or
        // hat above it, and the top of the chest, from the front and level. hud.gd's fixed camera
        // distance was carried over first and then pulled back once ("too tight a crop"), but a fixed
        // distance cannot follow a hairstyle or hat, and showed a bare head with no shoulders.

        /// <summary>Near black behind the face, as WoW's portraits have it (was hud.gd's olive PORTRAIT_BG_COLOR, 0.26 0.21 0.07, until 2026-10-14).</summary>
        public static readonly Color PortraitBackgroundColour = new Color(0.07f, 0.065f, 0.06f);

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

        [Header("The World of Warcraft style frame (PlayerHudBuilder lays it out, HudArt paints it)")]
        [Tooltip("The character's name, over the bars.")]
        public Text NameLabel;

        [Tooltip("The health bar's percentage.")]
        public Text HealthText;

        [Tooltip("The whole mana bar: hidden while the player has no mana yet (PlayerVitals.ManaUnlocked).")]
        public GameObject ManaBar;

        public RectTransform ManaFill;

        [Tooltip("The mana bar's percentage.")]
        public Text ManaText;

        [Tooltip("The pieces HudArt paints at run time (their art is drawn in code, not stored).")]
        public Image PortraitRing;

        public Image PortraitRimShadow;

        public Image Plate;

        public Image LevelRing;

        public Image[] BarWells;

        public Image[] BarFills;

        /// <summary>The name shown when the character has none (a body that never went through the creator).</summary>
        public const string DefaultName = "Player";

        private PlayerVitals _bound;
        private Button _hookedButton;
        private int _shownLevel = -1;
        private int _shownHealthPercent = -1;
        private int _shownManaPercent = -1;
        private bool _wasCursorCaptured;

        private void Awake()
        {
            if (Target == null)
            {
                Target = GetComponentInParent<PlayerVitals>();
            }

            HudArt.Paint(this);
            // The portrait's texture is the camera's own: never a white disc where the face should be.
            if (PortraitImage != null && PortraitImage.texture == null && PortraitCamera != null && PortraitCamera.targetTexture != null)
            {
                PortraitImage.texture = PortraitCamera.targetTexture;
            }

            // The reticle's shooting reactions (hit marker, blocked state,
            // reload dim) live in their own component, added here so no HUD
            // prefab has to be rebuilt for them.
            if (GetComponent<GunReticleView>() == null)
            {
                gameObject.AddComponent<GunReticleView>();
            }

            // The bar of skills in the bottom-left corner (keys 1 to 0), built the same way.
            if (GetComponent<HotbarView>() == null)
            {
                gameObject.AddComponent<HotbarView>();
            }

            // The portrait frame and the bar fade out while the C menu or one of its pages is open (MenuHudFade).
            if (GetComponent<MenuHudFade>() == null)
            {
                gameObject.AddComponent<MenuHudFade>();
            }

            // The graffiti canvas, opened by a right-click at a wall with a spray can in hand (2026-10-15).
            if (GetComponent<GraffitiMenuView>() == null)
            {
                gameObject.AddComponent<GraffitiMenuView>();
            }
        }

        private void OnEnable()
        {
            Bind(Target);
            HookButton(RespawnButton);
            ShowDead(_bound != null && _bound.IsDead);
            CharacterIdentity.LookApplied += OnLookApplied;
            StartCoroutine(CapturePortrait());
            ShowName();
        }

        private void OnDisable()
        {
            CharacterIdentity.LookApplied -= OnLookApplied;
            _refreshPending = false;
            Bind(null);
            HookButton(null);
        }

        private bool _refreshPending;
        private Inventory _boundInventory;

        /// <summary>
        /// The first portrait's pose and camera, relative to the body. Every later portrait (a helmet put on, a redraw) is taken in that pose
        /// from that spot (owner, 2026-10-18: "i think you changed the angle of the portrait which i specifically asked you not to do", "and the
        /// arms positioned different"): retaken mid-game, the body was in whatever pose it was in, and the shot moved with it.
        /// </summary>
        private PoseSnapshot _portraitPose;
        private Transform _portraitBody;
        private Vector3 _portraitCameraPosition;
        private Quaternion _portraitCameraRotation;

        /// <summary>The character just put on a new look (a redraw, a load): retake the portrait so it matches.</summary>
        private void OnLookApplied(CharacterIdentity identity)
        {
            if (Target != null && identity != null && identity.GetComponent<PlayerVitals>() == Target)
            {
                RefreshPortrait();
                ShowName();
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

            Transform body = Target.transform;
            Animator animator = Target.GetComponentInChildren<Animator>();
            Transform skeleton = animator != null ? animator.transform : body;
            // The pose of the first portrait, taken at load, is kept for good: a later body (a respawn, the networked body that appears when
            // hosting) wears it too, not whatever it is doing then (owner, 2026-10-23: a hand at the face after taking out the combat sword;
            // "i dont want it to change from the position it is at when you load in").
            bool first = _portraitPose == null;
            PoseSnapshot current = first ? null : PoseSnapshot.Of(skeleton);
            Transform[] hands = HeldItemRoots(body);
            var moved = new List<KeyValuePair<Renderer, int>>();
            var standIns = new List<GameObject>();
            try
            {
                if (!first && _portraitBody == body)
                {
                    _portraitPose.Apply();
                }
                else if (!first)
                {
                    _portraitPose.ApplyTo(skeleton);
                }
                float top = headRenderer.bounds.max.y;
                Bounds headBounds = headRenderer.bounds;
                foreach (Renderer renderer in Target.GetComponentsInChildren<Renderer>(false))
                {
                    if (!renderer.enabled || IsHeld(renderer.transform, hands) || renderer.transform.IsChildOf(PortraitCamera.transform))
                    {
                        continue;
                    }
                    if (renderer is SkinnedMeshRenderer skinned)
                    {
                        // The body was skinned to this frame's pose before this runs, so the bones put back above would not reach it: it is
                        // photographed as a still copy of itself in the pose the bones are in now.
                        standIns.Add(StillCopy(skinned, layer));
                    }
                    else
                    {
                        moved.Add(new KeyValuePair<Renderer, int>(renderer, renderer.gameObject.layer));
                        renderer.gameObject.layer = layer;
                    }

                    // The hair sits at head height or above: it sets how much room the top needs. Worn items do not (owner, 2026-10-18: show
                    // what is equipped but "dont alter the positioning of the playermodel in the portrait"), so a helmet never moves the shot.
                    if (renderer.bounds.center.y >= headBounds.center.y - headBounds.extents.y * 0.2f
                        && renderer.GetComponentInParent<IEquippableItem>() == null)
                    {
                        top = Mathf.Max(top, renderer.bounds.max.y);
                    }
                }

                if (first)
                {
                    Frame(PortraitCamera, headBounds, top, body.forward);
                    // Kept only in the plain pose: a shot of fists up (a punch thrown as the world loaded) or of anything else stands in
                    // until the body is plain, then is taken again (Update) and that pose kept.
                    _awaitingPlainPose = !InPlainPose(body, hands);
                    if (!_awaitingPlainPose)
                    {
                        _portraitBody = body;
                        _portraitPose = PoseSnapshot.Of(skeleton);
                        _portraitCameraPosition = body.InverseTransformPoint(PortraitCamera.transform.position);
                        _portraitCameraRotation = Quaternion.Inverse(body.rotation) * PortraitCamera.transform.rotation;
                    }
                }
                else
                {
                    PortraitCamera.transform.SetPositionAndRotation(body.TransformPoint(_portraitCameraPosition), body.rotation * _portraitCameraRotation);
                }
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
                foreach (GameObject standIn in standIns)
                {
                    DestroyImmediate(standIn.GetComponent<MeshFilter>().sharedMesh);
                    DestroyImmediate(standIn);
                }
                PortraitCamera.gameObject.SetActive(false);
                current?.Apply();
            }
            PortraitsTaken++;
            return true;
        }

        /// <summary>How many portraits have been taken: for tests of when it is retaken.</summary>
        public int PortraitsTaken { get; private set; }

        /// <summary>The portrait shown was taken out of the plain pose and waits to be taken again in it (and only then kept).</summary>
        private bool _awaitingPlainPose;

        /// <summary>The plain walk/idle state (PlayerLocomotion.LocomotionClipsOwnTheSpine's), restarted for a portrait taken late.</summary>
        private static readonly int IdleState = Animator.StringToHash("Locomotion");

        /// <summary>Whether the portrait shown is still waiting for the body to stand plain.</summary>
        public bool AwaitingPlainPose => _awaitingPlainPose;

        /// <summary>
        /// The body standing plain (PlayerLocomotion.ShowsPlainPose) with nothing in its hands, whose hold would pose the arms. A body without
        /// locomotion counts as plain.
        /// </summary>
        private static bool InPlainPose(Transform body, Transform[] hands)
        {
            var locomotion = body.GetComponent<PlayerLocomotion>();
            if (locomotion != null && !locomotion.ShowsPlainPose)
            {
                return false;
            }
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer.enabled && IsHeld(renderer.transform, hands))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A still copy of a skinned renderer in its bones' pose of this moment, on <paramref name="layer"/>, wearing what it wears.</summary>
        private static GameObject StillCopy(SkinnedMeshRenderer skinned, int layer)
        {
            var mesh = new Mesh { name = skinned.name + " (portrait)" };
            skinned.BakeMesh(mesh, true);
            var copy = new GameObject(mesh.name) { layer = layer };
            copy.transform.SetPositionAndRotation(skinned.transform.position, skinned.transform.rotation);
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = copy.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = skinned.sharedMaterials;
            MaterialBlocks.Copy(skinned, renderer, skinned.sharedMaterials.Length);
            return copy;
        }

        /// <summary>
        /// How many head-radii below the head's centre the picture reaches: the chin and a glimpse of the shoulders (owner, 2026-10-14: "zoom in
        /// ... so that it is just the head and a small glimpse of the shoulders visible ... i shouldnt see the whole players torso", with WoW's
        /// portraits to go by). It was 2.3, the top of the chest.
        /// </summary>
        public const float PortraitShoulderDropHeadRadii = 1.3f;

        /// <summary>Extra room around the framed area, because the picture is shown in a circle and its corners are cut off.</summary>
        public const float PortraitMargin = 1.1f;

        /// <summary>The camera stands this far round to the character's right, so the face turns toward the bars: a three-quarter view, as WoW's portraits have, not a mugshot.</summary>
        public const float PortraitTurnDegrees = 24.0f;

        /// <summary>Where the portrait's light hangs from its camera (right, up, forward, metres): up and to the left, so the face has a lit and a shaded side.</summary>
        public static readonly Vector3 PortraitLightOffset = new Vector3(-0.7f, 0.75f, 0.25f);

        /// <summary>
        /// Puts the camera in front of the character, a little round to its right, looking level, so the frame runs from the top of
        /// the hair (<paramref name="top"/>) down to the chin and the tops of the shoulders, centred on that stretch.
        /// </summary>
        public static void Frame(Camera camera, Bounds head, float top, Vector3 facing)
        {
            float radius = Mathf.Max(head.extents.x, head.extents.y, head.extents.z);
            float bottom = head.center.y - radius * PortraitShoulderDropHeadRadii;
            float centreY = (top + bottom) * 0.5f;
            float halfHeight = (top - bottom) * 0.5f * PortraitMargin;
            float distance = halfHeight / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            facing.y = 0.0f;
            facing = facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector3.forward;
            Vector3 view = Quaternion.AngleAxis(PortraitTurnDegrees, Vector3.up) * facing;
            var look = new Vector3(head.center.x, centreY, head.center.z);
            camera.transform.position = look + view * distance;
            camera.transform.rotation = Quaternion.LookRotation(-view, Vector3.up);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, distance + radius * 4.0f);
            Light key = camera.GetComponentInChildren<Light>(true);
            if (key != null)
            {
                key.transform.localPosition = PortraitLightOffset;
                key.range = Mathf.Max(key.range, distance + 1.5f);
            }
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
                if (renderer.gameObject.name == BodySurface.HeadName)
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
            if (_boundInventory != null)
            {
                _boundInventory.EquipmentChanged -= OnEquipmentChanged;
            }

            _bound = vitals;
            Target = vitals;
            _boundInventory = vitals != null ? vitals.GetComponent<Inventory>() : null;

            if (_bound != null)
            {
                _bound.Died += OnDied;
                _bound.Revived += OnRevived;
            }
            // Owner, 2026-10-18: "make the portrait in the top left show helmets and any other items equipped".
            _wornAtLastShot = WornNow();
            if (_boundInventory != null)
            {
                _boundInventory.EquipmentChanged += OnEquipmentChanged;
            }
        }

        /// <summary>
        /// The slots whose items the portrait shows. Not the hands: what is held is left out of the picture, so switching weapons does not retake it
        /// (owner, 2026-10-19: "the portrait in the top left changing when i am switching weapons just make it stay as the default").
        /// </summary>
        private static readonly string[] WornSlots =
            { EquipSlots.Helmet, EquipSlots.Mask, EquipSlots.Torso, EquipSlots.Gloves, EquipSlots.Legs, EquipSlots.Shoes, EquipSlots.Back };

        private string _wornAtLastShot;

        private string WornNow()
        {
            if (_boundInventory == null)
            {
                return "";
            }
            var worn = new System.Text.StringBuilder();
            foreach (string slot in WornSlots)
            {
                worn.Append(_boundInventory.EquippedItemId(slot)).Append('|');
            }
            return worn.ToString();
        }

        /// <summary>Retakes the portrait when what is WORN changed; a weapon taken out or put away leaves it as it is.</summary>
        private void OnEquipmentChanged()
        {
            string worn = WornNow();
            if (worn != _wornAtLastShot)
            {
                _wornAtLastShot = worn;
                RefreshPortrait();
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
            if (_awaitingPlainPose && !_refreshPending && InPlainPose(_bound.transform, HeldItemRoots(_bound.transform)))
            {
                // A portrait taken at load catches the idle as it starts; started again, this one is taken at the same moment of it (part
                // way through, the head sits a little otherwise and the framing follows it).
                Animator animator = _bound.GetComponentInChildren<Animator>();
                if (animator != null && animator.isActiveAndEnabled)
                {
                    animator.Play(IdleState, 0, 0.0f);
                }
                RefreshPortrait();
            }

            SetFill(HealthFill, Fraction(_bound.Health, _bound.MaxHealth));
            SetFill(StaminaFill, Fraction(_bound.Stamina, _bound.MaxStamina));
            SetPercent(HealthText, Fraction(_bound.Health, _bound.MaxHealth), ref _shownHealthPercent);
            if (NameLabel != null && string.IsNullOrEmpty(NameLabel.text))
            {
                ShowName();   // a label wired after the HUD woke up
            }
            if (ManaBar != null && ManaBar.activeSelf != _bound.ManaUnlocked)
            {
                ManaBar.SetActive(_bound.ManaUnlocked);
            }
            if (_bound.ManaUnlocked)
            {
                SetFill(ManaFill, Fraction(_bound.Mana, _bound.MaxMana));
                SetPercent(ManaText, Fraction(_bound.Mana, _bound.MaxMana), ref _shownManaPercent);
            }

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
        /// A bar's percentage, as WoW's frames show it: 100 only when full and 0 only when empty (a scratch reads 99, a sliver 1). The text is
        /// only rebuilt when the number changes.
        /// </summary>
        public static int PercentFor(float fraction)
        {
            if (fraction >= 1.0f)
            {
                return 100;
            }
            if (fraction <= 0.0f)
            {
                return 0;
            }
            return Mathf.Clamp(Mathf.FloorToInt(fraction * 100.0f), 1, 99);
        }

        private static void SetPercent(Text label, float fraction, ref int shown)
        {
            int percent = PercentFor(fraction);
            if (label == null || percent == shown)
            {
                return;
            }
            shown = percent;
            label.text = percent + "%";
        }

        /// <summary>The character's name over the bars (the creator's name for it; the default when it has none).</summary>
        public void ShowName()
        {
            if (NameLabel == null)
            {
                return;
            }
            CharacterIdentity identity = Target != null ? Target.GetComponent<CharacterIdentity>() : null;
            string name = identity != null && identity.Character != null ? identity.Character.Name : null;
            NameLabel.text = string.IsNullOrEmpty(name) ? DefaultName : name;
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
