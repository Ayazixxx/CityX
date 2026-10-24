using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// Orbit camera behind the player, with a spring arm that pulls in when
    /// something is between the camera and the head.
    ///
    /// The collision arm is not polish. This city is narrow canyons and 14m
    /// streets; without it the camera spends most of its time inside a wall,
    /// and you would be judging the buildings from the inside.
    ///
    /// THE FRAMING IS SAINTS ROW: THE THIRD'S (owner, 2026-10-23: "research the saints row the third camera pov and zoom and just copy that"):
    /// a 50 degree field of view, the camera close behind and over the right shoulder with the top of the head at the middle of the screen
    /// and the body filling the lower half; fine aim snaps in close over the shoulder and zooms to 30 degrees, and eases back out; a sprint
    /// widens the view a tenth. The game's own camera table (camera_free.xtbl) and frames of it, scaled 1.44x to this body: docs/CAMERA.md.
    /// </summary>
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        public Transform Target;

        [Tooltip(
            "Optional. While this is limp, the camera follows its own Torso rigidbody " +
            "instead of Target -- see ApplyCameraTransform's own doc comment.")]
        public Ragdoll Ragdoll;

        /// <summary>
        /// Requested: "make the camera follow the ragdoll when /sleep is
        /// used and the ragdoll is moved." Nothing moves the player's own
        /// root transform while limp (Ragdoll's own doc comment: "only the
        /// individual Rigidbodies do"), so Target alone leaves the camera
        /// frozen wherever the body went limp while physics carries the
        /// actual ragdoll somewhere else entirely -- down a slope, pushed,
        /// tumbling from a crash. A small fixed height rather than
        /// PivotHeightFraction * standing height: that fraction assumes a
        /// STANDING body (roughly chest height), which would aim the pivot
        /// well above a body lying flat on the ground.
        /// </summary>
        public const float RagdollPivotHeight = 0.4f;

        [Tooltip(
            "Where the camera pivots on the body, as a fraction of standing height: 0.73 is " +
            "about 1.9 m on the 2.6 m body (player.gd's own BASE_CAMERA_PIVOT_HEIGHT was 0.73 " +
            "of its body). A fraction rather than metres so it stays sensible when the height " +
            "changes. The camera itself sits FramingHeight above this.")]
        public float PivotHeightFraction = 0.73f;

        /// <summary>
        /// How far above the pivot the camera's line sits, in the view's own up, metres: the body is framed in the lower half of the screen and
        /// the middle of the screen shows what is in front, over the head (owner, 2026-10-17: "i feel like the camera pov needs to be adjusted so
        /// i can see more of whats going on in front of me"). In the view's own up, so the body keeps its place in the frame at any pitch. Eased
        /// away while aiming, whose over-the-shoulder framing is its own (AimPivotRaise, AimShoulderOffset). 1.0 from 2026-10-17 (the body well
        /// below the middle); 0.6 since 2026-10-23 puts the camera's line at the top of the head, where Saints Row: The Third looks. See docs/CAMERA.md.
        /// </summary>
        public float FramingHeight = DefaultFramingHeight;

        public const float DefaultFramingHeight = 0.6f;

        /// <summary>
        /// How far behind the pivot the camera hangs, metres (the spring arm's length, before walls pull it in). 4.0 was player.gd's own
        /// spring_length, 4.8 the 2026-10-17 framing, 5.0 Saints Row: The Third's 3.5 m on this 1.44x body; 6.0 is the owner's (2026-10-24:
        /// "set it do camera distance 6 and add the same difference in zoom out to the scope in pov with guns" -- AimDistance a metre further too).
        /// </summary>
        public float Distance = DefaultDistance;

        public const float DefaultDistance = 6.0f;
        public float LookDegreesPerPixel = 0.12f;

        /// <summary>The pitch the camera starts at, degrees down: looking a little down on the body, as the game's own camera rests.</summary>
        public const float DefaultPitch = 12.0f;

        /// <summary>
        /// Bug report: "its not letting me all the way straight up
        /// it cuts off at a certain point. make it so I can look all the way
        /// up and down." Was -35/70 -- barely past the horizon looking up,
        /// and short of straight down too.
        ///
        /// The true +/-90, not just short of it -- Jetpack.
        /// JetpackMinPitchDegrees/MaxPitchDegrees already settled this exact
        /// question for its own equipped-flight widening: this camera builds
        /// its rotation from a plain Quaternion.Euler(pitch, yaw, 0) rather
        /// than a LookRotation(forward, up), so there is no forward-
        /// parallel-to-up degeneracy at the pole the way a margin would
        /// otherwise guard against -- yaw is simply visually meaningless for
        /// the one frame the camera sits exactly at the pole, same as any
        /// first/third-person camera, and never a real bug.
        /// </summary>
        public float MinPitch = -90.0f;
        public float MaxPitch = 90.0f;

        [Tooltip(
            "Metres to the right of the spine the camera sits, Fortnite/GTA-style, so the body " +
            "is off to one side of frame instead of dead centre. Reported directly: standing " +
            "square behind the head put anything small and close in front of the player -- a " +
            "dropped pistol on the ground being the concrete case -- right behind their own " +
            "silhouette, with nothing on screen to actually judge alignment against. 0.5 was " +
            "player.gd's own SHOULDER_OFFSET_X; 0.87 is Saints Row: The Third's x_shift 0.6 on this body (2026-10-23).")]
        public float ShoulderOffset = DefaultShoulderOffset;

        public const float DefaultShoulderOffset = 0.87f;

        /// <summary>
        /// The game camera's field of view when neither aiming nor flying, degrees (vertical). 75 was player.gd's own camera.fov; 50 is Saints
        /// Row: The Third's base_fov (2026-10-23): far less wide-angle, the street's far end bigger, the body bigger on screen. Set on the Camera
        /// by the scene builder; this camera reads it from there at Start (RestFieldOfView).
        /// </summary>
        public const float DefaultFieldOfView = 50.0f;

        /// <summary>
        /// How far to the side of the pivot the camera sits while aiming, metres. HISTORY: 0.25, then 0.65, then 1.415 (player.gd's own
        /// ADS_SHOULDER_OFFSET_X) -- but with the camera still 4 m back, 1.4 m to the side put the gun at the far edge of the frame or out of it:
        /// "i cant even see this pistol when im aiming in with it which it doesnt do in any other third person shooter" (owner, 2026-10-14).
        /// The research (docs/THIRD_PERSON_PISTOL.md): aiming in a third-person shooter brings the camera IN over the shoulder (Gears, The
        /// Division, Fortnite, RE4's shoulder cam) so the character fills the left of the frame and the gun points into its middle. And this body's
        /// head is 0.7 m across with the gun held under its middle (a two-handed stance): from behind, a camera less than about 0.9 m to the
        /// side sees the head and not the gun (checked in FirearmCapture's renders at 0.6). So 1.0 m, kept when Saints Row: The Third's fine
        /// aim was copied on 2026-10-23 (its 0.75 m, measured from its frames, hid the Glock behind this body's bigger head:
        /// FirearmPlayTests.TheGunIsOnScreenAndNotBehindTheHeadWhileAiming); its distance (AimDistance, 2.9 m) and the camera's line at the
        /// shoulders (AimPivotRaise) are copied. On 2026-10-24 the aim pulled back a metre with Distance (3.9 m), and this went to 1.3 m to
        /// look past the head at the same angle (at 1.0 the head covered the Glock again). The gun agrees with the crosshair whatever this
        /// is (PlayerLocomotion aims the barrel at the camera ray's point).
        /// </summary>
        public float AimShoulderOffset = DefaultAimShoulderOffset;

        public const float DefaultAimShoulderOffset = 1.3f;

        public const float DefaultAimDistance = 3.9f;

        public const float DefaultAimPivotRaise = 0.35f;

        [Tooltip("How far back from the shoulder pivot the camera sits while aiming, metres (Distance otherwise). Closer is what makes the gun read.")]
        public float AimDistance = DefaultAimDistance;

        [Tooltip("Per second: how fast the camera snaps in to its aiming place. 6.7 = Saints Row's 0.15 s fine-aim blend.")]
        public float AimCameraBlendPerSecond = 6.7f;

        [Tooltip("Per second: how fast the camera eases back out of aiming. 2 = half a second, Saints Row's blend back to free roam.")]
        public float AimCameraReleasePerSecond = 2.0f;

        /// <summary>0 (the ordinary place) to 1 (in over the shoulder, aiming), as an S-curve.</summary>
        public float AimCameraBlend => Mathf.SmoothStep(0.0f, 1.0f, _aimCameraBlend);

        [Tooltip("How much higher the pivot sits while aiming, metres: the camera looks over the shoulder and down the gun, not along it.")]
        public float AimPivotRaise = DefaultAimPivotRaise;

        [Tooltip("Metres per second ShoulderOffset eases toward AimShoulderOffset when the aim state changes -- mirrors AimZoomDegreesPerSecond's own shape for FOV.")]
        public float AimShoulderOffsetMetresPerSecond = 1.0f;

        [Tooltip("Layers the spring arm collides with. Should include the city.")]
        public LayerMask CollisionLayers = ~0;

        /// <summary>
        /// NEVER PULLS CLOSER THAN THIS, even against a wall right on top of
        /// the pivot -- a live bug report, confirmed directly with a
        /// screenshot: swinging near buildings (which is to say, swinging
        /// at all, since every anchor is on one) let this clamp down to its
        /// old 0.4m, which combined with the camera's own 0.15m near clip
        /// plane left almost no headroom -- an outstretched limb (the
        /// swing's own arm reach pose, in particular) or a nearby wall
        /// corner would end up partly inside the near clip plane or so
        /// close the lens shows nothing but a warped close-up, which is
        /// exactly what "gets distorted to lengths that shouldn't be
        /// possible" and "the camera keeps moving, the character stays
        /// still" describe -- the character never moved or deformed at
        /// all; the CAMERA was the thing sitting somewhere absurd. 1.5m
        /// still lets the camera work in this city's own narrow alleys
        /// (see this class's own doc comment) without ever getting close
        /// enough to clip through anything it is trying to look at.
        /// </summary>
        public float MinDistance = 1.5f;

        [Tooltip(
            "Field of view while aiming (see IsAiming). Narrower than the ordinary field of " +
            "view, which is read from whatever the Camera component already starts at rather " +
            "than assumed here, so this only ever has to describe how much CLOSER aiming zooms. " +
            "50 was player.gd's own ADS_FOV_DEGREES; 30 is Saints Row: The Third's fine aim, a real 1.7x zoom (2026-10-23).")]
        public float AimFieldOfView = 30.0f;

        [Tooltip("Degrees of field of view crossed per second zooming in to aim: 20 degrees in Saints Row's 0.15 s.")]
        public float AimZoomDegreesPerSecond = 135.0f;

        [Tooltip("Degrees of field of view crossed per second zooming back out: 20 degrees in half a second.")]
        public float AimZoomOutDegreesPerSecond = 40.0f;

        /// <summary>
        /// Set every frame by PlayerInputReader, like IsAiming: the body is sprinting. Saints Row's camera has its own sprint entry; the one
        /// thing of it that is documented (its first-person mod mirrors it) is a field of view a tenth wider, the cheapest speed cue there is.
        /// </summary>
        public bool IsSprinting;

        [Tooltip("The field of view is this many times wider at a full sprint (1 = no change); aiming overrides it.")]
        public float SprintFieldOfViewScale = 1.1f;

        [Tooltip("Per second: how fast the sprint widening comes and goes. 2 = half a second, Saints Row's camera blend.")]
        public float SprintBlendPerSecond = 2.0f;

        private float _sprintBlend;

        /// <summary>0 (not sprinting) to 1 (the view fully widened).</summary>
        public float SprintBlend => _sprintBlend;

        /// <summary>
        /// Extra field of view, in degrees, while flying fast -- widening the view
        /// with speed is the cheapest, strongest speed cue there is. Set every frame
        /// by the jetpack (see FlightFeel); 0 on foot. Not applied while aiming down
        /// sights, where the zoom is what the player asked for.
        /// </summary>
        [System.NonSerialized] public float FlightFovBonus;

        /// <summary>
        /// Extra metres the camera hangs back, while flying fast -- the camera is on
        /// a spring, not welded to the character. Set every frame by the jetpack; 0 on
        /// foot. Still pulled in by walls like the rest of the distance.
        /// </summary>
        [System.NonSerialized] public float FlightDistanceBonus;

        /// <summary>
        /// Set every frame by PlayerInputReader -- see its own TickCombat,
        /// the one place that already knows whether a gun is out and right
        /// is held, or a melee weapon's own WeaponAimInput.IsAiming says so.
        /// This component stays a plain camera rig with no opinion on which
        /// input or which equipped item means "aiming"; it only zooms.
        /// </summary>
        public bool IsAiming;

        [Header("Aiming feel (third-person-shooter research; live-tunable with /gun)")]
        [Tooltip(
            "Mouse-look scale while aiming down sights. Zoomed-in aim needs finer control -- every " +
            "shooter scales sensitivity down with the field of view. 1 = unchanged; 0.6 is the aim field of view over the rest one (30/50), " +
            "so a mouse movement shifts the picture the same on screen in and out of aim.")]
        public float AimSensitivityMultiplier = 0.6f;

        [Tooltip("Per second, how fast a gun's camera kick settles back to where you were aiming.")]
        public float RecoilRecoverPerSecond = 10.0f;

        [Tooltip("Per second, how fast the camera-roll jolt from a shot dies away.")]
        public float ShakeDecayPerSecond = 14.0f;

        [Tooltip("The camera never kicks up further than this from a burst, however fast you fire.")]
        public float MaxRecoilPitchDegrees = 14.0f;

        [Tooltip("The camera never kicks further sideways than this, degrees.")]
        public float MaxRecoilYawDegrees = 6.0f;

        [Tooltip("Sideways kick settles this many times faster than the climb.")]
        public float RecoilYawRecoverScale = 1.6f;

        [Tooltip("Per second, how fast the camera slides across when you swap shoulders.")]
        public float ShoulderSwapPerSecond = 6.0f;

        private float _yaw;
        private float _pitch = DefaultPitch;
        private PlayerLocomotion _player;

        // THE IMPACT SHAKE (owner, 2026-10-17: "the hits dont feel impactful at all"): a blow taken or landed jolts the picture for a quarter of
        // a second, a smooth wobble that dies away rather than a random jump (action games scale it with the blow). It moves only where the
        // camera is drawn from, never where it looks: aim, the crosshair and the hit traces are untouched, and it ends exactly where it began.
        private float _shakeMetres;
        private float _shakeSeconds;
        private float _shakeRemaining;
        private float _shakePhaseX;
        private float _shakePhaseY;

        /// <summary>The follow camera of this machine's own player (the last one enabled), or null: whoever shows a hit asks it to shake.</summary>
        public static ThirdPersonCamera Local { get; private set; }

        /// <summary>How far the shake has the picture off its place right now, metres (for tests).</summary>
        public Vector3 ShakeOffset { get; private set; }

        /// <summary>
        /// Jolts the picture by up to <paramref name="metres"/> (dying away over <paramref name="seconds"/>). A bigger shake replaces a smaller
        /// one still going; a smaller one is ignored, so a flurry of hits does not add up to a blur.
        /// </summary>
        public void AddShake(float metres, float seconds)
        {
            if (!(metres > 0.0f) || !(seconds > 0.0f))
            {
                return;
            }
            float going = _shakeSeconds > 0.0f ? _shakeMetres * Mathf.Clamp01(_shakeRemaining / _shakeSeconds) : 0.0f;
            if (metres <= going)
            {
                return;
            }
            _shakeMetres = metres;
            _shakeSeconds = seconds;
            _shakeRemaining = seconds;
            _shakePhaseX = Random.value * 6.2831f;
            _shakePhaseY = Random.value * 6.2831f;
        }

        /// <summary>This frame's shake, in the camera's own right and up: two wobbles of unequal rate, dying away with the square of the life left.</summary>
        private Vector3 TickShake(float deltaTime)
        {
            if (_shakeRemaining <= 0.0f)
            {
                ShakeOffset = Vector3.zero;
                return Vector3.zero;
            }
            _shakeRemaining -= deltaTime;
            float life = Mathf.Clamp01(_shakeRemaining / _shakeSeconds);
            float amplitude = _shakeMetres * life * life;
            float t = _shakeSeconds - _shakeRemaining;
            float x = Mathf.Sin(t * 2.0f * Mathf.PI * 23.0f + _shakePhaseX) * 0.7f + Mathf.Sin(t * 2.0f * Mathf.PI * 37.0f + _shakePhaseX * 1.3f) * 0.3f;
            float y = Mathf.Sin(t * 2.0f * Mathf.PI * 29.0f + _shakePhaseY) * 0.7f + Mathf.Sin(t * 2.0f * Mathf.PI * 41.0f + _shakePhaseY * 1.3f) * 0.3f;
            ShakeOffset = new Vector3(x * amplitude, y * amplitude, 0.0f);
            return ShakeOffset;
        }

        private void OnEnable()
        {
            Local = this;
        }

        private void OnDisable()
        {
            if (Local == this)
            {
                Local = null;
            }
        }
        private Camera _camera;
        private float _restNearClip;

        /// <summary>How many times its own size the followed body is (Grow and Shrink scale its transform); 1 for anything else.</summary>
        private float BodyScale => Target != null ? Mathf.Clamp(Target.lossyScale.y, 0.01f, 100.0f) : 1.0f;
        private float _restFieldOfView;

        /// <summary>The field of view when neither aiming nor flying, degrees (vertical): read off the Camera at Start, settable live (/camera fov).</summary>
        public float RestFieldOfView
        {
            get => _restFieldOfView;
            set => _restFieldOfView = value;
        }
        private float _currentShoulderOffset;

        // Camera recoil is a VISUAL OFFSET layered on the aim (never written
        // back into _pitch), so it always settles to exactly where you were
        // aiming -- no residual drift, "full recovery". Because the gun fires
        // along this camera's forward, a fast burst DOES ride the kick up, which
        // is what makes recoil something to manage; the first shot is always
        // exactly on the crosshair.
        private float _recoilPitch;
        private float _shakeRoll;
        private float _recoilYaw;
        private float _recoilHold;
        private float _aimCameraBlend;

        // +1 = camera over the right shoulder, -1 = left. Slides between them.
        private float _shoulderSide = 1.0f;
        private float _currentShoulderSide = 1.0f;

        /// <summary>Which shoulder the camera is over: +1 right, -1 left.</summary>
        public float ShoulderSide => _shoulderSide;

        /// <summary>Swaps the camera to the other shoulder (Q). Standard third-person-shooter tech: peek round a corner without exposing the body.</summary>
        public void ToggleShoulder()
        {
            _shoulderSide = -_shoulderSide;
        }

        /// <summary>Current camera kick, degrees up (read by tests and the HUD).</summary>
        public float RecoilPitch => _recoilPitch;

        /// <summary>Current sideways kick, degrees to the right (an automatic's spray wanders).</summary>
        public float RecoilYaw => _recoilYaw;

        /// <summary>
        /// Kicks the view up and jolts it. Applied to the camera only, and it
        /// recovers on its own -- see the field comment above.
        /// </summary>
        public void AddRecoil(float pitchUpDegrees, float shakeRollDegrees)
        {
            AddRecoil(pitchUpDegrees, shakeRollDegrees, 0.0f, 0.0f);
        }

        /// <summary>
        /// The full kick: up, to the right (negative: left), the roll jolt, and how long it is held before it settles. An automatic holds its
        /// kick through a burst (a shot every 0.1 s, a hold of 0.13 s), so a spray CLIMBS and wanders along the gun's pattern and must be pulled
        /// back down -- CS:GO's AK, learnable because it is the same every time; a pistol's settles at once between shots.
        /// </summary>
        public void AddRecoil(float pitchUpDegrees, float shakeRollDegrees, float yawRightDegrees, float holdSeconds)
        {
            _recoilPitch = Mathf.Min(MaxRecoilPitchDegrees, _recoilPitch + Mathf.Max(0.0f, pitchUpDegrees));
            _recoilYaw = Mathf.Clamp(_recoilYaw + yawRightDegrees, -MaxRecoilYawDegrees, MaxRecoilYawDegrees);
            _shakeRoll += (Random.value < 0.5f ? -1.0f : 1.0f) * shakeRollDegrees;
            _recoilHold = Mathf.Max(_recoilHold, holdSeconds);
        }

        private void DecayRecoil(float deltaTime)
        {
            if (_recoilHold > 0.0f)
            {
                _recoilHold -= deltaTime;   // still inside a burst: the kick stays where the pattern put it
            }
            else
            {
                float recover = 1.0f - Mathf.Exp(-RecoilRecoverPerSecond * deltaTime);
                _recoilPitch -= _recoilPitch * recover;
                if (_recoilPitch < 0.0005f)
                {
                    _recoilPitch = 0.0f;
                }
                // Sideways settles faster than the climb (the research: horizontal recoil recovers quicker, so the climb is what you fight).
                float recoverYaw = 1.0f - Mathf.Exp(-RecoilRecoverPerSecond * RecoilYawRecoverScale * deltaTime);
                _recoilYaw -= _recoilYaw * recoverYaw;
                if (Mathf.Abs(_recoilYaw) < 0.0005f)
                {
                    _recoilYaw = 0.0f;
                }
            }

            float shake = 1.0f - Mathf.Exp(-ShakeDecayPerSecond * deltaTime);
            _shakeRoll -= _shakeRoll * shake;
            if (Mathf.Abs(_shakeRoll) < 0.0005f)
            {
                _shakeRoll = 0.0f;
            }
        }

        /// <summary>
        /// Settable so IntroCutscene can hand the camera back on the same
        /// yaw/pitch it was left on when it re-enables this component --
        /// otherwise the very next LateUpdate would snap to whatever
        /// leftover _yaw/_pitch this component last held from before the
        /// cutscene disabled it, popping the view. Degrees, same convention
        /// LateUpdate's own Quaternion.Euler(pitch, yaw, 0) already uses.
        /// </summary>
        public float Yaw
        {
            get => _yaw;
            set => _yaw = value;
        }

        public float Pitch
        {
            get => _pitch;
            set => _pitch = value;
        }

        /// <summary>What is currently locked on, or null.</summary>
        public Transform LockedTarget { get; private set; }

        public bool IsLockedOn => LockedTarget != null;

        private int _lockVisits;
        private float _sightLost;
        private PlayerVitals _vitals;

        private void Start()
        {
            if (Target != null)
            {
                _player = Target.GetComponent<PlayerLocomotion>();
                _vitals = Target.GetComponent<PlayerVitals>();
            }
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                _restFieldOfView = _camera.fieldOfView;
                _restNearClip = _camera.nearClipPlane;
            }
            _currentShoulderOffset = ShoulderOffset;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // POSITIONED HERE TOO, NOT ONLY IN LateUpdate. Bug
            // REPORT, confirmed directly in CityMassingSceneBuilder: this
            // component lands on the SAME camera GameObject the FlyCamera
            // editor preview already positioned at a street-level walking
            // view for opening the scene without pressing play -- that
            // leftover transform is nowhere near the "4.5m behind the
            // shoulder" shot this component is actually meant to produce,
            // and until now nothing corrected it until the first LateUpdate
            // ran. Ordinarily that is one invisible frame. But
            // DevConsoleView.Open() (opened immediately on spawning in, to
            // gear up before playing -- the reported repro, every time)
            // disables this component from its own Update(), which runs
            // BEFORE LateUpdate in the same frame -- Unity skips a
            // component's remaining scheduled calls for a frame the instant
            // it is disabled, so LateUpdate never ran even once. The camera
            // was then stuck rendering from that raw, leftover position for
            // as long as the console stayed open -- coincident with the
            // player's own spawn point, looking wherever the editor preview
            // last faced -- which is a point-blank, warped close-up of
            // whatever part of the body happened to be in front of the
            // lens: exactly "opening the console to spawn the grappler"
            // looked like. Calling the same positioning logic once here
            // closes the window entirely rather than racing to win it.
            ApplyCameraTransform();
        }

        /// <summary>
        /// LateUpdate, not Update: the player moves in Update, and a camera that
        /// reads its position in the same phase is a frame behind, which reads
        /// as the body swimming against the camera.
        /// </summary>
        private void LateUpdate()
        {
            // WHILE LOCKED THE MOUSE IS IGNORED ENTIRELY. Blending a lock with
            // live look input means two things fighting over the same yaw, and
            // the visible result is a camera that will not settle. Godot
            // (player.gd _update_lock_on) takes the same line.
            if (IsLockedOn)
            {
                TrackLock();
            }
            else
            {
                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    float look = LookDegreesPerPixel * (IsAiming ? AimSensitivityMultiplier : 1.0f);
                    _yaw += delta.x * look;
                    _pitch = Mathf.Clamp(
                        _pitch - delta.y * look, MinPitch, MaxPitch);
                }
            }

            DecayRecoil(Time.deltaTime);

            // NO ESCAPE HANDLING HERE. There used to be a bare "Escape frees the
            // cursor" fallback, from before there was any menu to open with it.
            // PauseMenuView owns Escape exclusively now, and it matters that
            // this component does not also react to the same key: re-reading
            // Escape here would race PauseMenuView's own Close(), which also
            // sets Cursor.lockState in the same frame -- whichever ran last
            // would win by accident, sometimes leaving the cursor free right
            // after the menu closed.

            ApplyCameraTransform();
        }

        /// <summary>
        /// TAB (read by PlayerInputReader, behind its menu/console gating --
        /// NOT here, which is what raced the C-menu last time) acquires,
        /// cycles and eventually lets go: one button for all three.
        ///
        /// Not locked: grabs whoever is nearest the crosshair. Locked: steps
        /// to the next enemy in a left-to-right sweep; the press after the last
        /// one releases.
        /// </summary>
        public void PressLockOn()
        {
            if (Target == null)
            {
                return;
            }

            List<Transform> candidates = LockOn.Candidates(
                Target.position, transform.forward, LockableNearby());

            if (!IsLockedOn)
            {
                LockedTarget = LockOn.Acquire(Target.position, transform.forward, candidates);
                _lockVisits = LockedTarget != null ? 1 : 0;
                _sightLost = 0.0f;
                return;
            }

            _lockVisits++;
            LockedTarget = LockOn.Cycle(LockedTarget, candidates, _lockVisits);
            if (LockedTarget == null)
            {
                _lockVisits = 0;
            }
            _sightLost = 0.0f;
        }

        public void ReleaseLock()
        {
            LockedTarget = null;
            _lockVisits = 0;
            _sightLost = 0.0f;
        }

        /// <summary>
        /// Everything damageable near the player EXCEPT the body doing the
        /// looking (without that, pressing lock-on picks yourself -- the nearest
        /// thing to the crosshair that can be hit) and EXCEPT pedestrians and
        /// story NPCs: the ask was to lock onto ENEMIES, and a lock that keeps
        /// landing on passers-by makes cycling useless.
        /// </summary>
        private List<IDamageable> LockableNearby()
        {
            var found = new List<IDamageable>();
            var seen = new HashSet<IDamageable>();
            Collider[] hits = Physics.OverlapSphere(
                Target.position, LockOn.Range, ~0, QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                if (hit.transform == Target || hit.transform.IsChildOf(Target))
                {
                    continue;
                }
                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || !seen.Add(damageable))
                {
                    continue;
                }
                if (damageable is Civilian || damageable is UniqueNpc)
                {
                    continue;
                }
                found.Add(damageable);
            }
            return found;
        }

        /// <summary>The middle of a target's body -- what the camera should look at, not its feet.</summary>
        private static Vector3 AimPoint(Transform target)
        {
            var controller = target.GetComponentInChildren<CharacterController>();
            if (controller != null)
            {
                return controller.bounds.center;
            }
            var collider = target.GetComponentInChildren<Collider>();
            return collider != null ? collider.bounds.center : target.position + Vector3.up;
        }

        /// <summary>
        /// Swings toward the target, and decides whether the lock survives.
        ///
        /// The yaw is LERPED rather than snapped, so acquiring reads as the
        /// camera turning to look rather than as a cut. The body takes its
        /// facing from this yaw while locked (PlayerLocomotion, LockedOn), so
        /// WASD becomes strafing around the target: the movement is still
        /// camera-relative and the camera is now always looking at the enemy.
        /// </summary>
        private void TrackLock()
        {
            var damageable = LockedTarget != null ? LockedTarget.GetComponentInParent<IDamageable>() : null;
            bool playerDead = _vitals != null && _vitals.IsDead;
            float distance = Target != null && LockedTarget != null
                ? Vector3.Distance(Target.position, LockedTarget.position)
                : float.MaxValue;

            if (Target == null || LockedTarget == null || playerDead
                || damageable == null || !damageable.IsAlive
                || distance > LockOn.Range * LockOn.ReleaseSlack)
            {
                ReleaseLock();
                return;
            }

            float height = (_player != null ? _player.StandingHeight : 1.8f) * BodyScale;
            Vector3 eye = Target.position + Vector3.up * (height * PivotHeightFraction);
            Vector3 aim = AimPoint(LockedTarget);

            // Out of sight is forgiven briefly -- somebody ducking behind a
            // corner mid-fight should not cost the lock, but standing behind a
            // building should.
            Vector3 toAim = aim - eye;
            bool blocked = Physics.Raycast(eye, toAim.normalized, out RaycastHit hit,
                               toAim.magnitude, CollisionLayers, QueryTriggerInteraction.Ignore)
                           && !hit.transform.IsChildOf(LockedTarget)
                           && !hit.transform.IsChildOf(Target)
                           && hit.transform != LockedTarget;

            _sightLost = blocked ? _sightLost + Time.deltaTime : 0.0f;
            if (_sightLost > LockOn.SightGraceSeconds)
            {
                ReleaseLock();
                return;
            }

            LockOn.Bearing(eye, aim, out float wantedYaw, out float wantedPitch);

            float step = 1.0f - Mathf.Exp(-LockOn.CameraLerpSpeed * Time.deltaTime);
            _yaw = Mathf.LerpAngle(_yaw, wantedYaw, step);
            _pitch = Mathf.Lerp(_pitch, wantedPitch, step);
        }

        /// <summary>
        /// The whole camera-placement job -- pivot, orbit rotation, shoulder
        /// offset, wall-collision pull-in, field of view -- pulled out of
        /// LateUpdate so Start can run it once immediately (see Start's own
        /// doc comment for why a single un-positioned frame matters here).
        /// </summary>
        private void ApplyCameraTransform()
        {
            if (Target == null)
            {
                return;
            }

            bool followingRagdoll = Ragdoll != null && Ragdoll.IsLimp && Ragdoll.Torso != null;
            // In fast, out slower (Saints Row's 0.15 s into fine aim, 0.5 s back): a snap to the gun reads as readiness, an ease out as relief.
            bool aiming = IsAiming && !followingRagdoll;
            _aimCameraBlend = Mathf.MoveTowards(_aimCameraBlend, aiming ? 1.0f : 0.0f,
                (aiming ? AimCameraBlendPerSecond : AimCameraReleasePerSecond) * Time.deltaTime);
            _sprintBlend = Mathf.MoveTowards(_sprintBlend, IsSprinting && !aiming ? 1.0f : 0.0f, SprintBlendPerSecond * Time.deltaTime);

            // GROW AND SHRINK (BodySizeAbility): every distance of the shot is the body's, so a giant is framed as anyone is, from ten times as
            // far, and a tiny body from close in. Its near plane comes in with a tiny body, so a wall it walks along is not cut open.
            float scale = BodyScale;
            if (_camera != null && _restNearClip > 0.0f)
            {
                float near = _restNearClip * Mathf.Clamp(scale, 0.2f, 1.0f);
                if (!Mathf.Approximately(_camera.nearClipPlane, near))
                {
                    _camera.nearClipPlane = near;
                }
            }

            Vector3 pivot;
            if (followingRagdoll)
            {
                // .transform.position, NOT the Rigidbody's own .position --
                // the latter is the physics-synced value, which can lag a
                // frame behind whatever the Transform was just set to
                // (Ragdoll.RepositionToRagdollRest reads the same
                // .transform.position for the identical reason). A camera
                // reacting to where the body visually is should never wait
                // on a physics sync it has no other reason to depend on.
                pivot = Ragdoll.Torso.transform.position + Vector3.up * (RagdollPivotHeight * scale);
            }
            else
            {
                float height = (_player != null ? _player.StandingHeight : 1.8f) * scale;
                pivot = Target.position + Vector3.up * (height * PivotHeightFraction);
                // Aiming: a little higher, over the shoulder, and closer (below): eased over a quarter of a second, an S-curve.
                pivot += Vector3.up * (AimPivotRaise * AimCameraBlend * scale);
            }

            // The recoil kick and roll jolt ride on top of where you are
            // aiming; a positive kick raises the view, i.e. LOWERS the pitch, and a spray's sideways wander turns it.
            Quaternion rotation = Quaternion.Euler(_pitch - _recoilPitch, _yaw + _recoilYaw, _shakeRoll);

            // OFFSET THE DESTINATION, NOT THE COLLISION ORIGIN. The spherecast
            // below still starts from the centred chest pivot -- see its own
            // comment on why that specifically has to be inside the capsule --
            // and simply aims at the shifted point instead, so a wall directly
            // behind the player's own centreline still pulls the shoulder-shot
            // camera in correctly rather than missing it because the cast
            // started somewhere already past the collider.
            _currentShoulderOffset = Mathf.MoveTowards(
                _currentShoulderOffset, IsAiming ? AimShoulderOffset : ShoulderOffset,
                AimShoulderOffsetMetresPerSecond * Time.deltaTime);
            _currentShoulderSide = Mathf.MoveTowards(
                _currentShoulderSide, _shoulderSide, ShoulderSwapPerSecond * Time.deltaTime);
            // Over the head (FramingHeight): the body sits low in the frame and the middle of the screen shows what is in front. Aiming eases
            // down to its own over-the-shoulder framing.
            float raise = FramingHeight * (1.0f - AimCameraBlend) * scale;
            Vector3 shoulderPivot = pivot + rotation * new Vector3(_currentShoulderOffset * _currentShoulderSide * scale, raise, 0.0f);
            float distance = Mathf.Lerp(Distance + FlightDistanceBonus, AimDistance, AimCameraBlend) * scale;
            Vector3 wanted = shoulderPivot - rotation * Vector3.forward * distance;

            // Pull in if a wall is in the way. The sphere radius keeps the near
            // plane out of the surface rather than flush against it, which is
            // what causes see-through corners.
            //
            // SphereCastAll rather than SphereCast because the cast STARTS
            // INSIDE THE PLAYER'S OWN CAPSULE -- the pivot is at chest height on
            // the body. A plain cast reports that capsule as the nearest hit and
            // pins the camera at minimum distance forever, which looks like the
            // camera being broken rather than like a collision bug.
            // The shoulder offset makes pivot-to-wanted slightly longer than
            // Distance itself (the hypotenuse of Distance and ShoulderOffset)
            // -- casting only as far as the flat Distance would leave the
            // last few centimetres in front of the actual camera unchecked.
            float wantedDistance = Vector3.Distance(pivot, wanted);
            Vector3 direction = (wanted - pivot).normalized;
            RaycastHit[] hits = Physics.SphereCastAll(
                pivot, 0.25f * scale, direction, wantedDistance, CollisionLayers & CityX.World.GlassFracture.AllButShards, QueryTriggerInteraction.Ignore);   // falling glass does not push the camera in

            float nearest = wantedDistance;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(Target) || hit.transform == Target)
                {
                    continue;
                }
                // A zero distance means the sphere began already overlapping,
                // which carries no useful direction information.
                if (hit.distance > 0.01f && hit.distance < nearest)
                {
                    nearest = hit.distance;
                }
            }

            if (nearest < wantedDistance)
            {
                wanted = pivot + direction * Mathf.Max(MinDistance * scale, nearest - 0.1f * scale);
            }

            // The impact shake, last: after the walls have had their say, and only on where the picture is taken from.
            wanted += rotation * (TickShake(Time.deltaTime) * scale);

            transform.position = wanted;
            transform.rotation = rotation;

            if (_camera != null)
            {
                // The sprint's widening rides on the rest field of view and blends on its own clock (_sprintBlend); the flight bonus the same.
                float rest = _restFieldOfView * Mathf.Lerp(1.0f, SprintFieldOfViewScale, _sprintBlend) + FlightFovBonus;
                float target = IsAiming ? AimFieldOfView : rest;
                _camera.fieldOfView = Mathf.MoveTowards(
                    _camera.fieldOfView, target, (IsAiming ? AimZoomDegreesPerSecond : AimZoomOutDegreesPerSecond) * Time.deltaTime);
            }
        }
    }
}
