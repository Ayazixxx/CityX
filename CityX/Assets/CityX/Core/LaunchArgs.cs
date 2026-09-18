using System;
using System.Globalization;

namespace CityX.Core
{
    /// <summary>
    /// Command-line configuration for a launched process.
    ///
    /// A dedicated shard has no menu to click, so its topology has to arrive
    /// on argv. The Phase 0 spike needs this before anything else: it is how
    /// one headless server and thirty client processes get told what they
    /// are.
    ///
    /// Deliberately pure -- no UnityEngine, no static state, no file access --
    /// so the whole parser is covered by EditMode tests that run in
    /// milliseconds without opening a scene.
    ///
    /// Usage:
    ///   CityX.exe -mode dedicated -port 7770 -seed 12345
    ///   CityX.exe -mode client -address 203.0.113.7 -port 7770
    ///   CityX.exe                                    (defaults to singleplayer)
    /// </summary>
    public readonly struct LaunchArgs
    {
        public const ushort DefaultPort = 7770;
        public const string DefaultAddress = "127.0.0.1";

        public readonly GameMode Mode;
        public readonly string Address;
        public readonly ushort Port;
        public readonly int WorldSeed;

        /// <summary>Null when parsing succeeded; a human-readable reason when it did not.</summary>
        public readonly string Error;

        public bool IsValid => Error == null;

        private LaunchArgs(GameMode mode, string address, ushort port, int worldSeed, string error)
        {
            Mode = mode;
            Address = address;
            Port = port;
            WorldSeed = worldSeed;
            Error = error;
        }

        /// <summary>What you get with no arguments at all: one player, no socket.</summary>
        public static LaunchArgs Default =>
            new LaunchArgs(GameMode.Singleplayer, DefaultAddress, DefaultPort, 0, null);

        /// <summary>
        /// Builds a valid LaunchArgs directly, for a caller that already has
        /// its own configuration (the title screen's own Host/Join UI) rather
        /// than an argv to parse. The constructor is private so every value
        /// this type can hold either came from Parse's own validation or from
        /// here -- there is no third, unvalidated way to build one.
        /// </summary>
        public static LaunchArgs For(GameMode mode, string address, ushort port, int worldSeed = 0) =>
            new LaunchArgs(mode, address, port, worldSeed, null);

        private static LaunchArgs Invalid(string error) =>
            new LaunchArgs(GameMode.Singleplayer, DefaultAddress, DefaultPort, 0, error);

        /// <summary>
        /// Parses argv. Unrecognised arguments are IGNORED rather than
        /// rejected, because Unity player builds are handed a pile of engine
        /// flags (-batchmode, -nographics, -logFile ...) that are none of our
        /// business. Only our own flags are validated, and a malformed value
        /// for one of ours is a hard error rather than a silent default --
        /// a shard that quietly comes up on the wrong port is worse than one
        /// that refuses to start.
        /// </summary>
        public static LaunchArgs Parse(string[] argv)
        {
            if (argv == null)
            {
                return Default;
            }

            GameMode mode = GameMode.Singleplayer;
            string address = DefaultAddress;
            ushort port = DefaultPort;
            int seed = 0;

            for (int i = 0; i < argv.Length; i++)
            {
                string flag = argv[i];
                if (string.IsNullOrEmpty(flag) || flag[0] != '-')
                {
                    continue;
                }

                switch (flag.ToLowerInvariant())
                {
                    case "-mode":
                    {
                        if (!TryTakeValue(argv, ref i, out string raw))
                        {
                            return Invalid("-mode requires a value (singleplayer|host|dedicated|client)");
                        }
                        if (!TryParseMode(raw, out mode))
                        {
                            return Invalid($"unknown -mode '{raw}' (expected singleplayer|host|dedicated|client)");
                        }
                        break;
                    }
                    case "-address":
                    {
                        if (!TryTakeValue(argv, ref i, out string raw))
                        {
                            return Invalid("-address requires a value");
                        }
                        address = raw;
                        break;
                    }
                    case "-port":
                    {
                        if (!TryTakeValue(argv, ref i, out string raw))
                        {
                            return Invalid("-port requires a value");
                        }
                        if (!ushort.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port == 0)
                        {
                            return Invalid($"-port '{raw}' is not a valid port (1-65535)");
                        }
                        break;
                    }
                    case "-seed":
                    {
                        if (!TryTakeValue(argv, ref i, out string raw))
                        {
                            return Invalid("-seed requires a value");
                        }
                        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out seed))
                        {
                            return Invalid($"-seed '{raw}' is not an integer");
                        }
                        break;
                    }
                }
            }

            return new LaunchArgs(mode, address, port, seed, null);
        }

        /// <summary>
        /// Consumes the value that follows a flag. A following token that is
        /// itself a flag does not count as a value, so `-port -batchmode`
        /// reports a missing value instead of silently eating the next flag.
        ///
        /// A leading '-' is NOT enough to call something a flag: `-seed -99`
        /// is a legitimate negative seed, and seeds are the world, so they
        /// have to survive the round trip exactly. A '-' followed by a digit
        /// is therefore a value; a '-' followed by anything else is a flag.
        /// </summary>
        private static bool TryTakeValue(string[] argv, ref int i, out string value)
        {
            value = null;
            if (i + 1 >= argv.Length)
            {
                return false;
            }
            string candidate = argv[i + 1];
            if (string.IsNullOrEmpty(candidate) || LooksLikeFlag(candidate))
            {
                return false;
            }
            value = candidate;
            i++;
            return true;
        }

        private static bool LooksLikeFlag(string token)
        {
            if (string.IsNullOrEmpty(token) || token[0] != '-')
            {
                return false;
            }
            // "-5" / "-2147483648" are values, not flags.
            return token.Length < 2 || !char.IsDigit(token[1]);
        }

        private static bool TryParseMode(string raw, out GameMode mode)
        {
            switch (raw.ToLowerInvariant())
            {
                case "singleplayer":
                case "sp":
                case "offline":
                    mode = GameMode.Singleplayer;
                    return true;
                case "host":
                case "hosted":
                case "hostedworld":
                case "listen":
                    mode = GameMode.HostedWorld;
                    return true;
                case "dedicated":
                case "server":
                case "shard":
                    mode = GameMode.DedicatedShard;
                    return true;
                case "client":
                case "join":
                case "remote":
                    mode = GameMode.RemoteClient;
                    return true;
                default:
                    mode = GameMode.Singleplayer;
                    return false;
            }
        }

        public override string ToString()
        {
            if (!IsValid)
            {
                return $"LaunchArgs(INVALID: {Error})";
            }
            return Mode == GameMode.Singleplayer
                ? $"LaunchArgs({Mode} loopback:{Port} seed={WorldSeed})"
                : $"LaunchArgs({Mode} {Address}:{Port} seed={WorldSeed})";
        }
    }
}
