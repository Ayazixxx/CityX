using CityX.World;
using FishNet;
using FishNet.Managing;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// ONE CLOCK FOR EVERY PLAYER'S TRAFFIC LIGHTS (docs/TRAFFIC_CONTROL.md, docs/MULTIPLAYER_STATE.md). A light is a pure function of the clock, so nothing is sent per
    /// light: in a session this gives <see cref="SignalClock"/> the session's own time, which every machine already keeps in step with the server's (FishNet's tick).
    /// Alone, the clock is this machine's.
    /// </summary>
    public sealed class NetworkedSignalClock : MonoBehaviour
    {
        [Tooltip("The session to work in; empty: the running one (FishNet's InstanceFinder). Tests bind one per machine.")]
        public NetworkManager Manager;

        private NetworkManager _manager;
        private bool _sharing;

        /// <summary>The session's seconds as this machine has them; this machine's own time out of a session.</summary>
        public double Seconds => InSession ? _manager.TimeManager.TicksToTime(_manager.TimeManager.Tick) : Time.timeAsDouble;

        private bool InSession => _manager != null && (_manager.IsServerStarted || _manager.IsClientStarted);

        private void Update()
        {
            if (_manager == null)
            {
                _manager = Manager != null ? Manager : InstanceFinder.NetworkManager;
            }
            bool share = InSession && Manager == null;
            if (share == _sharing)
            {
                return;
            }
            _sharing = share;
            if (share)
            {
                SignalClock.Shared = () => Seconds;
            }
            else
            {
                SignalClock.Shared = null;
            }
        }

        private void OnDisable()
        {
            if (_sharing)
            {
                _sharing = false;
                SignalClock.Shared = null;
            }
        }
    }
}
