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
        /// THE RULE. Every mode either owns the simulation or is connected to
        /// one that does. There must be no mode in which a client simulates
        /// gameplay by itself -- that is the divergence PORT_PLAN calls "the
        /// classic way these ports fail".
        /// </summary>
        [Test]
        public void EveryModeEitherHostsTheSimulationOrConnectsToOne()
        {
            foreach (GameMode mode in AllModes)
            {
                bool hostsIt = GameModeInfo.RunsServer(mode);
                bool connectsToIt = GameModeInfo.UsesTransport(mode);
                Assert.IsTrue(
                    hostsIt || connectsToIt,
                    $"{mode} neither runs a server nor opens a transport, so it would have to " +
                    "simulate gameplay locally with no authority. That breaks the one rule.");
            }
        }

        /// <summary>
        /// Singleplayer is the ONLY mode allowed to skip the wire, and it is
        /// still a full server. If a second transport-less mode ever appears,
        /// somebody is building an offline special case.
        /// </summary>
        [Test]
        public void SingleplayerIsTheOnlyTransportlessMode_AndItStillRunsTheServer()
        {
            foreach (GameMode mode in AllModes)
            {
                if (mode == GameMode.Singleplayer)
                {
                    Assert.IsFalse(GameModeInfo.UsesTransport(mode), "singleplayer should need no socket");
                    Assert.IsTrue(GameModeInfo.RunsServer(mode), "singleplayer is a server with one local client");
                    Assert.IsTrue(GameModeInfo.RunsClient(mode), "singleplayer has a local client");
                    continue;
                }
                Assert.IsTrue(
                    GameModeInfo.UsesTransport(mode),
                    $"{mode} skips the transport, which only singleplayer may do");
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
            Assert.IsTrue(GameModeInfo.UsesTransport(GameMode.RemoteClient));
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
