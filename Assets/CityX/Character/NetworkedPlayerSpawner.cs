using System.Collections;
using System.Collections.Generic;
using CityX.Core;
using CityX.Net;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Turns a live FishNet session into more than one visible player.
    ///
    /// THE SCENE'S OWN BAKED PLAYER BECOMES THE HOST'S BODY; EVERY OTHER
    /// CONNECTION GETS A FRESH CLONE. CityMassingSceneBuilder always places
    /// one Player instance directly in the scene -- the same instance every
    /// singleplayer session has always used, untouched by any of this when
    /// GameMode is Singleplayer (checked below; a genuine no-op). In a real
    /// networked session that SAME baked instance becomes the host's own
    /// body -- it already carries every editor-time wiring step (camera,
    /// HUD, rain, claim system) that a runtime spawn would otherwise have to
    /// rebuild from scratch -- and each further connection that joins gets a
    /// freshly instantiated clone instead, mirroring main.gd's own split
    /// between _spawn_local_player (the one, already-wired local body) and
    /// _try_spawn_player (a generic remote peer).
    ///
    /// CATCHES ALREADY-CONNECTED CONNECTIONS AT Start(), NOT ONLY FUTURE
    /// ONES. NetworkBootstrap.Begin ran back on the title screen, so by the
    /// time this scene has finished loading and this component's own Start()
    /// runs, the host's own local connection has very likely already reached
    /// ServerManager.Clients -- an event-only approach would miss it
    /// entirely. OnRemoteConnectionState is still subscribed afterward, for
    /// whoever connects later (ordinarily a friend, joining once the host is
    /// already in the game).
    ///
    /// NOT YET HANDLED, DELIBERATELY: GameMode.DedicatedShard. A headless
    /// shard has no local client and therefore no baked-object owner to hand
    /// off to -- giving every real player a spawned clone there (never using
    /// the baked instance at all) is the right shape, but is a genuinely
    /// separate, bigger change (the scene would need to stop baking a player
    /// at all for that mode) and is not what this pass ("play with my
    /// friend") asked for.
    /// </summary>
    public sealed class NetworkedPlayerSpawner : MonoBehaviour
    {
        [Tooltip("The scene's own baked Player instance -- becomes the host's own body.")]
        public NetworkObject LocalPlayer;

        [Tooltip("Cloned for every OTHER connection that joins.")]
        public GameObject PlayerPrefab;

        [Tooltip("Where a freshly spawned clone lands, before its own per-connection offset.")]
        public Transform SpawnPoint;

        /// <summary>Metres, a point on a small circle around SpawnPoint -- mirrors main.gd's own NETWORK_SPAWN_RADIUS.</summary>
        public const float SpawnRadius = 2.0f;

        private NetworkManager _manager;
        private readonly HashSet<int> _handled = new HashSet<int>();

        private void Start()
        {
            var bootstrap = FindAnyObjectByType<NetworkBootstrap>();
            if (bootstrap == null || bootstrap.Mode == GameMode.Singleplayer
                || !GameModeInfo.RunsServer(bootstrap.Mode))
            {
                return;
            }

            _manager = bootstrap.Manager;
            if (_manager == null)
            {
                return;
            }

            StartCoroutine(HandleAlreadyConnectedOnceLocalPlayerIsReady());
        }

        /// <summary>
        /// A freshly loaded scene's own NetworkObjects are not immediately
        /// spawn-ready server-side -- FishNet defers their own callbacks by
        /// one frame (see ServerObjects.SceneManager_sceneLoaded's own "do
        /// not invoke callbacks till next frame" comment). This component's
        /// Start() runs the same frame CityMassing.unity finishes loading,
        /// which is BEFORE that; calling GiveOwnership on LocalPlayer that
        /// early throws a NullReferenceException deep inside FishNet, since
        /// the object has not actually finished being registered as a
        /// server-side NetworkObject yet.
        /// </summary>
        private IEnumerator HandleAlreadyConnectedOnceLocalPlayerIsReady()
        {
            while (LocalPlayer != null && !LocalPlayer.IsServerInitialized)
            {
                yield return null;
            }

            foreach (NetworkConnection connection in _manager.ServerManager.Clients.Values)
            {
                HandleConnection(connection);
            }

            _manager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        private void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }
        }

        private void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                HandleConnection(connection);
            }
            else
            {
                _handled.Remove(connection.ClientId);
            }
        }

        private void HandleConnection(NetworkConnection connection)
        {
            if (connection == null || !connection.IsValid || !_handled.Add(connection.ClientId))
            {
                return;
            }

            if (LocalPlayer != null && connection == _manager.ClientManager.Connection)
            {
                LocalPlayer.GiveOwnership(connection);
                return;
            }

            SpawnClone(connection);
        }

        private void SpawnClone(NetworkConnection connection)
        {
            if (PlayerPrefab == null)
            {
                Debug.LogError("CityX: no player prefab to spawn for a new connection.");
                return;
            }

            Vector3 basePosition = SpawnPoint != null ? SpawnPoint.position : Vector3.zero;
            float angle = connection.ClientId;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * SpawnRadius;

            // GetPooledInstantiated, NOT a plain Object.Instantiate --
            // FishNet's own object pool is what correctly initializes a
            // fresh NetworkObject's internal state before its own Awake()
            // ever runs; a raw Instantiate leaves it in the same
            // "not initialized" state a bare test-time Instantiate does
            // (see NetworkedPlayerBuilder's own doc comment for the full
            // story), just for a real connection instead of a test.
            NetworkObject nob = _manager.GetPooledInstantiated(
                PlayerPrefab, basePosition + offset, Quaternion.identity, asServer: true);
            _manager.ServerManager.Spawn(nob, connection);
        }
    }
}
