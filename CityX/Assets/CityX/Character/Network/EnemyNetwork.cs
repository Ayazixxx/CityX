using System.Collections.Generic;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>A player asking for an enemy to be brought into the world (/spawn), which that player's machine will then run.</summary>
    public struct EnemySpawnRequest : IBroadcast
    {
        public string Kind;
        public Vector3 Position;
        public Quaternion Rotation;
    }

    /// <summary>An enemy's owner telling the server who killed it, so that player gets the kill's XP on their own machine.</summary>
    public struct EnemyKillMessage : IBroadcast
    {
        public int KillerClientId;
        public int Xp;
    }

    /// <summary>
    /// Enemies in a session (docs/MULTIPLAYER_STATE.md). Two jobs, FishNet BROADCASTS as the dropped items and the glass:
    ///
    /// SPAWNING: a player's /spawn asks the server, which checks the ask (a kind it knows, near the asker's own body, not too often, not too
    /// many alive) and spawns the networked enemy with the ASKER as its owner, so the asker's machine runs its brain and every machine sees it.
    /// With no session running (singleplayer) <see cref="TrySpawn"/> says no and the caller puts the plain enemy down on this machine alone.
    ///
    /// KILL CREDIT: a hit on an enemy's copy is decided on the owner's machine, so it is the owner's machine that sees the enemy die; it tells
    /// the server which player's hit killed it, and the server tells that player's machine, whose player takes the XP (a kill on this machine's
    /// own enemy is credited where it happens, by the attack itself).
    /// </summary>
    public sealed class EnemyNetwork : MonoBehaviour
    {
        [System.Serializable]
        public struct Kind
        {
            public string Name;
            public NetworkObject Prefab;
        }

        /// <summary>How far from the asker's body an enemy may be put down, metres (/spawn puts a row of up to 30 a few metres ahead).</summary>
        public const float SpawnReachMetres = 40.0f;

        public const int MaxSpawnsPerSecond = 30;

        /// <summary>The most networked enemies alive at once in a session.</summary>
        public const int MaxAlive = 120;

        /// <summary>The most XP one kill may claim.</summary>
        public const int MaxKillXp = 1000;

        public const int MaxKillsPerSecond = 20;

        [Tooltip("The enemies a player may ask for, by the /spawn name (set by the scene builder).")]
        public Kind[] Kinds = System.Array.Empty<Kind>();

        [Tooltip("The session to work in; empty: the running one (FishNet's InstanceFinder). Tests bind one per machine.")]
        public NetworkManager Manager;

        /// <summary>This machine's (the game's one; a test makes one per machine and binds each to its own manager).</summary>
        public static EnemyNetwork Current { get; private set; }

        /// <summary>What the server has spawned (for tests).</summary>
        public int Spawned { get; private set; }

        /// <summary>XP this machine's player was given for kills made on other machines' enemies (for tests).</summary>
        public int CreditedXp { get; private set; }

        private NetworkManager _manager;
        private bool _serverHandlers;
        private bool _clientHandlers;
        private readonly SenderRate _spawnRate = new SenderRate(MaxSpawnsPerSecond);
        private readonly SenderRate _killRate = new SenderRate(MaxKillsPerSecond);
        private readonly List<NetworkObject> _alive = new List<NetworkObject>();

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Update()
        {
            if (_manager == null)
            {
                _manager = Manager != null ? Manager : InstanceFinder.NetworkManager;
                if (_manager == null)
                {
                    return;
                }
            }

            bool server = _manager.IsServerStarted;
            if (server != _serverHandlers)
            {
                if (server)
                {
                    _manager.ServerManager.RegisterBroadcast<EnemySpawnRequest>(OnSpawnAtServer);
                    _manager.ServerManager.RegisterBroadcast<EnemyKillMessage>(OnKillAtServer);
                }
                else
                {
                    _manager.ServerManager.UnregisterBroadcast<EnemySpawnRequest>(OnSpawnAtServer);
                    _manager.ServerManager.UnregisterBroadcast<EnemyKillMessage>(OnKillAtServer);
                    _alive.Clear();
                }
                _serverHandlers = server;
            }

            bool client = _manager.IsClientStarted;
            if (client != _clientHandlers)
            {
                if (client)
                {
                    _manager.ClientManager.RegisterBroadcast<EnemyKillMessage>(OnKillAtClient);
                }
                else
                {
                    _manager.ClientManager.UnregisterBroadcast<EnemyKillMessage>(OnKillAtClient);
                }
                _clientHandlers = client;
            }
        }

        /// <summary>
        /// Asks for <paramref name="kind"/> to be brought into the session here, run by this machine. False when there is no session to ask
        /// (or no such networked enemy): the caller puts the plain one down locally instead.
        /// </summary>
        public bool TrySpawn(string kind, Vector3 position, Quaternion rotation)
        {
            if (!_clientHandlers || _manager == null || !_manager.IsClientStarted || PrefabFor(kind) == null)
            {
                return false;
            }
            _manager.ClientManager.Broadcast(new EnemySpawnRequest { Kind = kind, Position = position, Rotation = rotation });
            return true;
        }

        /// <summary>Tells the server (through <paramref name="manager"/>, the killed enemy's own session) that a player's hit killed it.</summary>
        public static void CreditKill(NetworkManager manager, int killerClientId, int xp)
        {
            if (manager != null && manager.IsClientStarted && xp > 0)
            {
                manager.ClientManager.Broadcast(new EnemyKillMessage { KillerClientId = killerClientId, Xp = xp });
            }
        }

        private NetworkObject PrefabFor(string kind)
        {
            foreach (Kind entry in Kinds)
            {
                if (entry.Prefab != null && entry.Name == kind)
                {
                    return entry.Prefab;
                }
            }
            return null;
        }

        // ---- the server ----

        private void OnSpawnAtServer(NetworkConnection sender, EnemySpawnRequest request, Channel channel)
        {
            NetworkObject prefab = PrefabFor(request.Kind);
            if (sender == null || !sender.IsValid || prefab == null || !WireChecks.Finite(request.Position) || !_spawnRate.Allow(sender.ClientId, Time.unscaledTime))
            {
                return;
            }
            if (!PlayerNear(sender, request.Position, SpawnReachMetres))
            {
                return;
            }
            _alive.RemoveAll(enemy => enemy == null || !enemy.IsSpawned);
            if (_alive.Count >= MaxAlive)
            {
                return;
            }
            Quaternion rotation = WireChecks.IsRotation(request.Rotation) ? request.Rotation : Quaternion.identity;
            NetworkObject enemy = _manager.GetPooledInstantiated(prefab, request.Position, rotation, asServer: true);
            _manager.ServerManager.Spawn(enemy, sender);
            _alive.Add(enemy);
            Spawned++;
        }

        private void OnKillAtServer(NetworkConnection sender, EnemyKillMessage message, Channel channel)
        {
            if (sender == null || !sender.IsValid || message.Xp <= 0 || !_killRate.Allow(sender.ClientId, Time.unscaledTime))
            {
                return;
            }
            if (_manager.ServerManager.Clients.TryGetValue(message.KillerClientId, out NetworkConnection killer) && killer != null && killer.IsValid)
            {
                _manager.ServerManager.Broadcast(killer, new EnemyKillMessage { KillerClientId = message.KillerClientId, Xp = Mathf.Min(message.Xp, MaxKillXp) });
            }
        }

        // ---- the killer's machine ----

        private void OnKillAtClient(EnemyKillMessage message, Channel channel)
        {
            PlayerVitals player = OwnPlayer(_manager.ClientManager.Connection);
            int xp = Mathf.Clamp(message.Xp, 0, MaxKillXp);
            if (player != null && xp > 0)
            {
                player.AddXp(xp);
                CreditedXp += xp;
            }
        }

        // ---- checks ----

        /// <summary>A connection's own player body (not an enemy it runs), or null.</summary>
        private static PlayerVitals OwnPlayer(NetworkConnection connection)
        {
            if (connection == null)
            {
                return null;
            }
            foreach (NetworkObject owned in connection.Objects)
            {
                if (owned != null && owned.GetComponent<NetworkedPlayerIdentity>() != null)
                {
                    return owned.GetComponent<PlayerVitals>();
                }
            }
            return null;
        }

        /// <summary>Whether the sender's own player body stands within <paramref name="reach"/> of a point.</summary>
        private static bool PlayerNear(NetworkConnection sender, Vector3 point, float reach)
        {
            PlayerVitals own = OwnPlayer(sender);
            return own != null && Vector3.Distance(own.transform.position, point) <= reach;
        }
    }
}
