using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
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
    /// EXPLICIT ASK: "make it so shards and fireball and all the spells are visible to all players on multiplayer." Over REAL sockets, to a
    /// DIFFERENT player: a dedicated server and two clients, each with its own spawned body (the NetworkedPlayer.prefab every joining player
    /// is given, which now carries the spells). Player A uses each ability on their own body; player B's copy of A's body must show it, and
    /// A's own copy must not have it shown back at them.
    ///
    /// All the machines of this test share one Unity scene, so A's real projectiles and B's cosmetic copies are told apart by the projectiles'
    /// Cosmetic flag, not by where they are.
    /// </summary>
    public sealed class NetworkedAbilitySyncPlayTests
    {
        private const string NetworkedPlayerPrefabPath = "Assets/CityX/Character/NetworkedPlayer.prefab";
        private static ushort _nextPort = 44600;
        private const float TimeoutSeconds = 30.0f;

        private GameObject _ground;
        private GameObject _serverGo;
        private NetworkManager _server;
        private readonly List<GameObject> _clientGos = new List<GameObject>();
        private readonly List<NetworkManager> _clients = new List<NetworkManager>();

        private GameObject _ownerBody;    // player A's own body, as A's client holds it
        private GameObject _remoteBody;   // player B's client's copy of that same body

        /// <summary>A bare sword for the owner's spin to be allowed to start (the spin needs a sword in hand).</summary>
        private sealed class FakeSword : IEquippableWeapon
        {
            public float GetMeleeDamage() => 10.0f;
            public float MeleeCooldownSeconds => 0.6f;
            public void RegisterLandedMeleeHit() { }
            public bool CanThrow() => false;
            public float ThrowSpeed => 0.0f;
            public void Throw(GameObject player, Vector3 origin, Vector3 direction) { }
            public string MovesetId => ComboChain.Sword;
            public ComboStage? HeavyStage => null;
        }

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
            foreach (NetworkManager client in _clients)
            {
                if (client != null && client.ClientManager.Started) client.ClientManager.StopConnection();
            }
            if (_server != null && _server.ServerManager.Started) _server.ServerManager.StopConnection(true);
            yield return null;

            foreach (NetworkedPlayerIdentity identity in Object.FindObjectsByType<NetworkedPlayerIdentity>(FindObjectsSortMode.None))
            {
                if (identity != null) Object.DestroyImmediate(identity.gameObject);
            }
            foreach (FireballProjectile f in Object.FindObjectsByType<FireballProjectile>(FindObjectsSortMode.None)) Object.DestroyImmediate(f.gameObject);
            foreach (IceShardProjectile s in Object.FindObjectsByType<IceShardProjectile>(FindObjectsSortMode.None)) Object.DestroyImmediate(s.gameObject);
            foreach (SwordWaveProjectile w in Object.FindObjectsByType<SwordWaveProjectile>(FindObjectsSortMode.None)) Object.DestroyImmediate(w.gameObject);
            foreach (PoisonGasCloud c in Object.FindObjectsByType<PoisonGasCloud>(FindObjectsSortMode.None)) Object.DestroyImmediate(c.gameObject);
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

#if UNITY_EDITOR
        /// <summary>Brings up a dedicated server and two clients, each with a spawned body 2 m apart, and finds A's body on A's client and on B's.</summary>
        private IEnumerator BringUpTwoPlayers()
        {
            LogAssert.ignoreFailingMessages = true;

            GameObject managerPrefab = Resources.Load<GameObject>("CityX.Spike/SpikeNetworkManager");
            Assert.IsNotNull(managerPrefab, "missing SpikeNetworkManager prefab -- run CityX/Spike/Rebuild Spike Assets");
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkedPlayerPrefabPath);
            Assert.IsNotNull(playerPrefab, "no NetworkedPlayer.prefab -- run CityX/Rebuild Massing Scene first");

            ushort port = _nextPort++;
            _serverGo = Object.Instantiate(managerPrefab);
            _serverGo.name = "Ability Server";
            _server = _serverGo.GetComponent<NetworkManager>();
            Transport serverTransport = _server.TransportManager.Transport;
            serverTransport.SetPort(port);
            serverTransport.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            _server.ServerManager.StartConnection(port);
            yield return WaitUntil(() => _server.IsServerStarted, "the server to start");

            for (int i = 0; i < 2; i++)
            {
                GameObject go = Object.Instantiate(managerPrefab);
                go.name = $"Ability Client {i}";
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
                NetworkObject body = _server.GetPooledInstantiated(playerPrefab, new Vector3(i * 4.0f, 0.5f, 0.0f), Quaternion.identity, asServer: true);
                _server.ServerManager.Spawn(body, connection);
            }
            yield return WaitUntil(
                () => _clients[0].ClientManager.Objects.Spawned.Count >= 2 && _clients[1].ClientManager.Objects.Spawned.Count >= 2,
                "both players to appear on both clients");

            NetworkObject ownerCopy = null;
            foreach (NetworkObject nob in _clients[0].ClientManager.Objects.Spawned.Values)
            {
                if (nob.IsOwner) ownerCopy = nob;
            }
            Assert.IsNotNull(ownerCopy, "client A never got ownership of its own body");
            NetworkObject remoteCopy = null;
            foreach (NetworkObject nob in _clients[1].ClientManager.Objects.Spawned.Values)
            {
                if (nob.ObjectId == ownerCopy.ObjectId) remoteCopy = nob;
            }
            Assert.IsNotNull(remoteCopy, "client B never received a copy of player A's body");
            Assert.IsFalse(remoteCopy.IsOwner, "client B should be merely watching player A's body");
            _ownerBody = ownerCopy.gameObject;
            _remoteBody = remoteCopy.gameObject;

            // A's own input reader would tick the abilities each frame; with no keyboard here the test ticks them itself.
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }
        }

        private static int CountCosmetic<T>(System.Func<T, bool> cosmetic) where T : Object
        {
            int n = 0;
            foreach (T t in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (cosmetic(t)) n++;
            }
            return n;
        }
