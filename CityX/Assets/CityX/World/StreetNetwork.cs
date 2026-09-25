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

        /// <summary>
        /// How far outward (in chunks) a search for the nearest street chunk
        /// ever needs to look on one axis. Two neighbouring streets, local or
        /// arterial, are never more than CityLayout.MaxStreetGap chunks apart
        /// (the nearest one is always well inside that) -- this is that outer
        /// bound, so the search below is provably bounded rather than merely
        /// "probably fine in practice."
        /// </summary>
        private const int MaxSearchChunks = CityLayout.MaxStreetGap;

        /// <summary>
        /// Nearest street chunk to (chunkX, chunkY) along one axis: the X
        /// axis if <paramref name="alongX"/>, else Y. Replaces what used to
        /// be closed-form arithmetic (one global LineSpacing) now that
        /// CityLayout.LocalStreetPeriodFor makes street spacing a property of
        /// which district a point is in, not a single number good everywhere
        /// -- this just asks the real grid directly instead of assuming a
        /// formula for it.
        /// </summary>
        private static int NearestStreetChunk(int chunkX, int chunkY, bool alongX)
        {
            int fixedCoord = alongX ? chunkY : chunkX;
            int searchFrom = alongX ? chunkX : chunkY;

            for (int d = 0; d <= MaxSearchChunks; d++)
            {
                int minus = searchFrom - d;
                int plus = searchFrom + d;
                bool minusIsStreet = alongX
                    ? CityLayout.IsStreetChunk(minus, fixedCoord)
                    : CityLayout.IsStreetChunk(fixedCoord, minus);
                if (minusIsStreet)
                {
                    return minus;
                }
                bool plusIsStreet = alongX
                    ? CityLayout.IsStreetChunk(plus, fixedCoord)
                    : CityLayout.IsStreetChunk(fixedCoord, plus);
                if (plusIsStreet)
                {
                    return plus;
                }
            }

            // Unreachable in practice -- an arterial guarantees a hit within
            // MaxSearchChunks -- but a real value beats an infinite loop if
            // that guarantee is ever violated by a future change.
            return searchFrom;
        }

        /// <summary>
        /// How far a point is from the nearest street line on the X axis, and
        /// which chunk that line is. Used to decide which of the two crossing
        /// streets a pedestrian is standing on, and how close they are to the
        /// junction.
        /// </summary>
        public static float DistanceToNearestLineX(Vector3 position)
        {
            int chunkX = Mathf.FloorToInt(position.x / CityLayout.ChunkSize);
            int chunkY = Mathf.FloorToInt(position.z / CityLayout.ChunkSize);
            int nearest = NearestStreetChunk(chunkX, chunkY, alongX: true);
            return Mathf.Abs(position.x - (float)CityLayout.ChunkCentreX(nearest));
        }

        /// <summary>The Z-axis counterpart of <see cref="DistanceToNearestLineX"/>.</summary>
        public static float DistanceToNearestLineZ(Vector3 position)
        {
            int chunkX = Mathf.FloorToInt(position.x / CityLayout.ChunkSize);
            int chunkY = Mathf.FloorToInt(position.z / CityLayout.ChunkSize);
            int nearest = NearestStreetChunk(chunkX, chunkY, alongX: false);
            return Mathf.Abs(position.z - (float)CityLayout.ChunkCentreZ(nearest));
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
            return DistanceToNearestLineX(position) <= DistanceToNearestLineZ(position);
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
            int chunkX = Mathf.FloorToInt(position.x / CityLayout.ChunkSize);
            int chunkY = Mathf.FloorToInt(position.z / CityLayout.ChunkSize);

            if (RunsNorthSouth(position))
            {
                float centre = (float)CityLayout.ChunkCentreX(NearestStreetChunk(chunkX, chunkY, alongX: true));
                float side = position.x >= centre ? 1.0f : -1.0f;
                return new Vector3(centre + side * PavementOffset, position.y, position.z);
            }
            else
            {
                float centre = (float)CityLayout.ChunkCentreZ(NearestStreetChunk(chunkX, chunkY, alongX: false));
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
        ///
        /// A LOCAL STREET NO LONGER RUNS FOREVER. A district's own local
        /// grid does not have to continue into its neighbour's (see
        /// CityLayout.LocalStreetPeriodFor's own doc comment) -- a straight
        /// step that used to always stay on the same infinite line can now
        /// walk off the end of a short local one right at a district edge.
        /// Re-snapping the raw step's target is what still guarantees "on a
        /// street" here: in the ordinary case (nowhere near a district
        /// edge) that re-snap is a no-op, since the raw target is already
        /// sitting exactly on its own pavement lane.
        /// </summary>
        public static Vector3 NextTarget(Vector3 from, DeterministicRandom random,
            float minStep, float maxStep)
        {
            Vector3 start = SnapToPavement(from);
            bool northSouth = RunsNorthSouth(from);

            // At a junction, both streets are underfoot, so either is a legal
            // way to leave it.
            bool atJunction = (northSouth ? DistanceToNearestLineZ(start) : DistanceToNearestLineX(start)) <= TurnRadius;
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

            Vector3 raw = northSouth
                ? new Vector3(start.x, start.y, start.z + step)
                : new Vector3(start.x + step, start.y, start.z);
            Vector3 target = SnapToPavement(raw);

            // The re-snap corrected a real discontinuity rather than just
            // confirming the same point -- try stepping the other way
            // before accepting whatever tiny (or backward) move it found,
            // since only one direction along the old line was actually
            // blocked.
            float distance = Vector2.Distance(
                new Vector2(start.x, start.z), new Vector2(target.x, target.z));
            if (distance < minStep * 0.5f)
            {
                Vector3 rawOpposite = northSouth
                    ? new Vector3(start.x, start.y, start.z - step)
                    : new Vector3(start.x - step, start.y, start.z);
                Vector3 targetOpposite = SnapToPavement(rawOpposite);
                float distanceOpposite = Vector2.Distance(
                    new Vector2(start.x, start.z), new Vector2(targetOpposite.x, targetOpposite.z));
                if (distanceOpposite > distance)
                {
                    target = targetOpposite;
                }
            }

            return target;
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
