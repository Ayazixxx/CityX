using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Utility.Template;
using UnityEngine;

namespace CityX.Spike
{
    /// <summary>
    /// A player that moves with client-side prediction and server
    /// reconciliation.
    ///
    /// This is the last Phase 0 criterion and the only one about FEEL rather
    /// than cost: "movement feels responsive at 100ms simulated latency".
    ///
    /// Without prediction, pressing forward on a 100ms link means waiting
    /// 100ms to see anything move -- the input goes to the server, the server
    /// moves you, the new position comes back. That is the difference between
    /// a game that feels alive and one that feels like remote desktop.
    ///
    /// With prediction the client simulates the move IMMEDIATELY using the
    /// same code the server will run, then corrects itself when the server's
    /// authoritative answer arrives. The governing rule is intact: the server
    /// still decides. The client is guessing what the server will say, not
    /// deciding for itself.
    ///
    /// Input is a field rather than a keyboard read, so tests can drive it.
    /// A real controller reads input here and nothing else changes.
    /// </summary>
    public class SpikePredictedPlayer : TickNetworkBehaviour
    {
        public struct MoveData : IReplicateData
        {
            public Vector2 Input;
            private uint _tick;

            public MoveData(Vector2 input)
            {
                Input = input;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct StateData : IReconcileData
        {
            public Vector3 Position;
            private uint _tick;

            public StateData(Vector3 position)
            {
                Position = position;
                _tick = 0;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        /// <summary>Metres per second.</summary>
        public const float MoveRate = 6.0f;

        /// <summary>
        /// Desired movement, set by whoever owns this object. A real build
        /// writes the input device here; the spike's tests write it directly.
        /// </summary>
        public Vector2 Input;

        /// <summary>
        /// Server-side count of corrections applied. A prediction system that
        /// never reconciles is not predicting, it is just moving locally and
        /// hoping -- so this being non-trivial is a sign of health, not a bug.
        /// </summary>
        public int ReconcileCount { get; private set; }

        private void Awake()
        {
            SetTickCallbacks(TickCallback.Tick | TickCallback.PostTick);
        }

        protected override void TimeManager_OnTick()
        {
            PerformReplicate(BuildMoveData());
        }

        protected override void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }

        private MoveData BuildMoveData()
        {
            // Only the controller builds input. For an owned player that is
            // the owning client; for a server-controlled object it would be
            // the server.
            if (!IsOwner)
            {
                return default;
            }
            return new MoveData(Input);
        }

        public override void CreateReconcile()
        {
            PerformReconcile(new StateData(transform.position));
        }

        /// <summary>
        /// The movement itself. Runs on the owning client immediately, and on
        /// the server authoritatively, and AGAIN on the client during replay
        /// after a correction. Identical code in all three cases, which is
        /// exactly why prediction can work at all -- if the client simulated
        /// movement differently from the server, every tick would disagree.
        /// </summary>
        [Replicate]
        private void PerformReplicate(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
        {
            float delta = (float)TimeManager.TickDelta;
            Vector3 move = new Vector3(md.Input.x, 0.0f, md.Input.y);
            if (move.sqrMagnitude > 1.0f)
            {
                move = move.normalized;
            }
            transform.position += move * (MoveRate * delta);
        }

        [Reconcile]
        private void PerformReconcile(StateData sd, Channel channel = Channel.Unreliable)
        {
            ReconcileCount++;
            transform.position = sd.Position;
        }
    }
}
