using FishNet.Object;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Makes a remote copy of a player actually LOOK like it is walking,
    /// running, crouching, aiming, etc. -- the direct answer to the explicit
    /// ask "have our character skins and animations synced up."
    ///
    /// THE OWNER'S OWN ANIMATOR IS THE SOURCE OF TRUTH, READ NOT RE-DERIVED.
    /// PlayerLocomotion.Tick already writes Speed/MoveX/MoveZ/Grounded/etc
    /// onto this same GameObject's Animator every frame for the owner's own
    /// local body -- this component does not reimplement any locomotion
    /// logic, it just reads those same parameters back out by name and
    /// forwards them. That is also why the parameter name strings below have
    /// to stay in lockstep with PlayerLocomotion's own Animator.StringToHash
    /// constants: there is one Animator Controller asset, shared by every
    /// player, and both scripts are naming slots on the exact same thing.
    ///
    /// ONE-SHOT ACTIONS ARE REPLICATED SEPARATELY, AS EVENTS: jump and dash
    /// (PlayerLocomotion.AnimatorTriggerFired) and every swing/punch/sword
    /// stage and block (PlayerAnimationDriver.StatePlayed) are sent as their
    /// own reliable RPC when the owner's body performs them, and replayed on
    /// every other copy. This carries only the VISUAL -- remote copies never
    /// run MeleeAttacker's hit/damage logic. Not covered: car enter/exit
    /// (vehicles aren't networked) and climbing (its Climbing bool isn't
    /// synced either).
    ///
    /// THROTTLED, NOT PER-FRAME: an owner sends its own state a few times a
    /// second rather than on every Update -- Godot's own MultiplayerSynchronizer
    /// applies the same kind of send-rate throttling automatically for its
    /// synced_is_walking/etc properties; this is the explicit version of
    /// that idea using an ordinary ServerRpc relay instead of a declarative
    /// synchronizer node, since this project has no Unity equivalent of one
    /// already established to reuse.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkedAnimatorSync : NetworkBehaviour
    {
        public float SendIntervalSeconds = 0.05f;

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int MoveXParameter = Animator.StringToHash("MoveX");
        private static readonly int MoveZParameter = Animator.StringToHash("MoveZ");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int CrouchingParameter = Animator.StringToHash("Crouching");
        private static readonly int AimingParameter = Animator.StringToHash("Aiming");
        private static readonly int SwordEquippedParameter = Animator.StringToHash("SwordEquipped");
        private static readonly int Sword1HEquippedParameter = Animator.StringToHash("Sword1HEquipped");
        private static readonly int GunEquippedParameter = Animator.StringToHash("GunEquipped");
        private static readonly int JetpackThrustingParameter = Animator.StringToHash("JetpackThrusting");
        private static readonly int DancingParameter = Animator.StringToHash("Dancing");
        private static readonly int PushUpPhaseParameter = Animator.StringToHash("PushUpPhase");
        private static readonly int BreakdancingParameter = Animator.StringToHash("Breakdancing");

        /// <summary>The Super Jump's crouch while Space charges it (a skill since 2026-10-14): other players see the crouch, then the jump.</summary>
        private static readonly int ChargingJumpParameter = Animator.StringToHash("ChargingJump");

        private static readonly int JumpTriggerParameter = Animator.StringToHash("Jump");
        private static readonly int DashTriggerParameter = Animator.StringToHash("Dash");

        private Animator _animator;
        private PlayerLocomotion _locomotion;
        private PlayerAnimationDriver _animationDriver;
        private float _sinceSend;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _locomotion = GetComponent<PlayerLocomotion>();
            _animationDriver = GetComponent<PlayerAnimationDriver>();
            _climbing = GetComponent<PlayerClimbing>();
        }

        // ONE-SHOTS (JUMP, DASH, PUNCHES/SWINGS, BLOCK). The continuous
        // parameters below are re-sent every SendIntervalSeconds, so a missed
        // packet heals itself a moment later. A jump or a punch happens once,
        // so it is sent as its own reliable RPC the instant the owner's body
        // performs it, and every other copy replays exactly that one action.
        // Subscribed unconditionally and gated on IsSpawned/IsOwner at send
        // time: ownership of the scene-baked host body is handed over after
        // this component starts (see NetworkedPlayerSpawner), so an
        // ownership check here would run too early.
        private void OnEnable()
        {
            if (_locomotion != null)
            {
                _locomotion.AnimatorTriggerFired += OnLocomotionTrigger;
            }
            if (_animationDriver != null)
            {
                _animationDriver.StatePlayed += OnStatePlayed;
                _animationDriver.TimedStatePlayed += OnTimedStatePlayed;
                _animationDriver.StateSpeedChanged += OnStateSpeedChanged;
            }
            if (_climbing != null)
            {
                _climbing.MantleStarted += OnMantleStarted;
            }
        }

        // A HIT-STOP ON EVERY SCREEN: the owner's swing freezes for a few frames when it lands (SoulsSwordCombat), which is most of what makes
        // the hit feel like one -- so the freeze (a speed of 0, then the speed back) is sent as its own event and the copy's swing freezes too.
        private void OnStateSpeedChanged(string stateName, float speed)
        {
            if (!IsLiveOwner)
            {
                return;
            }
            SendStateSpeedToServer(stateName, speed);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendStateSpeedToServer(string stateName, float speed)
        {
            BroadcastStateSpeed(stateName, speed);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastStateSpeed(string stateName, float speed)
        {
            if (_animationDriver != null)
            {
                _animationDriver.SetRemoteStateSpeed(stateName, speed);
            }
        }

        private void OnDisable()
        {
            if (_locomotion != null)
            {
                _locomotion.AnimatorTriggerFired -= OnLocomotionTrigger;
            }
            if (_animationDriver != null)
            {
                _animationDriver.StatePlayed -= OnStatePlayed;
                _animationDriver.TimedStatePlayed -= OnTimedStatePlayed;
                _animationDriver.StateSpeedChanged -= OnStateSpeedChanged;
            }
            if (_climbing != null)
            {
                _climbing.MantleStarted -= OnMantleStarted;
            }
        }

        /// <summary>Only these triggers are replayed from the wire; anything else is ignored.</summary>
        private static bool IsReplicatedTrigger(int parameterHash)
        {
            return parameterHash == JumpTriggerParameter || parameterHash == DashTriggerParameter;
        }

        /// <summary>
        /// True only for the live owner of a genuinely spawned body. The
        /// NetworkObject null check is load-bearing, not defensive: a body
        /// that never joined a session (singleplayer, or the real scene's
        /// baked player before/without one -- NetworkedPlayerIdentity
        /// deliberately un-networks it) has no initialised NetworkObject
        /// cache, so FishNet's own IsSpawned/IsOwner throw a
        /// NullReferenceException. These handlers run INSIDE
        /// PlayerLocomotion.Tick, so an unguarded throw there aborted the
        /// jump mid-frame (caught by JetpackGroundedJumpPlayTests and
        /// RealCityAnimatorStabilityPlayTests).
        /// </summary>
        private bool IsLiveOwner => NetworkObject != null && IsSpawned && IsOwner;

        private void OnLocomotionTrigger(int parameterHash)
        {
            if (!IsLiveOwner || !IsReplicatedTrigger(parameterHash))
            {
                return;
            }
            SendTriggerToServer(parameterHash);
        }

        private void OnStatePlayed(string stateName)
        {
            if (!IsLiveOwner)
            {
                return;
            }
            SendSwingStateToServer(stateName);
        }

        private void OnTimedStatePlayed(string stateName, float speed, float startSeconds)
        {
            if (!IsLiveOwner)
            {
                return;
            }
            SendTimedSwingToServer(stateName, speed, startSeconds);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendTriggerToServer(int parameterHash)
        {
            BroadcastTrigger(parameterHash);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastTrigger(int parameterHash)
        {
            if (_animator == null || !IsReplicatedTrigger(parameterHash))
            {
                return;
            }
            _animator.SetTrigger(parameterHash);
        }

        // Named apart from SendStateToServer/BroadcastState (the continuous
        // parameter sync) on purpose -- FishNet identifies an RPC by name, so
        // an overload of the same name is asking for a collision.
        [ServerRpc(RequireOwnership = true)]
        private void SendSwingStateToServer(string stateName)
        {
            BroadcastSwingState(stateName);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastSwingState(string stateName)
        {
            if (_animationDriver != null)
            {
                _animationDriver.PlayRemoteState(stateName);
            }
        }

        // The soulslike sword's swings: the state PLUS the speed and clip
        // offset the owner fitted it to, so a remote copy sees the same
        // paced swing rather than the raw (much slower) clip.
        [ServerRpc(RequireOwnership = true)]
        private void SendTimedSwingToServer(string stateName, float speed, float startSeconds)
        {
            BroadcastTimedSwing(stateName, speed, startSeconds);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastTimedSwing(string stateName, float speed, float startSeconds)
        {
            if (_animationDriver != null)
            {
                _animationDriver.PlayRemoteState(stateName, speed, startSeconds);
            }
        }

        // ---- CLIMBING, VAULTING AND THE MANTLE ------------------------------------------------------------------------------------------------
        // The body's position already travels with the transform, so another player sees it slide up a wall or over a fence; what was missing was
        // the POSE: the climbing animation, the hands on the ledge, the limbs kept off the wall. The owner sends them with the continuous state;
        // the copy sets the same Animator parameters and hands the same two plane/hand targets to its own PlayerLocomotion, whose LateUpdate does
        // the IK exactly as on the owner. The mantle's pull-up is a one-shot, so it goes as its own reliable event.
        private static readonly int ClimbingParameter = Animator.StringToHash("Climbing");
        private static readonly int ClimbDirectionParameter = Animator.StringToHash("ClimbDirection");
        private static readonly int ClimbSpeedScaleParameter = Animator.StringToHash("ClimbSpeedScale");
        private static readonly int ClimbTopOutParameter = Animator.StringToHash("ClimbTopOut");

        private PlayerClimbing _climbing;
        private bool _motionWasActive;

        // The continuous values a remote copy glides toward instead of snapping to (a copy hears them about twenty times a second).
        private float _targetSpeed, _targetMoveX, _targetMoveZ;
        private bool _hasTargets;

        /// <summary>How quickly a copy's walk/run blend follows what the owner sent, in seconds (a damped follow).</summary>
        public const float RemoteSmoothSeconds = 0.09f;

        private bool IsRemoteCopy => NetworkObject != null && IsSpawned && !IsOwner;

        private void OnMantleStarted()
        {
            if (!IsLiveOwner)
            {
                return;
            }
            SendMantleToServer();
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendMantleToServer()
        {
            BroadcastMantle();
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastMantle()
        {
            if (_animator == null)
            {
                return;
            }
            _animator.SetBool(ClimbingParameter, false);
            _animator.SetTrigger(ClimbTopOutParameter);
        }

        /// <summary>The owner's climbing/hands pose, sent at the same rate as the rest of the state, and once more when it ends so a copy lets go.</summary>
        private void SendMotionIfNeeded()
        {
            if (_locomotion == null)
            {
                return;
            }
            bool climbing = _animator.GetBool(ClimbingParameter);
            float wall = _locomotion.ClimbWallWeight;
            float hands = _locomotion.LedgeHandsWeight;
            bool active = climbing || wall > 0.01f || hands > 0.01f;
            if (!active && !_motionWasActive)
            {
                return;
            }
            _motionWasActive = active;
            SendMotionToServer(climbing, _animator.GetInteger(ClimbDirectionParameter), _locomotion.ClimbTravel, _animator.GetFloat(ClimbSpeedScaleParameter),
                wall, _locomotion.ClimbWallPoint, _locomotion.ClimbWallNormal,
                hands, _locomotion.LedgeLip, _locomotion.LedgeNormal);
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendMotionToServer(bool climbing, int direction, Vector2 travel, float speedScale, float wallWeight, Vector3 wallPoint, Vector3 wallNormal,
            float handsWeight, Vector3 lip, Vector3 ledgeNormal)
        {
            // A garbled packet must not poison a copy's IK.
            if (!WireChecks.Finite(wallPoint) || !WireChecks.Finite(wallNormal) || !WireChecks.Finite(lip) || !WireChecks.Finite(ledgeNormal)
                || float.IsNaN(wallWeight) || float.IsNaN(handsWeight) || float.IsNaN(speedScale) || float.IsNaN(travel.x) || float.IsNaN(travel.y))
            {
                return;
            }
            BroadcastMotion(climbing, Mathf.Clamp(direction, -1, 1), Vector2.ClampMagnitude(travel, 1.0f), Mathf.Clamp(speedScale, 0.0f, 4.0f),
                Mathf.Clamp01(wallWeight), wallPoint, wallNormal, Mathf.Clamp01(handsWeight), lip, ledgeNormal);
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastMotion(bool climbing, int direction, Vector2 travel, float speedScale, float wallWeight, Vector3 wallPoint, Vector3 wallNormal,
            float handsWeight, Vector3 lip, Vector3 ledgeNormal)
        {
            ApplyRemoteMotion(climbing, direction, travel, speedScale, wallWeight, wallPoint, wallNormal, handsWeight, lip, ledgeNormal);
        }

        /// <summary>
        /// Puts the owner's climbing pose on THIS copy. <paramref name="travel"/> is which way the owner is going on the wall (along it, up), so the copy's
        /// limbs step the same way, sideways included. Public so a test can drive it without a network.
        /// </summary>
        public void ApplyRemoteMotion(bool climbing, int direction, Vector2 travel, float speedScale, float wallWeight, Vector3 wallPoint, Vector3 wallNormal,
            float handsWeight, Vector3 lip, Vector3 ledgeNormal)
        {
            if (_animator != null)
            {
                _animator.SetBool(ClimbingParameter, climbing);
                _animator.SetInteger(ClimbDirectionParameter, direction);
                _animator.SetFloat(ClimbSpeedScaleParameter, speedScale);
            }
            if (_locomotion != null)
            {
                _locomotion.SetClimbWall(wallPoint, wallNormal, wallWeight);
                _locomotion.SetClimbTravel(climbing ? travel : Vector2.zero);
                _locomotion.SetLedgeHands(handsWeight, lip, ledgeNormal);
            }
        }

        /// <summary>A copy's per-frame work: glide the walk blend toward the owner's, and fit the mantle clip to the mantle's own length like the owner does.</summary>
        private void TickRemoteCopy(float deltaTime)
        {
            if (_animator == null)
            {
                return;
            }
            if (_hasTargets)
            {
                _animator.SetFloat(SpeedParameter, _targetSpeed, RemoteSmoothSeconds, deltaTime);
                _animator.SetFloat(MoveXParameter, _targetMoveX, RemoteSmoothSeconds, deltaTime);
                _animator.SetFloat(MoveZParameter, _targetMoveZ, RemoteSmoothSeconds, deltaTime);
            }
            AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("ClimbTopOut") && info.length > 0.01f)
            {
                _animator.speed = info.length / PlayerClimbing.MantleSeconds;
            }
            else if (_animator.speed != 1.0f && _animationDriver != null && !_animationDriver.IsPlaying("ClimbTopOut"))
            {
                // Only undo OUR fit (the swing replay manages its own speed through PlayerAnimationDriver on states it plays).
                if (_fittedMantle)
                {
                    _animator.speed = 1.0f;
                }
            }
            _fittedMantle = info.IsName("ClimbTopOut");
        }

        private bool _fittedMantle;

        private void Update()
        {
            // IsLiveOwner, not bare IsOwner: a body with no initialised NetworkObject (singleplayer, the baked player before a session starts) makes
            // FishNet's own IsOwner throw a NullReferenceException every frame.
            if (_animator == null)
            {
                return;
            }
            if (IsRemoteCopy)
            {
                TickRemoteCopy(Time.deltaTime);
                return;
            }
            if (!IsLiveOwner)
            {
                return;
            }

            _sinceSend += Time.deltaTime;
            if (_sinceSend < SendIntervalSeconds)
            {
                return;
            }
            _sinceSend = 0.0f;

            SendStateToServer(
                _animator.GetFloat(SpeedParameter),
                _animator.GetFloat(MoveXParameter),
                _animator.GetFloat(MoveZParameter),
                _animator.GetBool(GroundedParameter),
                _animator.GetBool(CrouchingParameter),
                _animator.GetBool(AimingParameter),
                _animator.GetBool(SwordEquippedParameter),
                _animator.GetBool(Sword1HEquippedParameter),
                _animator.GetBool(GunEquippedParameter),
                _animator.GetBool(JetpackThrustingParameter),
                _animator.GetBool(DancingParameter),
                _animator.GetBool(BreakdancingParameter),
                _animator.GetBool(ChargingJumpParameter),
                _animator.GetInteger(PushUpPhaseParameter));
            SendMotionIfNeeded();
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendStateToServer(float speed, float moveX, float moveZ, bool grounded,
            bool crouching, bool aiming, bool swordEquipped, bool sword1HEquipped, bool gunEquipped, bool jetpackThrusting,
            bool dancing, bool breakdancing, bool chargingJump, int pushUpPhase)
        {
            BroadcastState(speed, moveX, moveZ, grounded, crouching, aiming,
                swordEquipped, sword1HEquipped, gunEquipped, jetpackThrusting, dancing, breakdancing, chargingJump, pushUpPhase);
        }

        /// <summary>
        /// ExcludeOwner -- the owner's own Animator already carries these
        /// exact values live from its own Tick; applying them back a network
        /// round-trip later would only ever be redundant, never more correct.
        /// </summary>
        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastState(float speed, float moveX, float moveZ, bool grounded,
            bool crouching, bool aiming, bool swordEquipped, bool sword1HEquipped, bool gunEquipped, bool jetpackThrusting,
            bool dancing, bool breakdancing, bool chargingJump, int pushUpPhase)
        {
            if (_animator == null)
            {
                return;
            }
            _targetSpeed = speed;
            _targetMoveX = moveX;
            _targetMoveZ = moveZ;
            if (!_hasTargets)
            {
                // The first packet snaps (no glide from a stale pose); after that the copy follows (see TickRemoteCopy).
                _hasTargets = true;
                _animator.SetFloat(SpeedParameter, speed);
                _animator.SetFloat(MoveXParameter, moveX);
                _animator.SetFloat(MoveZParameter, moveZ);
            }
            _animator.SetBool(GroundedParameter, grounded);
            _animator.SetBool(CrouchingParameter, crouching);
            _animator.SetBool(AimingParameter, aiming);
            _animator.SetBool(SwordEquippedParameter, swordEquipped);
            _animator.SetBool(Sword1HEquippedParameter, sword1HEquipped);
            _animator.SetBool(GunEquippedParameter, gunEquipped);
            _animator.SetBool(JetpackThrustingParameter, jetpackThrusting);
            _animator.SetBool(DancingParameter, dancing);
            _animator.SetBool(BreakdancingParameter, breakdancing);
            _animator.SetBool(ChargingJumpParameter, chargingJump);
            _animator.SetInteger(PushUpPhaseParameter, pushUpPhase);
        }
    }
}
