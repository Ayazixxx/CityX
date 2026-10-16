using UnityEngine;

namespace CityX.World
{
    /// <summary>What rises above a tall tower's flat roof.</summary>
    public enum TowerTopKind
    {
        None = 0,

        /// <summary>A communications mast: a lattice tower on a base platform, a pole above it, a beacon at the tip (the Empire State's, Willis's).</summary>
        Mast = 1,

        /// <summary>A tapering needle on a short drum (One World Trade Center's, the Petronas pinnacles).</summary>
        Spire = 2,

        /// <summary>Three setback tiers narrowing to a needle: the art-deco crown (the Chrysler's, 40 Wall Street's).</summary>
        SteppedSpire = 3,

        /// <summary>A needle set at one corner of the roof, leaning out (the Bank of America Tower's).</summary>
        SlantedSpire = 4,

        /// <summary>Fins round the roof's edge rising past the parapet and closed by a ring at the top: the crown of a modern tower.</summary>
        Fins = 5,
    }

    /// <summary>
    /// A TOWER'S TOP (owner, 2026-10-15, with six icons: "a chance that these types of tips generate only on really tall towers ... implement a few
    /// different types ... research the ratio that these generally appear in comparison to flat roofs in real life cities").
    ///
    /// Research (docs/WORLD_GACHA.md): CTBUH counts a spire in a tower's height and an antenna not, and dropped its ranking by roof height in
    /// 2009 because flat-topped skyscrapers "are not as common in the modern era". Counted off the tallest-building lists: of New York's 68
    /// towers over 230 m, 5 carry a spire, 1 a mast, 11 a crown or stepped top and 51 are flat (a quarter topped; of the 19 over 300 m, 7);
    /// of Chicago's 57 over 150 m, 2 spires, 1 mast, 13 crowns, 41 flat (6 of the 7 over 300 m topped, 8 of 32 at 200-300 m, 2 of 18 at
    /// 150-200 m). So a tower of 110-240 m has about one chance in five of something above its roof, a crown of fins or setbacks the most
    /// likely, a mast or a spire a few per cent each; a supertall over 240 m better than an even chance, the spire the most likely there, the
    /// slanted one rare.
    ///
    /// Pure: from the building's seed on a salt of its own. Only the topmost volume of a tower gets one (a plain tower, the tower standing on its
    /// podium -- most of the city's towers -- or the top stage of a stepped one, a box or a round one; not a stage with another on it, not the leg
    /// of an L, not a set piece), when the whole tower reaches <see cref="LeastReach"/> (BuildingSite.Reach: a stage carries the building's mass
    /// but only its own height), on its flat parapet roof, in the middle (a slanted spire at a corner; a round roof's square inside its parapet),
    /// slid or shrunk off the stair's hatch as a billboard is. The mesh is CityMeshBuilder.AddTowerTop's (near and far: it is the skyline) and
    /// the skyline tier's box carries a stub of it.
    /// </summary>
    public readonly struct TowerTop
    {
        public readonly TowerTopKind Kind;

        /// <summary>How far above the roof the top reaches, metres (the mast's or spire's tip, the fins' ring).</summary>
        public readonly float Height;

        /// <summary>Where it stands on the roof, in the building's frame relative to its middle (the roof's middle for most; a slanted spire's corner).</summary>
        public readonly float X, Z;

        /// <summary>Its base's width on the roof, metres (a mast's platform, a spire's drum, a crown's first tier; 0 for the fins, which stand at the edge).</summary>
        public readonly float Size;

        public TowerTop(TowerTopKind kind, float height, float x, float z, float size)
        {
            Kind = kind;
            Height = height;
            X = x;
            Z = z;
            Size = size;
        }

        public bool Exists => Kind != TowerTopKind.None;

        public static readonly TowerTop None = new TowerTop(TowerTopKind.None, 0.0f, 0.0f, 0.0f, 0.0f);

        private const ulong Salt = 0x54544F50UL;   // "TTOP"

        /// <summary>The share of each kind, in enum order (None, Mast, Spire, SteppedSpire, SlantedSpire, Fins), for a tower of 110-240 m...</summary>
        public static readonly double[] TowerWeights = { 78.0, 5.0, 5.0, 5.0, 0.0, 7.0 };

        /// <summary>...and for a supertall over 240 m.</summary>
        public static readonly double[] SupertallWeights = { 45.0, 8.0, 20.0, 12.0, 6.0, 9.0 };

        /// <summary>The top's height as a share of the tower's reach, least and most, by kind.</summary>
        private static readonly float[] LeastShare = { 0.0f, 0.12f, 0.14f, 0.18f, 0.16f, 0.05f };

        private static readonly float[] MostShare = { 0.0f, 0.20f, 0.26f, 0.24f, 0.24f, 0.07f };

        /// <summary>A mast is never shorter than this, metres: a stub on a 110 m tower reads as plant, not a mast.</summary>
        public const float LeastMast = 18.0f;

        /// <summary>The whole tower must stand at least this far over its street floor, metres (a tower's mass begins at 110 m of height).</summary>
        public const float LeastReach = 100.0f;

        /// <summary>How far a top's base keeps from the roof's edge (the parapet and a walk behind it), metres.</summary>
        public const float EdgeMargin = 1.3f;

        /// <summary>How far a top keeps from the stair's hatch, metres (the landing round it).</summary>
        public const float HatchMargin = 1.2f;

        /// <summary>The smallest base a mast or spire stands on, and a stepped crown's smallest first tier, metres.</summary>
        public const float LeastBase = 2.5f;

        public const float LeastTier = 6.0f;

        /// <summary>The top a building gets: <see cref="None"/> for all but the topmost volume of a tower, with its flat parapet roof.</summary>
        public static TowerTop For(BuildingSite site)
        {
            if (!site.Exists || site.Params.IsSpecial || site.Wing || site.HasRoofObstacle || site.RoofSealed)
            {
                return None;
            }
            bool supertall = site.Params.Mass == BuildingMass.Supertall;
            if (!supertall && site.Params.Mass != BuildingMass.Tower)
            {
                return None;
            }
            float reach = site.Reach;
            if (reach < LeastReach || !RoofShape.For(site).HasParapet)
            {
                return None;
            }
            ulong seed = unchecked((ulong)(uint)site.Seed);
            double D(int i) => WorldHash.Unit01(i, 0, Salt, seed);
            double[] weights = supertall ? SupertallWeights : TowerWeights;
            double total = 0.0;
            foreach (double weight in weights) total += weight;
            double target = D(0) * total, cursor = 0.0;
            int kind = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                cursor += weights[i];
                if (weights[i] > 0.0 && target < cursor)
                {
                    kind = i;
                    break;
                }
            }
            if (kind == 0)
            {
                return None;
            }
            float height = reach * Mathf.Lerp(LeastShare[kind], MostShare[kind], (float)D(1));
            if (kind == (int)TowerTopKind.Mast)
            {
                height = Mathf.Max(height, LeastMast);
            }
            var chosen = (TowerTopKind)kind;
            float w = site.Params.Width, d = site.Params.Depth;
            if (site.Sides >= 3)
            {
                // A round tower: its roof's square inside the parapet (RoundFootprint), and no fins (they want a square's edges): a spire instead.
                w = d = RoundFootprint.Of(site).InnerHalf * 2.0f;
                if (chosen == TowerTopKind.Fins)
                {
                    chosen = TowerTopKind.Spire;
                }
            }
            float least = Mathf.Min(w, d);

            // Off the hatch the stair comes up through (and the landing round it), as the billboards keep off it.
            Rect hatch = Rect.zero;
            BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
            StairPlan roofStair = stairs.NoRoofStair || stairs.FloorCount == 0 ? null : stairs.Floors[stairs.FloorCount - 1];
            if (roofStair != null)
            {
                Rect h = roofStair.Hole;
                hatch = Rect.MinMaxRect(h.xMin - HatchMargin, h.yMin - HatchMargin, h.xMax + HatchMargin, h.yMax + HatchMargin);
            }

            if (chosen == TowerTopKind.Fins)
            {
                return new TowerTop(TowerTopKind.Fins, height, 0.0f, 0.0f, 0.0f);
            }
            if (chosen == TowerTopKind.SlantedSpire)
            {
                float size = Mathf.Min(4.0f, least * 0.25f);
                float inset = EdgeMargin + size * 0.5f;
                int corner = (int)(D(2) * 4.0) & 3;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    int c = (corner + attempt) & 3;
                    float sx = (c == 1 || c == 2) ? 1.0f : -1.0f, sz = c >= 2 ? 1.0f : -1.0f;
                    float x = sx * (w * 0.5f - inset), z = sz * (d * 0.5f - inset);
                    if (hatch.width <= 0.0f || !hatch.Overlaps(Foot(x, z, size)))
                    {
                        return new TowerTop(TowerTopKind.SlantedSpire, height, x, z, size);
                    }
                }
                return None;
            }
            if (chosen == TowerTopKind.SteppedSpire)
            {
                float size = least * 0.5f;
                if (Fit(hatch, w, d, LeastTier, ref size, out float x, out float z))
                {
                    return new TowerTop(TowerTopKind.SteppedSpire, height, x, z, size);
                }
                chosen = TowerTopKind.Spire;   // no room for the tiers beside the hatch: a plain spire instead
            }
            // A base of 2.5-5 m (a mast's platform 4 m at most), three tenths of the roof's narrow side; a roof too small for the least base gets none.
            float baseSize = Mathf.Clamp(least * 0.3f, LeastBase, chosen == TowerTopKind.Mast ? 4.0f : 5.0f);
            return Fit(hatch, w, d, LeastBase, ref baseSize, out float bx, out float bz)
                ? new TowerTop(chosen, height, bx, bz, baseSize)
                : None;
        }

        private static Rect Foot(float x, float z, float size)
        {
            return Rect.MinMaxRect(x - size * 0.5f, z - size * 0.5f, x + size * 0.5f, z + size * 0.5f);
        }

        /// <summary>
        /// Where a base <paramref name="size"/> across stands: the roof's middle, or slid off the hatch along the roof's longer side as far as the
        /// edge margin allows, or shrunk (to <paramref name="least"/>) until it fits beside it. False when nothing fits.
        /// </summary>
        private static bool Fit(Rect hatch, float w, float d, float least, ref float size, out float x, out float z)
        {
            x = 0.0f;
            z = 0.0f;
            if (hatch.width <= 0.0f || !hatch.Overlaps(Foot(0.0f, 0.0f, size)))
            {
                return size <= Mathf.Min(w, d) - 2.0f * EdgeMargin;
            }
            bool alongX = w >= d;
            float hc = alongX ? hatch.center.x : hatch.center.y;
            float hh = alongX ? hatch.width : hatch.height;
            for (float s = size; s >= least; s -= 0.5f)
            {
                float room = (alongX ? w : d) * 0.5f - EdgeMargin - s * 0.5f;   // how far from the middle the base's centre may go
                if (room < 0.0f)
                {
                    continue;
                }
                for (int side = 0; side < 2; side++)
                {
                    float sign = (hc >= 0.0f) == (side == 0) ? -1.0f : 1.0f;   // away from the hatch first, then past it
                    float at = Mathf.Clamp(hc + sign * (hh * 0.5f + s * 0.5f + 0.2f), -room, room);
                    float cx = alongX ? at : 0.0f, cz = alongX ? 0.0f : at;
                    if (!hatch.Overlaps(Foot(cx, cz, s)))
                    {
                        x = cx;
                        z = cz;
                        size = s;
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
