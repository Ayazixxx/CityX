using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Where a pedestrian can walk, as a pure function of the street grid.
    ///
    /// NO NAVMESH, AND NO GROUND PROBING. The Godot original picked a random
    /// point in a circle around the NPC, raycast down to see whether anything
    /// walkable was under it, and retried six times if not -- then still needed
    /// a whole obstacle-avoidance component, because a target picked behind a
    /// wall turned into a six-second grind against that wall.
    ///
    /// None of that is necessary here. The city already knows exactly where its
    /// streets are, as arithmetic, so a target can be chosen that is on one to
    /// begin with. A baked navmesh is also simply not an option for a world
    /// generated around the player as they walk into it.
    ///
    /// Everything is in metres in world space, and nothing here touches a
    /// scene, so it can all be checked without one.
    /// </summary>
    public static class StreetNetwork
    {
        /// <summary>
        /// Distance between parallel streets: every fourth chunk is a street,
        /// so 96m. Derived rather than typed, because if the block size changes
        /// pedestrians must follow it.
        /// </summary>
        public const float LineSpacing = CityLayout.StreetPeriod * CityLayout.ChunkSize;

        /// <summary>
        /// How far from a street's centreline a pedestrian walks. The mesh
        /// builder lays 4m of pavement along each edge of a 24m street, so its
        /// middle is 10m out -- walking the centreline would be walking down
        /// the middle of the road.
        /// </summary>
        public const float PavementOffset = CityLayout.ChunkSize * 0.5f - 2.0f;

        /// <summary>
        /// How close to a junction a pedestrian has to be before turning down
        /// the crossing street is an option. Roughly half a street's width, so
        /// the turn happens inside the junction rather than by cutting a corner
        /// through a building.
        /// </summary>
        public const float TurnRadius = CityLayout.ChunkSize * 0.5f;

        /// <summary>Chance of turning at a junction rather than walking on through.</summary>
        public const double TurnChance = 0.45;

        /// <summary>Which street line, by index, is nearest a given world coordinate.</summary>
        public static int NearestLineIndex(float coordinate)
        {
            return Mathf.RoundToInt((coordinate - CityLayout.ChunkSize * 0.5f) / LineSpacing);
        }

        /// <summary>The world coordinate of street line <paramref name="index"/>.</summary>
        public static float LineCoordinate(int index)
        {
            return index * LineSpacing + CityLayout.ChunkSize * 0.5f;
        }

        /// <summary>
        /// How far a point is from the nearest street line on one axis. Used to
        /// decide which of the two crossing streets a pedestrian is standing
        /// on, and how close they are to the junction.
        /// </summary>
        public static float DistanceToNearestLine(float coordinate)
        {
            return Mathf.Abs(coordinate - LineCoordinate(NearestLineIndex(coordinate)));
        }

        /// <summary>
        /// Which axis of street a point is standing on. True means it runs
        /// north-south (a fixed X), false means east-west (a fixed Z).
        ///
        /// Decided by whichever line is CLOSER, so a point in the middle of a
        /// block still resolves to something rather than to nothing -- there is
        /// no valid answer there, and a pedestrian who somehow ends up inside a
        /// block should walk out to the nearest street rather than freeze.
        /// </summary>
        public static bool RunsNorthSouth(Vector3 position)
        {
            return DistanceToNearestLine(position.x) <= DistanceToNearestLine(position.z);
        }

        /// <summary>
        /// Snaps a point onto the nearest pavement lane.
        ///
        /// The side of the street is kept if the point is already on one, so a
        /// pedestrian walking down the left-hand pavement stays on it rather
        /// than teleporting across the road at every waypoint.
        /// </summary>
        public static Vector3 SnapToPavement(Vector3 position)
        {
            if (RunsNorthSouth(position))
            {
                float centre = LineCoordinate(NearestLineIndex(position.x));
                float side = position.x >= centre ? 1.0f : -1.0f;
                return new Vector3(centre + side * PavementOffset, position.y, position.z);
            }
            else
            {
                float centre = LineCoordinate(NearestLineIndex(position.z));
                float side = position.z >= centre ? 1.0f : -1.0f;
                return new Vector3(position.x, position.y, centre + side * PavementOffset);
            }
        }

        /// <summary>
        /// The next place to walk to: a step along the current pavement, or --
        /// if standing in a junction -- a step down the crossing street.
        ///
        /// The result is ALWAYS on a street, by construction, which is the
        /// whole point. Nothing has to check afterwards whether it landed
        /// inside a building, because it cannot.
        /// </summary>
        public static Vector3 NextTarget(Vector3 from, DeterministicRandom random,
            float minStep, float maxStep)
        {
            Vector3 start = SnapToPavement(from);
            bool northSouth = RunsNorthSouth(from);

            // At a junction, both streets are underfoot, so either is a legal
            // way to leave it.
            bool atJunction = DistanceToNearestLine(northSouth ? start.z : start.x) <= TurnRadius;
            if (atJunction && random.NextDouble() < TurnChance)
            {
                northSouth = !northSouth;
                start = SnapToPavement(new Vector3(
                    northSouth ? from.x : start.x,
                    start.y,
                    northSouth ? start.z : from.z));
            }

            float step = (float)random.Range(minStep, maxStep);
            if (random.NextDouble() < 0.5)
            {
                step = -step;
            }

            return northSouth
                ? new Vector3(start.x, start.y, start.z + step)
                : new Vector3(start.x + step, start.y, start.z);
        }

        /// <summary>
        /// A deterministic spawn point for pedestrian <paramref name="index"/>
        /// near <paramref name="centre"/>, on a pavement.
        ///
        /// Deterministic on purpose: the same seed puts the same crowd in the
        /// same places on every machine, which is what a networked world needs
        /// if pedestrians are ever to exist on more than one of them.
        /// </summary>
        public static Vector3 SpawnPointNear(Vector3 centre, int index, int worldSeed, float radius)
        {
            var random = new DeterministicRandom(
                WorldHash.Coord(index, 0, 0x5A11E, unchecked((ulong)(long)worldSeed)));

            float angle = (float)random.Range(0.0, Mathf.PI * 2.0);
            float distance = (float)random.Range(radius * 0.25, radius);
            var point = new Vector3(
                centre.x + Mathf.Cos(angle) * distance,
                centre.y,
                centre.z + Mathf.Sin(angle) * distance);

            return SnapToPavement(point);
        }
    }
}
