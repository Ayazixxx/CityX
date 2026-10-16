namespace CityX.World
{
    /// <summary>A billboard on a building's flat roof (RoofBillboards), in the building's own frame.</summary>
    public readonly struct RoofBillboard
    {
        public readonly bool Exists;

        /// <summary>The board: how wide and how tall, metres, and how high its bottom stands over the roof.</summary>
        public readonly float Width;
        public readonly float Height;
        public readonly float Lift;

        /// <summary>Where its middle is along the street wall, metres from the roof's middle.</summary>
        public readonly float Offset;

        /// <summary>The ad: its background colour and its accent colour (indices into the builder's palette) and how the accent is laid out.</summary>
        public readonly int Background;
        public readonly int Accent;
        public readonly int Layout;

        /// <summary>Floodlights on the catwalk in front of it (they glow at night on a building with power).</summary>
        public readonly bool Floodlit;

        /// <summary>How the ad's lettering runs, 0..1: the builder reads how long each line of it is off this.</summary>
        public readonly float Copy;

        public RoofBillboard(float width, float height, float lift, float offset, int background, int accent, int layout, bool floodlit, float copy)
        {
            Exists = true;
            Width = width;
            Height = height;
            Lift = lift;
            Offset = offset;
            Background = background;
            Accent = accent;
            Layout = layout;
            Floodlit = floodlit;
            Copy = copy;
        }
    }

    /// <summary>
    /// BILLBOARDS ON THE ROOFS (World Gacha phase 4, 2026-10-15): the big painted board on steel legs that a Japanese street's mid-rise buildings
    /// carry over their street walls, read from far down the street and against the sky. On a flat roof only, standing just behind the street
    /// wall's parapet and facing the street, on its legs high enough that a body walks under it, with a catwalk in front and, mostly, floodlights.
    /// Pure: from the building's seed, so every machine has the same boards. The builder draws it (CityMeshBuilder.Billboards.cs) and keeps the
    /// roof plant off its ground.
    /// </summary>
    public static class RoofBillboards
    {
        /// <summary>The board's face, in from the street wall's outer face (behind the parapet), and how thick the board is, metres.</summary>
        public const float SetBack = 1.0f;

        public const float Thickness = 0.2f;

        /// <summary>The board's size, metres: as wide as 5 to 14 m (and 1.5 m in from the roof's ends), 3 to 5.2 m tall.</summary>
        public const float MinWidth = 5.0f;

        public const float MaxWidth = 14.0f;

        public const float MinHeight = 3.0f;

        public const float MaxHeight = 5.2f;

        /// <summary>How high the board's bottom stands over the roof: its catwalk (CatwalkBelow under it) clears a 2.6 m body walking along the roof.</summary>
        public const float Lift = 3.3f;

        /// <summary>The steel: legs this thick, standing this far behind the board's back.</summary>
        public const float LegSize = 0.24f;

        public const float LegsBehind = 0.7f;

        /// <summary>The catwalk in front of the board: how deep, and how far under the board's bottom.</summary>
        public const float CatwalkDepth = 0.8f;

        public const float CatwalkBelow = 0.3f;

        /// <summary>The buildings it stands on: no lower and no taller than these, metres.</summary>
        public const float MinBuildingHeight = 9.0f;

        public const float MaxBuildingHeight = 70.0f;

        /// <summary>The share of floodlit boards.</summary>
        public const double FloodlitShare = 0.7;

        /// <summary>How many colours the builder's ad palette has, and how many layouts its accent has.</summary>
        public const int Colours = 9;

        public const int Layouts = 4;

        private const ulong Salt = 0x424C4244UL;   // "BLBD"

        /// <summary>The share of buildings that could carry one that do, by district: downtown and the busy streets most.</summary>
        public static double ShareFor(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Core: return 0.30;
                case ZoneKind.Urban: return 0.25;
                case ZoneKind.OldTown: return 0.15;
                case ZoneKind.Industrial: return 0.10;
                case ZoneKind.Residential: return 0.06;
                default: return 0.05;
            }
        }

        /// <summary>The billboard on the roof of the building on a site, or none (Exists false).</summary>
        public static RoofBillboard For(BuildingSite site)
        {
            if (!site.Exists || site.Shell || site.Params.IsSpecial || site.Sides >= 3 || site.HasRoofObstacle || site.RoofSealed
                || site.Params.Height < MinBuildingHeight || site.Params.Height > MaxBuildingHeight || !RoofShape.For(site).HasParapet)
            {
                return default;
            }
            ulong seed = unchecked((ulong)(uint)site.Seed);
            double D(int i) => WorldHash.Unit01(i, 0, Salt, seed);
            if (D(0) >= ShareFor(site.Zone))
            {
                return default;
            }
            float room = site.Params.Width - 3.0f;   // 1.5 m in from each end of the roof
            float width = UnityEngine.Mathf.Min(room, MinWidth + (MaxWidth - MinWidth) * (float)D(1));
            if (width < MinWidth || site.Params.Depth < SetBack + Thickness + LegsBehind + LegSize + 3.0f)
            {
                return default;
            }
            float height = MinHeight + (MaxHeight - MinHeight) * (float)D(2);
            float offset = (room - width) * ((float)D(3) - 0.5f);

            // Off the hatch the stair comes up through onto the roof (and its landing round it): where it would stand on it, it slides to one end of
            // the roof or the other, then a narrower board is tried, and failing all that there is none.
            UnityEngine.Rect hatch = UnityEngine.Rect.zero;
            BuildingStairs.Layout stairs = BuildingStairs.PlanFor(site);
            StairPlan roofStair = stairs.NoRoofStair || stairs.FloorCount == 0 ? null : stairs.Floors[stairs.FloorCount - 1];
            if (roofStair != null)
            {
                UnityEngine.Rect h = roofStair.Hole;
                hatch = UnityEngine.Rect.MinMaxRect(h.xMin - 1.2f, h.yMin - 1.2f, h.xMax + 1.2f, h.yMax + 1.2f);
            }
            bool placed = hatch.width <= 0.0f || !hatch.Overlaps(Ground(site, width, offset));
            for (int attempt = 0; !placed && attempt < 4; attempt++)
            {
                float w = attempt < 2 ? width : UnityEngine.Mathf.Max(MinWidth, width * 0.65f);
                if (w > room) continue;
                float o = (room - w) * 0.5f * (attempt % 2 == 0 ? -1.0f : 1.0f);
                if (!hatch.Overlaps(Ground(site, w, o)))
                {
                    width = w;
                    offset = o;
                    placed = true;
                }
            }
            if (!placed)
            {
                return default;
            }

            int background = (int)(D(4) * Colours) % Colours;
            int accent = (background + 1 + (int)(D(5) * (Colours - 1))) % Colours;
            return new RoofBillboard(width, height, Lift, offset, background, accent, (int)(D(6) * Layouts) % Layouts, D(7) < FloodlitShare, (float)D(8));
        }

        /// <summary>
        /// The ground a billboard <paramref name="width"/> wide at <paramref name="offset"/> takes on its building's roof, in the building's frame
        /// relative to its middle (x across the street wall, z toward it): from the back of its legs to the front of its catwalk, a little wider.
        /// </summary>
        public static UnityEngine.Rect Ground(BuildingSite site, float width, float offset)
        {
            float front = site.Params.Depth * 0.5f - SetBack;
            float legsBack = front - Thickness - LegsBehind - LegSize;
            return UnityEngine.Rect.MinMaxRect(offset - width * 0.5f - 0.4f, legsBack - 0.4f, offset + width * 0.5f + 0.4f, front + CatwalkDepth);
        }
    }
}
