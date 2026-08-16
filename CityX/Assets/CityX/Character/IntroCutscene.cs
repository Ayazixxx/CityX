using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// Runs once, the very first time a genuinely new character enters the
    /// world: falls from the sky playing "Falling" (looped) while ruins1.mp3
    /// plays and the fog thins as the character descends, lands at exactly
    /// FallDurationSeconds and plays "FallingFlatImpact" (one-shot), holds,
    /// plays "GettingUp" (one-shot), then "LookingAround" (one-shot, a
    /// "taking in the new world" beat) during which the camera runs its own
    /// 3-beat sequence, then lerps the camera the rest of the way down to
    /// normal gameplay framing and hands control back. Ported from
    /// player/intro_cutscene.gd.
    ///
    /// SAME ORCHESTRATION SHAPE AS CLAIMCUTSCENE: a MonoBehaviour added to
    /// the player, ticked from its own Update, driving the scene's single
    /// shared Camera transform directly while ThirdPersonCamera and
    /// PlayerInputReader are disabled for the duration (see
    /// ClaimSystem.StartCutscene's own precedent) -- not a second Camera,
    /// which would need its own AudioListener and Unity only tolerates one
    /// enabled at a time.
    ///
    /// PITCH SIGN IS THE OPPOSITE OF GODOT'S OWN VALUES, deliberately. This
    /// project's ThirdPersonCamera computes camera position as
    /// `pivot - Quaternion.Euler(pitch, yaw, 0) * Vector3.forward *
    /// distance` -- working the sign out from that formula directly (not
    /// assumed): a POSITIVE pitch here rotates +Z toward -Y, so a large
    /// positive pitch swings the camera UP and ABOVE the pivot, looking
    /// DOWN at it. Godot's SpringArm3D reaches the identical visual result
    /// from a NEGATIVE camera_pivot pitch (see intro_cutscene.gd's own
    /// comment on OVERHEAD_PITCH_DEGREES). Every pitch constant below is
    /// the Godot value with its sign flipped for that reason; yaw carries
    /// over unchanged, matching ClaimCutsceneMath's own established note
    /// that Godot and Unity agree on rotation direction for yaw in this
    /// project.
    ///
    /// NO HIP-ROTATION RESET, unlike intro_cutscene.gd's own
    /// reset_hips_rotation() call. That existed because Godot's
    /// AnimationPlayer only writes the tracks a clip actually defines, so a
    /// rotation track "Falling" alone carries (its dive lean) stays applied
    /// forever once nothing else ever touches that same track again. This
    /// project's Humanoid-retargeted Mecanim clips don't have that failure
    /// mode: every clip on a Humanoid avatar defines a value for every
    /// muscle each frame, so the very next state (FallingFlatImpact, then
    /// Locomotion) fully replaces the pose rather than leaving anything
    /// behind.
    ///
    /// NO PORTRAIT-RACE WAIT NEEDED EITHER, for the equivalent reason
    /// intro_cutscene.gd itself gives for why ITS wait is safe to place
    /// after the positional teleport: PlayerHud's own CapturePortrait
    /// coroutine waits two frames before finding the head renderer and
    /// reading its CURRENT world bounds, so it is immune to where the body
    /// has moved to by then. What it is NOT immune to is which ANIMATION
    /// is playing at that moment -- so exactly like the Godot original,
    /// Begin() moves the player to altitude and arms the fall tween
    /// immediately, but the "Falling" clip itself does not start playing
    /// until PortraitCaptureDelayFrames frames later.
    /// </summary>
    public sealed class IntroCutscene : MonoBehaviour
    {
        // --- timing, ported from intro_cutscene.gd's own named constants ---

        public const float StartAltitude = 300.0f;
        public const float FallDurationSeconds = 15.0f;
        public const float ImpactHoldSeconds = 2.5f;
        public const float HandoffDurationSeconds = 1.5f;
        public const float CutsceneSpringLength = 9.0f;

        public const float OverheadSpringLength = 10.0f;
        public const float OverheadPitchDegrees = 80.0f;
        public const float CamSwitchLeadSeconds = 2.5f;
        public const float CamSwitchDurationSeconds = 1.5f;

        public const float FallDriftSpeed = 4.0f;
        public const float MaxDriftDistance = 10.0f;

        /// <summary>How many frames the falling ANIMATION waits before starting, so it never races PlayerHud's own 2-frame portrait capture. See this class's own doc comment.</summary>
        public const int PortraitCaptureDelayFrames = 6;

        // --- real baked clip lengths, read directly off Player.prefab's own
        // --- Animator states (every clip on this rig shares Mixamo's
        // --- generic "mixamo.com" internal take name, which makes a
        // --- runtime name lookup useless -- these are read once from the
        // --- built controller and hardcoded here the same way Godot's own
        // --- tests/test_intro_cutscene_check.gd documents its clip
        // --- lengths as known constants rather than deriving them live). ---
        public const float ImpactClipSeconds = 0.767f;
        public const float GettingUpClipSeconds = 7.6f;
        public const float LookingAroundClipSeconds = 12.667f;

        // --- fog. This project's fog is global, distance-based
        // --- (RenderSettings.fogStartDistance/fogEndDistance), not a
        // --- per-camera height-fog override the way Godot's
        // --- Camera3D.environment is -- there is no equivalent to scope a
        // --- fog change to just this camera, so this pushes the shared
        // --- values down to something dense for the fall and lerps them
        // --- back to whatever they were, rather than trying to reproduce
        // --- height fog's exact shape. ---
        public const float FallFogStartDistance = 4.0f;
        public const float FallFogEndDistance = 20.0f;
        public const float FogFadeDurationSeconds = 5.0f;

        // --- "Looking Around" 3-beat camera sequence. Durations sum to
        // --- 11.0s, inside LookingAroundClipSeconds's own 12.667s with
        // --- margin, so the rotate is always fully settled before the
        // --- clip's own end triggers handoff. ---
        public const float LookingAroundShotAHoldSeconds = 3.5f;
        public const float LookingAroundShotBHoldSeconds = 3.5f;
        public const float LookingAroundRotateDurationSeconds = 4.0f;
        public const float LookingAroundShotAYawDegrees = 150.0f;
        public const float LookingAroundShotAPitchDegrees = 8.0f;
        public const float LookingAroundShotASpringLength = 5.5f;
        public const float LookingAroundShotBYawDegrees = -130.0f;
        public const float LookingAroundShotBPitchDegrees = 35.0f;
        public const float LookingAroundShotBSpringLength = 10.5f;

        /// <summary>30% of unity gain -- GroundImpactSound's own real explicit ask, matching Godot's GROUND_IMPACT_VOLUME_LINEAR.</summary>
        public const float GroundImpactVolumeLinear = 0.3f;

        public event System.Action Finished;

        [Tooltip("Reparented onto the player, not this cutscene, so it survives past handoff and plays out to its own natural end.")]
        public AudioClip RuinsMusic;

        public AudioClip GroundImpactSound;

        private enum Phase { Falling, Impact, Hold, GettingUp, LookingAround, HandingOff, Done }

        private CharacterController _controller;
        private ThirdPersonCamera _thirdPersonCamera;
        private Animator _animator;
        private Transform _camera;

        private Vector3 _spawnPosition;
        private float _originalSpringLength;
        private float _pivotHeight;

        private Phase _phase;
        private float _phaseElapsed;
        private int _framesWaited;
        private bool _fallingClipStarted;
        private bool _driftActive;
        private bool _cameraSwitching;
        private float _cameraSwitchElapsed;
        private bool _fogFadeStarted;
        private float _fogFadeElapsed;
        private float _normalFogStart;
        private float _normalFogEnd;

        // Looking-around's own 3-beat sub-schedule, ticked only while
        // _phase == LookingAround.
        private enum LookBeat { ShotA, ShotB, Rotating, Settled }
        private LookBeat _lookBeat;
        private float _lookBeatElapsed;

        private AudioSource _musicSource;

        /// <summary>Public so a test can confirm the portrait-race delay is real rather than just not-throwing.</summary>
        public bool FallingClipStarted => _fallingClipStarted;

        /// <summary>
        /// Starts the cutscene. Called right after AddComponent, mirroring
        /// ClaimSystem.StartCutscene's own precedent -- the caller is
        /// responsible for disabling ThirdPersonCamera and PlayerInputReader
        /// first and re-enabling them from the Finished handler. Not taken
        /// as a parameter here: nothing in this class ever touches
        /// PlayerInputReader itself.
        /// </summary>
        public void Begin(CharacterController controller, ThirdPersonCamera thirdPersonCamera,
            Animator animator, Transform cameraTransform, PlayerLocomotion locomotion)
        {
            _controller = controller;
            _thirdPersonCamera = thirdPersonCamera;
            _animator = animator;
            _camera = cameraTransform;

            _spawnPosition = transform.position;
            _originalSpringLength = thirdPersonCamera.Distance;
            float height = locomotion != null ? locomotion.StandingHeight : 1.8f;
            _pivotHeight = height * thirdPersonCamera.PivotHeightFraction;

            // EVERYTHING POSITIONAL HAPPENS SYNCHRONOUSLY -- see this
            // class's own doc comment on why the falling animation's own
            // start is delayed instead.
            if (_controller != null)
            {
                _controller.enabled = false;
            }
            transform.position = _spawnPosition + Vector3.up * StartAltitude;
            if (_controller != null)
            {
                _controller.enabled = true;
                // The CharacterController desync bug: a raw transform.position
                // write is invisible to the very next Move() call without this.
                Physics.SyncTransforms();
            }

            _normalFogStart = RenderSettings.fogStartDistance;
            _normalFogEnd = RenderSettings.fogEndDistance;
            RenderSettings.fogStartDistance = FallFogStartDistance;
            RenderSettings.fogEndDistance = FallFogEndDistance;

            _thirdPersonCamera.Distance = OverheadSpringLength;
            _thirdPersonCamera.Pitch = OverheadPitchDegrees;
            _thirdPersonCamera.Yaw = 0.0f;
            PlaceCamera(OverheadSpringLength, OverheadPitchDegrees, 0.0f);

            if (RuinsMusic != null)
            {
                var musicObject = new GameObject("IntroCutsceneMusic");
                musicObject.transform.SetParent(transform.parent, false);
                _musicSource = musicObject.AddComponent<AudioSource>();
                _musicSource.clip = RuinsMusic;
                _musicSource.spatialBlend = 0.0f;
                _musicSource.loop = false;
                _musicSource.Play();
                // Not this cutscene's own destruction -- see this class's
                // own [Tooltip] on RuinsMusic. Self-frees once the track
                // actually finishes.
                Destroy(musicObject, RuinsMusic.length);
            }

            _phase = Phase.Falling;
            _phaseElapsed = 0.0f;
            _framesWaited = 0;
            _fallingClipStarted = false;
            _driftActive = true;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// Public and taking its own deltaTime for the same reason
        /// ClaimCutscene.Tick already does -- a test can fast-forward
        /// through the whole ~40 second sequence without waiting on real
        /// wall-clock seconds.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_phase == Phase.Done)
            {
                return;
            }

            if (!_fallingClipStarted)
            {
                _framesWaited++;
                if (_framesWaited > PortraitCaptureDelayFrames)
                {
                    _animator.Play("Falling", 0, 0.0f);
                    _fallingClipStarted = true;
                }
            }

            if (_driftActive)
            {
                TickDrift(deltaTime);
            }
            if (_cameraSwitching)
            {
                TickCameraSwitch(deltaTime);
            }
            if (_fogFadeStarted && _fogFadeElapsed < FogFadeDurationSeconds)
            {
                TickFogFade(deltaTime);
            }

            switch (_phase)
            {
                case Phase.Falling:
                    TickFalling(deltaTime);
                    break;
                case Phase.Impact:
                    TickImpact(deltaTime);
                    break;
                case Phase.Hold:
                    TickHold(deltaTime);
                    break;
                case Phase.GettingUp:
                    TickGettingUp(deltaTime);
                    break;
                case Phase.LookingAround:
                    TickLookingAround(deltaTime);
                    break;
                case Phase.HandingOff:
                    TickHandoff(deltaTime);
                    break;
            }
        }

        // --- Falling ---

        private void TickFalling(float deltaTime)
        {
            _phaseElapsed += deltaTime;
            float t = Mathf.Clamp01(_phaseElapsed / FallDurationSeconds);
            // Quad ease-in -- a single continuously-accelerating fall, no
            // slow-down flourish near impact (a real reported Godot bug;
            // see intro_cutscene.gd's own class comment).
            float eased = t * t;
            float y = Mathf.Lerp(_spawnPosition.y + StartAltitude, _spawnPosition.y, eased);
            SetPlayerY(y);
            // Re-anchored to the player EVERY frame, not just while
            // TickCameraSwitch is active. Without this the camera is only
            // ever placed once, in Begin() -- it sits frozen at the
            // spawn-altitude framing for the whole fall and only starts
            // tracking again in the last CamSwitchLeadSeconds, which reads
            // exactly like "the camera doesn't follow the player down."
            PlaceCamera(_thirdPersonCamera.Distance, _thirdPersonCamera.Pitch, _thirdPersonCamera.Yaw);

            if (!_cameraSwitching && _phaseElapsed >= FallDurationSeconds - CamSwitchLeadSeconds)
            {
                _cameraSwitching = true;
                _cameraSwitchElapsed = 0.0f;
                _driftActive = false;
            }

            if (t >= 1.0f)
            {
                StartImpact();
            }
        }

        /// <summary>
        /// Fortnite-Battle-Bus-style horizontal steering. World-axis
        /// relative, not camera-relative -- the overhead camera never
        /// rotates with mouse look during this window (PlayerInputReader is
        /// disabled for the whole cutscene), so there is no live facing to
        /// steer relative to.
        /// </summary>
        private void TickDrift(float deltaTime)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1.0f;
            if (keyboard.sKey.isPressed) input.y -= 1.0f;
            if (keyboard.dKey.isPressed) input.x += 1.0f;
            if (keyboard.aKey.isPressed) input.x -= 1.0f;
            if (input.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 position = transform.position;
            Vector3 moved = position + new Vector3(input.x, 0.0f, input.y).normalized * FallDriftSpeed * deltaTime;
            var offset = new Vector2(moved.x - _spawnPosition.x, moved.z - _spawnPosition.z);
            if (offset.magnitude > MaxDriftDistance)
            {
                offset = offset.normalized * MaxDriftDistance;
                moved.x = _spawnPosition.x + offset.x;
                moved.z = _spawnPosition.z + offset.y;
            }

            if (_controller != null)
            {
                _controller.enabled = false;
            }
            transform.position = new Vector3(moved.x, position.y, moved.z);
            if (_controller != null)
            {
                _controller.enabled = true;
            }
        }

        /// <summary>
        /// Swings the camera back down from the overhead freefall view to
        /// the normal cinematic framing -- see this class's own doc comment
        /// on the pitch sign.
        /// </summary>
        private void TickCameraSwitch(float deltaTime)
        {
            _cameraSwitchElapsed += deltaTime;
            float t = Mathf.Clamp01(_cameraSwitchElapsed / CamSwitchDurationSeconds);
            float springLength = Mathf.Lerp(OverheadSpringLength, CutsceneSpringLength, t);
            float pitch = Mathf.Lerp(OverheadPitchDegrees, 0.0f, t);
            _thirdPersonCamera.Distance = springLength;
            _thirdPersonCamera.Pitch = pitch;
            // Placed by TickFalling's own unconditional call, right after it
            // moves the player for this same frame -- not here, which would
            // place it a frame stale (TickCameraSwitch runs before TickFalling
            // in Tick()'s own ordering).

            if (t >= 1.0f)
            {
                _cameraSwitching = false;
            }
        }

        // --- Impact / hold / getting up ---

        private void StartImpact()
        {
            _phase = Phase.Impact;
            _phaseElapsed = 0.0f;
            _animator.Play("FallingFlatImpact", 0, 0.0f);
            if (GroundImpactSound != null)
            {
                AudioSource.PlayClipAtPoint(GroundImpactSound, transform.position,
                    GroundImpactVolumeLinear);
            }
        }

        private void TickImpact(float deltaTime)
        {
            _phaseElapsed += deltaTime;
            if (_phaseElapsed >= ImpactClipSeconds)
            {
                _phase = Phase.Hold;
                _phaseElapsed = 0.0f;
            }
        }

        private void TickHold(float deltaTime)
        {
            _phaseElapsed += deltaTime;
            if (_phaseElapsed >= ImpactHoldSeconds)
            {
                _phase = Phase.GettingUp;
                _phaseElapsed = 0.0f;
                _animator.Play("GettingUp", 0, 0.0f);
            }
        }

        private void TickGettingUp(float deltaTime)
        {
            _phaseElapsed += deltaTime;
            if (_phaseElapsed >= GettingUpClipSeconds)
            {
                StartLookingAround();
            }
        }

        // --- Looking around ---

        /// <summary>
        /// "Looking Around" always plays here -- confirmed placement: a
        /// "taking in the new world" beat between standing up and gameplay
        /// starting.
        /// </summary>
        private void StartLookingAround()
        {
            _phase = Phase.LookingAround;
            _phaseElapsed = 0.0f;
            _animator.Play("LookingAround", 0, 0.0f);

            _lookBeat = LookBeat.ShotA;
            _lookBeatElapsed = 0.0f;
            _thirdPersonCamera.Yaw = LookingAroundShotAYawDegrees;
            _thirdPersonCamera.Pitch = LookingAroundShotAPitchDegrees;
            _thirdPersonCamera.Distance = LookingAroundShotASpringLength;
            PlaceCamera(LookingAroundShotASpringLength, LookingAroundShotAPitchDegrees,
                LookingAroundShotAYawDegrees);
        }

        private void TickLookingAround(float deltaTime)
        {
            _phaseElapsed += deltaTime;
            TickLookAroundCamera(deltaTime);

            if (_phaseElapsed >= LookingAroundClipSeconds)
            {
                StartHandoff();
            }
        }

        private void TickLookAroundCamera(float deltaTime)
        {
            switch (_lookBeat)
            {
                case LookBeat.ShotA:
                    _lookBeatElapsed += deltaTime;
                    if (_lookBeatElapsed >= LookingAroundShotAHoldSeconds)
                    {
                        _lookBeat = LookBeat.ShotB;
                        _lookBeatElapsed = 0.0f;
                        _thirdPersonCamera.Yaw = LookingAroundShotBYawDegrees;
                        _thirdPersonCamera.Pitch = LookingAroundShotBPitchDegrees;
                        _thirdPersonCamera.Distance = LookingAroundShotBSpringLength;
                        PlaceCamera(LookingAroundShotBSpringLength, LookingAroundShotBPitchDegrees,
                            LookingAroundShotBYawDegrees);
                    }
                    break;

                case LookBeat.ShotB:
                    _lookBeatElapsed += deltaTime;
                    if (_lookBeatElapsed >= LookingAroundShotBHoldSeconds)
                    {
                        _lookBeat = LookBeat.Rotating;
                        _lookBeatElapsed = 0.0f;
                        // THE fog cue -- see FogFadeDurationSeconds's own
                        // comment. Starts on the third beat, as the camera
                        // begins turning back to normal framing.
                        StartFogFade();
                    }
                    break;

                case LookBeat.Rotating:
                    _lookBeatElapsed += deltaTime;
                    float t = Mathf.Clamp01(_lookBeatElapsed / LookingAroundRotateDurationSeconds);
                    float yaw = Mathf.LerpAngle(LookingAroundShotBYawDegrees, 0.0f, t);
                    float pitch = Mathf.Lerp(LookingAroundShotBPitchDegrees, 0.0f, t);
                    float distance = Mathf.Lerp(LookingAroundShotBSpringLength, CutsceneSpringLength, t);
                    _thirdPersonCamera.Yaw = yaw;
                    _thirdPersonCamera.Pitch = pitch;
                    _thirdPersonCamera.Distance = distance;
                    PlaceCamera(distance, pitch, yaw);
                    if (t >= 1.0f)
                    {
                        _lookBeat = LookBeat.Settled;
                    }
                    break;

                case LookBeat.Settled:
                    break;
            }
        }

        // --- Fog ---

        private void StartFogFade()
        {
            // Idempotent -- also called at handoff as a safety net, same
            // reasoning as _start_fog_fade's own comment in the original.
            if (_fogFadeStarted)
            {
                return;
            }
            _fogFadeStarted = true;
            _fogFadeElapsed = 0.0f;
        }

        private void TickFogFade(float deltaTime)
        {
            _fogFadeElapsed += deltaTime;
            float t = Mathf.Clamp01(_fogFadeElapsed / FogFadeDurationSeconds);
            RenderSettings.fogStartDistance = Mathf.Lerp(FallFogStartDistance, _normalFogStart, t);
            RenderSettings.fogEndDistance = Mathf.Lerp(FallFogEndDistance, _normalFogEnd, t);
        }

        // --- Handoff ---

        private void StartHandoff()
        {
            _phase = Phase.HandingOff;
            _phaseElapsed = 0.0f;
            StartFogFade();
        }

        private void TickHandoff(float deltaTime)
        {
            _phaseElapsed += deltaTime;
            float t = Mathf.Clamp01(_phaseElapsed / HandoffDurationSeconds);
            float distance = Mathf.Lerp(CutsceneSpringLength, _originalSpringLength, t);
            _thirdPersonCamera.Distance = distance;
            _thirdPersonCamera.Yaw = 0.0f;
            _thirdPersonCamera.Pitch = 0.0f;
            PlaceCamera(distance, 0.0f, 0.0f);

            if (t >= 1.0f)
            {
                Finish();
            }
        }

        private void Finish()
        {
            _phase = Phase.Done;
            RenderSettings.fogStartDistance = _normalFogStart;
            RenderSettings.fogEndDistance = _normalFogEnd;
            _animator.Play("Locomotion", 0, 0.0f);
            if (_controller != null)
            {
                Physics.SyncTransforms();
            }
            Finished?.Invoke();
        }

        // --- shared helpers ---

        private void SetPlayerY(float y)
        {
            Vector3 position = transform.position;
            if (_controller != null)
            {
                _controller.enabled = false;
            }
            transform.position = new Vector3(position.x, y, position.z);
            if (_controller != null)
            {
                _controller.enabled = true;
            }
        }

        /// <summary>
        /// Same pivot/rotation formula ThirdPersonCamera.LateUpdate itself
        /// uses, reused here rather than duplicated blind -- see this
        /// class's own doc comment on the pitch sign for how that formula
        /// was worked out.
        /// </summary>
        private void PlaceCamera(float distance, float pitch, float yaw)
        {
            Vector3 pivot = transform.position + Vector3.up * _pivotHeight;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0.0f);
            _camera.position = pivot - rotation * Vector3.forward * distance;
            _camera.rotation = rotation;
        }
    }
}
