using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Requested: "make sure things like jump and punch are shared
    /// between players." NetworkedAnimatorSync already mirrored the
    /// CONTINUOUS animation state (walking, aiming, ...) but deliberately
    /// skipped one-shot actions -- a jump or a punch happens once, so it
    /// needs its own event, not a periodically re-sent parameter.
    ///
    /// Three layers, cheapest first, so a failure points at the right one:
    /// the body announces the action, a copy replays it without echoing it,
    /// and -- the real proof -- an action on one player's body shows up on a
    /// DIFFERENT player's copy over genuine sockets.
    /// </summary>
    public sealed class NetworkedActionSyncPlayTests
    {
        private const string PlayerPrefabPath = "Assets/CityX/Character/Player.prefab";
        private const string NetworkedPlayerPrefabPath = "Assets/CityX/Character/NetworkedPlayer.prefab";
        private static ushort _nextPort = 44100;
        private const float TimeoutSeconds = 30.0f;

        private GameObject _ground;
        private GameObject _player;

        private GameObject _serverGo;
        private NetworkManager _server;
        private readonly List<GameObject> _clientGos = new List<GameObject>();
        private readonly List<NetworkManager> _clients = new List<NetworkManager>();

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            if (_player != null) Object.Destroy(_player);

            foreach (NetworkManager client in _clients)
            {
                if (client != null && client.ClientManager.Started)
                {
                    client.ClientManager.StopConnection();
                }
            }
            if (_server != null && _server.ServerManager.Started)
            {
                _server.ServerManager.StopConnection(true);
            }
            yield return null;

            foreach (NetworkedPlayerIdentity identity in
                     Object.FindObjectsByType<NetworkedPlayerIdentity>(FindObjectsSortMode.None))
            {
                if (identity != null)
                {
                    Object.DestroyImmediate(identity.gameObject);
                }
            }
            foreach (GameObject go in _clientGos)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _clientGos.Clear();
            _clients.Clear();
            if (_serverGo != null) Object.DestroyImmediate(_serverGo);
            if (_ground != null) Object.Destroy(_ground);
            LogAssert.ignoreFailingMessages = false;
        }

        // --- layer 1: the body announces what it just did ---

        [UnityTest]
        public IEnumerator JumpAndDashAnnounceThemselves()
        {
#if UNITY_EDITOR
            _player = SpawnPlainPlayer();
            var locomotion = _player.GetComponent<PlayerLocomotion>();

            for (int i = 0; i < 30; i++)
            {
                locomotion.Tick(default, 1.0f / 60.0f);
                yield return null;
            }

            var heard = new List<int>();
            locomotion.AnimatorTriggerFired += heard.Add;

            locomotion.Tick(new LocomotionInput { Jump = true }, 1.0f / 60.0f);
            locomotion.StartDash(Vector3.forward);

            CollectionAssert.Contains(heard, Animator.StringToHash("Jump"),
                "jumping on the ground never announced the Jump trigger, so no other player could see it");
            CollectionAssert.Contains(heard, Animator.StringToHash("Dash"),
                "dashing never announced the Dash trigger, so no other player could see it");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        /// <summary>
        /// REGRESSION, found by the full suite: a body with the sync
        /// component but no network session (singleplayer, the real scene's
        /// baked player) has no initialised NetworkObject, and the first
        /// version of the handlers threw from inside Tick -- aborting the
        /// jump. Playing without a session must be untouched by all of this.
        /// </summary>
        [UnityTest]
        public IEnumerator JumpingAndPunchingWithNoNetworkSessionStillWorks()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            _player = SpawnPlainPlayer();
            _player.AddComponent<NetworkedAnimatorSync>();
            // WHAT NetworkedPlayerIdentity.Awake DOES FOR THE REAL SINGLEPLAYER
            // BODY: mark the NetworkObject as not networked. Without it,
            // FishNet's NetworkObject.Start() deactivates the whole GameObject
            // when no NetworkManager is running -- the body then cannot move
            // (CharacterController.Move on an inactive controller does nothing),
            // which is a test-setup artefact, not the thing under test. Must be
            // called before Start, i.e. before the first yield.
            _player.GetComponent<NetworkObject>().SetIsNetworked(false);
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            var melee = _player.GetComponent<MeleeAttacker>();

            for (int i = 0; i < 30; i++)
            {
                locomotion.Tick(default, 1.0f / 60.0f);
                yield return null;
            }

            bool groundedBefore = locomotion.IsGrounded;
            float yBefore = _player.transform.position.y;
            locomotion.Tick(new LocomotionInput { Jump = true }, 1.0f / 60.0f);
            Assert.Greater(locomotion.Velocity.y, 0.0f,
                "the jump was aborted mid-Tick -- the network sync handler threw on a body with no session " +
                $"(grounded before: {groundedBefore}, y before: {yBefore:0.000}, y now: {_player.transform.position.y:0.000})");
            Assert.IsTrue(melee.TryAttack(), "a punch was refused or threw with no network session");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator APunchAnnouncesItsAnimationStateName()
        {
#if UNITY_EDITOR
            _player = SpawnPlainPlayer();
            var driver = _player.GetComponent<PlayerAnimationDriver>();
            var melee = _player.GetComponent<MeleeAttacker>();

            var heard = new List<string>();
            driver.StatePlayed += heard.Add;

            yield return null;
            Assert.IsTrue(melee.TryAttack(), "the punch was refused, so there is nothing to announce");

            Assert.AreEqual(1, heard.Count, "one punch should announce exactly one state");
            Assert.AreEqual(FistStrikes.RightStraightName, heard[0], "the opening punch of the unarmed chain");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        // --- layer 2: a copy replays it, without echoing it back ---

        [UnityTest]
        public IEnumerator AnotherPlayersCopyThrowsTheFinisherWhenToldItsName()
        {
#if UNITY_EDITOR
            _player = SpawnPlainPlayer();
            var driver = _player.GetComponent<PlayerAnimationDriver>();
            yield return null;
            driver.PlayRemoteState(SwordStrikes.Finisher1HName);
            yield return WaitUntil(() => driver.SwordPoser.IsStriking, "the replayed finisher to play");
            Assert.AreEqual(SwordStrikeKind.Finisher1H, driver.SwordPoser.Kind);
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AReplayedStatePlaysWithoutBeingReAnnounced()
        {
#if UNITY_EDITOR
            _player = SpawnPlainPlayer();
            var driver = _player.GetComponent<PlayerAnimationDriver>();
            Animator animator = _player.GetComponentInChildren<Animator>();

            int announced = 0;
            driver.StatePlayed += _ => announced++;

            yield return null;
            driver.PlayRemoteState(FistStrikes.RightStraightName);
            yield return WaitUntil(() => _player.GetComponent<FistPoseDriver>().IsStriking, "the replayed punch to play");

            Assert.AreEqual(0, announced,
                "a replayed state was announced again -- that would bounce it back over the network forever");

            // A garbled name off the wire must do nothing at all.
            driver.PlayRemoteState("NoSuchState");
            driver.PlayRemoteState(null);
            Assert.AreEqual(0, announced);
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }

        // --- layer 3: over real sockets, to a DIFFERENT player ---

        /// <summary>
        /// A dedicated server and two real clients, each with its own
        /// player body (the SpikeNetworkManager prefab every multi-manager
        /// test here uses -- see InterestManagementPlayerToPlayerPlayTests
        /// for why hand-built managers don't work). Player A dashes and
        /// punches; player B's copy of player A must play both.
        /// </summary>
        [UnityTest]
        public IEnumerator ADashAndAPunchOnOnePlayersBodyPlayOnAnotherPlayersCopy()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            GameObject managerPrefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(managerPrefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPrefabPath);
            Assert.IsNotNull(playerPrefab, "no NetworkedPlayer.prefab -- run CityX/Rebuild Massing Scene first");

            ushort port = _nextPort++;

            _serverGo = Object.Instantiate(managerPrefab);
            _serverGo.name = "Action Server";
            _server = _serverGo.GetComponent<NetworkManager>();
            Transport serverTransport = _server.TransportManager.Transport;
            serverTransport.SetPort(port);
            serverTransport.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            _server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => _server.IsServerStarted, "the server to start");

            for (int i = 0; i < 2; i++)
            {
                GameObject go = Object.Instantiate(managerPrefab);
                go.name = $"Action Client {i}";
                NetworkManager client = go.GetComponent<NetworkManager>();
                _clientGos.Add(go);
                _clients.Add(client);
                Transport t = client.TransportManager.Transport;
                t.SetPort(port);
                t.SetClientAddress("127.0.0.1");
                client.ClientManager.StartConnection("127.0.0.1", port);
                yield return null;
            }
            yield return WaitUntil(() => _server.ServerManager.Clients.Count >= 2, "both clients to connect");

            var ids = new List<int>(_server.ServerManager.Clients.Keys);
            ids.Sort();
            for (int i = 0; i < 2; i++)
            {
                NetworkConnection connection = _server.ServerManager.Clients[ids[i]];
                NetworkObject body = _server.GetPooledInstantiated(
                    playerPrefab, new Vector3(i * 2.0f, 0.5f, 0.0f), Quaternion.identity, asServer: true);
                _server.ServerManager.Spawn(body, connection);
            }

            // Each client needs to see BOTH bodies (they are 2 m apart, well
            // inside the view distance) before there is anything to compare.
            yield return WaitUntil(
                () => _clients[0].ClientManager.Objects.Spawned.Count >= 2
                      && _clients[1].ClientManager.Objects.Spawned.Count >= 2,
                "both players to appear on both clients");

            // Player A's own body, as player A's client holds it...
            NetworkObject ownerCopy = null;
            foreach (NetworkObject nob in _clients[0].ClientManager.Objects.Spawned.Values)
            {
                if (nob.IsOwner) ownerCopy = nob;
            }
            Assert.IsNotNull(ownerCopy, "client A never got ownership of its own body");

            // ...and player B's client's copy of that SAME body.
            NetworkObject remoteCopy = null;
            foreach (NetworkObject nob in _clients[1].ClientManager.Objects.Spawned.Values)
            {
                if (nob.ObjectId == ownerCopy.ObjectId) remoteCopy = nob;
            }
            Assert.IsNotNull(remoteCopy, "client B never received a copy of player A's body");
            Assert.IsFalse(remoteCopy.IsOwner, "client B should be merely watching player A's body");

            Animator remoteAnimator = remoteCopy.GetComponentInChildren<Animator>();
            Assert.IsNotNull(remoteAnimator, "the remote copy has no Animator");

            var ownerLocomotion = ownerCopy.GetComponent<PlayerLocomotion>();
            var ownerMelee = ownerCopy.GetComponent<MeleeAttacker>();

            ownerLocomotion.StartDash(Vector3.forward);
            yield return WaitForState(remoteAnimator, "Dash");

            Assert.IsTrue(ownerMelee.TryAttack(), "player A's punch was refused");
            yield return WaitUntil(() => remoteCopy.GetComponent<FistPoseDriver>().IsStriking || remoteCopy.GetComponent<FistPoseDriver>().Engage > 0.2f, "the punch to play on another player's copy");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        // --- helpers ---

#if UNITY_EDITOR
        private GameObject SpawnPlainPlayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            return Object.Instantiate(prefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
        }
#endif

        /// <summary>Polls every frame until the animator is in (or blending into) the named state.</summary>
        private static IEnumerator WaitForState(Animator animator, string stateName)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (true)
            {
                if (animator.GetCurrentAnimatorStateInfo(0).IsName(stateName)
                    || animator.GetNextAnimatorStateInfo(0).IsName(stateName))
                {
                    yield break;
                }
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"the animator never entered '{stateName}' within {TimeoutSeconds:0.#}s");
                }
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, string what)
        {
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail($"timed out after {TimeoutSeconds:0.#}s waiting for {what}");
                }
                yield return null;
            }
        }
    }
}
