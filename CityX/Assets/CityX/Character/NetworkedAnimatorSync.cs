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
    /// ONE-SHOT TRIGGERS (Jump, Dash, punches) ARE DELIBERATELY NOT
    /// REPLICATED HERE. Detecting "a trigger fired this frame" from outside
    /// PlayerLocomotion would need it to expose new eventing it does not
    /// have; the continuous parameters below already carry the visible
    /// walk/run/idle/crouch/aim/dance state that matters most for two
    /// friends recognising each other's movement, and full combat/action
    /// sync is its own later, separate step (see multiplayer_sync_roadmap
    /// and this session's own gap-analysis notes) -- not silently skipped,
    /// just not in this pass's scope.
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
        private static readonly int JetpackThrustingParameter = Animator.StringToHash("JetpackThrusting");
        private static readonly int DancingParameter = Animator.StringToHash("Dancing");
        private static readonly int BreakdancingParameter = Animator.StringToHash("Breakdancing");

        private Animator _animator;
        private float _sinceSend;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (_animator == null || !IsOwner)
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
                _animator.GetBool(JetpackThrustingParameter),
                _animator.GetBool(DancingParameter),
                _animator.GetBool(BreakdancingParameter));
        }

        [ServerRpc(RequireOwnership = true)]
        private void SendStateToServer(float speed, float moveX, float moveZ, bool grounded,
            bool crouching, bool aiming, bool swordEquipped, bool jetpackThrusting,
            bool dancing, bool breakdancing)
        {
            BroadcastState(speed, moveX, moveZ, grounded, crouching, aiming,
                swordEquipped, jetpackThrusting, dancing, breakdancing);
        }

        /// <summary>
        /// ExcludeOwner -- the owner's own Animator already carries these
        /// exact values live from its own Tick; applying them back a network
        /// round-trip later would only ever be redundant, never more correct.
        /// </summary>
        [ObserversRpc(ExcludeOwner = true)]
        private void BroadcastState(float speed, float moveX, float moveZ, bool grounded,
            bool crouching, bool aiming, bool swordEquipped, bool jetpackThrusting,
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
            _animator.SetBool(JetpackThrustingParameter, jetpackThrusting);
            _animator.SetBool(DancingParameter, dancing);
            _animator.SetBool(BreakdancingParameter, breakdancing);
        }
    }
}
