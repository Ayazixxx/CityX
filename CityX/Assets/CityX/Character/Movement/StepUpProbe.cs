using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A STEP UP: something knee-high in the way that a runner steps onto without breaking stride -- a doorstep, the sill of a floor-length shop
    /// window, a planter's rim, a low wall -- instead of stopping dead against it (owner, 2026-10-14, a screenshot of the body held at the foot of a
    /// shattered shop window: "im getting stuck when running into small obstacles like this when i should just be running right over this").
    ///
    /// The CharacterController steps 0.4 m on its own, in one frame. The city's shop windows have their sills 0.3 to 0.6 m over the pavement, and
    /// the vault only starts at 0.42 m and only for something that ENDS (a sill with a raised floor behind it does not). Up to
    /// <see cref="MaxRiseFraction"/> of standing height (0.65 m on the 2.6 m body: a quarter of it, the share Unreal's character takes by default)
    /// PlayerLocomotion lifts the body onto the top over a few frames instead, found here in stages like <see cref="VaultProbe"/>:
    ///  1. a near-vertical face in the way along the direction of travel, at shin height (just over <see cref="MinRise"/>; a kerb or a stair's riser
    ///     is the controller's own business), swept from the body's middle and from either side of it;
    ///  2. the face stands on the ground the body stands on: a flight's second riser stands on its first tread, so a staircase is never one step;
    ///  3. straight down just behind the face, a flat top no higher than the most a body steps;
    ///  4. a standing body's worth of room on the top just past the edge: a whole pane over a sill, or an opening lower than the body, refuses it
    ///     (a broken window too low to stand in is the vault's: a dive).
    /// Pure queries, no state.
    /// </summary>
    public static class StepUpProbe
    {
        public struct Step
        {
            /// <summary>The top's height (world Y).</summary>
            public float TopY;

            /// <summary>How far the top stands over the feet, metres.</summary>
            public float Rise;

            /// <summary>How far ahead of the capsule's side the face is, metres, along the direction of travel (0 when touching it).</summary>
            public float Gap;

            /// <summary>A point on the face, at shin height.</summary>
            public Vector3 FacePoint;

            /// <summary>The face's outward normal (toward the body), level.</summary>
            public Vector3 Normal;
        }

        /// <summary>
        /// Lower than this, metres, is left to the controller's own step: a kerb is 0.16 m, a stair's riser at most 0.235 (BuildingStairs.MaxRiser).
        /// The face is swept for just over it.
        /// </summary>
        public const float MinRise = 0.27f;

        /// <summary>The highest step, as a fraction of standing height: 0.65 m on the 2.6 m body.</summary>
        public const float MaxRiseFraction = 0.25f;

        /// <summary>The face's foot may be this far above or below the feet, metres: a sloping pavement, the controller's skin.</summary>
        public const float FootToleranceMetres = 0.12f;

        /// <summary>A face is near-vertical when its normal is at most this far off level (the y of the unit normal), as in VaultProbe.</summary>
        private const float MaxFaceNormalY = 0.3f;

        /// <summary>How squarely the body must be going at the face (dot of the travel and the face normal): within 60 degrees, as a vault.</summary>
        public const float MinFacingDot = 0.45f;

        /// <summary>A top is walkable when its normal's y is at least this (about 37 degrees).</summary>
        private const float MinTopNormalY = 0.8f;

        /// <summary>The top is looked for this far behind the face, metres: just behind it (a thin low wall), then deeper (a sill's frame, a rounded edge).</summary>
        private static readonly float[] TopInsets = { 0.05f, 0.16f };

        /// <summary>The face sweeps start from the body's middle and this far to either side of it, as fractions of the capsule's radius.</summary>
        private static readonly float[] SideFractions = { 0.0f, -0.7f, 0.7f };

        /// <summary>The room check's capsule is this much thinner than the body's, so a jamb the body only brushes does not refuse the step.</summary>
        private const float RoomRadiusFactor = 0.9f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];
        private static readonly Collider[] Overlaps = new Collider[16];

        /// <summary>The highest step a body of <paramref name="standingHeight"/> takes in its stride, metres.</summary>
        public static float MaxRise(float standingHeight)
        {
            return standingHeight * MaxRiseFraction;
        }

        /// <summary>
        /// The step ahead of a body standing at <paramref name="feet"/> going along <paramref name="direction"/>, looked for as far ahead of its side
        /// as the tallest step is high plus <paramref name="travel"/> (this frame's move), so the body can be lifted along a 45 degree line as it
        /// closes on it. <paramref name="self"/> (the body, its ragdoll and limbs) and people are not steps.
        /// </summary>
        public static bool TryFind(Vector3 feet, Vector3 direction, float standingHeight, float radius, float travel, Transform self, out Step step)
        {
            step = default;
            Vector3 forward = new Vector3(direction.x, 0.0f, direction.z);
            if (forward.sqrMagnitude < 1e-6f)
            {
                return false;
            }
            forward.Normalize();
            float maxRise = MaxRise(standingHeight);
            Vector3 across = Vector3.Cross(Vector3.up, forward);
            float reach = radius + maxRise + Mathf.Max(0.0f, travel);

            // 1. The nearest face in the way, at shin height.
            bool found = false;
            RaycastHit face = default;
            float gap = 0.0f;
            foreach (float side in SideFractions)
            {
                Vector3 origin = feet + Vector3.up * (MinRise + 0.02f) + across * (side * radius);
                if (!SolidRayHit(origin, forward, reach, self, out RaycastHit hit) || Mathf.Abs(hit.normal.y) > MaxFaceNormalY)
                {
                    continue;
                }
                Vector3 level = new Vector3(hit.normal.x, 0.0f, hit.normal.z).normalized;
                if (Vector3.Dot(level, -forward) < MinFacingDot)
                {
                    continue;   // a graze along a wall, not a step in the way
                }
                // The capsule's side, at this sweep's offset from its middle, is this far ahead of the middle.
                float side0 = radius * Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - side * side));
                float thisGap = Mathf.Max(0.0f, hit.distance - side0);
                if (!found || thisGap < gap)
                {
                    face = hit;
                    gap = thisGap;
                    found = true;
                }
            }
            if (!found)
            {
                return false;
            }
            Vector3 normal = new Vector3(face.normal.x, 0.0f, face.normal.z).normalized;

            // 2. The face stands on the body's own ground (not the next riser of a flight, not a ledge over a drop).
            Vector3 front = face.point + normal * 0.06f;
            front.y = feet.y + MinRise;
            if (!SolidRayHit(front, Vector3.down, MinRise + FootToleranceMetres + 0.05f, self, out RaycastHit foot) ||
                Mathf.Abs(foot.point.y - feet.y) > FootToleranceMetres)
            {
                return false;
            }

            // 3. A flat top just behind the face, inside the step's band. A ray that starts inside something taller passes through it and finds
            // nothing in the band, so a wall is not a step.
            float topY = float.NaN;
            foreach (float inset in TopInsets)
            {
                Vector3 above = face.point - normal * inset;
                above.y = feet.y + maxRise + 0.05f;
                if (!SolidRayHit(above, Vector3.down, maxRise + 0.05f - MinRise + 0.03f, self, out RaycastHit top) || top.normal.y < MinTopNormalY)
                {
                    continue;
                }
                float rise = top.point.y - feet.y;
                if (rise < MinRise || rise > maxRise)
                {
                    continue;
                }
                topY = float.IsNaN(topY) ? top.point.y : Mathf.Max(topY, top.point.y);
            }
            if (float.IsNaN(topY))
            {
                return false;
            }

            // 4. Room for the whole body on the top, its middle just past the edge.
            Vector3 landing = face.point - normal * (radius + 0.05f);
            float roomRadius = radius * RoomRadiusFactor;
            Vector3 low = new Vector3(landing.x, topY + roomRadius + 0.04f, landing.z);
            Vector3 high = new Vector3(landing.x, topY + standingHeight - roomRadius - 0.02f, landing.z);
            int count = Physics.OverlapCapsuleNonAlloc(low, high, roomRadius, Overlaps, CityX.World.GlassFracture.AllButShards, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!IsIgnored(Overlaps[i], self))
                {
                    return false;
                }
            }

            step = new Step
            {
                TopY = topY,
                Rise = topY - feet.y,
                Gap = gap,
                FacePoint = face.point,
                Normal = normal,
            };
            return true;
        }

        /// <summary>The nearest solid thing a ray meets that is not this body or a person.</summary>
        private static bool SolidRayHit(Vector3 origin, Vector3 direction, float distance, Transform self, out RaycastHit best)
        {
            best = default;
            // Not broken glass: bodies run through it (GlassFracture.LetThrough), so a falling piece is no step to climb.
            int count = Physics.RaycastNonAlloc(origin, direction, Hits, distance, CityX.World.GlassFracture.AllButShards, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = Hits[i];
                if (IsIgnored(h.collider, self) || h.distance >= nearest)
                {
                    continue;
                }
                nearest = h.distance;
                best = h;
                found = true;
            }
            return found;
        }

        private static bool IsIgnored(Collider c, Transform self)
        {
            return c == null || (self != null && c.transform.IsChildOf(self)) || c.GetComponentInParent<IDamageable>() != null;
        }
    }
}
