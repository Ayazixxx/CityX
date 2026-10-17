using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What juts from a wall alongside a climber -- a base course over the shop fronts, string courses, sills, a cornice (World.FacadeOrnament) -- so the
    /// climb can lean out round it and settle back onto the wall once past, the way a climber's hips swing out round a ledge. Before, the body rose
    /// straight into the underside of the first course over the street storey, stopped rising, and the stuck guard let go: every facade with a base
    /// course (most of the city, since 2026-10-09) could not be climbed.
    ///
    /// The scene part is one box sweep per frame (<see cref="Measure"/>); the decision is pure (<see cref="LeanFor"/>).
    /// </summary>
    public static class ClimbJut
    {
        /// <summary>The deepest a jut may be for the body to lean round it (a deep cornice is 0.85 m); deeper (a balcony) the climb stays on the wall.</summary>
        public const float MaxLean = 0.9f;

        /// <summary>How far past the head (climbing up) or the feet (climbing down) a jut is seen coming, so the body is out before it reaches it.</summary>
        public const float LookAhead = 0.5f;

        /// <summary>How wide a column of wall the body takes up.</summary>
        public const float BodyWidth = 0.44f;

        /// <summary>A jut smaller than this is ignored (the wall's own relief, a frame): the capsule's standoff already clears it.</summary>
        public const float Ignore = 0.03f;

        private static readonly RaycastHit[] Hits = new RaycastHit[24];

        /// <summary>How far out from the wall the body should hang for a jut of this depth: all of it, or none when it is too deep to lean round.</summary>
        public static float LeanFor(float jut)
        {
            return jut > MaxLean || jut < Ignore ? 0.0f : jut;
        }

        /// <summary>
        /// How far the wall alongside the body juts out past the plane the climb holds (<paramref name="planePoint"/>, <paramref name="normal"/> pointing
        /// out of the wall): the nearest solid face in a thin column as wide as the body, from <paramref name="below"/> metres under the feet (negative:
        /// above them) to <paramref name="above"/> metres over the head, found by sweeping the column in toward the wall from outside. 0 when nothing juts.
        /// </summary>
        public static float Measure(Vector3 feet, float height, float below, float above, Vector3 planePoint, Vector3 normal, Transform self)
        {
            Vector3 n = new Vector3(normal.x, 0.0f, normal.z);
            if (n.sqrMagnitude < 1e-6f)
            {
                return 0.0f;
            }
            n.Normalize();
            float bottom = -below;
            float top = height + above;
            if (top - bottom < 0.1f)
            {
                return 0.0f;
            }
            Vector3 centre = feet + Vector3.up * ((bottom + top) * 0.5f);
            centre -= n * Vector3.Dot(centre - planePoint, n);   // onto the wall plane
            float reach = MaxLean + 0.6f;
            Vector3 origin = centre + n * reach;
            var halfExtents = new Vector3(BodyWidth * 0.5f, (top - bottom) * 0.5f, 0.02f);
            int count = Physics.BoxCastNonAlloc(origin, halfExtents, -n, Hits, Quaternion.LookRotation(-n, Vector3.up), reach + 0.2f,
                ~0, QueryTriggerInteraction.Ignore);
            float jut = 0.0f;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                Collider c = hit.collider;
                // A hit at distance 0 overlapped the box where it started (a neighbour's corner, a lamp): no point to measure.
                if (c == null || hit.distance <= 0.0f || (self != null && c.transform.IsChildOf(self)) || c.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                jut = Mathf.Max(jut, Vector3.Dot(hit.point - planePoint, n));
            }
            return jut;
        }
    }
}
