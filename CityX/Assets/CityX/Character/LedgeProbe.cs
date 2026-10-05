using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Finds a REAL ledge on the wall being climbed: a flat, wide, deep top surface that belongs to this wall, with room above it to stand.
    ///
    /// WHY THIS EXISTS. Climbing used to decide "I have reached the top" from ONE ray that stopped hitting the wall. Any gap in the wall at that
    /// height (a window opening, a recessed floor, a balcony, a stray hole) read as the top, so the character mantled where there was no edge: it was
    /// moved up and 0.6 m INTO the wall, and stuck in it. The standard fix (every Unity ledge/parkour write-up, and Assassin's Creed's own annotated
    /// ledges) is to validate in stages and reject at every one:
    ///
    ///  1. find the wall face by a ray at hand height (not a person, near-vertical);
    ///  2. probe the TOP SURFACE in a grid behind the face (two depths x three across): every probe must find a walkable top (normal up) in the
    ///     reach band, and all must agree on its height: a flat, wide, deep shelf, not a pole, a spike, a window sill's lip or a slope;
    ///  3. the face just under the ledge must be the SAME face (a shelf far back from the wall is not this wall's top);
    ///  4. a capsule at the standing spot, up on the ledge, must be free: no ceiling, no wall, no object, or there is no room to get up.
    ///
    /// Pure queries, no state, so it can be tested with plain colliders.
    /// </summary>
    public static class LedgeProbe
    {
        public struct Ledge
        {
            /// <summary>The wall's top edge: on the face, at the ledge's height.</summary>
            public Vector3 Lip;

            /// <summary>Where the feet end up standing, on the ledge.</summary>
            public Vector3 Stand;

            /// <summary>The ledge's height (world Y of its top surface).</summary>
            public float Height;

            /// <summary>The wall face's outward normal, flattened level.</summary>
            public Vector3 Normal;
        }

        /// <summary>The top surface is looked for between these fractions of standing height above the feet.</summary>
        public const float BandLowFraction = 0.5f;

        public const float BandHighFraction = 1.35f;

        /// <summary>How deep into the wall (behind the face) the top surface must be solid, in metres: the two depths probed.</summary>
        public static readonly float[] Depths = { 0.3f, 0.65f };

        /// <summary>The top probes at one depth are spread this far either side of centre, in metres.</summary>
        public const float HalfWidth = 0.35f;

        /// <summary>All probes must agree on the ledge's height to within this, in metres.</summary>
        public const float FlatnessMetres = 0.12f;

        /// <summary>How far into the ledge the feet stand, past the face, in metres (plus the capsule radius).</summary>
        public const float StandInMetres = 0.3f;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];
        private static readonly Collider[] Overlaps = new Collider[16];

        public static bool TryFind(Vector3 feet, Vector3 outward, float standingHeight, float radius, Transform self, out Ledge ledge)
        {
            ledge = default;
            outward.y = 0.0f;
            if (outward.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            outward.Normalize();
            Vector3 tangent = Vector3.Cross(Vector3.up, outward);

            // 1. The wall face at hand height.
            Vector3 handHeight = feet + Vector3.up * (standingHeight * 0.7f);
            if (!SolidHit(handHeight + outward * 0.5f, -outward, 1.5f, self, out RaycastHit face) || Mathf.Abs(face.normal.y) > 0.3f)
            {
                return false;
            }
            Vector3 faceNormal = new Vector3(face.normal.x, 0.0f, face.normal.z).normalized;
            Vector3 facePoint = face.point;

            // 2. The top surface, in a grid behind the face.
            float yLow = feet.y + standingHeight * BandLowFraction;
            float yHigh = feet.y + standingHeight * BandHighFraction;
            float sum = 0.0f, min = float.MaxValue, max = float.MinValue;
            int count = 0;
            foreach (float depth in Depths)
            {
                for (int s = -1; s <= 1; s++)
                {
                    Vector3 from = facePoint - faceNormal * depth + tangent * (s * HalfWidth);
                    from.y = yHigh;
                    if (!SolidHit(from, Vector3.down, yHigh - yLow, self, out RaycastHit top) || top.normal.y < 0.85f)
                    {
                        return false;
                    }
                    sum += top.point.y;
                    min = Mathf.Min(min, top.point.y);
                    max = Mathf.Max(max, top.point.y);
                    count++;
                }
            }
            if (max - min > FlatnessMetres)
            {
                return false;
            }
            float height = sum / count;

            // 3. The face just under the ledge is the same face (within a quarter metre), not a shelf set far back from this wall.
            Vector3 under = new Vector3(facePoint.x, height - 0.3f, facePoint.z) + faceNormal * 0.5f;
            if (!SolidHit(under, -faceNormal, 1.0f, self, out RaycastHit underFace)
                || Vector3.Angle(underFace.normal, face.normal) > 25.0f
                || Mathf.Abs(Vector3.Dot(underFace.point - facePoint, faceNormal)) > 0.25f)
            {
                return false;
            }

            // 4. Room to stand on it: a capsule at the standing spot, clear of everything but this body.
            Vector3 stand = facePoint - faceNormal * (radius + StandInMetres);
            stand.y = height;
            if (!HasRoom(stand, standingHeight, radius, self))
            {
                return false;
            }

            ledge = new Ledge
            {
                Lip = new Vector3(facePoint.x, height, facePoint.z),
                Stand = stand,
                Height = height,
                Normal = faceNormal,
            };
            return true;
        }

        /// <summary>True when a standing capsule at <paramref name="feet"/> overlaps nothing solid except <paramref name="self"/>.</summary>
        public static bool HasRoom(Vector3 feet, float standingHeight, float radius, Transform self)
        {
            float r = radius * 0.9f;
            Vector3 bottom = feet + Vector3.up * (radius + 0.06f);
            Vector3 top = feet + Vector3.up * (standingHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, r, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = Overlaps[i];
                if (c == null || (self != null && c.transform.IsChildOf(self)) || c.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        /// <summary>The nearest solid hit that is not this body or a person.</summary>
        private static bool SolidHit(Vector3 origin, Vector3 direction, float distance, Transform self, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider c = Hits[i].collider;
                if (c == null || (self != null && c.transform.IsChildOf(self)) || c.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                if (Hits[i].distance < nearest)
                {
                    nearest = Hits[i].distance;
                    best = Hits[i];
                    found = true;
                }
            }
            return found;
        }
    }
}
