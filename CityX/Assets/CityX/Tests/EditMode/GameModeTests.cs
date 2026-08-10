using System;
using CityX.Core;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Guards PORT_PLAN.md's governing rule at the type level.
    ///
    /// These are not "does the enum have four values" tests. Each one encodes
    /// a claim the whole port depends on, written so that a future change
    /// which quietly reintroduces client-side authority fails here rather
    /// than three months later in a desync bug.
    /// </summary>
    public class GameModeTests
    {
        private static GameMode[] AllModes => (GameMode[])Enum.GetValues(typeof(GameMode));

        /// <summary>
        /// THE RULE. Anywhere there is a local view, there is an
        /// authoritative simulation behind it -- either running in this
        /// process, or on the far end of a connection this process makes.
        /// There must be no mode in which a client simulates gameplay by
        /// itself; that is the divergence PORT_PLAN calls "the classic way
        /// these ports fail".
        /// </summary>
        [Test]
        public void NoClientEverSimulatesWithoutAnAuthorityBehindIt()
        {
            foreach (GameMode mode in AllModes)
            {
                if (!GameModeInfo.RunsClient(mode))
                {
                    continue;
                }
                bool ownsTheSimulation = GameModeInfo.RunsServer(mode);
                bool dialsOutToOne = GameModeInfo.ConnectsToRemoteServer(mode);
                Assert.IsTrue(
                    ownsTheSimulation || dialsOutToOne,
                    $"{mode} has a local client but neither runs the simulation nor connects " +
                    "to one, so it would have to simulate gameplay with no authority.");
            }
        }

        /// <summary>
        /// Singleplayer still runs the full server -- it is a host, not an
        /// offline special case. What makes it singleplayer is that nobody
        /// else can reach it, NOT that it skips the network stack (FishNet
        /// has no transport-less mode; see GameModeInfo.AcceptsRemoteConnections).
        /// </summary>
        [Test]
        public void SingleplayerIsAHostThatNobodyElseCanReach()
        {
            Assert.IsTrue(GameModeInfo.RunsServer(GameMode.Singleplayer),
                "singleplayer is a server with one local client");
            Assert.IsTrue(GameModeInfo.RunsClient(GameMode.Singleplayer),
                "singleplayer has a local client");
            Assert.IsFalse(GameModeInfo.AcceptsRemoteConnections(GameMode.Singleplayer),
                "singleplayer must not be reachable from another machine");
            Assert.IsFalse(GameModeInfo.ConnectsToRemoteServer(GameMode.Singleplayer),
                "singleplayer owns its own simulation, so it dials nobody");
        }

        /// <summary>
        /// Singleplayer is the ONLY server that hides. If a second
        /// unreachable server mode appears, somebody is building an offline
        /// special case.
        /// </summary>
        [Test]
        public void EveryOtherServerModeIsReachable()
        {
            foreach (GameMode mode in AllModes)
            {
                if (!GameModeInfo.RunsServer(mode) || mode == GameMode.Singleplayer)
                {
                    continue;
                }
                Assert.IsTrue(
                    GameModeInfo.AcceptsRemoteConnections(mode),
                    $"{mode} runs a server nobody can connect to, which only singleplayer may do");
            }
        }

        /// <summary>
        /// Hosting and dialling out are mutually exclusive and exhaustive:
        /// you own the simulation or you connect to one, never both, never
        /// neither.
        /// </summary>
        [Test]
        public void OwningTheSimulationAndDiallingOutAreExactOpposites()
        {
            foreach (GameMode mode in AllModes)
            {
                Assert.AreNotEqual(
                    GameModeInfo.RunsServer(mode),
                    GameModeInfo.ConnectsToRemoteServer(mode),
                    $"{mode} must either own the simulation or connect to one, not both or neither");
            }
        }

        [Test]
        public void DedicatedShardIsHeadless()
        {
            Assert.IsTrue(GameModeInfo.RunsServer(GameMode.DedicatedShard));
            Assert.IsFalse(GameModeInfo.RunsClient(GameMode.DedicatedShard),
                "a dedicated shard must not stand up a local view -- it has no player at the keyboard");
        }

        [Test]
        public void RemoteClientOwnsNoSimulation()
        {
            Assert.IsFalse(GameModeInfo.RunsServer(GameMode.RemoteClient));
            Assert.IsTrue(GameModeInfo.RunsClient(GameMode.RemoteClient));
            Assert.IsTrue(GameModeInfo.ConnectsToRemoteServer(GameMode.RemoteClient),
                "a remote client must dial out -- it owns no simulation of its own");
            Assert.IsFalse(GameModeInfo.AcceptsRemoteConnections(GameMode.RemoteClient),
                "a client listens for nobody; it is the one doing the connecting");
        }

        /// <summary>
        /// IsHost is the risky case: the process can read authoritative state
        /// directly and skip the request/replicate round trip without any
        /// compile error. Pinning exactly which modes it covers makes that
        /// set easy to audit.
        /// </summary>
        [Test]
        public void IsHostCoversExactlySingleplayerAndHostedWorld()
        {
            Assert.IsTrue(GameModeInfo.IsHost(GameMode.Singleplayer));
            Assert.IsTrue(GameModeInfo.IsHost(GameMode.HostedWorld));
            Assert.IsFalse(GameModeInfo.IsHost(GameMode.DedicatedShard));
            Assert.IsFalse(GameModeInfo.IsHost(GameMode.RemoteClient));
        }

        /// <summary>
        /// A new enum value must be given deliberate topology. Without this,
        /// the `default:` arms in GameModeInfo silently classify anything new
        /// as "no server, no client", which would pass the other tests by
        /// accident but is never what anyone meant.
        /// </summary>
        [Test]
        public void NoModeIsClassifiedAsNeitherServerNorClient()
        {
            foreach (GameMode mode in AllModes)
            {
                Assert.IsTrue(
                    GameModeInfo.RunsServer(mode) || GameModeInfo.RunsClient(mode),
                    $"{mode} was added to the enum without being given topology in GameModeInfo");
            }
        }
    }
}
