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

        [Tooltip("The key to press on the empty belt, drawn as a key cap: the right arrow, or D once a WASD key has been pressed.")]
        public Text Prompt;

        [Tooltip("The key cap round the prompt: shown, hidden and flashed as one.")]
        public CanvasGroup PromptKey;

        [Tooltip("Lifts the arms, or lets them down (its ButtonAction calls TogglePose).")]
        public Button PoseButton;

        [Tooltip("The pieces of the little T-posing figure on the button.")]
        public Graphic[] PoseIcon = System.Array.Empty<Graphic>();

        public CreatorOverlays Overlays;

        public TitleScreen Title;

        /// <summary>How far past the edge of the screen the body waits, and how far past the other edge the view changes, metres.</summary>
        public const float BeyondTheEdge = 1.7f;

        /// <summary>
        /// How much further off the left the body waits at the start, metres: two seconds of the belt at its full run before it comes into
        /// sight (owner, 2026-11-10: "make the player model placed a little bit farther to the left off screen so it takes about +2 seconds
        /// for him to appear onto the screen where holding D").
        /// </summary>
        public const float WaitsFurtherOff = 2.0f * BeltRide.Speed;

        /// <summary>From where the view changes to the belt's end, metres: about two and a half seconds' ride.</summary>
        public const float EdgeToEnd = 6.0f;

        /// <summary>
        /// How far the body leans over the roller before physics takes it, degrees: past this, keeping its front foot on the roller as
        /// that curves away at the belt's full run would spin it round. It is let go turning as fast as it was, or it jumps.
        /// </summary>
        public const float HandOverAtDegrees = 12.0f;

        /// <summary>How far past the belt's end the middle of the body is let go if it has not been by then, metres.</summary>
        public const float ThroughTheEnd = 0.6f;

        /// <summary>The snag lets the back foot go once the body has pitched this far over the end, degrees, or this long after it was stood, seconds, whatever it did.</summary>
        public const float LetGoAtDegrees = 45.0f;

        public const float LetGoAfterSeconds = 1.5f;

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

        /// <summary>
        /// The key on the empty belt's key cap (owner, 2026-11-10: "just make it say \"→\" with a box around it so its visibly a key ... if you
        /// press a or any of the wasd keys then it changes to a D key ... and if you press the arrow keys then it goes back").
        /// </summary>
        public const string PromptArrow = "→";

        public const string PromptLetter = "D";

        /// <summary>How long the empty belt waits before the key cap shows, seconds.</summary>
        public const float PromptDelaySeconds = 2.0f;

        /// <summary>How long the key cap takes to come up once it shows, seconds.</summary>
        private const float PromptFadeInSeconds = 0.3f;

        /// <summary>How long the key cap takes to fade away while the belt is run, and to come back once it is let go, seconds (owner, 2026-11-10:
        /// "make it fade away when you hold it").</summary>
        private const float PromptHeldFadeSeconds = 0.3f;

        /// <summary>The pose button's background while the arms are up: CreatorControls' lit tool.</summary>
        private static readonly Color PoseOnBackground = new Color(0.30f, 0.26f, 0.12f, 1.0f);

        private BeltRide _ride;
        private BeltRide.Stage _shown;
        private System.Action _enterWorld;
        private float _toolsShown = 1.0f;
        private float _promptElapsed;
        private float _promptHeld;
        private float _sinceDrop;
        private Rigidbody _snag;
        private HumanBodyBones _backFoot;
        private float _lean;
        private float _leaning;
        private Vector3 _leanAxis;
        private Vector3 _back;
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
            DropTheSnag();
            _lean = 0.0f;
            _leaning = 0.0f;
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
                Belt.Speed = 0.0f;
            }
            SetTools(1.0f);
            ShowPrompt(false);
            ShowPose();
        }

        /// <summary>Starts from the empty belt: the body waits off the screen's left, alive, and the player rolls it in.</summary>
        public void Begin()
        {
            Stand();
            float edge = EdgeOfScreen();
            _ride = new BeltRide(-(edge + BeyondTheEdge + WaitsFurtherOff), edge + BeyondTheEdge, edge + BeyondTheEdge + EdgeToEnd + ThroughTheEnd);
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
                Prompt.text = PromptArrow;
            }
            ShowPrompt(true);
            if (PromptKey != null)
            {
                PromptKey.alpha = 0.0f;   // shown after PromptDelaySeconds (PulsePrompt)
                _promptHeld = 0.0f;
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
                Belt.Speed = _ride.BeltSpeed;
            }
            if (now != BeltRide.Stage.Fallen)
            {
                PlaceRider(_ride.BodyAt);
            }
            if (now == BeltRide.Stage.OnTheEdge && before == BeltRide.Stage.OnTheEdge && LeanOverTheRoller(deltaTime) >= HandOverAtDegrees)
            {
                _ride.LetGo();
                now = _ride.Now;
                _shown = now;
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
            ShowPrompt(false);
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

        /// <summary>
        /// Over the roller the body leans about its back foot just as far as keeps its front foot on the roller as that curves away under
        /// it, still standing in its idle (owner, 2026-11-10: "feel like he needs a little tilt it still feels like his left leg hovers in
        /// air"); the lean, degrees. <see cref="HandOverAtDegrees"/> over, physics takes it (<see cref="LetGo"/>).
        /// </summary>
        private float LeanOverTheRoller(float deltaTime)
        {
            if (Life == null || Life.Feet == null || !Life.Feet.IsDown || Belt == null)
            {
                return 0.0f;
            }
            Transform belt = Belt.transform;
            Transform root = Life.Animator.transform;
            Rider.localRotation = Quaternion.identity;   // facing the front, as ChangeView left it
            Vector3 left = root.TransformPoint(Life.Feet.Spot(HumanBodyBones.LeftFoot));
            Vector3 right = root.TransformPoint(Life.Feet.Spot(HumanBodyBones.RightFoot));
            bool leftBehind = belt.InverseTransformPoint(left).x < belt.InverseTransformPoint(right).x;
            Vector3 back = leftBehind ? left : right, front = leftBehind ? right : left;
            _backFoot = leftBehind ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;

            float past = belt.InverseTransformPoint(front).x - ConveyorBelt.Length;
            float r = ConveyorBelt.DrumRadius;
            float drop = past <= 0.0f ? 0.0f : past >= r ? r : r - Mathf.Sqrt(r * r - past * past);
            float stance = Mathf.Max(belt.InverseTransformPoint(front).x - belt.InverseTransformPoint(back).x, 0.05f);
            float lean = Mathf.Atan2(drop, stance) * Mathf.Rad2Deg;

            // Turned the way that takes the belt's run down, about the back ankle.
            _leanAxis = Vector3.Cross(belt.up, belt.right).normalized;
            Quaternion turn = Quaternion.AngleAxis(lean, _leanAxis);
            Rider.SetPositionAndRotation(back + turn * (Rider.position - back), turn * Rider.rotation);
            _leaning = deltaTime > 0.0f ? (lean - _lean) * Mathf.Deg2Rad / deltaTime : 0.0f;
            _lean = lean;
            _back = back;
            return lean;
        }

        /// <summary>
        /// Leaning over the roller (<see cref="LeanOverTheRoller"/>), the body is let go: limp, moving as it was, along with the belt and
        /// turning over its back foot, so that nothing about it jumps as physics takes it (owner, 2026-11-10: "visible goes limp, looks very
        /// game engine like not like a cool cutscene"). Its back foot is snagged where it stands and carried with the belt to the top of
        /// the roller and held there, and the body swings out over it (the owner's drawing: "thats like the last frame his right leg touches
        /// the roller after that he falls").
        /// </summary>
        private void LetGo()
        {
            _sinceDrop = 0.0f;
            if (Life != null)
            {
                Life.LetGo();
            }
            if (Ragdoll == null || Rider == null || Belt == null)
            {
                return;
            }
            Transform stage = Rider.parent;
            Vector3 along = stage != null ? stage.TransformDirection(Vector3.left) : Vector3.left;   // the screen's right
            Vector3 turning = _leanAxis * Mathf.Max(_leaning, 0.0f);
            if (!Ragdoll.LetGoTurning(along * BeltRide.Speed, turning, _back))
            {
                return;
            }
            var snag = new GameObject("Snag") { layer = gameObject.layer };
            snag.transform.position = _back;
            _snag = snag.AddComponent<Rigidbody>();
            _snag.isKinematic = true;
            Ragdoll.Pin(_backFoot, Belt.transform.forward, _snag);   // turning over the drop only, not toward the camera or away
        }

        /// <summary>The snag rides the belt to the top of the roller and stops there.</summary>
        private void FixedUpdate()
        {
            if (_snag == null || Belt == null)
            {
                return;
            }
            Transform belt = Belt.transform;
            Vector3 at = belt.InverseTransformPoint(_snag.position);
            at.x = Mathf.Min(at.x + Belt.Speed * Time.fixedDeltaTime, ConveyorBelt.Length);
            _snag.MovePosition(belt.TransformPoint(at));
        }

        /// <summary>Pitched far enough over (or held too long, whatever it did), the snag lets the back foot go.</summary>
        private void LetGoOnceOver()
        {
            if (Ragdoll == null || !Ragdoll.IsPinned)
            {
                return;
            }
            Animator animator = Ragdoll.Animator;
            Vector3 up = animator.GetBoneTransform(HumanBodyBones.Neck).position - animator.GetBoneTransform(HumanBodyBones.Hips).position;
            if (Vector3.Angle(Belt.transform.up, up) >= LetGoAtDegrees || _sinceDrop >= LetGoAfterSeconds)
            {
                Ragdoll.Unpin();
                DropTheSnag();
            }
        }

        private void DropTheSnag()
        {
            if (_snag != null)
            {
                Destroy(_snag.gameObject);
            }
            _snag = null;
        }

        /// <summary>The body falls out of the picture, the screen goes black behind it, and the world is entered.</summary>
        private void Fall(float deltaTime)
        {
            _sinceDrop += deltaTime;
            LetGoOnceOver();
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

        /// <summary>Shows or hides the key cap with its key.</summary>
        private void ShowPrompt(bool shown)
        {
            if (PromptKey != null)
            {
                PromptKey.gameObject.SetActive(shown);
            }
        }

        /// <summary>The key cap: nothing for its first seconds, then flashing, faded away while the belt is run; D once a WASD key is pressed, the
        /// arrow again on an arrow key.</summary>
        private void PulsePrompt(float deltaTime)
        {
            if (Prompt == null || PromptKey == null)
            {
                return;
            }
            _promptElapsed += deltaTime;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                {
                    Prompt.text = PromptLetter;
                }
                else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
                         keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    Prompt.text = PromptArrow;
                }
            }
            float shown = Mathf.Clamp01((_promptElapsed - PromptDelaySeconds) / PromptFadeInSeconds);
            float flash = Mathf.Lerp(0.45f, 1.0f, (Mathf.Sin((_promptElapsed - PromptDelaySeconds) * 2.8f + Mathf.PI * 0.5f) + 1.0f) * 0.5f);
            _promptHeld = Mathf.MoveTowards(_promptHeld, Mathf.Abs(Push()) > 0.0f ? 1.0f : 0.0f, deltaTime / PromptHeldFadeSeconds);
            PromptKey.alpha = shown * flash * (1.0f - _promptHeld);
        }
    }
}
