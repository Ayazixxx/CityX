using CityX.Core;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Net
{
    /// <summary>
    /// Turns a <see cref="GameMode"/> into a running FishNet topology.
    ///
    /// This is the single place where "which mode am I" becomes "what do I
    /// start", and it is deliberately the ONLY place. Everything downstream
    /// asks FishNet whether it is server or client; nothing downstream should
    /// ever branch on GameMode again. That is what keeps the three modes from
    /// drifting into three games.
    ///
    /// Attach to a GameObject that also has a NetworkManager, or leave the
    /// NetworkManager reference empty and it will find one in the scene.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkBootstrap : MonoBehaviour
    {
        [Tooltip("Mode used when the process was launched with no -mode argument (e.g. pressing Play in the editor).")]
        [SerializeField] private GameMode _editorDefaultMode = GameMode.Singleplayer;

        [Tooltip("Bind address for a server that should be reachable from other machines. 0.0.0.0 listens on every interface.")]
        [SerializeField] private string _publicBindAddress = "0.0.0.0";

        /// <summary>
        /// A singleplayer server binds here and nowhere else, so nothing
        /// outside this machine can reach it. This is what "singleplayer"
        /// actually means at the socket level -- see
        /// GameModeInfo.AcceptsRemoteConnections.
        /// </summary>
        public const string LoopbackBindAddress = "127.0.0.1";

        private NetworkManager _networkManager;

        /// <summary>The topology this process resolved to. Set before anything starts.</summary>
        public GameMode Mode { get; private set; }

        /// <summary>The parsed launch configuration this process started with.</summary>
        public LaunchArgs Args { get; private set; }

        private void Awake()
        {
            _networkManager = GetComponent<NetworkManager>();
            if (_networkManager == null)
            {
                _networkManager = FindAnyObjectByType<NetworkManager>();
            }
        }

        private void Start()
        {
            LaunchArgs args = LaunchArgs.Parse(System.Environment.GetCommandLineArgs());

            if (!args.IsValid)
            {
                // A misconfigured shard must not come up half-right and start
                // accepting players onto the wrong world. Refuse loudly.
                Debug.LogError($"[CityX] bad launch arguments: {args.Error}");
                Shutdown();
                return;
            }

            // No -mode on the command line means somebody pressed Play, or
            // double-clicked the exe. Fall back to the inspector default
            // rather than assuming.
            bool modeWasSpecified = HasModeArgument(System.Environment.GetCommandLineArgs());
            GameMode mode = modeWasSpecified ? args.Mode : _editorDefaultMode;

            Begin(mode, args);
        }

        /// <summary>
        /// Starts the topology for <paramref name="mode"/>. Public and
        /// parameterised so tests can drive it without a command line.
        /// </summary>
        public void Begin(GameMode mode, LaunchArgs args)
        {
            Mode = mode;
            Args = args;

            if (_networkManager == null)
            {
                Debug.LogError("[CityX] NetworkBootstrap has no NetworkManager; nothing started.");
                return;
            }

            Transport transport = _networkManager.TransportManager.Transport;
            if (transport == null)
            {
                Debug.LogError("[CityX] NetworkManager has no Transport assigned; nothing started.");
                return;
            }

            transport.SetPort(args.Port);

            if (GameModeInfo.RunsServer(mode))
            {
                // Singleplayer hides on loopback; every other server is meant
                // to be reachable. This one line is the entire difference
                // between "my game" and "a shard".
                string bind = GameModeInfo.AcceptsRemoteConnections(mode)
                    ? _publicBindAddress
                    : LoopbackBindAddress;
                transport.SetServerBindAddress(bind, IPAddressType.IPv4);
                _networkManager.ServerManager.StartConnection(args.Port);
            }

            if (GameModeInfo.RunsClient(mode))
            {
                // A host connects to its own server. Singleplayer and a
                // private-session host take the same path here -- they differ
                // only in who else can get in.
                string address = GameModeInfo.RunsServer(mode) ? LoopbackBindAddress : args.Address;
                _networkManager.ClientManager.StartConnection(address, args.Port);
            }

            Debug.Log($"[CityX] started {mode} -- {DescribeTopology(mode, args)}");
        }

        /// <summary>Stops whatever is running. Safe to call when nothing is.</summary>
        public void Shutdown()
        {
            if (_networkManager == null)
            {
                return;
            }
            if (_networkManager.ClientManager.Started)
            {
                _networkManager.ClientManager.StopConnection();
            }
            if (_networkManager.ServerManager.Started)
            {
                _networkManager.ServerManager.StopConnection(true);
            }
        }

        private static bool HasModeArgument(string[] argv)
        {
            if (argv == null)
            {
                return false;
            }
            foreach (string a in argv)
            {
                if (a != null && a.ToLowerInvariant() == "-mode")
                {
                    return true;
                }
            }
            return false;
        }

        private string DescribeTopology(GameMode mode, LaunchArgs args)
        {
            if (!GameModeInfo.RunsServer(mode))
            {
                return $"client -> {args.Address}:{args.Port}";
            }
            string bind = GameModeInfo.AcceptsRemoteConnections(mode) ? _publicBindAddress : LoopbackBindAddress;
            string client = GameModeInfo.RunsClient(mode) ? " + local client" : " (headless)";
            return $"server on {bind}:{args.Port}{client}, seed {args.WorldSeed}";
        }
    }
}
