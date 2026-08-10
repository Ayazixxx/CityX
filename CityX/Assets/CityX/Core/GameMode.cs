namespace CityX.Core
{
    /// <summary>
    /// How this process participates in the one simulation.
    ///
    /// PORT_PLAN.md's governing rule: there is exactly one simulation, it is
    /// server-authoritative, and singleplayer is a server with one local
    /// client. This enum is that rule made concrete -- it is a topology
    /// selector, NOT a feature switch. The world, the generator, the seed
    /// space and the content are identical in every value below. The only
    /// things that differ are how many players are in the world and who is
    /// running the server.
    ///
    /// Nothing in this project may branch on GameMode to decide *what the
    /// game does*. If you find yourself writing `if (mode == Singleplayer)`
    /// around gameplay logic, the rule has already been broken -- that is the
    /// exact failure mode this port exists to avoid, and it is how
    /// singleplayer and multiplayer become two divergent games.
    /// </summary>
    public enum GameMode
    {
        /// <summary>One player. A server with one local client and no transport at all.</summary>
        Singleplayer,

        /// <summary>~2-8 players. A player hosts: this process is both the server and a client.</summary>
        HostedWorld,

        /// <summary>Dozens of players. Headless dedicated build, persistent, no local client.</summary>
        DedicatedShard,

        /// <summary>This process joined somebody else's server (a HostedWorld or a DedicatedShard).</summary>
        RemoteClient,
    }

    /// <summary>
    /// The topology facts derived from a <see cref="GameMode"/>.
    ///
    /// Deliberately three orthogonal booleans rather than four hard-coded
    /// call sites, because the interesting question is almost never "which
    /// mode is this" -- it is "does this process own the simulation" or "do I
    /// need to stand up a transport". Systems should ask those.
    /// </summary>
    public static class GameModeInfo
    {
        /// <summary>True when this process owns and ticks the authoritative simulation.</summary>
        public static bool RunsServer(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Singleplayer:
                case GameMode.HostedWorld:
                case GameMode.DedicatedShard:
                    return true;
                case GameMode.RemoteClient:
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>True when a human is playing from this process and needs a local view.</summary>
        public static bool RunsClient(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Singleplayer:
                case GameMode.HostedWorld:
                case GameMode.RemoteClient:
                    return true;
                case GameMode.DedicatedShard:
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True when a real socket is needed. False ONLY for singleplayer,
        /// which still runs the full server-authoritative path -- it just
        /// short-circuits the wire. That is the whole trick: singleplayer is
        /// not a different code path, it is the same code path with the
        /// transport removed.
        /// </summary>
        public static bool UsesTransport(GameMode mode)
        {
            return mode != GameMode.Singleplayer;
        }

        /// <summary>
        /// True when this process both hosts the simulation and plays in it,
        /// which is the case that most often breaks server-authoritative code
        /// (it can read server state directly and accidentally skip the
        /// request/replicate round trip). Named so it is easy to grep for.
        /// </summary>
        public static bool IsHost(GameMode mode)
        {
            return RunsServer(mode) && RunsClient(mode);
        }
    }
}
