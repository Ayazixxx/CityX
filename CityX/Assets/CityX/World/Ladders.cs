using System.Collections.Generic;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// A ladder somebody can climb: where the plane of its rungs meets the floor, which way a climber's back is, and how high it goes.
    /// The top is the surface stepped onto (a street, a landing), behind the ladder.
    /// </summary>
    public struct Ladder
    {
        /// <summary>The middle of the ladder's foot, in the plane of its rungs.</summary>
        public Vector3 Foot;

        /// <summary>Level and one long: from the ladder toward whoever is on it.</summary>
        public Vector3 Normal;

        /// <summary>From the foot to the surface at the top.</summary>
        public float Height;

        /// <summary>Whether the top can be climbed out of and got onto (false under a shut lid).</summary>
        public bool TopOpen;

        public Vector3 Top => Foot + Vector3.up * Height;

        /// <summary>Where a body stands at the top: on the surface behind the ladder, clear of the hole it comes up through.</summary>
        public Vector3 StandAtTop => Top - Normal * (Ladders.OffWall + Ladders.StandBack);

        /// <summary>The middle of a rung, the lowest being rung 0.</summary>
        public Vector3 Rung(float index) => Foot + Vector3.up * (Ladders.FirstRung + index * Ladders.Pitch);

        /// <summary>How many rungs it has: the top one a step below the surface.</summary>
        public int RungCount => Mathf.Max(1, Mathf.FloorToInt((Height - Ladders.FirstRung - Ladders.TopRungBelow) / Ladders.Pitch) + 1);
    }

    /// <summary>
    /// Every ladder near a point: the ones the sewers stand under their manholes (a pure function of the city's plan, so nothing keeps a
    /// list of them) and the ones anything else has placed.
    /// </summary>
    public static class Ladders
    {
        /// <summary>
        /// Rung to rung, and the first rung over the floor. A real ladder's 30 cm, not 1.3 times it as the rest of the city is: this body is
        /// 2.6 m tall with the legs of a 1.8 m one, and it is the legs that climb.
        /// </summary>
        public const float Pitch = 0.3f;
        public const float FirstRung = 0.3f;

        /// <summary>The top rung is at least this far under the surface at the top.</summary>
        public const float TopRungBelow = 0.2f;

        public const float RungHalfWidth = 0.27f;

        /// <summary>How far the rungs stand off the wall behind them (where there is one): room for a foot and a hand.</summary>
        public const float OffWall = 0.2f;

        /// <summary>How far behind the wall's face a body stands at the top.</summary>
        public const float StandBack = 0.75f;

        private static readonly List<Ladder> Placed = new List<Ladder>();

        /// <summary>Stands a ladder that is not the sewers' own (a test's, a building's).</summary>
        public static void Place(Ladder ladder)
        {
            lock (Placed)
            {
                Placed.Add(ladder);
            }
        }

        /// <summary>Takes away every ladder that was placed (a new world; a test).</summary>
        public static void Clear()
        {
            lock (Placed)
            {
                Placed.Clear();
            }
        }

        /// <summary>The ladder nearest a point (anywhere along it, or where it is stood on at the top), within <paramref name="reach"/>.</summary>
        public static bool TryNearest(Vector3 point, float reach, out Ladder nearest)
        {
            nearest = default;
            float best = reach;
            bool found = false;
            lock (Placed)
            {
                foreach (Ladder ladder in Placed)
                {
                    Consider(ladder, point, ref best, ref nearest, ref found);
                }
            }
            CityWorld world = CurrentWorld.Instance;
            if (world != null)
            {
                SewerPlan sewers = SewerPlan.For(world.Plan);
                int cx = PlanWorld.ChunkOf(point.x), cy = PlanWorld.ChunkOf(point.z);
                foreach (int nodeId in world.Plan.JunctionsIn(cx, cy))
                {
                    SewerRoom room = sewers.Room(nodeId);
                    if (room?.Shaft != null)
                    {
                        Consider(Of(room.Shaft), point, ref best, ref nearest, ref found);
                    }
                }
            }
            return found;
        }

        /// <summary>A shaft's ladder: free-standing from the room's floor, its top through the neck to the street.</summary>
        public static Ladder Of(SewerShaft shaft)
        {
            var normal = new Vector3((float)shaft.Normal.X, 0.0f, (float)shaft.Normal.Y);
            Vec2 foot = shaft.Centre - shaft.Normal * (SewerPlan.NeckHalfWidth - OffWall);
            return new Ladder
            {
                Foot = new Vector3((float)foot.X, (float)shaft.Bottom, (float)foot.Y),
                Normal = normal,
                Height = (float)(shaft.Top - shaft.Bottom),
                TopOpen = shaft.Open,
            };
        }

        private static void Consider(Ladder ladder, Vector3 point, ref float best, ref Ladder nearest, ref bool found)
        {
            float along = Mathf.Clamp(point.y - ladder.Foot.y, 0.0f, ladder.Height);
            float distance = Vector3.Distance(point, ladder.Foot + Vector3.up * along);
            if (ladder.TopOpen)
            {
                distance = Mathf.Min(distance, Vector3.Distance(point, ladder.StandAtTop));
            }
            if (distance < best)
            {
                best = distance;
                nearest = ladder;
                found = true;
            }
        }
    }
}
