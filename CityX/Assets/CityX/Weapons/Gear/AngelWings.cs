using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// ANGEL WINGS (owner, 2026-10-28: "make winged flying starting with angel wings. make them large angel wings"; docs/WINGS.md): flight gear
    /// (<see cref="FlightGear"/>) worn between the shoulder blades, a pair of big white feathered wings drawn in code (AngelWingsBuilder; a
    /// stand-in until the owner's model). Flight is the shared FlightModel's, floatier than the jetpack's; what is the wings' own:
    /// <list type="bullet">
    /// <item>THE BEAT (<see cref="WingBeat"/>) follows the flight: slow deep beats hovering, quick ones climbing, a glide at speed with a beat
    /// now and then, beats added as speed is gained, a strong downstroke the instant the body takes off, and the body is BOBBED with it (each
    /// downstroke lifts it a little; a climb surges with the beats), so the rhythm and the movement are one thing.</item>
    /// <item>THE POSE (<see cref="WingPose"/>) is set bone by bone every frame after the Animator: folded on the back on the ground, open and
    /// beating in the air, swept back gliding, tucked descending, each feather fanned and twisting against the stroke.</item>
    /// <item>Other players' copies carry the same wings (ICosmeticMountable) and beat them by the same rules from what they can see: whether the
    /// wearer is flying (the synced JetpackThrusting parameter) and how their copy moves.</item>
    /// </list>
    /// The body leans forward flying ahead (less than the jetpack's Superman) and banks into a strafe.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class AngelWings : FlightGear, ICosmeticKeepsRunning
    {
        public override string ItemId => ItemRegistry.AngelWings;

        public const string PrefabPath = "Assets/CityX/Weapons/AngelWings.prefab";

        /// <summary>The longest primary, metres: the wing reaches this far past the hand (AngelWingsBuilder).</summary>
        public const float LongestPrimary = 1.15f;

        /// <summary>The flight's numbers, metres a second (the Flight Speed stat scales them): a glide is slower and floatier than a jetpack.</summary>
        public const float ClimbSpeed = 7.0f;

        public const float BoostClimbSpeed = 10.0f;

        public const float DescendSpeed = 9.0f;

        public const float CruiseSpeed = 10.0f;

        public const float FastSpeed = 17.0f;

        public const float Acceleration = 12.0f;

        /// <summary>Hovering, the body rises and sinks this fast with the beat (up on the downstroke), metres a second.</summary>
        public const float HoverBobSpeed = 0.35f;

        /// <summary>Climbing, the climb surges this much faster on the downstroke and slower on the upstroke, as a share.</summary>
        public const float ClimbSurgeShare = 0.35f;

        /// <summary>Flying forward the body tips this share of the way from upright toward the camera's line (the jetpack goes all the way).</summary>
        public const float LeanPitchShare = 0.45f;

        public const float StrafeRollDegrees = 25.0f;

        /// <summary>A copy's climb and descent, read off how it moves, metres a second.</summary>
        public const float CopyClimbSpeed = 1.5f;

        public const float CopyDescendSpeed = -2.5f;

        /// <summary>The wing roots on the Spine bone: between the shoulder blades, just below the shoulder line, against the back.</summary>
        public static readonly Vector3 BackPosition = new Vector3(0.0f, 0.35f, -0.20f);

        public const float BeatVolume = 0.55f;

        /// <summary>One wing's bones, set by AngelWingsBuilder: the arm, and a bone a feather, with each feather's built spread and how far out it sits.</summary>
        [System.Serializable]
        public sealed class Wing
        {
            public Transform Humerus;
            public Transform Forearm;
            public Transform Hand;
            public Transform[] Feathers;
            public float[] FeatherSpread;
            public float[] FeatherAlong;
        }

        public Wing Right;

        public Wing Left;

        private readonly WingBeat _beat = new WingBeat();
        private Transform _body;
        private bool _cosmeticFlying;
        private bool _wasCosmeticFlying;
        private bool _wasCosmeticClimbing;
        private Vector3 _lastBodyPosition;
        private bool _hasLastBodyPosition;
        private float _speed;
        private float _acceleration;
        private AudioSource _audio;

        public WingBeat Beat => _beat;

        protected override Vector3 MountPosition => BackPosition;

        protected override FlightTuning Tuning => new FlightTuning(ClimbSpeed, BoostClimbSpeed, DescendSpeed, CruiseSpeed, FastSpeed, Acceleration);

        protected override Vector2 LeanTarget(in FlightInput input)
        {
            float pitch = input.Move.y > 0.0f ? (input.CameraPitchDegrees + Jetpack.SupermanPitchOffsetDegrees) * LeanPitchShare : 0.0f;
            return new Vector2(pitch, -input.Move.x * StrafeRollDegrees);
        }

        /// <summary>The beat's lift on the body: a hover bobs with it, a climb surges with it, a descent is the tuck's steady sink.</summary>
        protected override float VerticalVelocityFor(float modelVertical, float deltaTime)
        {
            float beat = Mathf.Cos(2.0f * Mathf.PI * _beat.Phase) * Mathf.Clamp01(_beat.Strength);
            FlightModel model = Model;
            if (model.Climbing)
            {
                return modelVertical + ClimbSpeed * SpeedScale * ClimbSurgeShare * beat;
            }
            return model.Descending ? modelVertical : modelVertical + HoverBobSpeed * beat;
        }

        protected override void OnWorn(GameObject player)
        {
            _body = player.transform;
        }

        protected override void OnMounted(GameObject body)
        {
            _body = body.transform;
        }

        public override void SetCosmeticActive(bool active)
        {
            _cosmeticFlying = active;
        }

        protected override void OnEffectsStopped()
        {
            _cosmeticFlying = false;
        }

        /// <summary>After the Animator has posed the body: the beat ticks on and every wing bone is turned for it.</summary>
        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            MeasureBody(deltaTime);
            WingBeatInput input = IsEquipped ? OwnBeat() : CopyBeat();
            _beat.Tick(input, deltaTime);
            Pose();
            Voice(input);
        }

        /// <summary>How the wearer moves, from the body's own velocity when it is this machine's, from where it is seen on a copy.</summary>
        private void MeasureBody(float deltaTime)
        {
            Vector3 velocity;
            if (Locomotion != null)
            {
                velocity = Locomotion.Velocity;
            }
            else if (_body != null && _hasLastBodyPosition && deltaTime > 1e-5f)
            {
                velocity = (_body.position - _lastBodyPosition) / deltaTime;
            }
            else
            {
                velocity = Vector3.zero;
            }
            if (_body != null)
            {
                _lastBodyPosition = _body.position;
                _hasLastBodyPosition = true;
            }
            float speed = new Vector3(velocity.x, 0.0f, velocity.z).magnitude;
            float acceleration = deltaTime > 1e-5f ? (speed - _speed) / deltaTime : 0.0f;
            _acceleration = Mathf.Lerp(_acceleration, acceleration, Mathf.Clamp01(deltaTime * 8.0f));
            _speed = speed;
            VerticalSpeed = velocity.y;
        }

        /// <summary>The body's vertical speed as last measured, metres a second (a copy's climb and descent are read off it).</summary>
        public float VerticalSpeed { get; private set; }

        private WingBeatInput OwnBeat()
        {
            FlightModel model = Model;
            return new WingBeatInput
            {
                Flying = model.Flying,
                Climbing = model.Climbing,
                Descending = model.Descending,
                SpeedFraction = _speed / Mathf.Max(0.1f, CruiseSpeed * SpeedScale),
                ForwardAcceleration = _acceleration,
                TookOff = model.TookOff,
                ClimbBegan = model.ClimbBegan,
            };
        }

        private WingBeatInput CopyBeat()
        {
            bool climbing = _cosmeticFlying && VerticalSpeed > CopyClimbSpeed;
            var input = new WingBeatInput
            {
                Flying = _cosmeticFlying,
                Climbing = climbing,
                Descending = _cosmeticFlying && VerticalSpeed < CopyDescendSpeed,
                SpeedFraction = _speed / CruiseSpeed,
                ForwardAcceleration = _acceleration,
                TookOff = _cosmeticFlying && !_wasCosmeticFlying,
                ClimbBegan = climbing && !_wasCosmeticClimbing,
            };
            _wasCosmeticFlying = _cosmeticFlying;
            _wasCosmeticClimbing = climbing;
            return input;
        }

        private void Pose()
        {
            float shoulder = _beat.StrokeAngle(0.0f);
            float elbow = WingPose.ElbowShare * _beat.StrokeAngle(WingPose.ElbowLag);
            float wrist = WingPose.WristShare * _beat.StrokeAngle(WingPose.WristLag);
            WingPose.Arm arm = WingPose.ArmPose(_beat.Fold, _beat.Glide, _beat.Tuck, shoulder, elbow, wrist);
            float spread = WingPose.SpreadShare(_beat.Fold, _beat.Glide, _beat.Tuck);
            Apply(Right, arm, spread, false);
            Apply(Left, arm, spread, true);
        }

        private void Apply(Wing wing, in WingPose.Arm arm, float spread, bool mirrored)
        {
            if (wing == null || wing.Humerus == null || wing.Forearm == null || wing.Hand == null)
            {
                return;
            }
            wing.Humerus.localRotation = mirrored ? WingPose.Mirror(arm.Humerus) : arm.Humerus;
            wing.Forearm.localRotation = mirrored ? WingPose.Mirror(arm.Forearm) : arm.Forearm;
            wing.Hand.localRotation = mirrored ? WingPose.Mirror(arm.Hand) : arm.Hand;
            if (wing.Feathers == null)
            {
                return;
            }
            for (int i = 0; i < wing.Feathers.Length; i++)
            {
                if (wing.Feathers[i] == null)
                {
                    continue;
                }
                float along = wing.FeatherAlong != null && i < wing.FeatherAlong.Length ? wing.FeatherAlong[i] : 0.5f;
                float built = wing.FeatherSpread != null && i < wing.FeatherSpread.Length ? wing.FeatherSpread[i] : 0.0f;
                Quaternion pose = WingPose.FeatherPose(built, spread, along, _beat.StrokeVelocity);
                wing.Feathers[i].localRotation = mirrored ? WingPose.Mirror(pose) : pose;
            }
        }

        private void Voice(in WingBeatInput input)
        {
            if (!Application.isPlaying)
            {
                return;
            }
            if (input.TookOff)
            {
                Play(WingSounds.Takeoff, 0.9f, 1.0f);
            }
            else if (_beat.DownstrokeBegan && input.Flying && _beat.Strength > 0.2f)
            {
                Play(WingSounds.Beat, BeatVolume * Mathf.Clamp01(_beat.Strength), Mathf.Lerp(1.15f, 0.9f, Mathf.Clamp01(_beat.Strength)));
            }
            else if (!input.Flying && _beat.Fold > 0.0f && _beat.Fold < 0.05f)
            {
                Play(WingSounds.Rustle, 0.5f, 1.0f);
            }
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 1.0f;
            }
            _audio.pitch = pitch;
            _audio.PlayOneShot(clip, volume);
        }
    }
}
