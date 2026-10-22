using CityX.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// A new character's entrance (ported from player/intro_cutscene.gd, then made physical): the body falls from the sky as a ragdoll,
    /// through cloud that thins as it nears the city, hits the ground as a ragdoll, lies a moment, gets up (the ragdoll's own recovery, at a
    /// cinematic pace), looks around while the camera takes in the new world, and hands control back standing on the ground.
    ///
    /// Owner, 2026-10-20: "make it so you are ragdolling falling from the sky instead of an animation and make the fall impact as a ragdoll as
    /// well", "there is a jump cut in the cutscene mid fall right before the impact" (the scripted fall cut from a dive clip to a landing
    /// clip), "make it so there is some fog but it fades as you get closer to the surface like you are falling through the clouds", and "make
    /// it so your feet are on the ground when you take over control". 2026-10-22: "make it so you are laying flat horizontal during the fall
    /// ... and make some amount of flailing in the air like there is wind" -- the body is turned onto its back and the wind throws its arms
    /// and legs about (FallFlail).
    ///
    /// Everything during the fall follows the falling body's height above the ground, not a clock: the camera swings from looking down on
    /// the body to beside it as it nears the ground, and the cloud clears, so nothing jumps whatever the physics does. The camera follows a
    /// smoothed point (the body while it falls and lies, the standing player after), so changing what it follows never cuts.
    ///
    /// The scene's one Camera is driven directly while ThirdPersonCamera and PlayerInputReader are disabled (IntroCutsceneStarter), as
    /// ClaimCutscene does; the ragdoll is ticked here, since PlayerInputReader (which ticks it in play) is off.
    ///
    /// PITCH SIGN: ThirdPersonCamera puts the camera at pivot - Euler(pitch, yaw, 0) * forward * distance, so a positive pitch is above the
    /// pivot looking down (Godot's values had the other sign).
    /// </summary>
    public sealed class IntroCutscene : MonoBehaviour
    {
        public const float StartAltitude = 300.0f;

        /// <summary>
        /// How many frames the body hangs, standing, before it goes limp: PlayerHud captures the portrait off the head two frames in, and a
        /// tumbling head would be captured turned.
        /// </summary>
        public const int PortraitCaptureDelayFrames = 6;

        /// <summary>The air's drag on the falling body: about 35 m/s at the most (9.81 / this), so the fall from StartAltitude takes about 10 s.</summary>
        public const float FallDrag = 0.28f;

        /// <summary>The push the body is let go with: a slow tumble from the start.</summary>
        public static readonly Vector3 LetGoVelocity = new Vector3(0.0f, -3.0f, 0.0f);

        public const float FallDriftSpeed = 4.0f;
        public const float MaxDriftDistance = 10.0f;

        /// <summary>How fast the body's sideways speed comes round to what the drift keys ask (per second).</summary>
        public const float DriftResponse = 3.0f;

        // --- the cloud: thick fog high up, thinning to the scene's own as the body nears the ground ---
        public const float CloudFogStartDistance = 4.0f;
        public const float CloudFogEndDistance = 30.0f;

        /// <summary>Above this height over the ground the body is in the cloud; below <see cref="CloudClearAltitude"/> it is clear.</summary>
        public const float CloudThickAltitude = 250.0f;
        public const float CloudClearAltitude = 80.0f;

        // --- the camera during the fall: looking down on the body, then beside it as it nears the ground ---
        public const float OverheadSpringLength = 10.0f;
        public const float OverheadPitchDegrees = 80.0f;
        public const float BesideSpringLength = 9.0f;
        public const float BesidePitchDegrees = 15.0f;

        /// <summary>The camera swings from overhead to beside the body over this stretch of height above the ground.</summary>
        public const float CameraSwingTopAltitude = 45.0f;
        public const float CameraSwingBottomAltitude = 4.0f;

        /// <summary>How quickly the camera's followed point catches up with the body (per second): smooth, a few metres behind at full speed.</summary>
        public const float CameraFollowRate = 10.0f;

        // --- the landing ---

        /// <summary>A landing: the body was falling faster than this, and lost more than <see cref="ImpactSpeedLoss"/> of its speed in a frame.</summary>
        public const float ImpactSpeed = 8.0f;
        public const float ImpactSpeedLoss = 0.5f;

        /// <summary>How long the body lies still before it gets up; at the most <see cref="MaxLyingSeconds"/> after the landing, still or not.</summary>
        public const float LyingStillSeconds = 2.0f;
        public const float MaxLyingSeconds = 5.0f;

        /// <summary>How long the whole get-up clip takes here: its own length, a slow waking-up, against a quick recovery in play.</summary>
        public const float GettingUpPaceSeconds = GettingUpClipSeconds;

        /// <summary>A body fallen through the ground (a collider not there yet) is put back on it at this height over where it should have landed.</summary>
        public const float FellThroughMargin = 3.0f;

        // --- the baked clips' real lengths, read off the built controller (every clip shares Mixamo's take name) ---
        public const float GettingUpClipSeconds = 7.6f;
        public const float LookingAroundClipSeconds = 12.667f;

        /// <summary>The stood-up body eases into looking around over this long, rather than popping into the clip's first frame.</summary>
        public const float LookingAroundBlendSeconds = 0.4f;

        public const float HandoffDurationSeconds = 1.5f;
        public const float CutsceneSpringLength = 9.0f;

        /// <summary>
        /// The "looking around" beat's camera, glided through and never cut to: from beside the risen body round to its face, over to a high
        /// view of the city, then back behind it at the gameplay framing (11 s, inside the clip's 12.667 s).
        /// </summary>
        private static readonly CameraKey[] LookingAroundCamera =
        {
            new CameraKey(0.0f, BesideSpringLength, BesidePitchDegrees, 0.0f),
            new CameraKey(2.0f, 5.5f, 8.0f, 150.0f),
            new CameraKey(3.5f, 5.5f, 8.0f, 150.0f),
            new CameraKey(6.0f, 10.5f, 35.0f, -130.0f),
            new CameraKey(7.0f, 10.5f, 35.0f, -130.0f),
            new CameraKey(11.0f, CutsceneSpringLength, 0.0f, 0.0f),
        };

        private readonly struct CameraKey
        {
            public readonly float Seconds, Distance, Pitch, Yaw;

            public CameraKey(float seconds, float distance, float pitch, float yaw)
            {
                Seconds = seconds;
                Distance = distance;
                Pitch = pitch;
                Yaw = yaw;
            }
        }

        /// <summary>30% of unity gain, as Godot's GROUND_IMPACT_VOLUME_LINEAR.</summary>
        public const float GroundImpactVolumeLinear = 0.3f;

        /// <summary>How far down from over the player's feet the ground is looked for at hand-off.</summary>
        public const float GroundSearch = 3.0f;

        public event System.Action Finished;

        [Tooltip("Reparented onto the player, not this cutscene, so it survives past handoff and plays out to its own natural end.")]
        public AudioClip RuinsMusic;

        public AudioClip GroundImpactSound;

        private enum Phase { Hanging, Falling, Lying, GettingUp, LookingAround, HandingOff, Done }

        private CharacterController _controller;
        private ThirdPersonCamera _thirdPersonCamera;
        private Animator _animator;
        private Transform _camera;
        private Ragdoll _ragdoll;

        private Vector3 _spawnPosition;
        private float _originalSpringLength;
        private float _pivotHeight;
        private Vector3 _followed;

        private Phase _phase;
        private float _phaseElapsed;
        private int _framesWaited;
        private float _lastSpeed;
        private float _stillSeconds;
        private bool _landed;

        private float _normalFogStart;
        private float _normalFogEnd;


        private float[] _savedDrag;
        private CollisionDetectionMode[] _savedCollision;

        // The parts the fall turns and the wind throws about (FallFlail), found on the ragdoll's bones as it is let go.
        private Transform _hipsBone, _neckBone, _leftShoulder, _rightShoulder;
        private Rigidbody _chest;
        private readonly Rigidbody[] _limbEnds = new Rigidbody[FallFlail.LimbCount];
        private readonly Rigidbody[] _limbRoots = new Rigidbody[FallFlail.LimbCount];

        /// <summary>True once the body has gone limp and is falling.</summary>
        public bool IsFalling => _phase == Phase.Falling;

        /// <summary>True once the body has hit the ground.</summary>
        public bool HasLanded => _landed;

        /// <summary>
        /// Starts the cutscene. Called right after AddComponent; the caller disables ThirdPersonCamera and PlayerInputReader first and
        /// re-enables them from Finished.
        /// </summary>
        public void Begin(CharacterController controller, ThirdPersonCamera thirdPersonCamera, Animator animator, Transform cameraTransform,
            PlayerLocomotion locomotion)
        {
            _controller = controller;
            _thirdPersonCamera = thirdPersonCamera;
            _animator = animator;
            _camera = cameraTransform;
            _ragdoll = GetComponent<Ragdoll>();

            _spawnPosition = transform.position;
            _originalSpringLength = thirdPersonCamera.Distance;
            float height = locomotion != null ? locomotion.StandingHeight : 1.8f;
            _pivotHeight = height * thirdPersonCamera.PivotHeightFraction;

            MovePlayerTo(_spawnPosition + Vector3.up * StartAltitude);

            _normalFogStart = RenderSettings.fogStartDistance;
            _normalFogEnd = RenderSettings.fogEndDistance;
            SetCloud(StartAltitude);

            _followed = transform.position + Vector3.up * _pivotHeight;
            _thirdPersonCamera.Distance = OverheadSpringLength;
            _thirdPersonCamera.Pitch = OverheadPitchDegrees;
            _thirdPersonCamera.Yaw = 0.0f;
            PlaceCamera(OverheadSpringLength, OverheadPitchDegrees, 0.0f);

            if (RuinsMusic != null)
            {
                var musicObject = new GameObject("IntroCutsceneMusic");
                musicObject.transform.SetParent(transform.parent, false);
                var music = musicObject.AddComponent<AudioSource>();
                music.clip = RuinsMusic;
                music.spatialBlend = 0.0f;
                music.loop = false;
                music.volume = GameSettings.Load().MusicVolume;   // read fresh: the volume slider writes only GameSettings
                music.Play();
                Destroy(musicObject, RuinsMusic.length);          // plays out past the cutscene, then frees itself
            }

            _phase = Phase.Hanging;
            _phaseElapsed = 0.0f;
            _framesWaited = 0;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Public and taking its own deltaTime, as ClaimCutscene.Tick, so a test can run the sequence at its own pace.</summary>
        public void Tick(float deltaTime)
        {
            if (_phase == Phase.Done)
            {
                return;
            }
            if (_ragdoll != null && _phase != Phase.Hanging)
            {
                _ragdoll.Tick(deltaTime);
            }
            EaseFollowedPoint(deltaTime);
            switch (_phase)
            {
                case Phase.Hanging:
                    TickHanging();
                    break;
                case Phase.Falling:
                    TickFalling(deltaTime);
                    break;
                case Phase.Lying:
                    TickLying(deltaTime);
                    break;
                case Phase.GettingUp:
                    TickGettingUp();
                    break;
                case Phase.LookingAround:
                    TickLookingAround(deltaTime);
                    break;
                case Phase.HandingOff:
                    TickHandoff(deltaTime);
                    break;
            }
            if (_phase < Phase.LookingAround)
            {
                FollowBody();
            }
        }

        // --- the fall ---

        private void TickHanging()
        {
            _framesWaited++;
            if (_framesWaited <= PortraitCaptureDelayFrames)
            {
                return;
            }
            if (_ragdoll == null)
            {
                Debug.LogWarning("CityX: the intro has no ragdoll on the player -- skipping the fall.");
                MovePlayerTo(_spawnPosition);
                StartLookingAround();
                return;
            }
            FindFlyingParts();   // while the Animator is still on: going limp switches it off
            _ragdoll.Activate(LetGoVelocity);
            SetFallPhysics(true);
            _phase = Phase.Falling;
            _lastSpeed = 0.0f;
        }

        private void TickFalling(float deltaTime)
        {
            Rigidbody torso = _ragdoll.Torso;
            _phaseElapsed += deltaTime;
            Drift(deltaTime);
            Fly(deltaTime);
            SetCloud(torso.position.y - _spawnPosition.y);
            if (torso.position.y < _spawnPosition.y - FellThroughMargin)
            {
                PutBackOnTheGround();
            }
            float speed = torso.linearVelocity.magnitude;
            if (_lastSpeed > ImpactSpeed && speed < _lastSpeed * ImpactSpeedLoss)
            {
                Land();
            }
            _lastSpeed = speed;
        }

        /// <summary>
        /// Sideways steering in the air, Battle-Bus style: the keys ask for a sideways speed (world axes: the overhead camera never turns with
        /// the mouse here), every part of the body is brought round to it alike, and the body is kept within MaxDriftDistance of the spawn.
        /// </summary>
        private void Drift(float deltaTime)
        {
            Keyboard keyboard = Keyboard.current;
            var input = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) input.y += 1.0f;
                if (keyboard.sKey.isPressed) input.y -= 1.0f;
                if (keyboard.dKey.isPressed) input.x += 1.0f;
                if (keyboard.aKey.isPressed) input.x -= 1.0f;
            }
            if (Gamepad.current != null)
            {
                input += Gamepad.current.leftStick.ReadValue();
            }
            Vector3 wanted = Vector3.ClampMagnitude(new Vector3(input.x, 0.0f, input.y), 1.0f) * FallDriftSpeed;
            var offset = new Vector2(_ragdoll.Torso.position.x - _spawnPosition.x, _ragdoll.Torso.position.z - _spawnPosition.z);
            if (offset.magnitude > MaxDriftDistance)
            {
                wanted = -new Vector3(offset.x, 0.0f, offset.y).normalized * FallDriftSpeed;   // back toward the spawn
            }
            Vector3 sideways = _ragdoll.Torso.linearVelocity;
            sideways.y = 0.0f;
            Vector3 change = (wanted - sideways) * Mathf.Min(1.0f, DriftResponse * deltaTime);
            foreach (Rigidbody body in _ragdoll.Bodies)
            {
                if (body != null)
                {
                    body.linearVelocity += change;
                }
            }
        }

        /// <summary>The ragdoll's bones and bodies the fall works on: the torso's for its turn, the arms' and legs' for the wind.</summary>
        private void FindFlyingParts()
        {
            _hipsBone = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _neckBone = _animator.GetBoneTransform(HumanBodyBones.Neck) ?? _animator.GetBoneTransform(HumanBodyBones.Head);
            _leftShoulder = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _rightShoulder = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _chest = BodyOn(HumanBodyBones.Chest) ?? BodyOn(HumanBodyBones.Spine);   // RagdollBuilder's chest box, on whichever the rig has
            _limbRoots[FallFlail.LeftArm] = BodyOn(HumanBodyBones.LeftUpperArm);
            _limbRoots[FallFlail.RightArm] = BodyOn(HumanBodyBones.RightUpperArm);
            _limbRoots[FallFlail.LeftLeg] = BodyOn(HumanBodyBones.LeftUpperLeg);
            _limbRoots[FallFlail.RightLeg] = BodyOn(HumanBodyBones.RightUpperLeg);
            _limbEnds[FallFlail.LeftArm] = BodyOn(HumanBodyBones.LeftLowerArm);
            _limbEnds[FallFlail.RightArm] = BodyOn(HumanBodyBones.RightLowerArm);
            _limbEnds[FallFlail.LeftLeg] = BodyOn(HumanBodyBones.LeftLowerLeg);
            _limbEnds[FallFlail.RightLeg] = BodyOn(HumanBodyBones.RightLowerLeg);
        }

        private Rigidbody BodyOn(HumanBodyBones bone)
        {
            Transform t = _animator.GetBoneTransform(bone);
            return t != null ? t.GetComponent<Rigidbody>() : null;
        }

        /// <summary>
        /// The body brought round onto its back and held there, its arms and legs thrown about by the wind (FallFlail), as changes to the bodies'
        /// speeds, like Drift's, so it holds at any frame rate.
        /// </summary>
        private void Fly(float deltaTime)
        {
            if (_hipsBone == null || _neckBone == null || _leftShoulder == null || _rightShoulder == null)
            {
                return;
            }
            TurnOntoItsBack(deltaTime);
            ThrowLimbsAbout(deltaTime);
        }

        /// <summary>
        /// The whole body turned as one about its middle, each part's spin and the speed that turn gives it where it is: turning the trunk alone
        /// fought the joints, and the gusts on the limbs tumbled it.
        /// </summary>
        private void TurnOntoItsBack(float deltaTime)
        {
            Vector3 spine = (_neckBone.position - _hipsBone.position).normalized;
            Vector3 front = FallFlail.Front(_hipsBone.position, _neckBone.position, _leftShoulder.position, _rightShoulder.position);
            Vector3 trunkSpin = _chest != null ? (_ragdoll.Torso.angularVelocity + _chest.angularVelocity) * 0.5f : _ragdoll.Torso.angularVelocity;
            Vector3 turn = FallFlail.Righting(front, spine, trunkSpin) * deltaTime;
            Vector3 middle = Vector3.zero;
            float mass = 0.0f;
            foreach (Rigidbody body in _ragdoll.Bodies)
            {
                if (body != null)
                {
                    middle += body.worldCenterOfMass * body.mass;
                    mass += body.mass;
                }
            }
            middle /= Mathf.Max(mass, 1e-4f);
            foreach (Rigidbody body in _ragdoll.Bodies)
            {
                if (body != null)
                {
                    body.angularVelocity += turn;
                    body.linearVelocity += Vector3.Cross(turn, body.worldCenterOfMass - middle);
                }
            }
        }

        /// <summary>The wind on each arm and leg: all of it on the forearm and shin, a share on the upper arm and thigh.</summary>
        private void ThrowLimbsAbout(float deltaTime)
        {
            float fallSpeed = Mathf.Max(0.0f, -_ragdoll.Torso.linearVelocity.y);
            for (int limb = 0; limb < FallFlail.LimbCount; limb++)
            {
                Vector3 push = FallFlail.Wind(limb, _phaseElapsed, fallSpeed) * deltaTime;
                if (_limbEnds[limb] != null)
                {
                    _limbEnds[limb].linearVelocity += push;
                }
                if (_limbRoots[limb] != null)
                {
                    _limbRoots[limb].linearVelocity += push * FallFlail.UpperLimbShare;
                }
            }
        }

        private void Land()
        {
            _landed = true;
            SetFallPhysics(false);
            if (GroundImpactSound != null)
            {
                AudioSource.PlayClipAtPoint(GroundImpactSound, _ragdoll.Torso.position, GroundImpactVolumeLinear);
            }
            _phase = Phase.Lying;
            _phaseElapsed = 0.0f;
            _stillSeconds = 0.0f;
            SetCloud(0.0f);
        }

        /// <summary>The body fell through the ground (its collider was not there yet): every part lifted back over where it should have landed, still.</summary>
        private void PutBackOnTheGround()
        {
            float lift = _spawnPosition.y + 1.0f - _ragdoll.Torso.position.y;
            foreach (Rigidbody body in _ragdoll.Bodies)
            {
                if (body != null)
                {
                    body.position += Vector3.up * lift;
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
            Land();
        }

        /// <summary>
        /// The fall's own physics, set while falling and put back after: the air's drag on every part, and continuous collision (at 35 m/s a
        /// part moves half a metre a step, more than the ground's thickness).
        /// </summary>
        private void SetFallPhysics(bool falling)
        {
            Rigidbody[] bodies = _ragdoll.Bodies;
            if (falling)
            {
                _savedDrag = new float[bodies.Length];
                _savedCollision = new CollisionDetectionMode[bodies.Length];
            }
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i] == null)
                {
                    continue;
                }
                if (falling)
                {
                    _savedDrag[i] = bodies[i].linearDamping;
                    _savedCollision[i] = bodies[i].collisionDetectionMode;
                    bodies[i].linearDamping = FallDrag;
                    bodies[i].collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
                else if (_savedDrag != null)
                {
                    bodies[i].linearDamping = _savedDrag[i];
                    bodies[i].collisionDetectionMode = _savedCollision[i];
                }
            }
        }

        // --- lying and getting up ---

        private void TickLying(float deltaTime)
        {
            _phaseElapsed += deltaTime;
            _stillSeconds = _ragdoll.IsRestingOnGround ? _stillSeconds + deltaTime : 0.0f;
            if (_stillSeconds >= LyingStillSeconds || _phaseElapsed >= MaxLyingSeconds)
            {
                _ragdoll.BeginStandingUp(force: true, paceSeconds: GettingUpPaceSeconds);
                _phase = Phase.GettingUp;
            }
        }

        private void TickGettingUp()
        {
            if (!_ragdoll.IsLimp)
            {
                StartLookingAround();   // the ragdoll has handed the body back to the Animator, standing
            }
        }

        // --- looking around ---

        /// <summary>A "taking in the new world" beat between standing up and play.</summary>
        private void StartLookingAround()
        {
            _phase = Phase.LookingAround;
            _phaseElapsed = 0.0f;
            _animator.CrossFadeInFixedTime("LookingAround", LookingAroundBlendSeconds, 0);
        }

        private void TickLookingAround(float deltaTime)
        {
            _phaseElapsed += deltaTime;
            int next = 1;
            while (next < LookingAroundCamera.Length - 1 && LookingAroundCamera[next].Seconds <= _phaseElapsed)
            {
                next++;
            }
            CameraKey from = LookingAroundCamera[next - 1], to = LookingAroundCamera[next];
            float t = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(from.Seconds, to.Seconds, _phaseElapsed));
            SetShot(Mathf.Lerp(from.Distance, to.Distance, t), Mathf.Lerp(from.Pitch, to.Pitch, t), Mathf.LerpAngle(from.Yaw, to.Yaw, t));
            if (_phaseElapsed >= LookingAroundClipSeconds)
            {
                _phase = Phase.HandingOff;
                _phaseElapsed = 0.0f;
            }
        }

        // --- hand-off ---

        private void TickHandoff(float deltaTime)
        {
            _phaseElapsed += deltaTime;
            float t = Mathf.Clamp01(_phaseElapsed / HandoffDurationSeconds);
            SetShot(Mathf.Lerp(CutsceneSpringLength, _originalSpringLength, t), 0.0f, 0.0f);
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
            StandOnTheGround();
            Finished?.Invoke();
        }

        /// <summary>The player's feet put on the ground under them (the ragdoll's rest can leave the standing capsule a little over it).</summary>
        private void StandOnTheGround()
        {
            Vector3 feet = transform.position;
            RaycastHit[] hits = Physics.RaycastAll(feet + Vector3.up * GroundSearch, Vector3.down, 2.0f * GroundSearch, ~0, QueryTriggerInteraction.Ignore);
            float ground = float.MinValue;
            foreach (RaycastHit hit in hits)
            {
                bool ownBody = hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform);
                if (!ownBody && hit.point.y > ground)
                {
                    ground = hit.point.y;
                }
            }
            if (ground > float.MinValue)
            {
                MovePlayerTo(new Vector3(feet.x, ground, feet.z));
            }
        }

        // --- shared ---

        /// <summary>The cloud for a body this high over the ground: thick fog above CloudThickAltitude, the scene's own below CloudClearAltitude.</summary>
        private void SetCloud(float altitude)
        {
            float clear = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(CloudThickAltitude, CloudClearAltitude, altitude));
            RenderSettings.fogStartDistance = Mathf.Lerp(CloudFogStartDistance, _normalFogStart, clear);
            RenderSettings.fogEndDistance = Mathf.Lerp(CloudFogEndDistance, _normalFogEnd, clear);
        }

        /// <summary>
        /// The camera on the falling (or lying, or rising) body: the followed point eased toward it, the camera overhead high up and swung
        /// beside the body as it nears the ground.
        /// </summary>
        private void FollowBody()
        {
            float altitude = _followed.y - _spawnPosition.y;
            float beside = Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(CameraSwingTopAltitude, CameraSwingBottomAltitude, altitude));
            float distance = Mathf.Lerp(OverheadSpringLength, BesideSpringLength, beside);
            float pitch = Mathf.Lerp(OverheadPitchDegrees, BesidePitchDegrees, beside);
            _thirdPersonCamera.Distance = distance;
            _thirdPersonCamera.Pitch = pitch;
            _thirdPersonCamera.Yaw = 0.0f;
            PlaceCamera(distance, pitch, 0.0f);
        }

        /// <summary>The point the camera follows, eased toward the body while it falls and lies, and toward the player's own pivot once it rises.</summary>
        private void EaseFollowedPoint(float deltaTime)
        {
            bool onBody = _ragdoll != null && _ragdoll.IsLimp && !_ragdoll.IsStandingUp;
            Vector3 target = onBody ? _ragdoll.Torso.position : transform.position + Vector3.up * _pivotHeight;
            _followed = Vector3.Lerp(_followed, target, 1.0f - Mathf.Exp(-CameraFollowRate * deltaTime));
        }

        private void SetShot(float distance, float pitch, float yaw)
        {
            _thirdPersonCamera.Distance = distance;
            _thirdPersonCamera.Pitch = pitch;
            _thirdPersonCamera.Yaw = yaw;
            PlaceCamera(distance, pitch, yaw);
        }

        /// <summary>ThirdPersonCamera's own placement, around the followed point.</summary>
        private void PlaceCamera(float distance, float pitch, float yaw)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0.0f);
            _camera.position = _followed - rotation * Vector3.forward * distance;
            _camera.rotation = rotation;
        }

        private void MovePlayerTo(Vector3 position)
        {
            bool controllerOn = _controller != null && _controller.enabled;
            if (_controller != null)
            {
                _controller.enabled = false;
            }
            transform.position = position;
            if (controllerOn)
            {
                _controller.enabled = true;
            }
            Physics.SyncTransforms();   // a raw position write is invisible to the controller's next Move without this
        }
    }
}
