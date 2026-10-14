using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Finds a VAULT: a waist-to-chest-high obstacle with a flat top, thin enough to go over in one move, with clear ground to land on past it.
    ///
    /// The same staged-validation idea as <see cref="LedgeProbe"/> (a wall face, a flat top, room to be), but the body ends up on the FAR side:
    ///  1. sweeps forward (at several heights, in a fan) find the near-vertical faces the player is facing (a person fails, a glancing graze fails);
    ///     each is tried in turn, nearest first, so a plinth or a kerb in front of a window does not hide the wall under the sill;
    ///  2. the top, probed straight down just behind the face, must be flat and inside the vaultable band (below it is a step, above it a climb);
    ///  3. walking the top away from the player, it must end within <see cref="MaxThicknessMetres"/> (a building is not vaulted); anything lower
    ///     than the top past it (a street, a room's floor) is beyond the far edge;
    ///  4. an OPENING (a window: a sill between two jambs) is gone through its middle, wherever along the sill the player met it;
    ///  5. just past the far edge there must be ground within a short drop, with a standing capsule's worth of room, and the opening over the top
    ///     must be clear (no glass) and tall enough to pass, if only diving.
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

            /// <summary>
            /// The free height over the top, metres: a whole standing body's worth over a fence; over a WINDOW SILL, the opening up to the lintel
            /// (a shattered window is vaulted through with a dive when it is lower than the body: PlayerVaulting).
            /// </summary>
            public float Headroom;
        }

        /// <summary>
        /// Which stage of <see cref="TryFind(Vector3, Vector3, float, float, Transform, out Vault, out Refusal)"/> turned a vault down (for tests and
        /// the dev console). In the order the stages run: when several faces were tried, the furthest any of them got is the one reported.
        /// </summary>
        public enum Refusal
        {
            None,
            NoDirection,
            /// <summary>Nothing the player is facing within reach at vault heights.</summary>
            NoFace,
            /// <summary>No flat top inside the band that ends within the thickness limit.</summary>
            NoTop,
            /// <summary>No ground within a short drop past the far edge.</summary>
            NoLanding,
            /// <summary>No room to stand where the feet would land.</summary>
            NoRoomToLand,
            /// <summary>Something solid (a whole pane of glass) in the opening over the top.</summary>
            OpeningBlocked,
            /// <summary>Less than the least passable height over the top.</summary>
            TooLow,
            /// <summary>Something just past the far edge, in the height the body passes through.</summary>
            NoRoomPastTop,
        }

        /// <summary>The least free height over the top that can be gone through, metres: a body diving flat is about this thick with room to spare.</summary>
        public const float MinHeadroomMetres = 0.9f;

        /// <summary>
        /// The top must be between these fractions of standing height above the feet: lower is a step, higher is a climb. The low end is just over
        /// the controller's 0.4 m step (was 0.22, 0.57 m, which left a 0.4 to 0.57 m band -- a plinth-high window sill -- that could be neither
        /// stepped over nor vaulted).
        /// </summary>
        public const float BandLowFraction = 0.16f;

        public const float BandHighFraction = 0.8f;

        /// <summary>The obstacle may be at most this thick, in metres.</summary>
        public const float MaxThicknessMetres = 1.6f;

        /// <summary>How far ahead of the capsule the face is looked for, in metres.</summary>
        public const float ReachMetres = 1.1f;

        /// <summary>The ground past the far edge may be this far below the top, in metres, at most.</summary>
        public const float MaxDropMetres = 2.6f;

        /// <summary>
        /// Half the width of the way over the top that must be clear of anything solid, metres: the opening check's box. A body diving through a
        /// window is narrower than its shoulders are wide when it stands.
        /// </summary>
        public const float PassageHalfWidthMetres = 0.25f;

        /// <summary>
        /// The middle of the way through an opening is kept at least this far from either jamb, metres: the passage's half width, the frame round
        /// the glass (5 cm) and a little to spare. A window narrower than twice this is gone through its middle and judged by the opening check.
        /// </summary>
        public const float JambClearanceMetres = 0.33f;

        /// <summary>How far along the top, either way, a jamb is looked for, metres (a window wider than about twice this has no jamb to keep clear of).</summary>
        private const float SpanSearchMetres = 1.6f;

        /// <summary>The opening check starts this far in front of the near edge, metres, so a pane standing in the plane of the face found is inside it.</summary>
        private const float OpeningLeadMetres = 0.06f;

        /// <summary>The face-finding sweeps fan out by these angles from the running direction, in degrees.</summary>
        private static readonly float[] FanDegrees = { 0.0f, -30.0f, 30.0f, -55.0f, 55.0f };

        /// <summary>The radius of the sphere cast that finds the face, in metres.</summary>
        private const float SphereRadius = 0.22f;

        /// <summary>A face is near-vertical when its normal is at most this far off level (the y of the unit normal).</summary>
        private const float MaxFaceNormalY = 0.3f;

        /// <summary>
        /// When a sweep catches an EDGE (the top of a sill under the sphere's middle, or the underside of the stone ledge under a window sill over it),
        /// the face below is looked for with a ray this far under the edge, metres.
        /// </summary>
        private const float EdgeProbeDrop = 0.05f;

        /// <summary>The most faces that are tried, nearest first.</summary>
        private const int MaxFaces = 8;

        private static readonly RaycastHit[] SphereHits = new RaycastHit[8];

        /// <summary>The nearest solid thing a sphere sweeps into that is not this body or a person (a sphere that starts inside something is skipped).</summary>
        private static bool SolidSphereHit(Vector3 origin, float sphereRadius, Vector3 direction, float distance, Transform self, out RaycastHit best)
        {
            best = default;
            int count = Physics.SphereCastNonAlloc(origin, sphereRadius, direction, SphereHits, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = SphereHits[i];
                Collider c = h.collider;
                if (c == null || h.point == Vector3.zero || (self != null && c.transform.IsChildOf(self)) || c.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// The face-finding sweeps go at these fractions of standing height, from just over the band's low end (a 0.45 m sill: the next sweep up
        /// passes over it into the window) to the chest.
        /// </summary>
        private static readonly float[] FaceHeightFractions = { 0.17f, 0.26f, 0.4f, 0.55f, 0.7f };

        /// <summary>How squarely the player must be facing the obstacle (dot of the facing and the face normal): within 60 degrees.</summary>
        public const float MinFacingDot = 0.45f;

        private const float FlatnessMetres = 0.1f;
        private const float StepMetres = 0.15f;

        private static readonly float[] SidewaysMetres = { 0.0f, -0.25f, 0.25f, -0.45f, 0.45f };

        /// <summary>One face a sweep found: a point on it, its level outward normal, and how far ahead of the feet it is (level).</summary>
        private struct Face
        {
            public Vector3 Point;
            public Vector3 Normal;
            public float Reach;
        }

        private static readonly Face[] Faces = new Face[MaxFaces];

        /// <summary>
        /// Walks a flat top away from <paramref name="origin"/> (on the face) in steps: true when it is flat, in the band, and ENDS within the allowed
        /// thickness. A flat surface lower than the top past it is beyond the far edge: past a window sill that is the room's floor, which in a
        /// building standing higher than the street is inside the band too (it read as the top stepping down part-way, and the window was refused).
        /// A top that steps UP part-way is not one obstacle.
        /// </summary>
        private static bool TryWalkTop(Vector3 origin, Vector3 into, Vector3 across, float bandLow, float bandHigh, Transform self, out float height, out float lastTopDepth)
        {
            height = float.NaN;
            lastTopDepth = -1.0f;
            bool ended = false;
            float castHeight = bandHigh + 0.4f;
            for (float depth = 0.1f; depth <= MaxThicknessMetres + StepMetres; depth += StepMetres)
            {
                bool top = true;
                float sample = 0.0f;
                for (int s = -1; s <= 1 && top; s += 2)
                {
                    Vector3 from = origin + into * depth + across * (s * 0.12f);
                    from.y = castHeight;
                    top = LedgeProbe.SolidHit(from, Vector3.down, castHeight - bandLow + 0.2f, self, out RaycastHit h)
                        && h.normal.y >= 0.85f && h.point.y >= bandLow - 0.2f && h.point.y <= bandHigh + 0.2f
                        && (s == -1 || Mathf.Abs(h.point.y - sample) <= FlatnessMetres);
                    if (top)
                    {
                        sample = Mathf.Max(sample, h.point.y);
                    }
                }
                if (top && !float.IsNaN(height) && sample < height - FlatnessMetres)
                {
                    top = false;        // lower than the top: past the far edge
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
                else if (sample > height + FlatnessMetres)
                {
                    return false;       // the top steps up part-way
                }
                lastTopDepth = depth;
            }
            return ended && height >= bandLow && height <= bandHigh;
        }

        /// <summary>
        /// The face one sweep finds, if it is near-vertical and the player is facing it. A sphere that catches an EDGE gets the edge's normal,
        /// half up or half down, not the face's: the top of a sill under the sphere's middle, or the underside of the stone ledge a window sill
        /// sticks out over the wall, nearer than the wall itself. Then a ray just under the edge finds the face below it.
        /// </summary>
        private static bool FacingFace(Vector3 origin, Vector3 direction, float distance, Vector3 forward, float floorY, Transform self, out RaycastHit face)
        {
            if (!SolidSphereHit(origin, SphereRadius, direction, distance, self, out face))
            {
                return false;
            }
            if (Mathf.Abs(face.normal.y) > MaxFaceNormalY)
            {
                float y = face.normal.y > 0.0f ? Mathf.Min(origin.y, face.point.y - EdgeProbeDrop) : origin.y;
                if (y < floorY
                    || !LedgeProbe.SolidHit(new Vector3(origin.x, y, origin.z), direction, distance + SphereRadius, self, out face)
                    || Mathf.Abs(face.normal.y) > MaxFaceNormalY)
                {
                    return false;
                }
            }
            Vector3 flatNormal = new Vector3(face.normal.x, 0.0f, face.normal.z).normalized;
            return Vector3.Dot(-flatNormal, forward) >= MinFacingDot;
        }

        /// <summary>Adds a face to the ones to try, unless it is one already there (the same plane, close by), in which case the nearer sighting is kept.</summary>
        private static int AddFace(int count, Vector3 feet, in RaycastHit hit)
        {
            Vector3 normal = new Vector3(hit.normal.x, 0.0f, hit.normal.z).normalized;
            Vector3 level = hit.point - feet;
            level.y = 0.0f;
            var face = new Face { Point = hit.point, Normal = normal, Reach = level.magnitude };
            Vector3 across = Vector3.Cross(Vector3.up, normal);
            for (int i = 0; i < count; i++)
            {
                Vector3 apart = hit.point - Faces[i].Point;
                if (Vector3.Dot(Faces[i].Normal, normal) > 0.97f && Mathf.Abs(Vector3.Dot(apart, normal)) < 0.06f && Mathf.Abs(Vector3.Dot(apart, across)) < 0.5f)
                {
                    if (face.Reach < Faces[i].Reach)
                    {
                        Faces[i] = face;
                    }
                    return count;
                }
            }
            if (count < MaxFaces)
            {
                Faces[count] = face;
                return count + 1;
            }
            int furthest = 0;
            for (int i = 1; i < count; i++)
            {
                if (Faces[i].Reach > Faces[furthest].Reach) furthest = i;
            }
            if (face.Reach < Faces[furthest].Reach)
            {
                Faces[furthest] = face;
            }
            return count;
        }

        public static bool TryFind(Vector3 feet, Vector3 forward, float standingHeight, float radius, Transform self, out Vault vault)
        {
            return TryFind(feet, forward, standingHeight, radius, self, out vault, out _);
        }

        /// <summary>As <see cref="TryFind(Vector3, Vector3, float, float, Transform, out Vault)"/>, also saying which stage refused (<see cref="Refusal.None"/> when found).</summary>
        public static bool TryFind(Vector3 feet, Vector3 forward, float standingHeight, float radius, Transform self, out Vault vault, out Refusal refusal)
        {
            vault = default;
            refusal = Refusal.None;
            forward.y = 0.0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                refusal = Refusal.NoDirection;
                return false;
            }
            forward.Normalize();
            float bandLow = feet.y + standingHeight * BandLowFraction;
            float bandHigh = feet.y + standingHeight * BandHighFraction;

            // 1. The faces the body would run into. SPHERE casts (the width of a body), not rays, at several heights inside the vaultable band (an obstacle only
            // has a face below its top), in a wide fan: a thin ray only found the obstacle when it was aimed right at it ("it only vaults when I look at the edge").
            // EVERY face found is a candidate, nearest first: the nearest alone was a plinth or kerb in front of a window as often as the wall under its sill.
            int count = 0;
            foreach (float fraction in FaceHeightFractions)
            foreach (float angle in FanDegrees)
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
                Vector3 origin = feet + Vector3.up * (standingHeight * fraction);
                if (FacingFace(origin, direction, radius + ReachMetres, forward, feet.y + 0.1f, self, out RaycastHit hit))
                {
                    count = AddFace(count, feet, hit);
                }
            }
            if (count == 0)
            {
                refusal = Refusal.NoFace;
                return false;
            }
            for (int i = 1; i < count; i++)
            {
                Face f = Faces[i];
                int j = i - 1;
                for (; j >= 0 && Faces[j].Reach > f.Reach; j--)
                {
                    Faces[j + 1] = Faces[j];
                }
                Faces[j + 1] = f;
            }

            refusal = Refusal.NoTop;
            for (int i = 0; i < count; i++)
            {
                Refusal why = TryOver(Faces[i], standingHeight, radius, bandLow, bandHigh, self, out vault);
                if (why == Refusal.None)
                {
                    refusal = Refusal.None;
                    return true;
                }
                if (why > refusal)
                {
                    refusal = why;
                }
            }
            vault = default;
            return false;
        }

        /// <summary>Stages 2 to 5 for one face: the top, the way through an opening, the landing and the air over the top.</summary>
        private static Refusal TryOver(in Face face, float standingHeight, float radius, float bandLow, float bandHigh, Transform self, out Vault vault)
        {
            vault = default;
            Vector3 faceNormal = face.Normal;
            Vector3 into = -faceNormal;
            Vector3 across = Vector3.Cross(Vector3.up, faceNormal);

            // 2 + 3. The top, walked away from the player in steps: flat and agreeing, until it ends. Tried at the hit point and then to either side of it, since
            // a sphere that clips a narrow obstacle's corner hits the edge of it, not the middle.
            float height = float.NaN;
            float lastTopDepth = -1.0f;
            Vector3 origin = face.Point;
            bool walked = false;
            foreach (float sideways in SidewaysMetres)
            {
                origin = face.Point + across * sideways;
                if (TryWalkTop(origin, into, across, bandLow, bandHigh, self, out height, out lastTopDepth))
                {
                    walked = true;
                    break;
                }
            }
            if (!walked)
            {
                return Refusal.NoTop;
            }
            var nearEdge = new Vector3(origin.x, height, origin.z);
            Vector3 farEdge = nearEdge + into * lastTopDepth;

            // 4. THROUGH THE MIDDLE OF AN OPENING (owner, 2026-10-14: "the vaulting through window function isnt working on most windows"): the player
            // meets a window's wall wherever they run at it, and the way through was checked there, a hand's width from a jamb as often as not.
            float shift = OpeningShift(Vector3.Lerp(nearEdge, farEdge, 0.5f), across, self);
            nearEdge += across * shift;
            farEdge += across * shift;

            // 5. Ground to land on past the far edge, within a short drop, with room to stand.
            Vector3 land = farEdge + into * (radius + 0.35f);
            if (!LedgeProbe.SolidHit(new Vector3(land.x, height + 0.5f, land.z), Vector3.down, 0.5f + MaxDropMetres, self, out RaycastHit ground)
                || ground.normal.y < 0.7f)
            {
                return Refusal.NoLanding;
            }
            land.y = ground.point.y;
            if (!LedgeProbe.HasRoom(land, standingHeight, radius, self))
            {
                return Refusal.NoRoomToLand;
            }
            // OVER THE TOP must be open enough to pass (owner, 2026-10-13: "you cant vault into a window which you should be able to if its been
            // shattered"): a window's sill is a fine vault except that the lintel is over it, and asking for a whole standing body's worth of air
            // refused every window below 2.5 m. The free height over the top is measured and handed on; a body dives flat through a low one.
            // First, the opening itself must be CLEAR: a whole pane of glass stands in the wall's thickness, exactly where a sweep would start (and a
            // sweep that starts inside something ignores it), so a box the depth of the top and the least passable height is checked for anything solid.
            if (!OpeningIsClear(nearEdge, farEdge, into, self))
            {
                return Refusal.OpeningBlocked;
            }
            Vector3 topMiddle = Vector3.Lerp(nearEdge, farEdge, 0.5f);
            float headroom = FreeHeightAbove(topMiddle, standingHeight, radius * 0.8f, self);
            if (headroom < MinHeadroomMetres)
            {
                return Refusal.TooLow;
            }
            // And past the far edge too (no rail or sign just beyond), as tall as the body can be there.
            if (!LedgeProbe.HasRoom(farEdge + into * 0.15f + Vector3.up * 0.05f, Mathf.Min(standingHeight * 0.9f, headroom - 0.05f), radius * 0.8f, self))
            {
                return Refusal.NoRoomPastTop;
            }

            vault = new Vault
            {
                NearEdge = nearEdge,
                FarEdge = farEdge,
                Land = land,
                Height = height,
                Normal = faceNormal,
                Headroom = headroom,
            };
            return Refusal.None;
        }

        /// <summary>The height over the top, metres, at which the opening is measured across: the middle of the opening check's box.</summary>
        private const float OpeningMiddleHeight = 0.08f + (MinHeadroomMetres - 0.16f) * 0.5f;

        /// <summary>
        /// How far to slide the way over along the top, metres (+ toward <paramref name="across"/>), so that it passes through the middle of an
        /// OPENING: rays along the top, at the height of the opening check's middle, find the jambs either side; the way is kept
        /// <see cref="JambClearanceMetres"/> from each, or put midway when the opening is narrower than that allows. With nothing either side
        /// within reach (a fence, a box, a wide shop window) nothing moves.
        /// </summary>
        private static float OpeningShift(Vector3 topMiddle, Vector3 across, Transform self)
        {
            Vector3 from = topMiddle + Vector3.up * OpeningMiddleHeight;
            float left = LedgeProbe.SolidHit(from, -across, SpanSearchMetres, self, out RaycastHit l) ? l.distance : float.PositiveInfinity;
            float right = LedgeProbe.SolidHit(from, across, SpanSearchMetres, self, out RaycastHit r) ? r.distance : float.PositiveInfinity;
            float lowest = JambClearanceMetres - left;     // the least shift that keeps clear of the left jamb (-infinity with none)
            float highest = right - JambClearanceMetres;   // the most that keeps clear of the right one
            if (lowest <= highest)
            {
                return Mathf.Clamp(0.0f, lowest, highest);
            }
            return (right - left) * 0.5f;                  // narrower than a clear way: through its middle
        }

        /// <summary>Nothing hit this far over the top counts as open sky: more than any body's worth, so a fence is never mistaken for a low opening.</summary>
        public const float OpenSkyExtraMetres = 0.5f;

        /// <summary>
        /// True when something stands over the top within reach (a window lintel, a ceiling): the vault goes THROUGH an opening rather than over a
        /// wall top under open sky (FreeHeightAbove reads open sky as its whole sweep).
        /// </summary>
        public static bool IsThroughAnOpening(in Vault vault, float standingHeight)
        {
            return vault.Headroom < standingHeight + OpenSkyExtraMetres - 0.01f;
        }

        /// <summary>
        /// How much open air there is straight up from <paramref name="point"/>: a sphere the width of a body, swept upward. Open sky reads as
        /// <paramref name="standingHeight"/> plus <see cref="OpenSkyExtraMetres"/>.
        /// </summary>
        private static float FreeHeightAbove(Vector3 point, float standingHeight, float sphereRadius, Transform self)
        {
            Vector3 origin = point + Vector3.up * (sphereRadius + 0.05f);
            float sweep = standingHeight + OpenSkyExtraMetres;
            if (SolidSphereHit(origin, sphereRadius, Vector3.up, sweep, self, out RaycastHit hit))
            {
                // The sphere's top touches whatever is over the point: that far above the point is the ceiling.
                return Mathf.Max(0.0f, 0.05f + sphereRadius * 2.0f + hit.distance);
            }
            return sweep;
        }

        private static readonly Collider[] OpeningOverlaps = new Collider[8];

        /// <summary>
        /// True when the air over the top, from a hand's width above it up to the least passable height, across the top's depth and the passage's
        /// width, holds nothing solid (this body and people aside): a whole pane of glass, a bar, a sign across the opening all fail it. It starts a
        /// little in front of the near edge, so that a pane standing in the very plane of the face found (the face found WAS the glass) is in it.
        /// </summary>
        private static bool OpeningIsClear(Vector3 nearEdge, Vector3 farEdge, Vector3 into, Transform self)
        {
            Vector3 start = nearEdge - into * OpeningLeadMetres;
            Vector3 end = farEdge - into * 0.03f;
            float depth = Mathf.Max(Vector3.Distance(start, end), 0.06f);
            Vector3 centre = Vector3.Lerp(start, end, 0.5f) + Vector3.up * OpeningMiddleHeight;
            var half = new Vector3(PassageHalfWidthMetres, (MinHeadroomMetres - 0.16f) * 0.5f, depth * 0.5f);
            Quaternion frame = Quaternion.LookRotation(into, Vector3.up);
            int count = Physics.OverlapBoxNonAlloc(centre, half, OpeningOverlaps, frame, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = OpeningOverlaps[i];
                if (c == null || (self != null && c.transform.IsChildOf(self)) || c.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                return false;
            }
            return true;
        }
    }
}
