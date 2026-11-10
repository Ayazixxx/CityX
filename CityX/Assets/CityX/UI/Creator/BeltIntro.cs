using CityX.Character;
using CityX.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// A character is made on a conveyor belt in the sky (docs/BELT_INTRO.md; owner, 2026-11-10: "a conveyer belt in the sky you make your
    /// character on that rolls them off at the end"). The belt stands empty with a prompt; A / D or the arrows run it either way, and
    /// holding right rolls a plain white body in from the left, standing and idling. In the middle the belt stops and the creator's tools
    /// fade in. When the character is finished the belt carries it off to the right; past the edge of the screen the view changes to one
    /// from further back that shows the belt's end, the body is rolled off it and falls limp, the screen goes black, and the world loads
    /// with the fall from the sky (IntroCutscene) coming up out of the black.
    ///
    /// The creator shown without <see cref="Begin"/> (its tests, its pictures) is the creator as it always was: the body in the middle,
    /// holding still, the tools up. Where the body is and how fast the belt runs is <see cref="BeltRide"/>'s; this puts it on the screen.
    /// </summary>
    public sealed class BeltIntro : MonoBehaviour
    {
        [Header("Wired by CharacterCreatorBuilder")]
        public CharacterCreator Creator;

        public CreatorLife Life;

        public ConveyorBelt Belt;

        [Tooltip("What rides the belt: the body's turntable.")]
        public Transform Rider;

        [Tooltip("The creator's own camera: the body is made in front of it.")]
        public Camera MakingCamera;

        [Tooltip("The camera from further back that shows the belt's end.")]
        public Camera WideCamera;

        public Ragdoll Ragdoll;

        [Tooltip("The belt's running sound, a loop: heard as far as the belt runs.")]
        public AudioSource Hum;

        [Tooltip("The creator's tool column: faded in when the body arrives, out when it leaves.")]
        public CanvasGroup Tools;

        [Tooltip("PRESS D OR the right arrow, on the empty belt.")]
        public Text Prompt;

        [Tooltip("Lifts the arms, or lets them down (its ButtonAction calls TogglePose).")]
        public Button PoseButton;

        [Tooltip("The pieces of the little T-posing figure on the button.")]
        public Graphic[] PoseIcon = System.Array.Empty<Graphic>();

        public CreatorOverlays Overlays;

        public TitleScreen Title;

        /// <summary>How far past the edge of the screen the body waits, and how far past the other edge the view changes, metres.</summary>
        public const float BeyondTheEdge = 1.7f;

        /// <summary>From where the view changes to the belt's end, metres: about two and a half seconds' ride.</summary>
        public const float EdgeToEnd = 6.0f;

        /// <summary>How long the tools take to fade in or out, seconds.</summary>
        public const float ToolsFadeSeconds = 0.6f;

        /// <summary>The wider view: how far in front of the belt, how far under its top the eye is and looks, and its field of view.</summary>
        public const float WideDistance = 15.0f;

        public const float WideEyeBelow = 1.5f;
        public const float WideLookBelow = 3.5f;
        public const float WideFieldOfView = 50.0f;

        /// <summary>The screen starts to go black once the limp body is this far under the belt, or this long after it was let go.</summary>
        public const float BlackBelow = 6.0f;

        public const float BlackAfterSeconds = 2.5f;
        public const float BlackFadeSeconds = 0.6f;

        /// <summary>How loud the belt is at its full run.</summary>
        public const float HumVolume = 0.54f;

        /// <summary>The share of that it is heard at from the wider view, the camera being further from it.</summary>
        public const float WideHumShare = 0.65f;

        public const string PromptText = "PRESS  D  OR  →";

        /// <summary>The pose button's background while the arms are up: CreatorControls' lit tool.</summary>
        private static readonly Color PoseOnBackground = new Color(0.30f, 0.26f, 0.12f, 1.0f);

        private BeltRide _ride;
        private BeltRide.Stage _shown;
        private System.Action _enterWorld;
        private float _toolsShown = 1.0f;
        private float _promptElapsed;
        private float _sinceDrop;
        private float _blackFor = -1.0f;
        private float _riderHeight;
        private bool _heightKnown;

        /// <summary>The ride under way, or null while the creator stands as it always did.</summary>
        public BeltRide Ride => _ride;

        private void OnEnable()
        {
            Stand();
        }

        private void OnDisable()
        {
            // The rest is put right when the panel next comes up (its stage goes off with it now).
            _ride = null;
            _enterWorld = null;
            SoundTheBelt();
            if (Title != null && Title.Select != null)
            {
                Title.Select.Departure = null;
            }
        }

        /// <summary>The creator as it always was: the body in the middle holding still, the tools up, no ride.</summary>
        public void Stand()
        {
            _ride = null;
            _enterWorld = null;
            _blackFor = -1.0f;
            SoundTheBelt();
            if (Title != null && Title.Select != null)
            {
                Title.Select.Departure = null;
            }
            if (Ragdoll != null && Ragdoll.IsLimp)
            {
                Ragdoll.Deactivate();
            }
            if (Life != null)
            {
                Life.enabled = true;
                Life.SnapStill();
            }
            if (Creator != null)
            {
                Creator.enabled = true;
                Creator.SetClickable(true);
                Creator.TurnToFront(1.0f);
            }
            if (Overlays != null)
            {
                Overlays.enabled = true;
            }
            ShowCameras(wide: false);
            PlaceRider(0.0f);
            PlaceBelt(EdgeOfScreen() + BeyondTheEdge + EdgeToEnd);
            if (Belt != null)
            {
                Belt.Travel = 0.0f;
            }
            SetTools(1.0f);
            if (Prompt != null)
            {
                Prompt.gameObject.SetActive(false);
            }
            ShowPose();
        }

        /// <summary>Starts from the empty belt: the body waits off the screen's left, alive, and the player rolls it in.</summary>
        public void Begin()
        {
            Stand();
            float edge = EdgeOfScreen();
            _ride = new BeltRide(-(edge + BeyondTheEdge), edge + BeyondTheEdge, edge + BeyondTheEdge + EdgeToEnd);
            _shown = _ride.Now;
            PlaceRider(_ride.BodyAt);
            if (Life != null)
            {
                Life.ComeAlive(atOnce: true);   // out of sight: no arms coming down as it rolls in
            }
            if (Creator != null)
            {
                Creator.enabled = false;   // nothing is turned, zoomed or painted until it is in the middle
            }
            if (Title != null && Title.Select != null)
            {
                Title.Select.Departure = RollOut;
            }
            SetTools(0.0f);
            _promptElapsed = 0.0f;
            if (Prompt != null)
            {
                Prompt.text = PromptText;
                Prompt.gameObject.SetActive(true);
            }
            ShowPose();
        }

        /// <summary>The character is made and the world is ready to load: the belt carries the body off first, then <paramref name="enterWorld"/>.</summary>
        public void RollOut(System.Action enterWorld)
        {
            if (_ride == null || _ride.Now != BeltRide.Stage.Making)
            {
                enterWorld?.Invoke();
                return;
            }
            _enterWorld = enterWorld;
            _ride.Finish();
            _shown = _ride.Now;
            if (Life != null)
            {
                Life.ComeAlive();
            }
            if (Creator != null)
            {
                Creator.enabled = false;
                Creator.SetClickable(false);
            }
            if (Overlays != null)
            {
                Overlays.enabled = false;   // Escape no longer backs out: the character exists
            }
            ShowPose();
        }

        private void Update()
        {
            if (_ride == null)
            {
                return;
            }
            float deltaTime = Time.deltaTime;
            _ride.Step(_ride.Now == BeltRide.Stage.RollingIn ? Push() : 0.0f, deltaTime);
            BeltRide.Stage now = _ride.Now;
            BeltRide.Stage before = _shown;
            _shown = now;
            if (Belt != null)
            {
                Belt.Travel = _ride.Travel;
            }
            if (now != BeltRide.Stage.Fallen)
            {
                PlaceRider(_ride.BodyAt);
            }

            if (now != before)
            {
                switch (now)
                {
                    case BeltRide.Stage.Making: Arrive(); break;
                    case BeltRide.Stage.OnTheEdge: ChangeView(); break;
                    case BeltRide.Stage.Fallen: LetGo(); break;
                }
            }

            switch (now)
            {
                case BeltRide.Stage.RollingIn:
                    PulsePrompt(deltaTime);
                    break;
                case BeltRide.Stage.Making:
                    SetTools(Mathf.MoveTowards(_toolsShown, 1.0f, deltaTime / ToolsFadeSeconds));
                    if (PoseKeyPressed())
                    {
                        TogglePose();
                    }
                    break;
                case BeltRide.Stage.RollingOut:
                    SetTools(Mathf.MoveTowards(_toolsShown, 0.0f, deltaTime / ToolsFadeSeconds));
                    if (Creator != null)
                    {
                        Creator.TurnToFront(1.0f - Mathf.Exp(-6.0f * deltaTime));
                    }
                    break;
                case BeltRide.Stage.Fallen:
                    Fall(deltaTime);
                    break;
            }
            SoundTheBelt();
        }

        // --- the stages ---

        private void Arrive()
        {
            if (Prompt != null)
            {
                Prompt.gameObject.SetActive(false);
            }
            if (Creator != null)
            {
                Creator.enabled = true;
            }
        }

        /// <summary>Past the edge of the screen: the view from further back, the belt's end in it.</summary>
        private void ChangeView()
        {
            SetTools(0.0f);
            if (Creator != null)
            {
                Creator.TurnToFront(1.0f);
            }
            ShowCameras(wide: true);
        }

        /// <summary>At the belt's end: the body is the ragdoll's, going as fast as the belt was.</summary>
        private void LetGo()
        {
            _sinceDrop = 0.0f;
            if (Life != null)
            {
                Life.LetGo();
            }
            if (Ragdoll != null && Rider != null)
            {
                Transform stage = Rider.parent;
                Vector3 along = stage != null ? stage.TransformDirection(Vector3.left) : Vector3.left;   // the screen's right
                Ragdoll.MeasureLimitsFromThisPose();
                Ragdoll.Activate(along * BeltRide.Speed);
            }
        }

        /// <summary>The body falls out of the picture, the screen goes black behind it, and the world is entered.</summary>
        private void Fall(float deltaTime)
        {
            _sinceDrop += deltaTime;
            if (_blackFor < 0.0f)
            {
                float fallen = Ragdoll != null && Ragdoll.Torso != null && Rider != null
                    ? Rider.parent.position.y - Ragdoll.Torso.position.y
                    : 0.0f;
                if (fallen >= BlackBelow || _sinceDrop >= BlackAfterSeconds)
                {
                    _blackFor = 0.0f;
                    ScreenBlack.FadeTo(1.0f, BlackFadeSeconds);
                }
                return;
            }
            _blackFor += deltaTime;
            if (_blackFor >= BlackFadeSeconds + 0.1f && _enterWorld != null)
            {
                System.Action enter = _enterWorld;
                _enterWorld = null;
                enter();
            }
        }

        /// <summary>
        /// The belt is heard as hard as it runs (owner, 2026-11-10: "make this noise play when going right or left on the conveyer belt,
        /// and at the end when the conveyer belt rolls your character off"): up and down with its speed, so it neither starts nor stops
        /// with a click, and out with the picture as that goes black.
        /// </summary>
        private void SoundTheBelt()
        {
            if (Hum == null)
            {
                return;
            }
            float heard = _ride != null ? _ride.Running : 0.0f;
            if (WideCamera != null && WideCamera.enabled)
            {
                heard *= WideHumShare;
            }
            if (_blackFor >= 0.0f)
            {
                heard *= 1.0f - Mathf.Clamp01(_blackFor / BlackFadeSeconds);
            }
            Hum.volume = HumVolume * heard;
            if (heard > 0.0f && !Hum.isPlaying)
            {
                Hum.Play();
            }
            else if (heard <= 0.0f && Hum.isPlaying)
            {
                Hum.Stop();
            }
        }

        // --- the pose ---

        /// <summary>The button and the T key: arms up, or back down.</summary>
        public void TogglePose()
        {
            if (Life == null || (_ride != null && _ride.Now != BeltRide.Stage.Making))
            {
                return;
            }
            if (Life.Alive)
            {
                Life.HoldStill();
            }
            else
            {
                Life.ComeAlive();
            }
            ShowPose();
        }

        /// <summary>The button lit while the arms are up (or on their way), as the tool buttons are lit while they are the active one.</summary>
        private void ShowPose()
        {
            if (Life == null || PoseButton == null)
            {
                return;
            }
            PoseButton.targetGraphic.color = Life.Alive ? CreatorKit.Idle : PoseOnBackground;
            foreach (Graphic part in PoseIcon)
            {
                part.color = Life.Alive ? CreatorKit.Paper : CreatorKit.Gold;
            }
        }

        private bool PoseKeyPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.tKey.wasPressedThisFrame && (Overlays == null || !Overlays.Blocking);
        }

        // --- what is shown ---

        /// <summary>The keys that run the belt: A and D, the arrows, a controller's stick or pad. -1 left to 1 right.</summary>
        private static float Push()
        {
            float push = 0.0f;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) push += 1.0f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) push -= 1.0f;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                float stick = pad.leftStick.ReadValue().x + pad.dpad.ReadValue().x;
                if (Mathf.Abs(stick) > 0.2f)
                {
                    push += stick;
                }
            }
            return Mathf.Clamp(push, -1.0f, 1.0f);
        }

        /// <summary>How far from the middle of the screen its edge is, at the body, metres.</summary>
        private float EdgeOfScreen()
        {
            if (MakingCamera == null)
            {
                return 5.5f;
            }
            float away = Mathf.Abs(MakingCamera.transform.localPosition.z);
            return away * Mathf.Tan(MakingCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * MakingCamera.aspect;
        }

        /// <summary>The body <paramref name="along"/> the belt. The stage's cameras look back along -Z, so the screen's right is the stage's -X.</summary>
        private void PlaceRider(float along)
        {
            if (Rider == null)
            {
                return;
            }
            if (!_heightKnown)
            {
                _riderHeight = Rider.localPosition.y;
                _heightKnown = true;
            }
            Rider.localPosition = new Vector3(-along, _riderHeight, 0.0f);
        }

        /// <summary>The belt with its end <paramref name="endAlong"/> the screen, and the wider view placed on that end.</summary>
        private void PlaceBelt(float endAlong)
        {
            if (Belt != null)
            {
                // Turned round, so that it runs toward the screen's right.
                Belt.transform.localRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
                Belt.transform.localPosition = new Vector3(-endAlong + ConveyorBelt.Length, 0.0f, 0.0f);
            }
            if (WideCamera != null)
            {
                var eye = new Vector3(-(endAlong + 0.5f), -WideEyeBelow, WideDistance);
                var look = new Vector3(-(endAlong + 1.0f), -WideLookBelow, 0.0f);
                WideCamera.transform.localPosition = eye;
                WideCamera.transform.localRotation = Quaternion.LookRotation(look - eye, Vector3.up);
                WideCamera.fieldOfView = WideFieldOfView;
            }
        }

        private void ShowCameras(bool wide)
        {
            if (MakingCamera != null) MakingCamera.enabled = !wide;
            if (WideCamera != null) WideCamera.enabled = wide;
        }

        private void SetTools(float shown)
        {
            _toolsShown = shown;
            bool usable = shown >= 1.0f;
            if (Tools != null)
            {
                Tools.alpha = shown;
                Tools.interactable = usable;
                Tools.blocksRaycasts = usable;
            }
            if (PoseButton != null)
            {
                PoseButton.gameObject.SetActive(shown > 0.0f);
                if (PoseButton.TryGetComponent(out CanvasGroup group))
                {
                    group.alpha = shown;
                }
            }
        }

        private void PulsePrompt(float deltaTime)
        {
            if (Prompt == null)
            {
                return;
            }
            _promptElapsed += deltaTime;
            Color colour = Prompt.color;
            colour.a = Mathf.Lerp(0.45f, 1.0f, (Mathf.Sin(_promptElapsed * 2.8f) + 1.0f) * 0.5f);
            Prompt.color = colour;
        }
    }
}
