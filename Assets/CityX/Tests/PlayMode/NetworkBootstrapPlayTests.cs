using System.Collections;
using CityX.Core;
using CityX.Net;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Transporting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The first tests with real teeth: these open actual UDP sockets,
    /// really start a FishNet server, and really connect a client to it.
    ///
    /// Everything before this proved the code COMPILES against FishNet.
    /// These prove it RUNS. That distinction matters -- PORT_PLAN's Phase 0
    /// exists because "does this stack actually work for us" is the entire
    /// question behind the port, and a compile is not an answer to it.
    ///
    /// Deliberately still tiny: one process, host topology, no entities, no
    /// prediction. The 30-clients-and-200-AI numbers come later; this is the
    /// foundation they stand on, and if it were broken every measurement
    /// after it would be meaningless.
    /// </summary>
    public class NetworkBootstrapPlayTests
    {
        /// <summary>
        /// High and unusual, to avoid colliding with anything the developer
        /// happens to be running. Each test bumps it so a socket left in
        /// TIME_WAIT by the previous test cannot make the next one flaky --
        /// that failure mode is intermittent and expensive to diagnose, so
        /// it is designed out rather than debugged later.
        /// </summary>
        private static ushort _nextPort = 41750;

        private GameObject _host;
        private NetworkManager _networkManager;
        private NetworkBootstrap _bootstrap;
        private DefaultPrefabObjects _spawnablePrefabs;

        /// <summary>Generous: a socket bind plus a connect handshake, not a frame of gameplay.</summary>
        private const float ConnectTimeoutSeconds = 15.0f;

        [SetUp]
        public void SetUp()
        {
            // Errors are only ignored while the NetworkManager is being
            // CONSTRUCTED, and are re-enabled before any test body runs, so
            // real failures still fail.
            //
            // Why this is needed at all: NetworkManager.OnValidate() fires
            // during AddComponent, sees SpawnablePrefabs is null, and logs an
            // error. There is no ordering that avoids it -- the message is
            // emitted inside the AddComponent call itself, before any line
            // here can assign the field. In the editor outside play mode
            // FishNet quietly self-heals by loading DefaultPrefabObjects, but
            // that path is skipped during play, so it logs instead.
            //
            // Ignoring is used rather than LogAssert.Expect because
            // OnValidate is editor-only: in a standalone player test run the
            // message never appears, and an unmet Expect would fail the test
            // for the opposite reason.
            LogAssert.ignoreFailingMessages = true;
            try
            {
                // Built inactive so AutoStartOnPlay can be turned off BEFORE
                // Start() runs. Otherwise the bootstrap would connect itself
                // using Unity's own command line and the test would be racing it.
                _host = new GameObject("CityX Test NetworkManager");
                _host.SetActive(false);

                _networkManager = _host.AddComponent<NetworkManager>();

                // In a real build this is the DefaultPrefabObjects asset wired
                // up in the inspector. These tests spawn no networked objects,
                // so an empty collection is sufficient, and does not couple the
                // tests to project content. Assigned while still inactive, so
                // it is in place before Awake's own validation runs.
                _spawnablePrefabs = ScriptableObject.CreateInstance<DefaultPrefabObjects>();
                _networkManager.SpawnablePrefabs = _spawnablePrefabs;

                _bootstrap = _host.AddComponent<NetworkBootstrap>();
                _bootstrap.AutoStartOnPlay = false;

                _host.SetActive(true);
            }
            finally
            {
                // finally, so a throw during construction cannot leave error
                // checking disabled for every test that follows.
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_bootstrap != null)
            {
                _bootstrap.Shutdown();
            }
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }
            if (_spawnablePrefabs != null)
            {
                Object.DestroyImmediate(_spawnablePrefabs);
            }
        }

        /// <summary>
        /// THE ONE THAT MATTERS. Singleplayer is not an offline special case;
        /// it is a server with one local client. If this passes, the
        /// governing rule in PORT_PLAN is not just a design note -- it runs.
        /// </summary>
        [UnityTest]
        public IEnumerator SingleplayerReallyComesUpAsAHost()
        {
            _bootstrap.Begin(GameMode.Singleplayer, ArgsOnFreshPort());

            yield return WaitUntil(() => _networkManager.IsHostStarted, "singleplayer to come up as a host");

            Assert.IsTrue(_networkManager.IsServerStarted, "the server never started");
            Assert.IsTrue(_networkManager.IsClientStarted, "the local client never connected and authenticated");
            Assert.IsTrue(_networkManager.IsHostStarted, "server and client are up but FishNet does not consider this a host");
        }

        /// <summary>
        /// What actually makes singleplayer single-player: the socket is not
        /// reachable from another machine. This is the difference between
        /// "my game" and "an open server on your home network", and it is one
        /// line in NetworkBootstrap -- so it gets a test.
        /// </summary>
        [UnityTest]
        public IEnumerator SingleplayerBindsLoopbackAndIsUnreachableFromOutside()
        {
            _bootstrap.Begin(GameMode.Singleplayer, ArgsOnFreshPort());

            yield return WaitUntil(() => _networkManager.IsServerStarted, "the singleplayer server to start");

            Transport transport = _networkManager.TransportManager.Transport;
            string bind = transport.GetServerBindAddress(IPAddressType.IPv4);
            Assert.AreEqual(NetworkBootstrap.LoopbackBindAddress, bind,
                "a singleplayer server bound something other than loopback -- it would accept strangers");
        }

        /// <summary>
        /// The private-session path from PORT_PLAN's mode table: a player
        /// hosts, and this process is both the server and a client. Same code
        /// path as singleplayer; the ONLY difference is that this one is
        /// reachable.
        /// </summary>
        [UnityTest]
        public IEnumerator AHostedWorldComesUpReachableAndAlsoPlays()
        {
            _bootstrap.Begin(GameMode.HostedWorld, ArgsOnFreshPort());

            yield return WaitUntil(() => _networkManager.IsHostStarted, "the hosted world to come up");

            Transport transport = _networkManager.TransportManager.Transport;
            string bind = transport.GetServerBindAddress(IPAddressType.IPv4);
            Assert.AreNotEqual(NetworkBootstrap.LoopbackBindAddress, bind,
                "a hosted world bound loopback -- friends could never connect to it");
            Assert.IsTrue(_networkManager.IsClientStarted,
                "the host has no local client, so the person hosting cannot play");
        }

        /// <summary>
        /// A dedicated shard has nobody at the keyboard. If it stood up a
        /// local client it would be burning a connection slot and simulating
        /// a view for no one.
        /// </summary>
        [UnityTest]
        public IEnumerator ADedicatedShardStartsHeadlessWithNoLocalClient()
        {
            _bootstrap.Begin(GameMode.DedicatedShard, ArgsOnFreshPort());

            yield return WaitUntil(() => _networkManager.IsServerStarted, "the shard's server to start");

            // Give a local client every chance to appear, so this asserts
            // absence rather than just winning a race.
            yield return new WaitForSeconds(0.5f);

            Assert.IsTrue(_networkManager.IsServerStarted, "the shard's server is not running");
            Assert.IsFalse(_networkManager.IsClientStarted,
                "a dedicated shard started a local client; it has no player at the keyboard");
        }

        /// <summary>
        /// Shutdown has to really release the socket, or the next launch --
        /// or the next test -- cannot bind the port.
        /// </summary>
        [UnityTest]
        public IEnumerator ShutdownReturnsTheProcessToOffline()
        {
            _bootstrap.Begin(GameMode.Singleplayer, ArgsOnFreshPort());
            yield return WaitUntil(() => _networkManager.IsHostStarted, "the host to come up before shutting it down");

            _bootstrap.Shutdown();

            yield return WaitUntil(() => _networkManager.IsOffline, "the process to return to offline");
            Assert.IsTrue(_networkManager.IsOffline, "something is still running after Shutdown()");
        }

        // --- helpers ---

        private static LaunchArgs ArgsOnFreshPort()
        {
            ushort port = _nextPort++;
            return LaunchArgs.Parse(new[] { "-port", port.ToString(), "-seed", "12345" });
        }

        /// <summary>
        /// WaitUntil with a real timeout and a readable failure. Unity's own
        /// WaitUntil hangs forever, which in a batch-mode run means the whole
        /// suite stalls until the outer timeout kills it and tells you
        /// nothing about which test hung.
        /// </summary>
        private static IEnumerator WaitUntil(System.Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + ConnectTimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"timed out after {ConnectTimeoutSeconds:0.#}s waiting for {what}");
                }
                yield return null;
            }
        }
    }
}
