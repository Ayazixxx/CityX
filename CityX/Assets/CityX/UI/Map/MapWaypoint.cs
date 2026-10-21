using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The player's own waypoint: set by right-clicking the map, shown on the map and the minimap with its route along the streets
    /// (RouteFinder: GTA's GPS, a shortest path over the road graph), and in the world as a beam of light (WaypointBeacon). The route is worked
    /// out again whenever the player has moved on far enough from where it was last worked out, and the waypoint clears itself when they arrive.
    ///
    /// The player's own, like GTA's: other players do not see it. A waypoint shared with a faction comes with factions.
    /// </summary>
    public sealed class MapWaypoint : MonoBehaviour
    {
        /// <summary>Within this of the waypoint, it is reached and goes.</summary>
        public const float ArriveMetres = 8.0f;

        /// <summary>The route is worked out again once the player is this far from where it last started.</summary>
        public const float RerouteMetres = 10.0f;

        public CityStreamer Streamer;

        public Transform Player;

        public bool Has { get; private set; }

        /// <summary>Where it is, world (x, z).</summary>
        public Vector2 Point { get; private set; }

        /// <summary>The way there along the streets, world (x, z), starting where the player was when it was worked out. Empty when there is none.</summary>
        public readonly List<Vector2> Route = new List<Vector2>();

        /// <summary>How far the route is, metres (the straight line when there is no route).</summary>
        public float RouteMetres { get; private set; }

        /// <summary>Raised when one is set, cleared or reached.</summary>
        public event System.Action Changed;

        private Vector2 _routedFrom;
        private CityWorld _world;
        private int _worldSeed;

        public void Set(Vector2 point)
        {
            Has = true;
            Point = point;
            Reroute();
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (!Has)
            {
                return;
            }
            Has = false;
            Route.Clear();
            RouteMetres = 0.0f;
            Changed?.Invoke();
        }

        /// <summary>How far, straight, from the player to the waypoint, metres.</summary>
        public float StraightMetres => Player == null ? 0.0f : Vector2.Distance(Flat(Player.position), Point);

        private void Update()
        {
            if (!Has || Player == null)
            {
                return;
            }
            Vector2 here = Flat(Player.position);
            if (Vector2.Distance(here, Point) <= ArriveMetres)
            {
                Clear();   // arrived
                return;
            }
            if (Vector2.Distance(here, _routedFrom) >= RerouteMetres)
            {
                Reroute();
            }
            else if (Route.Count > 0)
            {
                // The line leaves from the player, not from where they were when it was worked out.
                Route[0] = here;
            }
        }

        /// <summary>Works the route out again from where the player is now. Public so a test can ask for it at once.</summary>
        public void Reroute()
        {
            Route.Clear();
            Vector2 here = Player != null ? Flat(Player.position) : Point;
            _routedFrom = here;
            CityWorld world = World();
            List<Vec2> found = world != null ? RouteFinder.Find(world.Plan.Data, new Vec2(here.x, here.y), new Vec2(Point.x, Point.y)) : null;
            if (found == null)
            {
                RouteMetres = Vector2.Distance(here, Point);
                return;
            }
            foreach (Vec2 p in found)
            {
                Route.Add(new Vector2((float)p.X, (float)p.Y));
            }
            RouteMetres = (float)RouteFinder.Length(found);
        }

        private CityWorld World()
        {
            if (Streamer == null)
            {
                return null;
            }
            if (_world == null || _worldSeed != Streamer.WorldSeed)
            {
                _world = new CityWorld(Streamer.WorldSeed);
                _worldSeed = Streamer.WorldSeed;
            }
            return _world;
        }

        public static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);
    }
}
