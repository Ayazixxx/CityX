using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Is there a real, climbable WALL in front of the player? Not "did one ray hit something".
    ///
    /// The old check fired a single ray at chest height and grabbed whatever it hit within a metre -- the wall at
    /// the top of a stair, a doorway's jamb, the frame beside a window all count as "a wall" to one ray. Ledge
    /// detection built in Unity is done in stages for exactly this reason (a horizontal check with several rays,
    /// then vertical checks, then the surface angle, rejecting candidates at every step so odd geometry does not
    /// pass). This is that shape, sized for a wall grab:
    ///
    ///   * a 3 x 3 grid of rays -- three across, at knee, chest and head height -- and ALL nine must hit, so the
    ///     surface is at least ~0.7m wide and reaches from the knee to above the head. A doorway, a window
    ///     opening or a lamp post has a gap in that grid; a stair riser or a low fence never reaches the head;
    ///   * they must all be on ONE flat face: hit distances within 0.3m of each other and normals within 20
    ///     degrees, so a corner, a jamb (a thin pier beside a hole) or a pile of crates does not count;
    ///   * the face must be near-vertical, and the player must be facing it (within 45 degrees);
    ///   * and what is hit must be a wall: not a person, not a door, not glass shards flying about.
    ///
    /// Only asked while the player is holding the grab key (see <see cref="GrabIntent"/>), so its nine raycasts
    /// cost nothing in ordinary play.
    /// </summary>
    public static class WallProbe
    {
        public const float Reach = 1.0f;
        public const float HalfWidth = 0.35f;

        /// <summary>How high up the body the rays go, as fractions of standing height: knee, chest, head.</summary>
        private static readonly float[] HeightFractions = { 0.2f, 0.5f, 0.8f };

        public const float MaxDepthSpreadMetres = 0.3f;
        public const float MaxNormalSpreadDegrees = 20.0f;
        public const float MaxFacingDegrees = 45.0f;

        /// <summary>A face steeper than this (dot with up) is not a wall.</summary>
        public const float WallMaxSlopeDot = 0.3f;

        public readonly struct Result
        {
            public readonly Vector3 Point;
            public readonly Vector3 Normal;
            public readonly float Distance;

            public Result(Vector3 point, Vector3 normal, float distance)
            {
                Point = point;
                Normal = normal;
                Distance = distance;
            }
        }

        /// <param name="feet">The player's position (at their feet).</param>
        /// <param name="forward">The way they are facing.</param>
        public static bool TryFind(Vector3 feet, Vector3 forward, float standingHeight, out Result result)
        {
            result = default;
            Vector3 flat = new Vector3(forward.x, 0.0f, forward.z);
            if (flat.sqrMagnitude < 1e-6f)
            {
                return false;
            }
            flat.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, flat);

            Vector3 normalSum = Vector3.zero;
            Vector3 pointSum = Vector3.zero;
            float nearest = float.MaxValue;
            float farthest = float.MinValue;
            Vector3 firstNormal = Vector3.zero;
            int hits = 0;

            foreach (float fraction in HeightFractions)
            {
                for (int side = -1; side <= 1; side++)
                {
                    Vector3 origin = feet + Vector3.up * (standingHeight * fraction) + right * (side * HalfWidth);
                    if (!Physics.Raycast(origin, flat, out RaycastHit hit, Reach, ~0, QueryTriggerInteraction.Ignore))
                    {
                        return false;   // a gap in the grid: a doorway, a window, the end of the wall
                    }
                    if (!IsWall(hit))
                    {
                        return false;
                    }

                    if (hits == 0)
                    {
                        firstNormal = hit.normal;
                    }
                    else if (Vector3.Angle(firstNormal, hit.normal) > MaxNormalSpreadDegrees)
                    {
                        return false;   // not one flat face
                    }

                    nearest = Mathf.Min(nearest, hit.distance);
                    farthest = Mathf.Max(farthest, hit.distance);
                    normalSum += hit.normal;
                    pointSum += hit.point;
                    hits++;
                }
            }

            if (farthest - nearest > MaxDepthSpreadMetres)
            {
                return false;   // a jamb, a corner, steps: the surface is not all at one depth
            }

            Vector3 normal = normalSum.normalized;
            Vector3 normalFlat = new Vector3(normal.x, 0.0f, normal.z);
            if (normalFlat.sqrMagnitude < 1e-6f || Vector3.Angle(flat, -normalFlat) > MaxFacingDegrees)
            {
                return false;   // not facing it
            }

            result = new Result(pointSum / hits, normal, (nearest + farthest) * 0.5f);
            return true;
        }

        /// <summary>Whether a ray hit is a wall: solid world, near-vertical -- not a body, a door or debris.</summary>
        private static bool IsWall(in RaycastHit hit)
        {
            Collider collider = hit.collider;
            if (collider == null || IgnoredByShots.Covers(collider))
            {
                return false;
            }
            if (collider.GetComponentInParent<IDamageable>() != null)
            {
                return false;   // a person is not a wall
            }
            if (collider.GetComponentInParent<IOpenableDoor>() != null)
            {
                return false;   // and neither is a door
            }
            return Mathf.Abs(hit.normal.y) <= WallMaxSlopeDot;
        }
    }
}
