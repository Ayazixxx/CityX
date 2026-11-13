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

        [Tooltip("Start the topology automatically on Start(). Turn off when something else decides when to connect -- a main menu, or a test.")]
        [SerializeField] private bool _autoStartOnPlay = true;

        /// <summary>
        /// Whether to bring the topology up automatically. A real build with
        /// a main menu will want this off and will call
        /// <see cref="Begin"/> when the player picks a mode; a dedicated
        /// shard wants it on.
        /// </summary>
        public bool AutoStartOnPlay
        {
            get => _autoStartOnPlay;
            set => _autoStartOnPlay = value;
        }

        /// <summary>
        /// A singleplayer server binds here and nowhere else, so nothing
        /// outside this machine can reach it. This is what "singleplayer"
        /// actually means at the socket level -- see
        /// GameModeInfo.AcceptsRemoteConnections.
        /// </summary>
        public const string LoopbackBindAddress = "127.0.0.1";

        /// <summary>How often a session sends and reads, a second. Every copy of the game must use the same (FishNet does not share it).</summary>
        public const ushort TicksPerSecond = 60;

        private NetworkManager _networkManager;

        /// <summary>
        /// The NetworkManager this bootstrap drives. Public so a caller that
        /// needs to know WHEN a connection attempt actually succeeds or fails
        /// (the title screen's own Join button, which -- unlike Host -- has
        /// no synchronous answer) can subscribe to its ClientManager's own
        /// OnClientConnectionState directly, rather than this class growing
        /// a duplicate event for everything FishNet already exposes.
        /// </summary>
        public NetworkManager Manager => _networkManager;

        /// <summary>The topology this process resolved to. Set before anything starts.</summary>
        public GameMode Mode { get; private set; }

        /// <summary>The parsed launch configuration this process started with.</summary>
        public LaunchArgs Args { get; private set; }

        /// <summary>
        /// Whether this copy plays by itself. Only then may its clock be stopped (the pause menu, the console): in a shared world a stopped
        /// clock freezes this player's body on everybody else's screen and everybody else's on this one. No bootstrap (most tests) is alone.
        /// </summary>
        public static bool PlaysAlone
        {
            get
            {
                NetworkBootstrap bootstrap = FindAnyObjectByType<NetworkBootstrap>();
                return bootstrap == null || bootstrap.Mode == GameMode.Singleplayer;
            }
        }

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
            if (!_autoStartOnPlay)
            {
                return;
            }

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
        ///
        /// Returns whether every socket this topology needs actually came up.
        /// A dedicated shard's own startup path has always ignored this (a
        /// misconfigured shard already refuses loudly above, in Start()), but
        /// a menu that just asked to host on a specific port needs to know
        /// synchronously whether that port was actually free -- ENet-style
        /// peers (and FishNet's own StartConnection) report a bind failure
        /// immediately, not asynchronously, so there is a real answer to
        /// give here rather than an optimistic assumption.
        /// </summary>
        public bool Begin(GameMode mode, LaunchArgs args)
        {
            Mode = mode;
            Args = args;

            if (_networkManager == null)
            {
                Debug.LogError("[CityX] NetworkBootstrap has no NetworkManager; nothing started.");
                return false;
            }

            Transport transport = _networkManager.TransportManager.Transport;
            if (transport == null)
            {
                Debug.LogError("[CityX] NetworkManager has no Transport assigned; nothing started.");
                return false;
            }

            transport.SetPort(args.Port);
            // Packets leave and are read only on a tick: at FishNet's thirty a second each hop waited a thirtieth of a second at both ends.
            _networkManager.TimeManager.SetTickRate(TicksPerSecond);
            bool ok = true;

            // A shared world goes on when this window is not the one in front (alt-tab, a second screen): stopped, this player froze on
            // the others' screens and was dropped from the session a few seconds later.
            Application.runInBackground = mode != GameMode.Singleplayer;

            if (GameModeInfo.RunsServer(mode))
            {
                // Singleplayer hides on loopback; every other server is meant
                // to be reachable. This one line is the entire difference
                // between "my game" and "a shard".
                string bind = GameModeInfo.AcceptsRemoteConnections(mode)
                    ? _publicBindAddress
                    : LoopbackBindAddress;
                transport.SetServerBindAddress(bind, IPAddressType.IPv4);
                ok &= _networkManager.ServerManager.StartConnection(args.Port);
            }

            if (GameModeInfo.RunsClient(mode))
            {
                // A host connects to its own server. Singleplayer and a
                // private-session host take the same path here -- they differ
                // only in who else can get in.
                string address = GameModeInfo.RunsServer(mode) ? LoopbackBindAddress : args.Address;
                ok &= _networkManager.ClientManager.StartConnection(address, args.Port);
            }

            if (ok)
            {
                Debug.Log($"[CityX] started {mode} -- {DescribeTopology(mode, args)}");
            }
            else
            {
                Debug.LogWarning($"[CityX] failed to start {mode} -- {DescribeTopology(mode, args)}");
            }
            return ok;
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

        /// <summary>
        /// Ends the session this process is in, for going back to the title. The manager lives on past the scene (DontDestroyOnLoad), and left alive it made the new title's own destroy
        /// itself, so Host and Join said "No networking available", the connection stayed open with the body standing on the other screens, and a solo game next found no body.
        /// </summary>
        public static void LeaveSession()
        {
            NetworkBootstrap bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            (CharacterSources.Current as System.IDisposable)?.Dispose();
            CharacterSources.Current = null;
            if (bootstrap == null || bootstrap.Mode == GameMode.Singleplayer)
            {
                return;
            }
            bootstrap.Shutdown();
            Application.runInBackground = false;
            Destroy(bootstrap.gameObject);
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
