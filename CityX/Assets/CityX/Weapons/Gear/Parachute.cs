using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE PARACHUTE BACKPACK (owner, 2026-11-19: "make a good parachute backpack that you equip on your back and when equipped you can use it by
    /// pressing space mid air ... id like to design the backpack myself later but just make a basic one that works for now"; docs/PARACHUTE.md).
    /// Worn on the back (the jetpack's slot). The jump itself is <see cref="ParachuteFlight"/>: a fall is timed, after two seconds the body spreads
    /// out face down and Space opens the canopy, which is flown down with WASD and flared to land. This puts the flight on the body: its velocity
    /// (PlayerLocomotion's forced velocity), the way it faces, no fall damage under an open canopy, a hard landing's knock-down, the camera
    /// pulled back to show the canopy; the body's <see cref="ParachuteRider"/> shows it (the poses, the canopy, the sounds) here and to other
    /// players.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Parachute : MonoBehaviour, IEquippableItem, IParachute, ICosmeticMountable
    {
        public string ItemId => ItemRegistry.Parachute;

        public const float InteractRange = 2.5f;

        /// <summary>Flat against the spine, far enough out not to sink into the back.</summary>
        public static readonly Vector3 BackPosition = new Vector3(0.0f, 0.02f, -0.30f);

        public static readonly Vector3 BackRotationEuler = Vector3.zero;

        /// <summary>Under the canopy the camera stands this much further back, metres, so the canopy over the head is in view.</summary>
        public const float CanopyPullBack = 3.0f;

        /// <summary>The camera's pull-back follows over this long, seconds.</summary>
        public const float PullBackSeconds = 0.6f;

        [Tooltip("The folded pack, worn whenever this is equipped.")]
        public GameObject PackVisual;

        [Tooltip("The canopy (ParachuteCanopy), shown by the body's ParachuteRider while it is out.")]
        public ParachuteCanopy Canopy;

        [Tooltip("The rush of air in a fall (jetpack_wind_loop.wav), played by the body's ParachuteRider.")]
        public AudioClip WindClip;

        private SphereCollider _pickupTrigger;
        private readonly GroundPickup _pickup = new GroundPickup();
        private readonly ParachuteFlight _flight = new ParachuteFlight();
        private bool _equipped;
        private PlayerLocomotion _locomotion;
        private PlayerVitals _vitals;
        private ParachuteRider _rider;
        private ThirdPersonCamera _camera;
        private JetpackSpeedLines _speedLines;
        private float _savedMinPitch;
        private float _savedMaxPitch;
        private float _pullBack;
        private float _pullBackVelocity;
        private float _fov;
        private float _fovVelocity;
        private int _lastTickFrame = -10;
        private bool _forcing;
        private bool _knockDown;
        private Vector3 _knockedDownAt;

        /// <summary>The jump as it stands.</summary>
        public ParachuteFlight Flight => _flight;

        public bool DrivesBody => _flight.DrivesBody;

        /// <summary>The canopy is out (opening or open).</summary>
        public bool IsOpen => _flight.State == ChuteState.Opening || _flight.State == ChuteState.Open;

        /// <summary>The camera it pulls back. Set on equip; a test can point it at its own.</summary>
        public ThirdPersonCamera FeelCamera
        {
            get => _camera;
            set => _camera = value;
        }

        private void Awake()
        {
            _pickupTrigger = GetComponent<SphereCollider>();
        }

        private void Update()
        {
            if (!_equipped)
            {
                if (_pickup.TappedWithin(transform.position, InteractRange))
                {
                    GroundPickup.Take(_pickup.Player, gameObject, ItemRegistry.Parachute);
                }
                return;
            }
            // Not ticked for a frame (the body knocked down, on a wall, on a rope): whatever was out is gone.
            if (_flight.DrivesBody && Time.frameCount > _lastTickFrame + 1)
            {
                Tick(false, Vector2.zero, Time.deltaTime, continuous: false);
            }
        }

        private void OnTriggerEnter(Collider other) => _pickup.Entered(other);

        private void OnTriggerExit(Collider other) => _pickup.Left(other);

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform spine = animator != null ? animator.GetBoneTransform(HumanBodyBones.Spine) : null;
            if (spine != null)
            {
                transform.SetParent(spine, false);
                transform.localPosition = BackPosition;
                transform.localEulerAngles = BackRotationEuler;
            }
            _equipped = true;
            if (_pickupTrigger != null)
            {
                _pickupTrigger.enabled = false;
            }
            _locomotion = player.GetComponent<PlayerLocomotion>();
            _vitals = player.GetComponent<PlayerVitals>();
            _rider = ParachuteRider.On(player);
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedParachute = this;
                _camera = input.OrbitCamera;
            }
            // Looking straight down for where to land, as the jetpack and the wings allow for as long as they are worn.
            if (_camera != null)
            {
                _savedMinPitch = _camera.MinPitch;
                _savedMaxPitch = _camera.MaxPitch;
                _camera.MinPitch = FlightGear.MinPitchDegrees;
                _camera.MaxPitch = FlightGear.MaxPitchDegrees;
                _speedLines = JetpackSpeedLines.Attach(_camera.transform);
            }
        }

        public void OnUnequip(GameObject player)
        {
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null && ReferenceEquals(input.EquippedParachute, this))
            {
                input.EquippedParachute = null;
            }
            if (_forcing && _locomotion != null)
            {
                _locomotion.ReleaseForcedVelocity();
            }
            _rider?.PutAway();
            if (_camera != null)
            {
                _camera.MinPitch = _savedMinPitch;
                _camera.MaxPitch = _savedMaxPitch;
                _camera.Pitch = Mathf.Clamp(_camera.Pitch, _savedMinPitch, _savedMaxPitch);
                _camera.FlightFovBonus = 0.0f;
                _camera.FlightDistanceBonus = 0.0f;
            }
            if (_speedLines != null)
            {
                Destroy(_speedLines.gameObject);
            }
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.Spine, BackPosition, BackRotationEuler, 1.0f);
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        public void Tick(bool spacePressed, Vector2 move, float deltaTime)
        {
            Tick(spacePressed, move, deltaTime, Time.frameCount <= _lastTickFrame + 1);
        }

        private void Tick(bool spacePressed, Vector2 move, float deltaTime, bool continuous)
        {
            _lastTickFrame = Time.frameCount;
            if (_locomotion == null)
            {
                return;
            }
            _flight.Tick(new ChuteInput
            {
                Airborne = !_locomotion.IsGrounded,
                SpacePressed = spacePressed,
                Move = move,
                BodyVelocity = _locomotion.Velocity,
                HeadingDegrees = _locomotion.transform.eulerAngles.y,
                Continuous = continuous,
                HitWall = _locomotion.HitWallThisFrame,
            }, deltaTime);

            if (_flight.DrivesBody)
            {
                _locomotion.ForceVelocity(_flight.Velocity);
                _forcing = true;
                _locomotion.transform.rotation = Quaternion.Euler(0.0f, _flight.HeadingDegrees, 0.0f);
                // Under a canopy the height it came from never hurts: how it lands does (below).
                if (IsOpen && _vitals != null)
                {
                    _vitals.ResetFallTracking(_locomotion.transform.position.y);
                }
            }
            else if (_forcing)
            {
                _locomotion.ReleaseForcedVelocity();
                _forcing = false;
            }
            if (_flight.LandedThisFrame)
            {
                Landed();
            }
            TickCamera(deltaTime);
            _rider?.ShowOwn(_flight, move, deltaTime);
        }

        private void Landed()
        {
            if (_vitals == null)
            {
                return;
            }
            _vitals.ResetFallTracking(_locomotion.transform.position.y);
            if (_flight.Landing == ChuteLanding.Hard)
            {
                _vitals.TakeDamage(ParachuteFlight.DamageOf(_flight.Landing, _flight.LandingSink));
                // Knocked down once the body has moved this frame (LateUpdate): the input reader moves it after this, and a body gone limp cannot be moved.
                _knockedDownAt = _locomotion.Velocity;
                _knockedDownAt.y = 0.0f;
                _knockDown = true;
            }
        }

        private void LateUpdate()
        {
            if (_knockDown && _vitals != null)
            {
                _vitals.TriggerLaunchKnockdown(_knockedDownAt);
            }
            _knockDown = false;
        }

        /// <summary>
        /// The view widens and pulls back with the speed of a fall, as a jetpack's does (FlightFeel), with the streaks of air past it; under the
        /// canopy it stands further back still, so the canopy is in view.
        /// </summary>
        private void TickCamera(float deltaTime)
        {
            if (_camera == null || deltaTime <= 0.0f)
            {
                return;
            }
            bool falling = _flight.State == ChuteState.Freefall || _flight.State == ChuteState.Opening;
            float fraction = falling ? FlightFeel.SpeedFraction(_locomotion.Velocity.magnitude) : 0.0f;
            float back = (IsOpen ? CanopyPullBack : 0.0f) + FlightFeel.DistanceBonus(fraction, 0.0f);
            _pullBack = Mathf.SmoothDamp(_pullBack, back, ref _pullBackVelocity, PullBackSeconds, Mathf.Infinity, deltaTime);
            _fov = Mathf.SmoothDamp(_fov, FlightFeel.FovBonus(fraction, 0.0f), ref _fovVelocity, FlightFeel.SmoothSeconds, Mathf.Infinity, deltaTime);
            _camera.FlightDistanceBonus = _pullBack;
            _camera.FlightFovBonus = _fov;
            if (_speedLines != null)
            {
                _speedLines.Set(fraction, _locomotion.Velocity);
            }
        }
    }
}
