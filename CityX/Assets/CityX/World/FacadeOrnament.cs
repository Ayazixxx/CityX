namespace CityX.World
{
    /// <summary>
    /// The relief on a building's walls: what stands proud of the flat wall so it catches light and throws shadow. Decided once per building, like
    /// <see cref="WindowPlan"/>, and read by the mesh builder.
    ///
    /// WHY (2026-10-09, "it looks like shit ... implement what you can learn from this pdf"). The survey of procedural city generation the owner sent
    /// (Kelly and McCabe 2006) grades the systems it reviews, and the only one it calls truly realistic is Wonka et al.'s Instant Architecture (2003):
    /// split grammars, a facade divided into floors, floors into bays, bays into a window with its sill and lintel, with "distinctive building features
    /// such as vertical details on the edges of buildings" and a ground floor set apart as a shop. Our buildings had the windows but nothing else, so a
    /// wall read as a flat grid. This adds the classic three-part facade every real street has:
    ///   - a BASE: a projecting course over the ground floor that sets the street storey apart;
    ///   - a SHAFT: string courses at the floor lines, piers between bays, a lintel over each window;
    ///   - a TOP: a cornice where the wall meets the roof, deeper on masonry, a thin crown band on glass;
    /// and corner piers up the building's edges.
    ///
    /// The family decides the vocabulary: masonry (punched windows) gets the full classical set, glass towers only a base canopy and a crown,
    /// sheds plain pilasters. Each part is then rolled from the building's seed, so two masonry blocks side by side are rarely dressed alike.
    /// </summary>
    public readonly struct FacadeOrnament
    {
        /// <summary>A course projecting over the ground floor (only on a building at street level).</summary>
        public readonly bool BaseCourse;

        /// <summary>A string course at every Nth row line above the base (0 = none).</summary>
        public readonly int CourseEveryRows;

        /// <summary>The cornice at the roof line: how tall and how far it projects (0 = none).</summary>
        public readonly float CorniceHeight;
        public readonly float CorniceDepth;

        /// <summary>
        /// The cornice is a band from the roof line up to the parapet's top (a crown in front of the parapet) rather than hanging down from the top by its
        /// own height: glass walls whose top windows run to the roof. Either way its top is the coping's (CityMeshBuilder.AddWallOrnament).
        /// </summary>
        public readonly bool CorniceAboveRoof;

        /// <summary>A pier between window bays every Nth column boundary (0 = none).</summary>
        public readonly int PierEveryColumns;

        /// <summary>Piers up the building's corners.</summary>
        public readonly bool CornerPiers;

        /// <summary>A stone lintel over each window.</summary>
        public readonly bool Lintels;

        /// <summary>
        /// How far a small concrete canopy (庇, hisashi) over each window above the street storey stands out from the wall, metres; 0 = none. World
        /// Gacha's detail, and the everyday Japanese house's: a thin hood that keeps the rain off the window. A building has these or lintels, not
        /// both.
        /// </summary>
        public readonly float CanopyDepth;

        // Sizes, metres. Kept small and fixed so nothing reaches a neighbour or a window: every part sits in the wall between the panes.
        public const float BaseCourseHeight = 0.42f;
        public const float BaseCourseDepth = 0.24f;
        public const float CourseHeight = 0.16f;
        public const float CourseDepth = 0.11f;
        public const float PierWidth = 0.34f;
        public const float PierDepth = 0.14f;
        public const float CornerPierHalf = 0.28f;
        public const float LintelHeight = 0.17f;
        public const float LintelDepth = 0.07f;
        public const float LintelOverhang = 0.10f;

        /// <summary>
        /// A window canopy: how deep it is at least and at most (a real one is 0.3 to 0.45 m; the city is 1.3 times life size), how thick, how far past
        /// each side of the window it runs and how far over the window's head it sits. Shallower than ClimbJut.MaxLean, so a climb leans round it as it
        /// does round a cornice.
        /// </summary>
        public const float MinCanopyDepth = 0.36f;
        public const float MaxCanopyDepth = 0.55f;
        public const float CanopyThickness = 0.08f;
        public const float CanopyOverhang = 0.14f;
        public const float CanopyGap = 0.05f;

        /// <summary>The deepest a cornice ever projects (a masonry block's, For's 0.45 + 0.40): nothing on a wall stands further out.</summary>
        public const float MaxCorniceDepth = 0.85f;

        public FacadeOrnament(bool baseCourse, int courseEveryRows, float corniceHeight, float corniceDepth, bool corniceAboveRoof,
            int pierEveryColumns, bool cornerPiers, bool lintels, float canopyDepth = 0.0f)
        {
            BaseCourse = baseCourse;
            CourseEveryRows = courseEveryRows;
            CorniceHeight = corniceHeight;
            CorniceDepth = corniceDepth;
            CorniceAboveRoof = corniceAboveRoof;
            PierEveryColumns = pierEveryColumns;
            CornerPiers = cornerPiers;
            Lintels = lintels && canopyDepth <= 0.0f;
            CanopyDepth = canopyDepth;
        }

        public bool HasCanopies => CanopyDepth > 0.0f;

        /// <summary>The window canopies a building rolls (<paramref name="die"/> against its district's share; <paramref name="size"/> sets the depth): 0 for none.</summary>
        private static float Canopy(BuildingSite site, double die, double size)
        {
            if (site.OnRoof || die >= CanopyShare(site.Zone))
            {
                return 0.0f;
            }
            return MinCanopyDepth + (MaxCanopyDepth - MinCanopyDepth) * (float)size;
        }

        /// <summary>The share of buildings with a punched or scattered facade that have window canopies, by district: the homes zones most.</summary>
        public static double CanopyShare(ZoneKind zone)
        {
            switch (zone)
            {
                case ZoneKind.Residential: return 0.45;
                case ZoneKind.OldTown: return 0.50;
                case ZoneKind.Outskirts: return 0.40;
                case ZoneKind.Urban: return 0.22;
                case ZoneKind.Core: return 0.08;
                default: return 0.0;   // industrial: sheds
            }
        }

        public bool HasCornice => CorniceHeight > 0.0f;

        private const ulong OrnamentSalt = 0x4F524E414DUL;   // "ORNAM"

        /// <summary>
        /// The ornament for the building on a site, given its facade family and wall material. A building with a pitched roof or a flat one with
        /// eaves (RoofShape) has no parapet for a cornice to cap: its eaves are its crown.
        /// </summary>
        public static FacadeOrnament For(BuildingSite site, FacadeStyle style, WallMaterial wall)
        {
            FacadeOrnament o = ForStyle(site, style, wall);
            return RoofShape.For(site).HasParapet
                ? o
                : new FacadeOrnament(o.BaseCourse, o.CourseEveryRows, 0.0f, o.CorniceDepth, o.CorniceAboveRoof, o.PierEveryColumns, o.CornerPiers, o.Lintels, o.CanopyDepth);
        }

        private static FacadeOrnament ForStyle(BuildingSite site, FacadeStyle style, WallMaterial wall)
        {
            ulong s = unchecked((ulong)(long)site.Seed);
            double R(int i) => WorldHash.Unit01(i, 0, OrnamentSalt, s);
            bool street = !site.OnRoof;
            bool masonry = wall == WallMaterial.Brick || wall == WallMaterial.Stone || wall == WallMaterial.Plaster;
            bool tall = site.Params.Height >= 60.0f;

            switch (style)
            {
                case FacadeStyle.Punched:
                    return new FacadeOrnament(
                        baseCourse: street && R(0) < 0.92,
                        courseEveryRows: R(1) < 0.70 ? (R(2) < 0.75 ? 2 : 1) : 0,
                        corniceHeight: R(3) < (masonry ? 0.92 : 0.70) ? (masonry ? 0.55f + 0.45f * (float)R(4) : 0.45f + 0.25f * (float)R(4)) : 0.0f,
                        corniceDepth: masonry ? 0.45f + 0.40f * (float)R(5) : 0.30f + 0.20f * (float)R(5),
                        corniceAboveRoof: false,
                        pierEveryColumns: R(6) < (masonry ? 0.45 : 0.30) ? (R(7) < 0.5 ? 2 : 4) : 0,
                        cornerPiers: R(8) < (masonry ? 0.80 : 0.55),
                        lintels: masonry ? R(9) < 0.75 : R(9) < 0.35,
                        canopyDepth: Canopy(site, R(10), R(11)));
                case FacadeStyle.Scattered:
                    return new FacadeOrnament(
                        baseCourse: street && R(0) < 0.60,
                        courseEveryRows: R(1) < 0.30 ? 2 : 0,
                        corniceHeight: R(3) < 0.55 ? 0.6f + 0.6f * (float)R(4) : 0.0f,
                        corniceDepth: 0.35f + 0.35f * (float)R(5),
                        corniceAboveRoof: false,
                        pierEveryColumns: 0,
                        cornerPiers: R(8) < 0.40,
                        lintels: R(9) < 0.30,
                        canopyDepth: Canopy(site, R(10), R(11)));
                case FacadeStyle.Ribbon:
                    // The bands are the facade; a ledge under each and a crown.
                    return new FacadeOrnament(
                        baseCourse: street && R(0) < 0.80,
                        courseEveryRows: R(1) < 0.65 ? 2 : 0,
                        corniceHeight: R(3) < 0.60 ? 0.5f + 0.4f * (float)R(4) : 0.0f,
                        corniceDepth: 0.25f + 0.25f * (float)R(5),
                        corniceAboveRoof: true,
                        pierEveryColumns: 0,
                        cornerPiers: R(8) < 0.35,
                        lintels: false);
                case FacadeStyle.Curtain:
                    // Glass to the edges: a canopy over the street storey and a crown band, nothing across the glass.
                    return new FacadeOrnament(
                        baseCourse: street && R(0) < 0.75,
                        courseEveryRows: 0,
                        corniceHeight: R(3) < (tall ? 0.70 : 0.45) ? 0.8f + 1.4f * (float)R(4) : 0.0f,
                        corniceDepth: 0.18f + 0.20f * (float)R(5),
                        corniceAboveRoof: true,
                        pierEveryColumns: 0,
                        cornerPiers: R(8) < 0.25,
                        lintels: false);
                case FacadeStyle.HighBand:
                    // Sheds: pilasters between the bays and a plain coping.
                    return new FacadeOrnament(
                        baseCourse: false,
                        courseEveryRows: 0,
                        corniceHeight: R(3) < 0.35 ? 0.35f : 0.0f,
                        corniceDepth: 0.20f,
                        corniceAboveRoof: false,
                        pierEveryColumns: R(6) < 0.65 ? (R(7) < 0.5 ? 2 : 3) : 0,
                        cornerPiers: R(8) < 0.50,
                        lintels: false);
                default:
                    return new FacadeOrnament(
                        baseCourse: street && R(0) < 0.40,
                        courseEveryRows: 0,
                        corniceHeight: R(3) < 0.30 ? 0.4f : 0.0f,
                        corniceDepth: 0.25f,
                        corniceAboveRoof: false,
                        pierEveryColumns: R(6) < 0.40 ? 3 : 0,
                        cornerPiers: R(8) < 0.40,
                        lintels: false);
            }
        }
    }
}
