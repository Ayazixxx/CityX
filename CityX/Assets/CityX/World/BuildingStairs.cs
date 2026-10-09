using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>The families of stair a building can have. Switchback covers the U (two flights) and the Z and double-U (three and four).</summary>
    public enum StairKind
    {
        /// <summary>One straight flight.</summary>
        Straight,

        /// <summary>Two flights in a line with a landing between them.</summary>
        Landing,

        /// <summary>Two flights at a right angle round a landing.</summary>
        L,

        /// <summary>Parallel flights, turned back on themselves at landings. The fire-stair shape.</summary>
        Switchback,

        /// <summary>Four flights round the edge of an open well, landings at three corners.</summary>
        Wrap,
    }

    /// <summary>
    /// One floor's staircase, placed in the building's own frame (relative to the footprint's centre; x and z, the Rect's y is z).
    ///
    /// EVERY STAIR IS COMFORTABLE BY CONSTRUCTION: the tread is fixed and the number of risers is chosen from the climb, so the slope can
    /// never be steeper than <see cref="BuildingStairs.MaxAngleDegrees"/>. A flight that does not fit the room is folded (landing, turn)
    /// or another kind is tried, never made steeper.
    /// </summary>
    public sealed class StairPlan
    {
        public StairKind Kind;
        public int Flights;

        /// <summary>Walled in on all sides but a doorway: a stair core.</summary>
        public bool Enclosed;

        /// <summary>Which side of <see cref="Bounds"/> the doorway is in: 0 = -x, 1 = +x, 2 = -z, 3 = +z. Meaningful when enclosed.</summary>
        public int DoorSide;

        /// <summary>The floor carves no corner room: its partition would have walled the stair off from the way in.</summary>
        public bool SuppressRoom;

        /// <summary>The steps and landings, lowest first. <see cref="Tops"/> is how far each one's top is above the floor it starts from.</summary>
        public Rect[] Treads;
        public float[] Tops;

        /// <summary>The opening in the ceiling above: wherever a body on the stair would otherwise hit it.</summary>
        public Rect Hole;

        /// <summary>The stair and the cells either end of it that a body walks in and out by.</summary>
        public Rect Bounds;

        /// <summary>Where you step on from, and where you step off at the floor above.</summary>
        public Rect Entry;
        public Rect Exit;

        /// <summary>The climb, in metres, and how many risers (landings count as one).</summary>
        public float Rise;
        public int Risers;

        public float AngleDegrees;

        public Vector2 EntryPoint => Entry.center;
        public Vector2 ExitPoint => Exit.center;

        /// <summary>A path a walker follows from the entry to the exit (the middle of every step in order).</summary>
        public Vector2[] Route()
        {
            var route = new Vector2[Treads.Length + 2];
            route[0] = Entry.center;
            for (int i = 0; i < Treads.Length; i++)
            {
                route[i + 1] = Treads[i].center;
            }
            route[Treads.Length + 1] = Exit.center;
            return route;
        }
    }

    /// <summary>
    /// Every building's stairs: which kind each floor has, where it stands, and the opening it needs.
    ///
    /// Asked: "there are still very steep staircases at the top floors of some buildings ... a wide variety of staircases". The old layout
    /// put one straight flight in a rotating corner and gave it whatever room the floor had, so the slope was whatever came out (a median
    /// 47 degrees, 77 on a downtown top floor). This one:
    ///   * fixes the tread and derives the risers from the climb, so no flight is ever steeper than about 33 degrees;
    ///   * folds the climb when the room is short: landings, L turns, switchbacks, wraps round an open well;
    ///   * gives a building a character: tall and downtown buildings usually have a walled-in stair core, the same shaft on every floor;
    ///     the rest vary from floor to floor between straight, landing, L, switchback and wrap stairs;
    ///   * stops the top floor from swallowing a hundred metres: a tall building is fitted out up to <see cref="BuildingInterior.MaxFloors"/>
    ///     storeys and sealed above.
    ///
    /// A PURE FUNCTION OF THE SITE, so the near building, the distant shell and the tests all see the same stairs.
    /// </summary>
    public static class BuildingStairs
    {
        public const float Tread = 0.36f;
        public const float Width = 1.7f;

        /// <summary>The steepest riser a flight is built with; the angle is atan(riser / tread), about 33 degrees.</summary>
        public const float MaxRiser = 0.235f;

        public const float MaxAngleDegrees = 36.0f;

        /// <summary>Free floor kept at each end of a stair, so there is somewhere to stand before the first step and after the last.</summary>
        public const float Cell = 1.2f;

        /// <summary>
        /// How far under the ceiling a step may be before the stair needs its opening there. The player is 2.6 m tall with a half-metre-wide
        /// capsule that climbs a step ahead of itself, so a step with less than about 3.4 m over it (and the slab is 0.2 m thick) blocks the head:
        /// "my head is hitting the roof in this stairwell and it does not let me go up". The first version used 2.9 m of clearance and trapped people.
        /// </summary>
        public const float HeadClear = 3.9f;

        /// <summary>Clearance a stair that stands over another stair's opening must leave above the one below.</summary>
        public const float StackClear = 3.7f;

        /// <summary>How deep a step is underneath: it is a stringer, not a solid pile, so a stair can sit above another's opening.</summary>
        public const float Soffit = 0.55f;

        /// <summary>Thickness of the walls of a stair core.</summary>
        public const float CoreWall = 0.2f;

        /// <summary>The doorway into a core.</summary>
        public const float CoreDoor = 1.6f;

        /// <summary>Free floor in front of a core's doorway, from the shaft wall to whatever is beyond (the outer wall, a partition): room to stand and turn.</summary>
        public const float DoorLobby = 1.9f;

        /// <summary>How far the room's walls are in from the footprint's half extent: the wall thickness and a hair.</summary>
        private const float OuterWall = BuildingInterior.WallThickness + 0.05f;

        // ---- the plan for a whole building ----

        /// <summary>The stairs of one building.</summary>
        public sealed class Layout
        {
            /// <summary>The flight down to the basement, or null when there is none.</summary>
            public StairPlan Basement;

            /// <summary>One per floor; null where nothing fitted (should be vanishingly rare, and is a test failure).</summary>
            public StairPlan[] Floors;

            public int FloorCount;
            public float Storey;

            /// <summary>The ceiling of the top floor is not the roof: a tall building's upper part is sealed and has no stair.</summary>
            public bool SealedTop;

            /// <summary>
            /// The roof is under a sealed block (a twisted, wonky or cantilevered tower's sculpture, BuildingSite.Shell): there is no stair up to it, as
            /// there would be nowhere to come out.
            /// </summary>
            public bool RoofCovered;

            /// <summary>Whether the top floor has no stair: its ceiling is sealed, or its roof is covered.</summary>
            public bool NoRoofStair => SealedTop || RoofCovered;

            /// <summary>Every floor shares one stair core.</summary>
            public bool Core;

            /// <summary>The climb of one floor, bottom to its ceiling.</summary>
            public float RiseOf(int floor, float height)
            {
                float bottom = floor * Storey;
                return BuildingInterior.FloorTop(floor, FloorCount, height, Storey) - bottom;
            }
        }

        public static Layout PlanFor(BuildingSite site)
        {
            float width = site.Params.Width, depth = site.Params.Depth;
            if (site.Sides >= 3)
            {
                // A round building's stairs go in the square in its middle (RoundFootprint.InnerHalf): the room the planner sees is that square
                // and a wall's thickness round it.
                width = depth = 2.0f * (RoundFootprint.Of(site).InnerHalf + BuildingInterior.WallThickness + 0.05f);
            }
            return PlanFor(width, depth, site.Params.Height, site.Params.HasBasement && site.Sides < 3, site.Params.Mass,
                site.Zone, site.Seed, site.HasRoofObstacle
                    ? Rect.MinMaxRect(site.ObstacleX0 - 0.4f, site.ObstacleZ0 - 0.4f, site.ObstacleX1 + 0.4f, site.ObstacleZ1 + 0.4f)
                    : Rect.zero,
                site.RoofSealed);
        }

        public static Layout PlanFor(float width, float depth, float height, bool basement, BuildingMass mass, ZoneKind zone, int seed, Rect roofObstacle,
            bool roofCovered = false)
        {
            float storey = BuildingInterior.StoreyHeight(height);
            int floors = BuildingInterior.FloorCountFor(height);
            var layout = new Layout
            {
                FloorCount = floors,
                Storey = storey,
                SealedTop = BuildingInterior.IsSealedTop(height, storey, floors),
                Floors = new StairPlan[floors],
            };
            layout.RoofCovered = roofCovered && !layout.SealedTop;

            float hx = width * 0.5f - BuildingInterior.WallThickness - 0.05f;
            float hz = depth * 0.5f - BuildingInterior.WallThickness - 0.05f;
            var door = Rect.MinMaxRect(-1.7f, hz - 2.6f, 1.7f, hz);
            bool hasObstacle = roofObstacle.width > 0.0f && roofObstacle.height > 0.0f;

            var rises = new float[floors];
            float maxRise = basement ? storey : 0.0f;
            for (int f = 0; f < floors; f++)
            {
                rises[f] = layout.RiseOf(f, height);
                maxRise = Mathf.Max(maxRise, rises[f]);
            }

            // A stair core: the same enclosed U on every floor. Tried first for the buildings that earn one.
            bool wantsCore = WantsCore(mass, zone, floors, seed);
            if (wantsCore && TryCore(layout, hx, hz, door, hasObstacle, roofObstacle, rises, maxRise, basement, seed))
            {
                layout.Core = true;
                return layout;
            }

            // Otherwise the stair changes from floor to floor.
            StairPlan previous = null;
            int firstFloor = 0;
            if (basement)
            {
                // The flight down is chosen first, and the lobby then has to work round it; if the lobby cannot (the flight cuts a narrow room in two), try
                // another flight down, and in the end do without a basement.
                for (int variant = 0; variant < 6 && layout.Basement == null; variant++)
                {
                    StairPlan down = PlanMixed(seed, -1, storey, hx, hz, Rect.zero, false, null, zone, mass, variant);
                    if (down == null)
                    {
                        continue;
                    }
                    bool lobbyIsRoof = floors == 1 && !layout.SealedTop;
                    StairPlan lobby = floors == 1 && layout.NoRoofStair
                        ? null
                        : PlanMixed(seed, 0, rises[0], hx, hz, lobbyIsRoof && hasObstacle ? roofObstacle : Rect.zero, true, down, zone, mass);
                    if (lobby != null || floors == 1 && layout.NoRoofStair)
                    {
                        layout.Basement = down;
                        layout.Floors[0] = lobby;
                        previous = lobby ?? down;
                        firstFloor = 1;
                    }
                }
            }
            for (int f = firstFloor; f < floors; f++)
            {
                bool roof = f == floors - 1 && !layout.SealedTop;
                if (f == floors - 1 && layout.NoRoofStair)
                {
                    // Nothing above the top storey to climb to (or a sealed block standing on the roof).
                    layout.Floors[f] = null;
                    break;
                }
                layout.Floors[f] = PlanMixed(seed, f, rises[f], hx, hz, roof && hasObstacle ? roofObstacle : Rect.zero, f == 0, previous, zone, mass);
                if (layout.Floors[f] == null && roof && hasObstacle)
                {
                    // Nothing cleared the volume on the roof by its margin: settle for clearing the volume itself.
                    layout.Floors[f] = PlanMixed(seed, f, rises[f], hx, hz, Inflate(roofObstacle, -0.4f), f == 0, previous, zone, mass);
                }
                previous = layout.Floors[f] ?? previous;
            }
            return layout;
        }

        // ---- choosing ----

        private static bool WantsCore(BuildingMass mass, ZoneKind zone, int floors, int seed)
        {
            if (floors < 3)
            {
                return false;
            }
            bool tall = mass == BuildingMass.Tower || mass == BuildingMass.Supertall || mass == BuildingMass.Slab;
            float chance = tall ? 0.65f : (zone == ZoneKind.Core || zone == ZoneKind.Urban) ? 0.35f : 0.08f;
            return Unit(seed, 0x51A1) < chance;
        }

        private static readonly StairKind[] Fallbacks =
        {
            StairKind.Switchback, StairKind.L, StairKind.Wrap, StairKind.Straight, StairKind.Landing,
        };

        /// <summary>The kind a floor would like, by what kind of place it is, before the room has its say.</summary>
        private static StairKind Preferred(int seed, int floor, ZoneKind zone, BuildingMass mass)
        {
            float roll = Unit(seed, 0x7000 + Mathf.FloorToInt(floor / 3.0f));
            bool downtown = zone == ZoneKind.Core || zone == ZoneKind.Urban;
            bool industrial = zone == ZoneKind.Industrial;
            float straight, landing, l, back;
            if (downtown) { straight = 0.15f; landing = 0.05f; l = 0.25f; back = 0.30f; }
            else if (industrial) { straight = 0.35f; landing = 0.25f; l = 0.10f; back = 0.20f; }
            else { straight = 0.30f; landing = 0.12f; l = 0.28f; back = 0.18f; }
            if (roll < straight) { return StairKind.Straight; }
            roll -= straight;
            if (roll < landing) { return StairKind.Landing; }
            roll -= landing;
            if (roll < l) { return StairKind.L; }
            roll -= l;
            if (roll < back) { return StairKind.Switchback; }
            return StairKind.Wrap;
        }

        // ---- shapes ----

        private struct FBox
        {
            public float U0, V0, U1, V1;
            public int Level;
        }

        private sealed class Shape
        {
            public StairKind Kind;
            public int Flights;
            public readonly List<FBox> Boxes = new List<FBox>();
            public Rect Entry;
            public Rect Exit;
            public int Levels;

            public void Add(float u0, float v0, float u1, float v1, int level)
            {
                Boxes.Add(new FBox { U0 = Mathf.Min(u0, u1), V0 = Mathf.Min(v0, v1), U1 = Mathf.Max(u0, u1), V1 = Mathf.Max(v0, v1), Level = level });
            }

            public Rect Bounds()
            {
                float u0 = Mathf.Min(Entry.xMin, Exit.xMin), u1 = Mathf.Max(Entry.xMax, Exit.xMax);
                float v0 = Mathf.Min(Entry.yMin, Exit.yMin), v1 = Mathf.Max(Entry.yMax, Exit.yMax);
                foreach (FBox b in Boxes)
                {
                    u0 = Mathf.Min(u0, b.U0); u1 = Mathf.Max(u1, b.U1);
                    v0 = Mathf.Min(v0, b.V0); v1 = Mathf.Max(v1, b.V1);
                }
                return Rect.MinMaxRect(u0, v0, u1, v1);
            }
        }

        private static Rect R(float u0, float v0, float u1, float v1) => Rect.MinMaxRect(Mathf.Min(u0, u1), Mathf.Min(v0, v1), Mathf.Max(u0, u1), Mathf.Max(v0, v1));

        /// <summary>The treads per flight a kind needs to make <paramref name="need"/> risers (landings count as one each).</summary>
        private static int FlightSteps(StairKind kind, int flights, int need)
        {
            int n;
            switch (kind)
            {
                case StairKind.Straight: n = need; break;
                case StairKind.Landing:
                case StairKind.L: n = Mathf.CeilToInt((need - 1) / 2.0f); break;
                case StairKind.Wrap: n = Mathf.CeilToInt((need - 3) / 4.0f); break;
                default: n = Mathf.CeilToInt((need - (flights - 1)) / (float)flights); break;
            }
            return Mathf.Max(2, n);
        }

        private static Shape MakeShape(StairKind kind, int flights, int n)
        {
            float t = Tread, w = Width;
            var s = new Shape { Kind = kind, Flights = flights };
            switch (kind)
            {
                case StairKind.Straight:
                    s.Flights = 1;
                    for (int i = 0; i < n; i++)
                    {
                        s.Add(i * t, 0, (i + 1) * t, w, i + 1);
                    }
                    s.Levels = n;
                    s.Entry = R(-Cell, 0, 0, w);
                    s.Exit = R(n * t, 0, n * t + Cell, w);
                    break;

                case StairKind.Landing:
                    s.Flights = 2;
                    for (int i = 0; i < n; i++)
                    {
                        s.Add(i * t, 0, (i + 1) * t, w, i + 1);
                    }
                    s.Add(n * t, 0, n * t + w, w, n + 1);
                    for (int j = 0; j < n; j++)
                    {
                        s.Add(n * t + w + j * t, 0, n * t + w + (j + 1) * t, w, n + 2 + j);
                    }
                    s.Levels = 2 * n + 1;
                    s.Entry = R(-Cell, 0, 0, w);
                    s.Exit = R(2 * n * t + w, 0, 2 * n * t + w + Cell, w);
                    break;

                case StairKind.L:
                    s.Flights = 2;
                    for (int i = 0; i < n; i++)
                    {
                        s.Add(i * t, 0, (i + 1) * t, w, i + 1);
                    }
                    s.Add(n * t, 0, n * t + w, w, n + 1);
                    for (int j = 0; j < n; j++)
                    {
                        s.Add(n * t, w + j * t, n * t + w, w + (j + 1) * t, n + 2 + j);
                    }
                    s.Levels = 2 * n + 1;
                    s.Entry = R(-Cell, 0, 0, w);
                    s.Exit = R(n * t, w + n * t, n * t + w, w + n * t + Cell);
                    break;

                case StairKind.Switchback:
                    for (int i = 0; i < flights; i++)
                    {
                        float v0 = i * w, v1 = v0 + w;
                        for (int j = 0; j < n; j++)
                        {
                            int level = i * (n + 1) + j + 1;
                            if (i % 2 == 0)
                            {
                                s.Add(j * t, v0, (j + 1) * t, v1, level);
                            }
                            else
                            {
                                s.Add(n * t - (j + 1) * t, v0, n * t - j * t, v1, level);
                            }
                        }
                        if (i < flights - 1)
                        {
                            int landing = (i + 1) * (n + 1);
                            if (i % 2 == 0)
                            {
                                s.Add(n * t, v0, n * t + w, v1 + w, landing);
                            }
                            else
                            {
                                s.Add(-w, v0, 0, v1 + w, landing);
                            }
                        }
                    }
                    s.Levels = flights * n + flights - 1;
                    s.Entry = R(-Cell, 0, 0, w);
                    if (flights % 2 == 0)
                    {
                        s.Exit = R(-Cell, (flights - 1) * w, 0, flights * w);
                    }
                    else
                    {
                        s.Exit = R(n * t, (flights - 1) * w, n * t + Cell, flights * w);
                    }
                    break;

                default: // Wrap
                {
                    s.Flights = 4;
                    float x = 2 * w + n * t;
                    float z = x;
                    for (int j = 0; j < n; j++)
                    {
                        s.Add(w + j * t, 0, w + (j + 1) * t, w, j + 1);
                    }
                    s.Add(x - w, 0, x, w, n + 1);
                    for (int j = 0; j < n; j++)
                    {
                        s.Add(x - w, w + j * t, x, w + (j + 1) * t, n + 2 + j);
                    }
                    s.Add(x - w, z - w, x, z, 2 * n + 2);
                    for (int j = 0; j < n; j++)
                    {
                        s.Add(x - w - (j + 1) * t, z - w, x - w - j * t, z, 2 * n + 3 + j);
                    }
                    s.Add(0, z - w, w, z, 3 * n + 3);
                    for (int j = 0; j < n; j++)
                    {
                        s.Add(0, z - w - (j + 1) * t, w, z - w - j * t, 3 * n + 4 + j);
                    }
                    s.Levels = 4 * n + 3;
                    s.Entry = R(0, 0, w, w);
                    s.Exit = R(0, 0, w, w);
                    break;
                }
            }
            return s;
        }

        // ---- placing ----

        private struct Xf
        {
            public bool Swap, FlipU, FlipV;
            public float U0, U1, V0, V1;
            public float Dx, Dz;
        }

        private static Rect Map(Rect r, in Xf x)
        {
            float a = r.xMin, b = r.xMax, c = r.yMin, d = r.yMax;
            if (x.FlipU)
            {
                float na = x.U0 + x.U1 - b, nb = x.U0 + x.U1 - a;
                a = na; b = nb;
            }
            if (x.FlipV)
            {
                float nc = x.V0 + x.V1 - d, nd = x.V0 + x.V1 - c;
                c = nc; d = nd;
            }
            if (x.Swap)
            {
                return Rect.MinMaxRect(c + x.Dx, a + x.Dz, d + x.Dx, b + x.Dz);
            }
            return Rect.MinMaxRect(a + x.Dx, c + x.Dz, b + x.Dx, d + x.Dz);
        }

        private static bool Overlap(Rect a, Rect b)
        {
            const float e = 0.01f;
            return a.xMin < b.xMax - e && a.xMax > b.xMin + e && a.yMin < b.yMax - e && a.yMax > b.yMin + e;
        }

        /// <summary>Everything that decides where a stair may stand on one floor.</summary>
        private struct Context
        {
            public float Hx, Hz;
            public float Rise;
            public Rect Door;          // zero when the floor has no front door to keep clear
            public Rect Avoid;         // the hole must not overlap this (a volume standing on the roof)
            public StairPlan Previous; // the stair that arrives on this floor
            public float Pad;          // extra clearance round the stair (a core's walls)
            public int Floor;          // which floor this is (the lobby is 0)
            public float OuterHx, OuterHz;   // half extents of the whole footprint (the carved room is cut against these)
            public bool Reach;         // check that a body can walk from where it arrives to the stair
        }

        /// <summary>The stair of the given shape on one floor, at the given placement; null if anything is wrong with it.</summary>
        private static StairPlan Realize(Shape shape, in Xf x, in Context c, bool enclosed)
        {
            float riser = c.Rise / shape.Levels;
            int count = shape.Boxes.Count;
            var plan = new StairPlan
            {
                Kind = shape.Kind,
                Flights = shape.Flights,
                Enclosed = enclosed,
                Treads = new Rect[count],
                Tops = new float[count],
                Rise = c.Rise,
                Risers = shape.Levels,
                AngleDegrees = Mathf.Atan2(riser, Tread) * Mathf.Rad2Deg,
            };

            bool any = false;
            float hx0 = 0, hx1 = 0, hz0 = 0, hz1 = 0;
            for (int i = 0; i < count; i++)
            {
                FBox b = shape.Boxes[i];
                plan.Treads[i] = Map(Rect.MinMaxRect(b.U0, b.V0, b.U1, b.V1), x);
                plan.Tops[i] = b.Level * riser;
                if (plan.Tops[i] > c.Rise - HeadClear)
                {
                    Rect r = plan.Treads[i];
                    if (!any)
                    {
                        hx0 = r.xMin; hx1 = r.xMax; hz0 = r.yMin; hz1 = r.yMax;
                        any = true;
                    }
                    else
                    {
                        hx0 = Mathf.Min(hx0, r.xMin); hx1 = Mathf.Max(hx1, r.xMax);
                        hz0 = Mathf.Min(hz0, r.yMin); hz1 = Mathf.Max(hz1, r.yMax);
                    }
                }
            }
            plan.Hole = Rect.MinMaxRect(hx0, hz0, hx1, hz1);
            plan.Entry = Map(shape.Entry, x);
            plan.Exit = Map(shape.Exit, x);
            plan.Bounds = Map(shape.Bounds(), x);

            // ---- the rules ----
            Rect room = Rect.MinMaxRect(-c.Hx, -c.Hz, c.Hx, c.Hz);
            // A core has its doorway in the end wall (directly away from the first step), and needs a lobby in front of it: room to stand and turn,
            // not a gap a body cannot squeeze down between the shaft and the outer wall.
            float padLeft = c.Pad, padRight = c.Pad, padBack = c.Pad, padFront = c.Pad;
            if (enclosed)
            {
                Vector2 away = plan.Entry.center - plan.Treads[0].center;
                plan.DoorSide = Mathf.Abs(away.x) > Mathf.Abs(away.y) ? (away.x < 0.0f ? 0 : 1) : (away.y < 0.0f ? 2 : 3);
                switch (plan.DoorSide)
                {
                    case 0: padLeft = DoorLobby; break;
                    case 1: padRight = DoorLobby; break;
                    case 2: padBack = DoorLobby; break;
                    default: padFront = DoorLobby; break;
                }
            }
            Rect padded = Rect.MinMaxRect(plan.Bounds.xMin - padLeft, plan.Bounds.yMin - padBack, plan.Bounds.xMax + padRight, plan.Bounds.yMax + padFront);
            const float slack = 0.001f;
            if (padded.xMin < room.xMin - slack || padded.xMax > room.xMax + slack || padded.yMin < room.yMin - slack || padded.yMax > room.yMax + slack)
            {
                return null;
            }
            if (c.Door.width > 0.0f && Overlap(padded, c.Door))
            {
                return null;
            }
            if (c.Avoid.width > 0.0f && Overlap(plan.Hole, c.Avoid))
            {
                return null;
            }
            if (Overlap(plan.Exit, plan.Hole))
            {
                return null;
            }

            StairPlan prev = c.Previous;
            if (prev != null)
            {
                if (Overlap(plan.Entry, prev.Hole))
                {
                    return null;
                }
                for (int i = 0; i < count; i++)
                {
                    if (Overlap(plan.Treads[i], prev.Exit))
                    {
                        return null;
                    }
                    // A step standing over the opening below must clear whatever is climbing through it.
                    for (int j = 0; j < prev.Treads.Length; j++)
                    {
                        if (Overlap(plan.Treads[i], prev.Treads[j])
                            && prev.Rise + plan.Tops[i] - Soffit - prev.Tops[j] < StackClear)
                        {
                            return null;
                        }
                    }
                }
            }

            // A body must be able to WALK from where it arrives on this floor (off the last stair, or in at the front door) to the foot of this one:
            // past the shaft walls and their doorway, the carved room's partition, the opening below. A stair that fits and clears the ceiling can still
            // be walled in by its own neighbours.
            if (c.Reach && !CanReach(plan, prev, c))
            {
                // The carved room may be what shuts it in (a partition across the way): give that floor no room rather than no stair.
                plan.SuppressRoom = true;
                if (!CanReach(plan, prev, c))
                {
                    return null;
                }
            }

            return plan;
        }

        // ---- walking on a floor ----

        private static bool CanReach(StairPlan plan, StairPlan prev, in Context c)
        {
            List<Rect> solids = FloorSolids(plan, prev, c.Floor, c.OuterHx, c.OuterHz);
            // From the front door (on the lobby), and from the stair that arrives here (a basement flight, on the lobby).
            if ((c.Floor == 0 || prev == null) && WalkPath(solids, c.Hx, c.Hz, new Vector2(0.0f, c.Hz - 1.0f), plan.EntryPoint) == null)
            {
                return false;
            }
            return prev == null || WalkPath(solids, c.Hx, c.Hz, prev.ExitPoint, plan.EntryPoint) != null;
        }

        /// <summary>
        /// The solid things on one floor that a body cannot walk through, as rectangles in the building frame: the stair's own steps (the lowest ones are the
        /// way on, so not those), a core's walls (with the doorway left open), the opening in the floor below the stair that arrives, and the carved room's
        /// partitions (with their doorway). <paramref name="outerHx"/> and <paramref name="outerHz"/> are the footprint's half extents.
        /// </summary>
        public static List<Rect> FloorSolids(StairPlan plan, StairPlan arriving, int floor, float outerHx, float outerHz)
        {
            var solids = new List<Rect>();
            if (plan != null)
            {
                for (int i = 0; i < plan.Treads.Length; i++)
                {
                    // A step whose underside is lower than a body is tall is a wall to someone beside it.
                    if (Mathf.Max(0.0f, plan.Tops[i] - Soffit) < 2.8f && plan.Tops[i] > 0.45f)
                    {
                        solids.Add(plan.Treads[i]);
                    }
                }
                if (plan.Enclosed)
                {
                    float t = CoreWall;
                    Rect b = plan.Bounds;
                    Rect[] sides =
                    {
                        Rect.MinMaxRect(b.xMin - t, b.yMin - t, b.xMin, b.yMax + t),
                        Rect.MinMaxRect(b.xMax, b.yMin - t, b.xMax + t, b.yMax + t),
                        Rect.MinMaxRect(b.xMin, b.yMin - t, b.xMax, b.yMin),
                        Rect.MinMaxRect(b.xMin, b.yMax, b.xMax, b.yMax + t),
                    };
                    for (int side = 0; side < 4; side++)
                    {
                        Rect wall = sides[side];
                        if (side != plan.DoorSide)
                        {
                            solids.Add(wall);
                            continue;
                        }
                        bool alongZ = side < 2;
                        float a0 = alongZ ? wall.yMin : wall.xMin, a1 = alongZ ? wall.yMax : wall.xMax;
                        float mid = (a0 + a1) * 0.5f, g0 = mid - CoreDoor * 0.5f, g1 = mid + CoreDoor * 0.5f;
                        if (alongZ)
                        {
                            solids.Add(Rect.MinMaxRect(wall.xMin, wall.yMin, wall.xMax, g0));
                            solids.Add(Rect.MinMaxRect(wall.xMin, g1, wall.xMax, wall.yMax));
                        }
                        else
                        {
                            solids.Add(Rect.MinMaxRect(wall.xMin, wall.yMin, g0, wall.yMax));
                            solids.Add(Rect.MinMaxRect(g1, wall.yMin, wall.xMax, wall.yMax));
                        }
                    }
                }
            }
            if (arriving != null && arriving.Hole.width > 0.0f)
            {
                solids.Add(arriving.Hole);
            }

            BuildingInterior.Room room = RoomFor(Mathf.Max(floor, 0), outerHx, outerHz, plan, arriving);
            if (room.Exists)
            {
                const float t = 0.2f;
                solids.Add(Rect.MinMaxRect(room.DoorWallX - t * 0.5f, room.Plan.yMin, room.DoorWallX + t * 0.5f, room.DoorZ0));
                solids.Add(Rect.MinMaxRect(room.DoorWallX - t * 0.5f, room.DoorZ1, room.DoorWallX + t * 0.5f, room.Plan.yMax));
                solids.Add(Rect.MinMaxRect(room.Plan.xMin, room.Plan.yMax - t * 0.5f, room.Plan.xMax, room.Plan.yMax + t * 0.5f));
            }
            return solids;
        }

        /// <summary>
        /// A route a body (0.45 m radius) can walk across a floor (half extents <paramref name="hx"/> by <paramref name="hz"/>, centred on the origin) between
        /// two points, round the given solid rectangles; null where there is none. Waypoints about every metre and a half.
        /// </summary>
        public static List<Vector2> WalkPath(List<Rect> solids, float hx, float hz, Vector2 from, Vector2 to)
        {
            const float radius = 0.45f;
            float cell = Mathf.Max(0.25f, Mathf.Sqrt(4.0f * hx * hz / 40000.0f));
            int nx = Mathf.Max(1, Mathf.CeilToInt(2.0f * hx / cell)), nz = Mathf.Max(1, Mathf.CeilToInt(2.0f * hz / cell));
            var free = new bool[nx * nz];
            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    float x = -hx + (ix + 0.5f) * cell, z = -hz + (iz + 0.5f) * cell;
                    bool ok = x > -hx + radius && x < hx - radius && z > -hz + radius && z < hz - radius;
                    for (int s = 0; s < solids.Count && ok; s++)
                    {
                        Rect r = solids[s];
                        ok = !(x > r.xMin - radius && x < r.xMax + radius && z > r.yMin - radius && z < r.yMax + radius);
                    }
                    free[iz * nx + ix] = ok;
                }
            }
            int Index(Vector2 p) => Mathf.Clamp(Mathf.FloorToInt((p.y + hz) / cell), 0, nz - 1) * nx + Mathf.Clamp(Mathf.FloorToInt((p.x + hx) / cell), 0, nx - 1);
            int start = Index(from), goal = Index(to);
            if (!free[start] || !free[goal])
            {
                return null;
            }
            var parent = new int[nx * nz];
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = -2;
            }
            var queue = new Queue<int>();
            queue.Enqueue(start);
            parent[start] = -1;
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                if (c == goal)
                {
                    break;
                }
                int cx = c % nx, cz = c / nx;
                for (int d = 0; d < 4; d++)
                {
                    int ax = cx + (d == 0 ? 1 : d == 1 ? -1 : 0), az = cz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (ax < 0 || az < 0 || ax >= nx || az >= nz)
                    {
                        continue;
                    }
                    int a = az * nx + ax;
                    if (free[a] && parent[a] == -2)
                    {
                        parent[a] = c;
                        queue.Enqueue(a);
                    }
                }
            }
            if (parent[goal] == -2)
            {
                return null;
            }
            var path = new List<Vector2>();
            int step = 0;
            for (int c = goal; c != -1; c = parent[c], step++)
            {
                if (step % Mathf.Max(1, Mathf.RoundToInt(1.5f / cell)) == 0 || c == start)
                {
                    path.Add(new Vector2(-hx + ((c % nx) + 0.5f) * cell, -hz + ((c / nx) + 0.5f) * cell));
                }
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Tries every sensible placement of a shape (corners, the middle of each wall, and the centre for a wrap; eight orientations each),
        /// beginning at a point and an orientation that vary from building to building so they do not all hug the same corner.
        /// </summary>
        private static StairPlan Place(Shape shape, in Context c, int salt, bool enclosed, out Xf found)
        {
            Rect fb = shape.Bounds();
            float pad = c.Pad;
            int positions = shape.Kind == StairKind.Wrap ? 9 : 8;
            int startPosition = Mathf.FloorToInt(Unit(salt, 0xA1) * positions);
            int startOrientation = Mathf.FloorToInt(Unit(salt, 0xB2) * 8.0f);

            for (int pi = 0; pi < positions; pi++)
            {
                int position = (startPosition + pi) % positions;
                for (int oi = 0; oi < 8; oi++)
                {
                    int orientation = (startOrientation + oi) % 8;
                    var x = new Xf
                    {
                        Swap = (orientation & 1) != 0,
                        FlipU = (orientation & 2) != 0,
                        FlipV = (orientation & 4) != 0,
                        U0 = fb.xMin, U1 = fb.xMax, V0 = fb.yMin, V1 = fb.yMax,
                    };
                    float bw = (x.Swap ? fb.height : fb.width) + 2.0f * pad;
                    float bh = (x.Swap ? fb.width : fb.height) + 2.0f * pad;
                    if (bw > 2.0f * c.Hx || bh > 2.0f * c.Hz)
                    {
                        continue;
                    }

                    float left = -c.Hx, right = c.Hx - bw, midX = -bw * 0.5f;
                    float back = -c.Hz, front = c.Hz - bh, midZ = -bh * 0.5f;
                    float px, pz;
                    switch (position)
                    {
                        case 0: px = left; pz = back; break;
                        case 1: px = right; pz = back; break;
                        case 2: px = right; pz = front; break;
                        case 3: px = left; pz = front; break;
                        case 4: px = midX; pz = back; break;
                        case 5: px = midX; pz = front; break;
                        case 6: px = left; pz = midZ; break;
                        case 7: px = right; pz = midZ; break;
                        default: px = midX; pz = midZ; break;
                    }
                    x.Dx = px + pad - (x.Swap ? fb.yMin : fb.xMin);
                    x.Dz = pz + pad - (x.Swap ? fb.xMin : fb.yMin);

                    StairPlan plan = Realize(shape, x, c, enclosed);
                    if (plan != null)
                    {
                        found = x;
                        return plan;
                    }
                }
            }
            found = default;
            return null;
        }

        private static StairPlan PlanMixed(int seed, int floor, float rise, float hx, float hz, Rect avoid, bool frontDoor, StairPlan previous, ZoneKind zone, BuildingMass mass, int variant = 0)
        {
            var c = new Context
            {
                Hx = hx, Hz = hz, Rise = rise,
                Door = frontDoor ? Rect.MinMaxRect(-1.7f, hz - 2.6f, 1.7f, hz) : Rect.zero,
                Avoid = avoid, Previous = previous, Pad = 0.0f,
                Floor = floor, OuterHx = hx + OuterWall, OuterHz = hz + OuterWall, Reach = floor >= 0,
            };
            int need = Mathf.CeilToInt(rise / MaxRiser - 0.0001f);
            StairKind first = Preferred(seed + variant * 31, floor, zone, mass);

            var order = new List<(StairKind, int)>(8) { (first, first == StairKind.Switchback ? 2 : 0) };
            foreach (StairKind kind in Fallbacks)
            {
                if (kind == StairKind.Switchback)
                {
                    foreach (int flights in new[] { 2, 3, 4 })
                    {
                        order.Add((kind, flights));
                    }
                }
                else
                {
                    order.Add((kind, 0));
                }
            }

            int salt = Mix(seed, floor + 77 + variant * 1013);
            foreach ((StairKind kind, int flights) in order)
            {
                int k = kind == StairKind.Switchback ? flights : 0;
                int n = FlightSteps(kind, k, need);
                Shape shape = MakeShape(kind, k, n);
                StairPlan plan = Place(shape, c, salt, false, out _);
                if (plan != null)
                {
                    return plan;
                }
            }
            return null;
        }

        /// <summary>Plans a walled-in U stair that stands in the same place on every floor, or reports that this building cannot have one.</summary>
        private static bool TryCore(Layout layout, float hx, float hz, Rect door, bool hasObstacle, Rect obstacle, float[] rises, float maxRise, bool basement, int seed)
        {
            int need = Mathf.CeilToInt(maxRise / MaxRiser - 0.0001f);
            int n = FlightSteps(StairKind.Switchback, 2, need);
            Shape shape = MakeShape(StairKind.Switchback, 2, n);

            // The place is chosen against the busiest floor (the lobby, with its door and the opening of any basement flight) and the roof.
            var c = new Context
            {
                Hx = hx, Hz = hz, Rise = maxRise, Door = door,
                Avoid = !layout.NoRoofStair && hasObstacle ? obstacle : Rect.zero, Pad = CoreWall,
                Floor = 0, OuterHx = hx + OuterWall, OuterHz = hz + OuterWall, Reach = true,
            };
            StairPlan lead = Place(shape, c, Mix(seed, 991), true, out Xf placement);
            if (lead == null)
            {
                return false;
            }

            StairPlan previous = null;
            if (basement)
            {
                var cb = c; cb.Rise = layout.Storey; cb.Avoid = Rect.zero; cb.Door = Rect.zero; cb.Reach = false;
                layout.Basement = Realize(shape, placement, cb, true);
                if (layout.Basement == null)
                {
                    return false;
                }
                previous = layout.Basement;
            }
            for (int f = 0; f < layout.FloorCount; f++)
            {
                if (f == layout.FloorCount - 1 && layout.NoRoofStair)
                {
                    break;
                }
                var cf = c;
                cf.Rise = rises[f];
                cf.Floor = f;
                cf.Previous = previous;
                bool top = f == layout.FloorCount - 1;
                cf.Avoid = top && hasObstacle ? obstacle : Rect.zero;
                cf.Door = f == 0 ? door : Rect.zero;
                StairPlan plan = Realize(shape, placement, cf, true);
                if (plan == null)
                {
                    layout.Basement = null;
                    return false;
                }
                layout.Floors[f] = plan;
                previous = plan;
            }
            return true;
        }

        // ---- the carved room, dodging the stairs ----

        /// <summary>
        /// The room a floor carves out of a back corner, in whichever corner the stairs leave free: the usual one, else the other, else none.
        /// </summary>
        public static BuildingInterior.Room RoomFor(int floor, float halfX, float halfZ, StairPlan stair, StairPlan arriving)
        {
            if (stair != null && stair.SuppressRoom)
            {
                return default;
            }
            BuildingInterior.Room room = BuildingInterior.RoomFor(floor, halfX, halfZ);
            if (!room.Exists)
            {
                return room;
            }
            if (!Clashes(room, stair, arriving))
            {
                return room;
            }
            BuildingInterior.Room other = BuildingInterior.RoomIn(room.Plan.center.x > 0.0f, halfX, halfZ);
            return other.Exists && !Clashes(other, stair, arriving) ? other : default;
        }

        private static bool Clashes(in BuildingInterior.Room room, StairPlan stair, StairPlan arriving)
        {
            Rect r = Rect.MinMaxRect(room.Plan.xMin - 0.4f, room.Plan.yMin - 0.4f, room.Plan.xMax + 0.4f, room.Plan.yMax + 0.4f);
            if (stair != null && (Overlap(r, stair.Bounds) || stair.Enclosed && Overlap(r, Inflate(stair.Bounds, CoreWall))))
            {
                return true;
            }
            return arriving != null && (Overlap(r, arriving.Exit) || Overlap(r, arriving.Hole));
        }

        private static Rect Inflate(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        // ---- hashing ----

        private static int Mix(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA6Bu;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        private static float Unit(int seed, int salt) => (Mix(seed, salt) & 0xFFFFFF) / (float)0x1000000;
    }
}
