using System.Diagnostics;
using FishNet.Managing;
using FishNet.Transporting;
using Debug = UnityEngine.Debug;

namespace CityX.Spike
{
    /// <summary>
    /// Measures the two numbers PORT_PLAN Phase 0 actually turns on:
    /// how long a server tick takes, and how many bytes a client receives.
    ///
    /// Both are measured rather than estimated, because the whole point of a
    /// spike is to replace opinion with data. A spike that concludes "feels
    /// fine" has not done its job.
    ///
    /// Not a MonoBehaviour: it attaches to managers that already exist, so a
    /// test can measure a server and a client separately in the same process
    /// without either of them knowing they are being watched.
    /// </summary>
    public class SpikeMetrics
    {
        private readonly Stopwatch _tickWatch = new Stopwatch();

        private NetworkManager _server;
        private Transport _clientTransport;

        // --- server tick ---
        private double _tickTotalMs;
        private int _tickSamples;
        private double _tickWorstMs;

        // --- client traffic ---
        private long _bytesReceived;
        private double _measureStartedAt;

        /// <summary>Mean server tick duration in milliseconds over the sample window.</summary>
        public double MeanTickMs => _tickSamples == 0 ? 0.0 : _tickTotalMs / _tickSamples;

        /// <summary>Worst single tick in the window. A good mean with a terrible worst case still stutters.</summary>
        public double WorstTickMs => _tickWorstMs;

        public int TickSamples => _tickSamples;

        /// <summary>Total bytes this client received from the server during the window.</summary>
        public long BytesReceived => _bytesReceived;

        /// <summary>Received bytes per second, the number that decides hosting cost.</summary>
        public double BytesPerSecond
        {
            get
            {
                double elapsed = UnityEngine.Time.realtimeSinceStartupAsDouble - _measureStartedAt;
                return elapsed <= 0.0 ? 0.0 : _bytesReceived / elapsed;
            }
        }

        /// <summary>Starts timing the server's tick loop.</summary>
        public void AttachServer(NetworkManager server)
        {
            _server = server;
            _server.TimeManager.OnPreTick += OnPreTick;
            _server.TimeManager.OnPostTick += OnPostTick;
        }

        /// <summary>
        /// Counts bytes arriving at a client, straight off the transport.
        ///
        /// Measured at the transport rather than through FishNet's own
        /// statistics because those live behind an API the package marks for
        /// removal. Raw received bytes are what the hosting bill is actually
        /// computed from anyway.
        /// </summary>
        public void AttachClient(Transport clientTransport)
        {
            _clientTransport = clientTransport;
            _clientTransport.OnClientReceivedData += OnClientReceivedData;
        }

        public void Detach()
        {
            if (_server != null)
            {
                _server.TimeManager.OnPreTick -= OnPreTick;
                _server.TimeManager.OnPostTick -= OnPostTick;
                _server = null;
            }
            if (_clientTransport != null)
            {
                _clientTransport.OnClientReceivedData -= OnClientReceivedData;
                _clientTransport = null;
            }
        }

        /// <summary>
        /// Clears every sample. Call after the world has settled so warm-up
        /// costs -- the initial burst that spawns hundreds of entities, JIT,
        /// allocations -- do not get averaged into the steady-state numbers
        /// and quietly ruin them.
        /// </summary>
        public void ResetWindow()
        {
            _tickTotalMs = 0.0;
            _tickSamples = 0;
            _tickWorstMs = 0.0;
            _bytesReceived = 0;
            _measureStartedAt = UnityEngine.Time.realtimeSinceStartupAsDouble;
        }

        private void OnPreTick()
        {
            _tickWatch.Restart();
        }

        private void OnPostTick()
        {
            _tickWatch.Stop();
            double ms = _tickWatch.Elapsed.TotalMilliseconds;
            _tickTotalMs += ms;
            _tickSamples++;
            if (ms > _tickWorstMs)
            {
                _tickWorstMs = ms;
            }
        }

        private void OnClientReceivedData(ClientReceivedDataArgs args)
        {
            _bytesReceived += args.Data.Count;
        }

        public string Describe(string label, int entityCount)
        {
            return $"[CityX.Spike] {label}: {entityCount} entities | " +
                   $"tick mean {MeanTickMs:0.000}ms worst {WorstTickMs:0.000}ms over {TickSamples} ticks | " +
                   $"client in {BytesPerSecond / 1024.0:0.0} KB/s";
        }

        public void Log(string label, int entityCount)
        {
            Debug.Log(Describe(label, entityCount));
        }
    }
}