#endif

        [UnityTest]
        public IEnumerator TheJoiningPlayersBodyHasTheSpellsAndTheSyncToo()
        {
#if UNITY_EDITOR
            yield return BringUpTwoPlayers();
            foreach (GameObject body in new[] { _ownerBody, _remoteBody })
            {
                Assert.IsNotNull(body.GetComponent<NetworkedAbilitySync>(), "no ability sync on the body");
                Assert.IsNotNull(body.GetComponent<FireballCaster>(), "a joining player cannot cast the fireball");
                Assert.IsNotNull(body.GetComponent<IceShardCaster>(), "a joining player cannot cast ice shards");
                Assert.IsNotNull(body.GetComponent<SwordWaveCaster>(), "a joining player cannot throw the sword wave");
                Assert.IsNotNull(body.GetComponent<PoisonGasCaster>(), "a joining player cannot blow poison gas");
                Assert.IsNotNull(body.GetComponent<PoisonGasCaster>().CloudPrefab, "the poison gas has no prefab");
                Assert.IsNotNull(body.GetComponent<BloodrageAbility>(), "a joining player cannot use Bloodrage");
                Assert.IsNotNull(body.GetComponent<FireballCaster>().FireballPrefab, "the fireball has no prefab");
                Assert.IsNotNull(body.GetComponent<IceShardCaster>().ShardPrefab, "ice shards have no prefab");
                Assert.IsNotNull(body.GetComponent<SwordWaveCaster>().WavePrefab, "the sword wave has no prefab");
            }
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AFireballAnIceVolleyAndASwordWaveOnOnePlayersBodyAreShownOnAnothersCopy()
        {
#if UNITY_EDITOR
            yield return BringUpTwoPlayers();
            Vector3 chest = _ownerBody.transform.position + Vector3.up * 1.7f;

            // --- the fireball
            var fireball = _ownerBody.GetComponent<FireballCaster>();
            Assert.IsTrue(fireball.TryCast(chest, Vector3.forward, _ownerBody.GetComponentsInChildren<Collider>()));
            yield return WaitUntil(() => CountCosmetic<FireballProjectile>(f => f != null && f.Cosmetic) >= 1, "B to be shown A's fireball");
            Assert.AreEqual(1, CountCosmetic<FireballProjectile>(f => f.Cosmetic), "one cast, one copy");
            var remoteRaised = _remoteBody.GetComponent<RemoteAbilityPose>();
            Assert.IsNotNull(remoteRaised, "B's copy of A never raised its hand");
            Assert.IsNull(_ownerBody.GetComponent<RemoteAbilityPose>(), "A's own body was shown its own cast back");

            // --- the ice shards: four, one after another
            var ice = _ownerBody.GetComponent<IceShardCaster>();
            Assert.IsTrue(ice.TryCast(chest, Vector3.forward + Vector3.up * 0.2f, _ownerBody.GetComponentsInChildren<Collider>()));
            for (int i = 0; i < 60; i++)
            {
                ice.Tick(0.05f);
                yield return null;
            }
            yield return WaitUntil(() => CountCosmetic<IceShardProjectile>(s => s != null && s.Cosmetic) >= 4, "B to be shown all four of A's shards");
            Assert.AreEqual(4, CountCosmetic<IceShardProjectile>(s => s.Cosmetic), "a volley is four shards");
            Assert.AreEqual(4, CountCosmetic<IceShardProjectile>(s => !s.Cosmetic), "and A has its own four real ones");

            // --- the sword wave
            var wave = _ownerBody.GetComponent<SwordWaveCaster>();
            Assert.IsTrue(wave.TryCast(1.7f, Vector3.forward, _ownerBody.GetComponentsInChildren<Collider>()));
            for (int i = 0; i < 20; i++)
            {
                wave.Tick(0.05f);
                yield return null;
            }
            yield return WaitUntil(() => CountCosmetic<SwordWaveProjectile>(w => w != null && w.Cosmetic) >= 1, "B to be shown A's sword wave");

            // --- the poison gas: a cosmetic cloud on B's copy, which must poison nothing
            var gas = _ownerBody.GetComponent<PoisonGasCaster>();
            Assert.IsTrue(gas.TryCast(chest, Vector3.forward, _ownerBody.GetComponentsInChildren<Collider>()));
            yield return WaitUntil(() => CountCosmetic<PoisonGasCloud>(c => c != null && c.Cosmetic) >= 1, "B to be shown A's poison gas");
            Assert.AreEqual(1, CountCosmetic<PoisonGasCloud>(c => !c.Cosmetic), "and A has its own real cloud");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator BloodrageAndTheSwordSpinOnOnePlayersBodyAreShownOnAnothersCopy()
        {
#if UNITY_EDITOR
            yield return BringUpTwoPlayers();

            // --- Bloodrage: the aura and the animations on B's copy of A, for as long as A's lasts
            var ownerRage = _ownerBody.GetComponent<BloodrageAbility>();
            var remoteRage = _remoteBody.GetComponent<BloodrageAbility>();
            Assert.IsTrue(ownerRage.TryActivate());
            yield return WaitUntil(() => remoteRage.IsActive, "B to be shown A's Bloodrage");
            Assert.IsNotNull(_remoteBody.transform.Find("BloodrageAura(Clone)"), "no red aura on B's copy of A");

            // --- the sword spin
            var ownerMelee = _ownerBody.GetComponent<MeleeAttacker>();
            var remoteMelee = _remoteBody.GetComponent<MeleeAttacker>();
            ownerMelee.EquippedWeapon = new FakeSword();
            // (A's locomotion is not ticking here, so it never reports grounded; the spin's own ground probe finds the floor 0.5 m below.)
            Assert.IsTrue(ownerMelee.TrySpinAttack(), "A's spin was refused");
            yield return WaitUntil(() => remoteMelee.IsShowingRemoteSpin, "B to be shown A's sword spin");
            Assert.IsFalse(_ownerBody.GetComponent<MeleeAttacker>().IsShowingRemoteSpin, "A's own body was shown its own spin back");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }

        // --- helpers ---

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
