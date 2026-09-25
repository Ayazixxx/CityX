using FishNet.Object;
using UnityEngine;

namespace CityX.Spike
{
    /// <summary>
    /// A connected player's presence in the spike world.
    ///
    /// It exists mainly to give interest management something to measure
    /// FROM: FishNet's DistanceCondition computes distance between an entity
    /// and the objects a connection OWNS, so without an owned object per
    /// connection there is no "here" to be near, and every entity would be
    /// visible to everyone.
    ///
    /// No input, no prediction yet -- those come with the latency work. This
    /// is a position the server can move so a test can drive a player across
    /// the field and watch what becomes visible.
    /// </summary>
    public class SpikePlayer : NetworkBehaviour
    {
        /// <summary>Server-side count of connected player presences.</summary>
        public static int LiveCount { get; private set; }

        private bool _counted;

        public override void OnStartServer()
        {
            base.OnStartServer();
            _counted = true;
            LiveCount++;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_counted)
            {
                _counted = false;
                LiveCount--;
            }
        }

        /// <summary>
        /// Moves this player. Server-authoritative by construction: there is
        /// no client-side path that sets position, so the spike cannot
        /// accidentally prove something a real client could not do.
        /// </summary>
        public void ServerMoveTo(Vector3 position)
        {
            if (!IsServerStarted)
            {
                Debug.LogError("[CityX.Spike] ServerMoveTo called off the server.");
                return;
            }
            transform.position = position;
        }

        public static void ResetLiveCount()
        {
            LiveCount = 0;
        }
    }
}
