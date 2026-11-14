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

        /// <summary>
        /// 0 for an enemy a player put down (/spawn, a summon). Otherwise one of the world's own (MobSpawner), and the asker's own number
        /// for this ask: the answer and any word of its death carry it back.
        /// </summary>
        public long WorldTicket;

        /// <summary>One of the world's own: whether it appears inside a building (a humanoid) or out of doors (a ghost, a monster). Each has its own budget.</summary>
        public bool Indoors;
    }

    /// <summary>The server's answer to a player who asked for one of the world's own enemies: the enemy it spawned for them, or none.</summary>
    public struct WorldMobReply : IBroadcast
    {
        public long WorldTicket;

        /// <summary>The spawned enemy's network object id, or -1: there are as many alive near the asker as there may be.</summary>
        public int ObjectId;
    }

    /// <summary>An enemy's owner telling the server who killed it, so that player gets the kill's XP on their own machine.</summary>
    public struct EnemyKillMessage : IBroadcast
    {
        public int KillerClientId;
        public int Xp;
    }

    /// <summary>The machine that runs one of the world's own enemies telling the server it has been killed: it no longer counts toward the budget where it lies.</summary>
    public struct WorldMobKilled : IBroadcast
    {
        public long WorldTicket;
    }

    /// <summary>
    /// Enemies in a session (docs/MULTIPLAYER_STATE.md). Two jobs, FishNet BROADCASTS as the dropped items and the glass:
    ///
    /// SPAWNING: a player's /spawn asks the server, which checks the ask (a kind it knows, near the asker's own body, not too often, not too
    /// many alive) and spawns the networked enemy with the ASKER as its owner, so the asker's machine runs its brain and every machine sees it.
    /// With no session running (singleplayer) <see cref="TrySpawn"/> says no and the caller puts the plain enemy down on this machine alone.
    ///
    /// THE WORLD'S OWN ENEMIES (docs/MOB_SPAWNING.md): each machine's MobSpawner rolls random spots round its own player and asks for an
    /// enemy at one, out of doors or indoors. The server spawns it for everyone, the asker's machine running it, unless there are already
    /// as many alive near the asker as there may be (MobHabitats.Most, within MobHabitats.CrowdMetres), whoever runs them: players
    /// standing together share one budget. It answers the asker, and the machine that runs an enemy tells it of the kill (a body lying
    /// dead does not count).
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

        /// <summary>How far from the asker's body one of the world's own enemies may be put down, metres: they appear out of reach and out of sight.</summary>
        public const float WorldSpawnReachMetres = 140.0f;

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

        /// <summary>The server's: the world's own enemies alive in the session, each with who runs it, that player's ticket for it, and whether it is an indoor one.</summary>
        private readonly List<(NetworkObject Enemy, int OwnerClientId, long Ticket, bool Indoors)> _worldMobs = new List<(NetworkObject, int, long, bool)>();

        /// <summary>This machine's answers from the server whose enemy has not arrived here yet: the ask's ticket, the enemy's object id, when the wait is given up.</summary>
        private readonly List<(long Ticket, int ObjectId, float Until)> _awaited = new List<(long, int, float)>();

        /// <summary>How long this machine waits for an enemy the server said it spawned to arrive, seconds.</summary>
        public const float ArrivalWaitSeconds = 5.0f;

        /// <summary>
        /// The server has answered this machine's ask for one of the world's own enemies: the ask's ticket and the enemy, here and this
        /// machine's to run, or null (there are as many alive near here as there may be).
        /// </summary>
        public event System.Action<long, GameObject> WorldMobAnswered;

        /// <summary>This machine is in a running session: enemies are asked of its server, and only the kinds it shares can be.</summary>
        public bool InSession => _clientHandlers && _manager != null && _manager.IsClientStarted;

        /// <summary>Whether the session has a networked <paramref name="kind"/>.</summary>
        public bool Shares(string kind) => PrefabFor(kind) != null;

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
                    _manager.ServerManager.RegisterBroadcast<WorldMobKilled>(OnWorldMobKilledAtServer);
                }
                else
                {
                    _manager.ServerManager.UnregisterBroadcast<EnemySpawnRequest>(OnSpawnAtServer);
                    _manager.ServerManager.UnregisterBroadcast<EnemyKillMessage>(OnKillAtServer);
                    _manager.ServerManager.UnregisterBroadcast<WorldMobKilled>(OnWorldMobKilledAtServer);
                    _alive.Clear();
                    _worldMobs.Clear();
                }
                _serverHandlers = server;
            }

            bool client = _manager.IsClientStarted;
            if (client != _clientHandlers)
            {
                if (client)
                {
                    _manager.ClientManager.RegisterBroadcast<EnemyKillMessage>(OnKillAtClient);
                    _manager.ClientManager.RegisterBroadcast<WorldMobReply>(OnWorldMobReply);
                }
                else
                {
                    _manager.ClientManager.UnregisterBroadcast<EnemyKillMessage>(OnKillAtClient);
                    _manager.ClientManager.UnregisterBroadcast<WorldMobReply>(OnWorldMobReply);
                    _awaited.Clear();
                }
                _clientHandlers = client;
            }
            AnswerArrivals();
        }

        /// <summary>
        /// Asks for one of the world's own enemies to be brought into the session here, run by this machine; the answer comes by
        /// <see cref="WorldMobAnswered"/> under <paramref name="ticket"/> (this machine's own number for the ask, never 0). False when
        /// there is no session to ask or it does not share the kind.
        /// </summary>
        public bool TrySpawnWorldMob(string kind, Vector3 position, Quaternion rotation, long ticket, bool indoors)
        {
            if (ticket == 0L || !InSession || !Shares(kind))
            {
                return false;
            }
            _manager.ClientManager.Broadcast(new EnemySpawnRequest { Kind = kind, Position = position, Rotation = rotation, WorldTicket = ticket, Indoors = indoors });
            return true;
        }

        /// <summary>Tells the server that the enemy this machine was given under <paramref name="ticket"/> has been killed: it no longer counts toward the budget.</summary>
        public void NoteWorldMobKilled(long ticket)
        {
            if (ticket != 0L && InSession)
            {
                _manager.ClientManager.Broadcast(new WorldMobKilled { WorldTicket = ticket });
            }
        }

        private void OnWorldMobKilledAtServer(NetworkConnection sender, WorldMobKilled message, Channel channel)
        {
            if (sender == null || !sender.IsValid || !_killRate.Allow(sender.ClientId, Time.unscaledTime))
            {
                return;
            }
            // Only the machine an enemy was given to can call it killed: its ticket is its own.
            _worldMobs.RemoveAll(mob => mob.OwnerClientId == sender.ClientId && mob.Ticket == message.WorldTicket);
        }

        /// <summary>
        /// The server's: how many of the world's own enemies, indoor or outdoor ones, are alive in the session within
        /// MobHabitats.CrowdMetres of <paramref name="point"/>, whoever runs them.
        /// </summary>
        private int WorldMobsNear(Vector3 point, bool indoors)
        {
            _worldMobs.RemoveAll(mob => mob.Enemy == null || !mob.Enemy.IsSpawned);   // taken away (their player left them)
            int near = 0;
            foreach ((NetworkObject enemy, int _, long _, bool mobIndoors) in _worldMobs)
            {
                if (mobIndoors == indoors && Vector3.Distance(enemy.transform.position, point) <= CityX.World.MobHabitats.CrowdMetres)
                {
                    near++;
                }
            }
            return near;
        }

        private void OnWorldMobReply(WorldMobReply reply, Channel channel)
        {
            if (reply.ObjectId < 0)
            {
                WorldMobAnswered?.Invoke(reply.WorldTicket, null);
                return;
            }
            _awaited.Add((reply.WorldTicket, reply.ObjectId, Time.unscaledTime + ArrivalWaitSeconds));
        }

        /// <summary>Hands on each answered enemy once it has arrived on this machine (the server's answer can come a moment before the enemy).</summary>
        private void AnswerArrivals()
        {
            for (int i = _awaited.Count - 1; i >= 0; i--)
            {
                (long ticket, int objectId, float until) = _awaited[i];
                NetworkObject enemy = null;
                bool here = _manager != null && _manager.IsClientStarted
                    && _manager.ClientManager.Objects.Spawned.TryGetValue(objectId, out enemy) && enemy != null;
                if (here || Time.unscaledTime >= until)
                {
                    _awaited.RemoveAt(i);
                    WorldMobAnswered?.Invoke(ticket, here ? enemy.gameObject : null);
                }
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
            bool worlds = request.WorldTicket != 0L;
            if (!PlayerNear(sender, request.Position, worlds ? WorldSpawnReachMetres : SpawnReachMetres))
            {
                return;
            }
            _alive.RemoveAll(enemy => enemy == null || !enemy.IsSpawned);
            PlayerVitals asker = worlds ? OwnPlayer(sender) : null;
            bool full = worlds && asker != null
                && WorldMobsNear(asker.transform.position, request.Indoors) >= CityX.World.MobHabitats.Most(request.Indoors ? CityX.World.MobHabitat.Indoors : CityX.World.MobHabitat.Outdoors);
            if (_alive.Count >= MaxAlive || full)
            {
                if (worlds)
                {
                    _manager.ServerManager.Broadcast(sender, new WorldMobReply { WorldTicket = request.WorldTicket, ObjectId = -1 });
                }
                return;
            }
            Quaternion rotation = WireChecks.IsRotation(request.Rotation) ? request.Rotation : Quaternion.identity;
            NetworkObject enemy = _manager.GetPooledInstantiated(prefab, request.Position, rotation, asServer: true);
            _manager.ServerManager.Spawn(enemy, sender);
            _alive.Add(enemy);
            Spawned++;
            if (worlds)
            {
                _worldMobs.Add((enemy, sender.ClientId, request.WorldTicket, request.Indoors));
                _manager.ServerManager.Broadcast(sender, new WorldMobReply { WorldTicket = request.WorldTicket, ObjectId = enemy.ObjectId });
            }
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
