using System.Collections.Generic;
using CityX.World;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>One pane as the machine that broke it has it now, on its way between machines.</summary>
    public struct GlassPaneMessage : IBroadcast
    {
        public int Building;
        public int Wall;
        public int Column;
        public int Row;
        public int State;
        public float[] HoleU;
        public float[] HoleV;
        public Vector3 Point;
        public Vector3 Direction;
        public int Strength;
    }

    /// <summary>A joining player asking which panes are broken.</summary>
    public struct GlassStateRequest : IBroadcast
    {
    }

    /// <summary>Every broken pane and its holes, for a joining player: each pane's holes follow in order in HoleU and HoleV, Holes[i] of them.</summary>
    public struct GlassStateMessage : IBroadcast
    {
        public int[] Building;
        public int[] Wall;
        public int[] Column;
        public int[] Row;
        public int[] State;
        public int[] Holes;
        public float[] HoleU;
        public float[] HoleV;
    }

    /// <summary>
    /// Keeps every machine's broken glass the same in a session (owner, 2026-10-16, with the door glass: the multiplayer-first rule, anything a
    /// player does the others see). A pane this machine's player breaks -- a window or a door's glass, shot, struck or walked into -- goes client ->
    /// server -> every client as the pane now stands (its state and its holes) with where the blow landed and which way it went, so the others hear
    /// it and see its pieces fall from there; a joining player asks the server for every broken pane. Until then broken glass was this machine's
    /// alone: another player's shot is only its flash and tracer here (PistolWeapon.OnRemoteShot), so a window they broke stood whole on your screen.
    ///
    /// MERGED, NEVER OVERWRITTEN (GlassDamage.Merge): a pane only ever goes further -- whole, cracked with more holes, gone -- so a message heard
    /// twice or late changes nothing, and the machine that broke the pane hearing its own news back is a no-op.
    ///
    /// THE SERVER CHECKS the numbers (a building of the plan every machine has, a state, at most GlassDamage.MaxBulletHoles holes inside the pane)
    /// and at most <see cref="MaxChangesPerSecond"/> a second per player, and keeps its own record of every broken pane for the players who join
    /// later. FishNet BROADCASTS, not RPCs on a body: glass broken across the city must be broken when you get there. Alone, nothing is started
    /// and this does nothing: GlassDamage.Relay stays null.
    /// </summary>
    public sealed class GlassDamageNetwork : MonoBehaviour, IGlassRelay
    {
        /// <summary>The most panes one player may break per second (automatic fire along a row of windows).</summary>
        public const int MaxChangesPerSecond = 30;

        [Tooltip("The session to work in; empty: the running one (FishNet's InstanceFinder). Tests bind one per machine.")]
        public NetworkManager Manager;

        /// <summary>The record other players' glass is merged into; null: the city's own (GlassDamage.Current). Tests give each machine one.</summary>
        public GlassDamage Target;

        /// <summary>How many pane changes and full states this machine has heard from the server (for tests).</summary>
        public int ChangesHeard { get; private set; }
        public int StatesHeard { get; private set; }

        /// <summary>How many broken panes the last full state heard had (for tests).</summary>
        public int LastStateSize { get; private set; }

        /// <summary>What the server knows of the city's glass, for players who join later.</summary>
        private readonly GlassDamage _serverRecord = new GlassDamage();

        private NetworkManager _manager;
        private bool _serverHandlers;
        private bool _clientHandlers;
        private bool _requested;
        private readonly Dictionary<int, (float windowStart, int count)> _rate = new Dictionary<int, (float, int)>();

        private GlassDamage Record => Target ?? GlassDamage.Current;

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
                    _manager.ServerManager.RegisterBroadcast<GlassPaneMessage>(OnChangeAtServer);
                    _manager.ServerManager.RegisterBroadcast<GlassStateRequest>(OnStateRequestAtServer);
                }
                else
                {
                    _manager.ServerManager.UnregisterBroadcast<GlassPaneMessage>(OnChangeAtServer);
                    _manager.ServerManager.UnregisterBroadcast<GlassStateRequest>(OnStateRequestAtServer);
                }
                _serverHandlers = server;
            }

            bool client = _manager.IsClientStarted;
            if (client != _clientHandlers)
            {
                if (client)
                {
                    _manager.ClientManager.RegisterBroadcast<GlassPaneMessage>(OnChangeAtClient);
                    _manager.ClientManager.RegisterBroadcast<GlassStateMessage>(OnStateAtClient);
                    GlassDamage.Relay = this;
                    _requested = false;   // asked for once the server has let this client in (below)
                }
                else
                {
                    _manager.ClientManager.UnregisterBroadcast<GlassPaneMessage>(OnChangeAtClient);
                    _manager.ClientManager.UnregisterBroadcast<GlassStateMessage>(OnStateAtClient);
                    if (ReferenceEquals(GlassDamage.Relay, this))
                    {
                        GlassDamage.Relay = null;
                    }
                }
                _clientHandlers = client;
            }

            // A joining player asks which panes are broken, once the server has authenticated it (a broadcast before that is dropped).
            if (_clientHandlers && !_requested && _manager.ClientManager.Connection != null && _manager.ClientManager.Connection.IsAuthenticated)
            {
                _requested = true;
                _manager.ClientManager.Broadcast(new GlassStateRequest());
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(GlassDamage.Relay, this))
            {
                GlassDamage.Relay = null;
            }
        }

        public void Send(in PaneChange change)
        {
            if (_manager != null && _manager.IsClientStarted)
            {
                _manager.ClientManager.Broadcast(ToMessage(change));
            }
        }

        private void OnChangeAtServer(NetworkConnection sender, GlassPaneMessage message, Channel channel)
        {
            if (sender == null || !WithinRate(sender.ClientId, Time.unscaledTime) || !Acceptable(message))
            {
                return;
            }
            _serverRecord.Merge(ToChange(message), out _);
            _manager.ServerManager.Broadcast(message);
        }

        private void OnStateRequestAtServer(NetworkConnection sender, GlassStateRequest request, Channel channel)
        {
            if (sender == null)
            {
                return;
            }
            _manager.ServerManager.Broadcast(sender, ToState(_serverRecord.Snapshot()));
        }

        private void OnChangeAtClient(GlassPaneMessage message, Channel channel)
        {
            ChangesHeard++;
            if (Acceptable(message))
            {
                Bring(ToChange(message));
            }
        }

        private void OnStateAtClient(GlassStateMessage state, Channel channel)
        {
            StatesHeard++;
            List<PaneChange> panes = FromState(state);
            LastStateSize = panes.Count;
            foreach (PaneChange pane in panes)
            {
                Bring(pane);
            }
        }

        /// <summary>Merges a pane heard from the server into this machine's record and, if it moved here, has the city play it.</summary>
        private void Bring(in PaneChange change)
        {
            GlassDamage record = Record;
            if (record == null)
            {
                return;
            }
            record.Merge(change, out bool changed);
            if (changed && ReferenceEquals(record, GlassDamage.Current))
            {
                GlassDamage.RaiseHeard(change);
            }
        }

        private bool WithinRate(int client, float now)
        {
            if (!_rate.TryGetValue(client, out (float windowStart, int count) r) || now - r.windowStart >= 1.0f)
            {
                r = (now, 0);
            }
            r.count++;
            _rate[client] = r;
            return r.count <= MaxChangesPerSecond;
        }

        // --- the messages (public static so they are tested without a network) ---

        public static GlassPaneMessage ToMessage(in PaneChange change)
        {
            int n = change.Impacts != null ? change.Impacts.Length : 0;
            var message = new GlassPaneMessage
            {
                Building = change.Key.Building,
                Wall = (int)change.Key.Wall,
                Column = change.Key.Column,
                Row = change.Key.Row,
                State = (int)change.State,
                HoleU = new float[n],
                HoleV = new float[n],
                Point = change.Point,
                Direction = change.Direction,
                Strength = (int)change.Strength,
            };
            for (int i = 0; i < n; i++)
            {
                message.HoleU[i] = change.Impacts[i].x;
                message.HoleV[i] = change.Impacts[i].y;
            }
            return message;
        }

        public static PaneChange ToChange(in GlassPaneMessage message)
        {
            int n = message.HoleU == null || message.HoleV == null ? 0 : Mathf.Min(message.HoleU.Length, message.HoleV.Length);
            var holes = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                holes[i] = new Vector2(message.HoleU[i], message.HoleV[i]);
            }
            return new PaneChange(new PaneKey(message.Building, (WindowGrid.Wall)message.Wall, message.Column, message.Row), (PaneState)message.State, holes,
                message.Point, message.Direction, (BlowStrength)message.Strength);
        }

        public static GlassStateMessage ToState(List<PaneChange> panes)
        {
            int holes = 0;
            foreach (PaneChange p in panes)
            {
                holes += p.Impacts != null ? p.Impacts.Length : 0;
            }
            var state = new GlassStateMessage
            {
                Building = new int[panes.Count],
                Wall = new int[panes.Count],
                Column = new int[panes.Count],
                Row = new int[panes.Count],
                State = new int[panes.Count],
                Holes = new int[panes.Count],
                HoleU = new float[holes],
                HoleV = new float[holes],
            };
            int h = 0;
            for (int i = 0; i < panes.Count; i++)
            {
                PaneChange p = panes[i];
                state.Building[i] = p.Key.Building;
                state.Wall[i] = (int)p.Key.Wall;
                state.Column[i] = p.Key.Column;
                state.Row[i] = p.Key.Row;
                state.State[i] = (int)p.State;
                int n = p.Impacts != null ? p.Impacts.Length : 0;
                state.Holes[i] = n;
                for (int k = 0; k < n; k++, h++)
                {
                    state.HoleU[h] = p.Impacts[k].x;
                    state.HoleV[h] = p.Impacts[k].y;
                }
            }
            return state;
        }

        /// <summary>The panes of a full state that pass the same checks as a single change.</summary>
        public static List<PaneChange> FromState(in GlassStateMessage state)
        {
            var panes = new List<PaneChange>();
            int n = state.Building == null ? 0 : state.Building.Length;
            if (state.Wall == null || state.Column == null || state.Row == null || state.State == null || state.Holes == null
                || state.Wall.Length < n || state.Column.Length < n || state.Row.Length < n || state.State.Length < n || state.Holes.Length < n)
            {
                return panes;
            }
            int total = state.HoleU == null || state.HoleV == null ? 0 : Mathf.Min(state.HoleU.Length, state.HoleV.Length);
            int h = 0;
            for (int i = 0; i < n; i++)
            {
                int holes = Mathf.Max(0, state.Holes[i]);
                if (holes > GlassDamage.MaxBulletHoles || h + holes > total)
                {
                    return panes;   // a malformed state: keep what came before it
                }
                var message = new GlassPaneMessage
                {
                    Building = state.Building[i],
                    Wall = state.Wall[i],
                    Column = state.Column[i],
                    Row = state.Row[i],
                    State = state.State[i],
                    HoleU = new float[holes],
                    HoleV = new float[holes],
                };
                for (int k = 0; k < holes; k++, h++)
                {
                    message.HoleU[k] = state.HoleU[h];
                    message.HoleV[k] = state.HoleV[h];
                }
                if (Acceptable(message))
                {
                    panes.Add(ToChange(message));
                }
            }
            return panes;
        }

        /// <summary>
        /// A pane message names a pane that could be one (a building of the plan when the world is known, a wall, a cell), a state past whole, and at
        /// most GlassDamage.MaxBulletHoles holes, each inside the pane.
        /// </summary>
        public static bool Acceptable(in GlassPaneMessage message)
        {
            if (message.Building < 0 || message.Wall < 0 || message.Wall > 63 || message.Column < 0 || message.Row < 0 || message.Row > 0xFFF
                || message.State < (int)PaneState.Cracked || message.State > (int)PaneState.Shattered
                || message.Strength < 0 || message.Strength > (int)BlowStrength.Bullet)
            {
                return false;
            }
            int holes = message.HoleU == null ? 0 : message.HoleU.Length;
            if (holes > GlassDamage.MaxBulletHoles || (message.HoleV == null ? 0 : message.HoleV.Length) != holes)
            {
                return false;
            }
            for (int i = 0; i < holes; i++)
            {
                if (!(message.HoleU[i] >= 0.0f && message.HoleU[i] <= 1.0f && message.HoleV[i] >= 0.0f && message.HoleV[i] <= 1.0f))
                {
                    return false;   // also false for NaN
                }
            }
            CityWorld world = CurrentWorld.Instance;
            return world == null || message.Building < world.BuildingCount;
        }
    }
}
