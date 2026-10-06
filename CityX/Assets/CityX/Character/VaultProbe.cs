using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Finds a VAULT: a waist-to-chest-high obstacle with a flat top, thin enough to go over in one move, with clear ground to land on past it.
    ///
    /// The same staged-validation idea as <see cref="LedgeProbe"/> (a wall face, a flat top, room to be), but the body ends up on the FAR side:
    ///  1. a ray forward (at several heights, in a small fan) must hit a near-vertical face the player is facing (a person fails, a glancing graze fails);
    ///  2. the top, probed straight down just behind the face, must be flat and inside the vaultable band (below it is a step, above it a climb);
    ///  3. walking the top away from the player, it must end within <see cref="MaxThicknessMetres"/> (a building is not vaulted);
    ///  4. just past the far edge there must be ground within a short drop, with a standing capsule's worth of room.
    /// Pure queries, no state.
    /// </summary>
    public static class VaultProbe
    {
        public struct Vault
        {
            /// <summary>The obstacle's near edge: on the face, at the top's height.</summary>
            public Vector3 NearEdge;

            /// <summary>The obstacle's far edge, at the top's height.</summary>
            public Vector3 FarEdge;

            /// <summary>Where the feet land.</summary>
            public Vector3 Land;

            /// <summary>The top's height (world Y).</summary>
            public float Height;

            /// <summary>The obstacle face's outward normal (toward the player), flattened level.</summary>
            public Vector3 Normal;
        }

        /// <summary>The top must be between these fractions of standing height above the feet: lower is a step, higher is a climb.</summary>
        public const float BandLowFraction = 0.22f;

        public const float BandHighFraction = 0.8f;

        /// <summary>The obstacle may be at most this thick, in metres.</summary>
        public const float MaxThicknessMetres = 1.6f;

        /// <summary>How far ahead of the capsule the face is looked for, in metres.</summary>
        public const float ReachMetres = 1.1f;

        /// <summary>The ground past the far edge may be this far below the top, in metres, at most.</summary>
        public const float MaxDropMetres = 2.6f;

        /// <summary>The face-finding rays fan out by these angles from the running direction, in degrees.</summary>
        private static readonly float[] FanDegrees = { 0.0f, -25.0f, 25.0f };

        /// <summary>The face-finding rays go at these fractions of standing height (knee to chest).</summary>
        private static readonly float[] FaceHeightFractions = { 0.26f, 0.4f, 0.55f, 0.7f };

        /// <summary>How squarely the player must be facing the obstacle (dot of the facing and the face normal): within 60 degrees.</summary>
        public const float MinFacingDot = 0.5f;

        private const float FlatnessMetres = 0.1f;
        private const float StepMetres = 0.15f;

        public static bool TryFind(Vector3 feet, Vector3 forward, float standingHeight, float radius, Transform self, out Vault vault)
        {
            vault = default;
            forward.y = 0.0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            forward.Normalize();
            float bandLow = feet.y + standingHeight * BandLowFraction;
            float bandHigh = feet.y + standingHeight * BandHighFraction;

            // 1. The face, in a small fan (so a corner or an angled approach still finds it), facing the player.
            // Rays go at several heights inside the vaultable band, because an obstacle only has a face at heights below its top.
            RaycastHit mid = default;
            bool seen = false;
            float nearest = float.MaxValue;
            foreach (float fraction in FaceHeightFractions)
            foreach (float angle in FanDegrees)
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                if (LedgeProbe.SolidHit(feet + Vector3.up * (standingHeight * fraction), direction, radius + ReachMetres, self, out RaycastHit candidate) && Mathf.Abs(candidate.normal.y) <= 0.3f
                    && candidate.distance < nearest)
                {
                    Vector3 flatNormal = new Vector3(candidate.normal.x, 0.0f, candidate.normal.z).normalized;
                    if (Vector3.Dot(-flatNormal, forward) >= MinFacingDot)
                    {
                        mid = candidate;
                        nearest = candidate.distance;
                        seen = true;
                    }
                }
            }
            if (!seen)
            {
                return false;
            }
            Vector3 faceNormal = new Vector3(mid.normal.x, 0.0f, mid.normal.z).normalized;
            Vector3 into = -faceNormal;
            Vector3 across = Vector3.Cross(Vector3.up, faceNormal);

            // 2 + 3. The top, walked away from the player in steps: flat and agreeing, until it ends.
            float height = float.NaN;
            float lastTopDepth = -1.0f;
            bool ended = false;
            float castHeight = bandHigh + 0.4f;
            for (float depth = 0.1f; depth <= MaxThicknessMetres + StepMetres; depth += StepMetres)
            {
                bool top = true;
                float sample = 0.0f;
                for (int s = -1; s <= 1 && top; s += 2)
                {
                    Vector3 from = mid.point + into * depth + across * (s * 0.25f);
                    from.y = castHeight;
                    top = LedgeProbe.SolidHit(from, Vector3.down, castHeight - bandLow + 0.2f, self, out RaycastHit h)
                        && h.normal.y >= 0.85f && h.point.y >= bandLow - 0.2f && h.point.y <= bandHigh + 0.2f
                        && (s == -1 || Mathf.Abs(h.point.y - sample) <= FlatnessMetres);
                    if (top)
                    {
                        sample = Mathf.Max(sample, h.point.y);
                    }
                }
                if (!top)
                {
                    if (lastTopDepth < 0.0f)
                    {
                        return false;   // nothing flat right behind the face: a slope, a rail, a gap
                    }
                    ended = true;
                    break;
                }
                if (float.IsNaN(height))
                {
                    height = sample;
                }
                else if (Mathf.Abs(sample - height) > FlatnessMetres)
                {
                    return false;       // the top steps up or down part-way
                }
                lastTopDepth = depth;
            }
            if (!ended || height < bandLow || height > bandHigh)
            {
                return false;
            }

            // 4. Ground to land on past the far edge, within a short drop, with room to stand.
            Vector3 farEdge = mid.point + into * lastTopDepth;
            farEdge.y = height;
            Vector3 land = farEdge + into * (radius + 0.35f);
            if (!LedgeProbe.SolidHit(new Vector3(land.x, height + 0.5f, land.z), Vector3.down, 0.5f + MaxDropMetres, self, out RaycastHit ground)
                || ground.normal.y < 0.7f)
            {
                return false;
            }
            land.y = ground.point.y;
            if (!LedgeProbe.HasRoom(land, standingHeight, radius, self))
            {
                return false;
            }
            // Over the top and past the far edge must be open too (no rail or sign just beyond).
            if (!LedgeProbe.HasRoom(farEdge + into * 0.15f + Vector3.up * 0.05f, standingHeight * 0.9f, radius * 0.8f, self))
            {
                return false;
            }

            vault = new Vault
            {
                NearEdge = new Vector3(mid.point.x, height, mid.point.z),
                FarEdge = farEdge,
                Land = land,
                Height = height,
                Normal = faceNormal,
            };
            return true;
        }
    }
}
