using FishNet.Object;
using UnityEngine;

namespace CityX.Spike
{
    /// <summary>
    /// A stand-in for one server-simulated entity: a faction NPC, a wild
    /// animal, or a player's summon. Deliberately a capsule with no art, no
    /// combat and no pathing.
    ///
    /// What it exists to cost: one server tick of movement work per entity,
    /// plus one entity's worth of replication traffic. Those are the two
    /// things that decide whether 200-400 entities is viable, and they are
    /// what PORT_PLAN's Phase 0 is trying to measure. Adding behaviour on top
    /// would make the numbers prettier and meaningless.
    ///
    /// Movement runs on the SERVER ONLY, off the network tick rather than
    /// Update. That is not a detail -- it is the governing rule applied to
    /// NPCs. Clients receive replicated positions; they never simulate these
    /// themselves, so there is nothing to disagree about.
    /// </summary>
    public class SpikeAgent : NetworkBehaviour
    {
        /// <summary>Metres per second. Fast enough that replication is continuous, not idle.</summary>
        private const float MinSpeed = 1.5f;
        private const float MaxSpeed = 4.0f;

        /// <summary>How quickly heading drifts, in radians per second.</summary>
        private const float TurnRate = 0.6f;

        /// <summary>
        /// Agents turn back rather than wandering to infinity. A bounded
        /// field keeps entity density realistic for interest management --
        /// entities scattered across an unbounded plane would make any
        /// distance-based culling look far better than it really is.
        /// </summary>
        private const float FieldRadius = 120.0f;

        private float _heading;
        private float _speed;
        private bool _driving;

        /// <summary>Server-side count of live agents. Cheap sanity check for tests and for the HUD.</summary>
        public static int LiveCount { get; private set; }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Deterministic per-object so a run is reproducible: two runs with
            // the same entity count produce the same motion, which matters
            // when comparing tick-time measurements between them.
            int seed = ObjectId * 7919;
            System.Random rng = new System.Random(seed);
            _heading = (float)rng.NextDouble() * Mathf.PI * 2.0f;
            _speed = Mathf.Lerp(MinSpeed, MaxSpeed, (float)rng.NextDouble());

            _driving = true;
            LiveCount++;
            TimeManager.OnTick += OnTick;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_driving)
            {
                TimeManager.OnTick -= OnTick;
                _driving = false;
                LiveCount--;
            }
        }

        private void OnTick()
        {
            float delta = (float)TimeManager.TickDelta;

            // Drift the heading, and steer back toward the middle once the
            // agent reaches the edge of the field.
            _heading += (Mathf.PerlinNoise(ObjectId * 0.37f, (float)TimeManager.Tick * 0.01f) - 0.5f) * TurnRate * delta * 10.0f;

            Vector3 position = transform.position;
            Vector3 flat = new Vector3(position.x, 0.0f, position.z);
            if (flat.sqrMagnitude > FieldRadius * FieldRadius)
            {
                float inward = Mathf.Atan2(-flat.z, -flat.x);
                _heading = Mathf.LerpAngle(_heading * Mathf.Rad2Deg, inward * Mathf.Rad2Deg, 0.25f) * Mathf.Deg2Rad;
            }

            Vector3 step = new Vector3(Mathf.Cos(_heading), 0.0f, Mathf.Sin(_heading)) * (_speed * delta);
            transform.position = position + step;
        }

        /// <summary>
        /// Test hook. Static counters survive a domain that is never reloaded
        /// between play-mode entries (this project disables domain reload for
        /// speed), so a run that ends badly could otherwise poison the next.
        /// </summary>
        public static void ResetLiveCount()
        {
            LiveCount = 0;
        }
    }
}
