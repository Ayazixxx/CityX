using System;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Where a pedestrian can walk, as a pure function of the road plan.
    ///
    /// NO NAVMESH, AND NO GROUND PROBING. The city already knows exactly where its roads are -- they are the edges of the plan -- so
    /// a target can be chosen that is on a pavement to begin with, and a baked navmesh is not an option for a world generated around
    /// the player as they walk into it.
    ///
    /// A pedestrian walks ALONG a road, on one side of it, and at a junction takes one of the roads that leave it. "One side" is kept
    /// relative to the way they are walking, so someone on their left-hand pavement stays on it round every corner.
    ///
    /// Reads the running world through <see cref="CurrentWorld"/> (the streamer publishes it), so callers keep the simple
    /// position-in, position-out shape they always had. With no world published everything returns its input unchanged.
    /// </summary>
    public static class StreetNetwork
    {
        /// <summary>How far from a road's centreline a pedestrian walks on a street: out past the kerb, in the middle of the pavement.</summary>
        public const float PavementOffset = 7.0f;

        /// <summary>The pavement's middle, from a road's centreline, for a road with this half width.</summary>
        public static float OffsetFor(double halfWidth) => (float)(halfWidth + 1.5);

        private const int MaxHops = 8;

        private static PlanWorld CurrentPlan => CurrentWorld.Instance?.Plan;

        /// <summary>The nearest road to a point: which, and how far along it (metres from its A end).</summary>
        private static EdgeGeometry NearestRoad(PlanWorld plan, double x, double z, out double along, out double signedSide)
        {
            along = 0.0;
            signedSide = 0.0;
            int cx = PlanWorld.ChunkOf(x);
            int cy = PlanWorld.ChunkOf(z);
            EdgeGeometry best = null;
            double bestDist = double.MaxValue;
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    var ids = plan.EdgesIn(cx + dx, cy + dy);
                    for (int i = 0; i < ids.Count; i++)
                    {
                        EdgeGeometry g = plan.Geometry.Edges[ids[i]];
                        if (g == null) continue;
                        double d = PlanMath.DistanceToSegment(new Vec2(x, z), g.A, g.B, out double t);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            best = g;
                            along = t * g.Length;
                            signedSide = Vec2.Cross(g.Dir, new Vec2(x, z) - g.A);
                        }
                    }
                }
            }
            return best;
        }

        private static Vector3 PointOn(PlanWorld plan, EdgeGeometry g, double along, double side)
        {
            Vec2 p = g.A + g.Dir * along + g.Dir.Perp * (side * OffsetFor(g.HalfWidth));
            double y;
            if (g.Edge.Bridge)
            {
                double hA = plan.GroundHeight(g.A.X, g.A.Y);
                double hB = plan.GroundHeight(g.B.X, g.B.Y);
                y = hA + (hB - hA) * (g.Length > 0.0 ? along / g.Length : 0.0);
            }
            else
            {
                y = plan.GroundHeight(p.X, p.Y);
            }
            return new Vector3((float)p.X, (float)y + 0.2f, (float)p.Y);
        }

        /// <summary>
        /// Snaps a point onto the nearest pavement. The side of the road is kept if the point is already on one, so a pedestrian
        /// walking down the left-hand pavement stays on it rather than teleporting across the road at every waypoint.
        /// </summary>
        public static Vector3 SnapToPavement(Vector3 position)
        {
            PlanWorld plan = CurrentPlan;
            if (plan == null) return position;
            EdgeGeometry g = NearestRoad(plan, position.x, position.z, out double along, out double signedSide);
            if (g == null) return position;
            Vector3 p = PointOn(plan, g, along, signedSide >= 0.0 ? 1.0 : -1.0);
            return new Vector3(p.x, position.y, p.z);
        }

        /// <summary>
        /// The next place to walk to: a step along the current pavement, round a corner if it runs out. The result is always on a
        /// road's pavement, by construction.
        /// </summary>
        public static Vector3 NextTarget(Vector3 from, DeterministicRandom random, float minStep, float maxStep)
        {
            PlanWorld plan = CurrentPlan;
            if (plan == null) return from;

            EdgeGeometry g = NearestRoad(plan, from.x, from.z, out double along, out double signedSide);
            if (g == null) return from;

            double side = signedSide >= 0.0 ? 1.0 : -1.0;       // left (+1) or right (-1) of the road's A->B direction
            double direction = random.NextDouble() < 0.5 ? 1.0 : -1.0;
            double remaining = random.Range(minStep, maxStep);
            double handed = side * direction;                    // which hand the pavement is on, relative to the way we walk

            for (int hop = 0; hop < MaxHops; hop++)
            {
                double room = direction > 0.0 ? g.Length - along : along;
                if (remaining <= room)
                {
                    along += direction * remaining;
                    break;
                }
                remaining -= room;

                RoadNode node = direction > 0.0 ? g.Edge.B : g.Edge.A;
                // Pick another road at this junction (or turn back at a dead end).
                int count = node.Edges.Count;
                RoadEdge next = null;
                if (count > 1)
                {
                    int pick = random.Below(count - 1);
                    for (int i = 0, seen = 0; i < count; i++)
                    {
                        if (node.Edges[i] == g.Edge) continue;
                        if (seen++ == pick) { next = node.Edges[i]; break; }
                    }
                }
                if (next == null)
                {
                    direction = -direction;
                    handed = -handed;
                    along = direction > 0.0 ? 0.0 : g.Length;
                    continue;
                }
                EdgeGeometry ng = plan.Geometry.Edges[next.Id];
                if (ng == null) break;
                g = ng;
                bool atStart = next.A == node;
                direction = atStart ? 1.0 : -1.0;
                along = atStart ? 0.0 : g.Length;
                side = handed * direction;
            }

            return PointOn(plan, g, along, side);
        }

        /// <summary>
        /// A deterministic spawn point for pedestrian <paramref name="index"/> near <paramref name="centre"/>, on a pavement.
        /// Deterministic on purpose: the same seed puts the same crowd in the same places on every machine.
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
