using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// THE WORLD'S ENEMIES, ROUND THIS MACHINE'S PLAYER (owner, 2026-11-14: "lets try implementing mobs spawning in the world ... monster and
    /// ghost type enemies spawn outside and humanoid enemies spawn indoors for starters", and "it's not that the mobs have set spawn locations
    /// right? it should be more akin to minecraft where they spawn at random"; docs/MOB_SPAWNING.md).
    ///
    /// AT RANDOM, AS MINECRAFT DOES: nothing has a set place. While there is room under the budget (<see cref="MobHabitats.Most"/>, out of
    /// doors and indoors each their own), every few seconds random spots are rolled round the player: over the ground, where on open
    /// ground a ghost or a monster appears (<see cref="MobHabitats.At"/>), and inside a random one of the buildings near by, where a
    /// humanoid appears on a random one of its floors (<see cref="MobHabitats.BuildingsNear"/>, <see cref="MobHabitats.TryIndoorSpot"/>). An enemy the player has left <see cref="MobHabitats.CrowdMetres"/> behind is taken out of the world.
    /// A killed one is simply gone: another appears somewhere else in its own time. But A CLEARED BUILDING STAYS EMPTY (owner, 2026-11-14:
    /// "Cleared buildings should stop spawning enemies"): once the last enemy alive in a building is killed, nothing appears in it for a
    /// day (<see cref="MobHabitats.ClearedSeconds"/>); in a session the server keeps that for everyone.
    ///
    /// IN A SESSION each machine does this for its own player and ASKS THE SERVER for each enemy (EnemyNetwork), which spawns it for
    /// everyone, run by the asker's machine as with /spawn, and counts what is alive near the asker whoever runs it: players standing
    /// together share one budget. Only kinds the session shares are asked for.
    ///
    /// An enemy is put down only where this machine has the city built in full (floors and their colliders) and the spot is sound: ground
    /// under it at the height the plan says, and indoors room to stand.
    /// </summary>
    public sealed class MobSpawner : MonoBehaviour
    {
        [Tooltip("The city being streamed: its plan says what a spot is, and what it has built says where an enemy can be put down.")]
        public CityStreamer Streamer;

        [Tooltip("The enemies, as the console's /spawn puts them down when playing alone.")]
        public GameObject WraithPrefab;

        public GameObject CrimsonClawPrefab;

        public GameObject SwordsmanPrefab;

        public GameObject ShootaPrefab;

        [Tooltip("Off: nothing new appears.")]
        public bool Spawning = true;

        /// <summary>How often the enemies held are looked over and new spots rolled, seconds.</summary>
        public const float LookEverySeconds = 0.5f;

        /// <summary>How long the server's answer is waited for, seconds.</summary>
        public const float AnswerWaitSeconds = 8.0f;

        /// <summary>The ground has to be within this of the height the plan gives a spot, metres: a roof, an awning or a deck over the street is not the street.</summary>
        public const float GroundHeightTolerance = 1.5f;

        public const float FloorHeightTolerance = 0.6f;

        /// <summary>The room a humanoid needs to stand in, metres.</summary>
        public const float StandingRadius = 0.45f;

        public const float StandingHeight = 2.4f;

        private sealed class Held
        {
            public GameObject Body;
            public IDamageable Life;
            public MobHabitat Habitat;
            public long Ticket;

            /// <summary>The building it appeared in (its id in the plan), or -1 out of doors.</summary>
            public int Building = -1;
        }

        /// <summary>The session says a building this machine asked to put an enemy in has been cleared (by anyone): nothing is asked for it again for this long, seconds.</summary>
        public const float ClearedAskAgainSeconds = 600.0f;

        /// <summary>The buildings cleared of their enemies, and when each may have enemies again (Time.time): MobHabitats.ClearedSeconds.</summary>
        private readonly Dictionary<int, float> _cleared = new Dictionary<int, float>();

        private static readonly MobHabitat[] Habitats = { MobHabitat.Outdoors, MobHabitat.Indoors };

        private readonly List<Held> _held = new List<Held>();
        private readonly List<GameObject> _corpses = new List<GameObject>();

        /// <summary>The server has been asked and has not answered yet: the ask's ticket, its habitat, when the wait is given up.</summary>
        private readonly Dictionary<long, (MobHabitat Habitat, float Until, int Building)> _asked = new Dictionary<long, (MobHabitat, float, int)>();

        private readonly List<long> _late = new List<long>();

        /// <summary>The buildings that can hold enemies near the chunk the player was last in (their ids), for a humanoid's spot to be rolled in.</summary>
        private readonly List<int> _near = new List<int>();

        private Vector2Int _nearCentre;
        private bool _nearKnown;
        private readonly float[] _nextSpawn = new float[3];
        private readonly System.Random _dice = new System.Random();
        private EnemyNetwork _network;
        private float _nextLook;
        private long _lastTicket;

        /// <summary>Whether building <paramref name="building"/> (its id in the plan) has been cleared and is still empty.</summary>
        public bool IsCleared(int building) => _cleared.TryGetValue(building, out float until) && Time.time < until;

        /// <summary>How many of the world's enemies this machine has alive (or asked for) just now.</summary>
        public int Alive => _held.Count + _asked.Count;

        /// <summary>How many of them are of <paramref name="habitat"/>.</summary>
        public int AliveIn(MobHabitat habitat)
        {
            int count = 0;
            foreach (Held held in _held)
            {
                if (held.Habitat == habitat) count++;
            }
            foreach ((MobHabitat asked, float _, int _) in _asked.Values)
            {
                if (asked == habitat) count++;
            }
            return count;
        }

        /// <summary>Adds the enemies this machine has alive to <paramref name="into"/>, each with the habitat it appeared in (for tests and captures).</summary>
        public void CopyAlive(List<(GameObject Body, MobHabitat Habitat)> into)
        {
            foreach (Held held in _held)
            {
                if (held.Body != null) into.Add((held.Body, held.Habitat));
            }
        }

        private void OnDisable()
        {
            Listen(null);
        }

        /// <summary>Turns the world's enemies on or off (/mobs): off, the ones alive here are taken away at once and nothing appears.</summary>
        public void SetSpawning(bool on)
        {
            Spawning = on;
            if (on)
            {
                return;
            }
            foreach (Held held in _held)
            {
                if (held.Body != null)
                {
                    EnemySession.Remove(held.Body);
                }
            }
            _held.Clear();
        }

        private void Listen(EnemyNetwork network)
        {
            if (_network == network)
            {
                return;
            }
            if (_network != null)
            {
                _network.WorldMobAnswered -= OnAnswered;
                _network.WorldMobRefusedAsCleared -= OnRefusedAsCleared;
            }
            _network = network;
            if (_network != null)
            {
                _network.WorldMobRefusedAsCleared += OnRefusedAsCleared;   // (first: the answer that follows forgets the ask)
                _network.WorldMobAnswered += OnAnswered;
            }
        }

        private void Update()
        {
            NoteTheDead();
            if (Time.time < _nextLook)
            {
                return;
            }
            _nextLook = Time.time + LookEverySeconds;
            GameObject player = LocalPlayerBody.Find();
            if (player == null || Streamer == null || Streamer.World == null)
            {
                return;
            }
            Listen(EnemyNetwork.Current);
            Look(Streamer.World.Plan, player.transform.position);
        }

        /// <summary>
        /// Every frame, of the few enemies held: a dead one is this machine's no longer (and the session is told, so it stops counting
        /// toward the budget). Its body lies where it fell, to be looted, until the player leaves it. (Every frame because a dead ghost
        /// fades out of the world by itself.)
        /// </summary>
        private void NoteTheDead()
        {
            for (int i = _held.Count - 1; i >= 0; i--)
            {
                Held held = _held[i];
                if (held.Body != null && (held.Life == null || held.Life.IsAlive))
                {
                    continue;
                }
                if (held.Body != null)
                {
                    _corpses.Add(held.Body);
                }
                if (_network != null && held.Ticket != 0L)
                {
                    _network.NoteWorldMobKilled(held.Ticket);
                }
                _held.RemoveAt(i);
                // The last one alive in its building: the building is cleared, and stays empty.
                if (held.Building >= 0 && !HoldsOneIn(held.Building))
                {
                    _cleared[held.Building] = Time.time + MobHabitats.ClearedSeconds;
                }
            }
        }

        private bool HoldsOneIn(int building)
        {
            foreach (Held held in _held)
            {
                if (held.Building == building)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>One look: what has been left behind is taken away, then spots are rolled for each habitat that has room.</summary>
        private void Look(PlanWorld plan, Vector3 viewer)
        {
            for (int i = _held.Count - 1; i >= 0; i--)
            {
                if (_held[i].Body != null && MobSpawnRules.TooFar(_held[i].Body.transform.position, viewer))
                {
                    EnemySession.Remove(_held[i].Body);
                    _held.RemoveAt(i);
                }
            }
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                if (_corpses[i] == null || MobSpawnRules.TooFar(_corpses[i].transform.position, viewer))
                {
                    if (_corpses[i] != null)
                    {
                        EnemySession.Remove(_corpses[i]);
                    }
                    _corpses.RemoveAt(i);
                }
            }
            _late.Clear();
            foreach (KeyValuePair<long, (MobHabitat Habitat, float Until, int Building)> ask in _asked)
            {
                if (Time.time >= ask.Value.Until) _late.Add(ask.Key);
            }
            foreach (long ticket in _late)
            {
                _asked.Remove(ticket);
            }
            if (!Spawning)
            {
                return;
            }
            foreach (MobHabitat habitat in Habitats)
            {
                if (AliveIn(habitat) >= MobHabitats.Most(habitat) || Time.time < _nextSpawn[(int)habitat])
                {
                    continue;
                }
                for (int roll = 0; roll < MobSpawnRules.SpotsRolledEachLook; roll++)
                {
                    if (TryPutDownAtRandom(plan, viewer, habitat))
                    {
                        _nextSpawn[(int)habitat] = Time.time + MobSpawnRules.SecondsBetweenSpawns;
                        break;
                    }
                }
            }
        }

        private float Roll() => (float)_dice.NextDouble();

        /// <summary>Rolls one spot round the player; if it is <paramref name="habitat"/>, built here and sound, an enemy appears on it.</summary>
        private bool TryPutDownAtRandom(PlanWorld plan, Vector3 viewer, MobHabitat habitat)
        {
            Vector2 spot;
            BuildingSite site;
            int building = -1;
            if (habitat == MobHabitat.Outdoors)
            {
                // Anywhere in the ring round the player, if it is open ground.
                spot = MobSpawnRules.SpotRound(viewer, MobSpawnRules.OutdoorsFromMetres, MobSpawnRules.ToMetres, Roll(), Roll());
                if (MobHabitats.At(plan, spot.x, spot.y, out site) != MobHabitat.Outdoors)
                {
                    return false;
                }
            }
            else
            {
                // One of the buildings near by, then a spot inside it, if that is in the ring.
                var centre = new Vector2Int(PlanWorld.ChunkOf(viewer.x), PlanWorld.ChunkOf(viewer.z));
                if (!_nearKnown || centre != _nearCentre)
                {
                    _nearKnown = true;
                    _nearCentre = centre;
                    _near.Clear();
                    MobHabitats.BuildingsNear(plan, centre.x, centre.y, Mathf.CeilToInt(MobSpawnRules.ToMetres / (float)PlanWorld.ChunkSize), _near);
                }
                if (_near.Count == 0)
                {
                    return false;
                }
                building = _near[_dice.Next(_near.Count)];
                site = plan.Buildings[building];
                if (IsCleared(building) || !MobHabitats.TryIndoorSpot(plan, site, Roll(), Roll(), out double x, out double z))
                {
                    return false;
                }
                spot = new Vector2((float)x, (float)z);
                float away = Vector2.Distance(spot, new Vector2(viewer.x, viewer.z));
                if (away < MobSpawnRules.IndoorsFromMetres || away > MobSpawnRules.ToMetres)
                {
                    return false;
                }
            }
            // (A building is built by the chunk its centre is in.)
            Vector2Int chunk = habitat == MobHabitat.Indoors ? new Vector2Int(site.OwnerChunkX, site.OwnerChunkY)
                : new Vector2Int(PlanWorld.ChunkOf(spot.x), PlanWorld.ChunkOf(spot.y));
            if (!Streamer.IsChunkLoaded(chunk) || Streamer.IsChunkFar(chunk))
            {
                return false;   // not built in full here: no floors, no colliders to stand on
            }
            Vector3 ground;
            if (habitat == MobHabitat.Outdoors)
            {
                var at = new Vector3(spot.x, (float)plan.GroundHeight(spot.x, spot.y), spot.y);
                if (Mathf.Abs(at.y - viewer.y) > MobSpawnRules.WithinHeightMetres || !TryGround(at, 6.0f, GroundHeightTolerance, out ground))
                {
                    return false;
                }
            }
            else
            {
                float floorY = MobHabitats.FloorY(site, _dice.Next(MobHabitats.Floors(site)));
                var at = new Vector3(spot.x, floorY, spot.y);
                if (Mathf.Abs(floorY - viewer.y) > MobSpawnRules.WithinHeightMetres
                    || !TryGround(at, 1.6f, FloorHeightTolerance, out ground)
                    || Physics.CheckCapsule(ground + Vector3.up * (StandingRadius + 0.15f), ground + Vector3.up * (StandingHeight - StandingRadius),
                        StandingRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    return false;   // no floor there (the stair's well), or no room to stand (a wall, the stair)
                }
            }

            string kind = MobSpawnRules.KindFor(MobHabitats.FamilyFor(habitat, Roll()), Roll());
            GameObject prefab = PrefabFor(kind);
            bool session = _network != null && _network.InSession;
            if (prefab == null || (session && !_network.Shares(kind)))
            {
                return false;
            }
            Vector3 position = ground + Vector3.up * MobSpawnRules.HeightOffGround(kind);
            Quaternion facing = Quaternion.Euler(0.0f, Roll() * 360.0f, 0.0f);
            if (session)
            {
                long ticket = ++_lastTicket;
                if (!_network.TrySpawnWorldMob(kind, position, facing, ticket, building))
                {
                    return false;
                }
                _asked[ticket] = (habitat, Time.time + AnswerWaitSeconds, building);
                return true;
            }
            GameObject body = Instantiate(prefab, position, facing);
            // The city's, not whichever scene happens to be the active one: it leaves the world with the city.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(body, gameObject.scene);
            _held.Add(new Held { Body = body, Life = body.GetComponent<IDamageable>(), Habitat = habitat, Building = building });
            return true;
        }

        private void OnRefusedAsCleared(long ticket)
        {
            if (_asked.TryGetValue(ticket, out (MobHabitat Habitat, float Until, int Building) ask) && ask.Building >= 0)
            {
                _cleared[ask.Building] = Time.time + ClearedAskAgainSeconds;
            }
        }

        private void OnAnswered(long ticket, GameObject body)
        {
            if (!_asked.TryGetValue(ticket, out (MobHabitat Habitat, float Until, int Building) ask))
            {
                if (body != null)
                {
                    EnemySession.Remove(body);   // answered too late: nobody here is waiting for it any more
                }
                return;
            }
            _asked.Remove(ticket);
            if (body != null)   // (null: the budget near here is full, with enemies other players' machines run)
            {
                _held.Add(new Held { Body = body, Life = body.GetComponent<IDamageable>(), Habitat = ask.Habitat, Ticket = ticket, Building = ask.Building });
            }
        }

        private GameObject PrefabFor(string kind)
        {
            switch (kind)
            {
                case MobSpawnRules.Ghost: return WraithPrefab;
                case MobSpawnRules.Monster: return CrimsonClawPrefab;
                case MobSpawnRules.Gunner: return ShootaPrefab;
                default: return SwordsmanPrefab;
            }
        }

        /// <summary>The ground under <paramref name="at"/>, if a ray down from <paramref name="from"/> above it finds any within <paramref name="tolerance"/> of its height.</summary>
        private static bool TryGround(Vector3 at, float from, float tolerance, out Vector3 ground)
        {
            ground = at;
            if (!Physics.Raycast(at + Vector3.up * from, Vector3.down, out RaycastHit hit, from + tolerance + 1.0f, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - at.y) > tolerance)
            {
                return false;
            }
            ground = hit.point;
            return true;
        }
    }
}
