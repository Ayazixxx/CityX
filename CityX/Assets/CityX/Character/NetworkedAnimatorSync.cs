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
        public float SendIntervalSeconds = 0.08f;

        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int MoveXParameter = Animator.StringToHash("MoveX");
        private static readonly int MoveZParameter = Animator.StringToHash("MoveZ");
        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int CrouchingParameter = Animator.StringToHash("Crouching");
        private static readonly int AimingParameter = Animator.StringToHash("Aiming");
        private static readonly int SwordEquippedParameter = Animator.StringToHash("SwordEquipped");
        private static readonly int GunEquippedParameter = Animator.StringToHash("GunEquipped");
        private static readonly int JetpackThrustingParameter = Animator.StringToHash("JetpackThrusting");
        private static readonly int DancingParameter = Animator.StringToHash("Dancing");
        private static readonly int BreakdancingParameter = Animator.StringToHash("Breakdancing");

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

        private void Update()
        {
            // IsLiveOwner, not bare IsOwner: a body with no initialised NetworkObject (singleplayer, the baked player before a session starts) makes
            // FishNet's own IsOwner throw a NullReferenceException every frame.
            if (_animator == null || !IsLiveOwner)
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
                _animator.GetBool(GunEquippedParameter),
                _animator.GetBool(JetpackThrustingParameter),
                _animator.GetBool(DancingParameter),
                _animator.GetBool(BreakdancingParameter));
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendStateToServer(float speed, float moveX, float moveZ, bool grounded,
            bool crouching, bool aiming, bool swordEquipped, bool gunEquipped, bool jetpackThrusting,
            bool dancing, bool breakdancing)
        {
            BroadcastState(speed, moveX, moveZ, grounded, crouching, aiming,
                swordEquipped, gunEquipped, jetpackThrusting, dancing, breakdancing);
        }

        /// <summary>
        /// ExcludeOwner -- the owner's own Animator already carries these
        /// exact values live from its own Tick; applying them back a network
        /// round-trip later would only ever be redundant, never more correct.
        /// </summary>
        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastState(float speed, float moveX, float moveZ, bool grounded,
            bool crouching, bool aiming, bool swordEquipped, bool gunEquipped, bool jetpackThrusting,
            bool dancing, bool breakdancing)
        {
            if (_animator == null)
            {
                return;
            }
            _animator.SetFloat(SpeedParameter, speed);
            _animator.SetFloat(MoveXParameter, moveX);
            _animator.SetFloat(MoveZParameter, moveZ);
            _animator.SetBool(GroundedParameter, grounded);
            _animator.SetBool(CrouchingParameter, crouching);
            _animator.SetBool(AimingParameter, aiming);
            _animator.SetBool(SwordEquippedParameter, swordEquipped);
            _animator.SetBool(GunEquippedParameter, gunEquipped);
            _animator.SetBool(JetpackThrustingParameter, jetpackThrusting);
            _animator.SetBool(DancingParameter, dancing);
            _animator.SetBool(BreakdancingParameter, breakdancing);
        }
    }
}
