using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// THE TUTORIAL HOUSE, designed by hand (docs/OLD_GUYS_HOUSE.md): every other building is generated from the world seed, this one is drawn
    /// room by room. Pure data in the house's own frame: metres, x east, z north (away from the street), y up, the origin at the front corner of
    /// the house at street level, the street side facing -z. OldGuysHouseBuilder (View) turns it into meshes, colliders and furniture.
    ///
    /// Sized for the 2.6 m player with the camera 5 m behind (owner, 2026-10-31: "it feel very cramped in these rooms ... make slightly taller
    /// ceilings and slightly bigger rooms ... its a third person game so with the camera positioning it feels very tight"): a third-person game
    /// builds its rooms about twice their real size and keeps the furniture real (docs/OLD_GUYS_HOUSE.md, "Sizes"), so the rooms are 10 to 12 m
    /// across where the camera turns, halls 4.6 m, doors 2 x 3.6 m and storeys 5 m with 4.7 m clear.
    /// </summary>
    public static class OldGuysHouse
    {
        public const float Storey = 5.0f;
        public const float SlabThickness = 0.3f;
        public const float OuterWallThickness = 0.3f;
        public const float InnerWallThickness = 0.2f;
        public const float DoorWidth = 2.0f;
        public const float DoorHeight = 3.6f;
        public const float RailHeight = 1.2f;

        /// <summary>Clear height under a ceiling: a storey less its slab.</summary>
        public const float ClearHeight = Storey - SlabThickness;

        /// <summary>The smallest room the camera fits in, a metre past its 5 m behind the player; only the WC and the gallery walk are smaller.</summary>
        public const float CameraRoomSide = 6.0f;

        /// <summary>A hall or a corridor: the player's width four times over, room for the camera beside them.</summary>
        public const float HallWidth = 4.5f;

        /// <summary>The house itself under its roof: the main block, the plan's own numbers (Plan.TutorialLot).</summary>
        public const float Width = (float)Plan.TutorialLot.HouseX1;

        public const float Depth = (float)Plan.TutorialLot.HouseZ1;

        /// <summary>Stairs: the stair kit's tread (BuildingStairs) and a storey in 22 risers of 0.23 m, under 33 degrees.</summary>
        public const float Tread = 0.36f;

        public const int RisersPerStorey = 22;

        public static float Riser => Storey / RisersPerStorey;

        /// <summary>A stringer step hangs this far under its tread, so the flight above another leaves headroom under it.</summary>
        public const float StepDepth = 0.55f;

        /// <summary>The top of the walls and the flat ceiling under the roof.</summary>
        public const float EavesY = 3 * Storey;

        /// <summary>
        /// A modern single-slope roof (owner, 2026-10-31: "make it a more modern home roof maybe flat or slanted"): its top at the front edge, over
        /// the driveway, and at the back edge, about 10 degrees between. The walls rise under it to meet it.
        /// </summary>
        public const float RoofHighY = (float)Plan.TutorialLot.HouseHeight;
        public const float RoofLowY = EavesY + 0.6f;
        public const float RoofOverhang = 0.6f;
        public const float RoofFrontOverhang = 1.5f;
        public const float RoofThickness = 0.35f;

        /// <summary>The height of the roof's top surface over a line across the house (z on the plan, the overhangs included).</summary>
        public static float RoofTopAt(float z) =>
            RoofHighY + (RoofLowY - RoofHighY) * (z + RoofFrontOverhang) / (Depth + RoofFrontOverhang + RoofOverhang);

        public static float LevelY(int level) => level * Storey;

        public enum Finish
        {
            Plaster,
            Wallpaper,
            Brick,
            Concrete,
            Tile,
            Wood,
            Carpet,
            Paving,
            Deck,
            Render,
            Roof,
            Metal,
        }

        public enum OpeningKind
        {
            Door,
            Arch,
            Window,
            FrontDoor,
            RollUpDoor,
        }

        /// <summary>A rectangle on the ground plan.</summary>
        public readonly struct Area
        {
            public readonly float X0, Z0, X1, Z1;

            public Area(float x0, float z0, float x1, float z1)
            {
                X0 = x0; Z0 = z0; X1 = x1; Z1 = z1;
            }

            public bool Contains(float x, float z) => x >= X0 && x <= X1 && z >= Z0 && z <= Z1;

            public bool Overlaps(Area o) => X0 < o.X1 - 1e-3f && o.X0 < X1 - 1e-3f && Z0 < o.Z1 - 1e-3f && o.Z0 < Z1 - 1e-3f;

            public Vector2 Centre => new Vector2((X0 + X1) * 0.5f, (Z0 + Z1) * 0.5f);
        }

        public sealed class Room
        {
            public string Name;
            public int Level;
            public Area[] Floors;
            public Finish Floor;
            public Color FloorTint;
            public Color WallTint;

            /// <summary>Under the sky (the driveway, the porch): no ceiling light, no indoor look.</summary>
            public bool Outdoors;

            /// <summary>Open up through the floor above (the living room, two storeys tall under the gallery).</summary>
            public bool DoubleHeight;

            /// <summary>Allowed under <see cref="CameraRoomSide"/>: the WC, the gallery's walk over the living room.</summary>
            public bool Small;

            public float FloorY => LevelY(Level);

            public float CeilingY => DoubleHeight ? EavesY - SlabThickness : FloorY + ClearHeight;

            public bool Contains(float x, float z)
            {
                foreach (Area a in Floors) if (a.Contains(x, z)) return true;
                return false;
            }
        }

        public readonly struct Opening
        {
            public readonly float From, To, Sill, Head;
            public readonly OpeningKind Kind;

            public Opening(float from, float to, float sill, float head, OpeningKind kind)
            {
                From = from; To = to; Sill = sill; Head = head; Kind = kind;
            }

            public float Width => To - From;

            /// <summary>A way through for a body (not a window, nor the boarded front door).</summary>
            public bool Passable => Kind == OpeningKind.Door || Kind == OpeningKind.Arch;
        }

        /// <summary>A straight wall: along x at z = <see cref="At"/> (AlongX) or along z at x = At, from From to To, from Y0 up to Y1.</summary>
        public sealed class Wall
        {
            public bool AlongX;
            public float At, From, To, Y0, Y1, Thickness;
            public bool Outer;
            public Opening[] Openings;
        }

        /// <summary>A solid block: the porch, the landing at the top of the outdoor steps, the footing under the kitchen.</summary>
        public readonly struct Block
        {
            public readonly Vector3 Min, Max;
            public readonly Finish Top;
            public readonly Color TopTint;

            public Block(Vector3 min, Vector3 max, Finish top, Color topTint)
            {
                Min = min; Max = max; Top = top; TopTint = topTint;
            }
        }

        /// <summary>One step or landing of a stair: its tread's area and height, and how far under it the step hangs.</summary>
        public readonly struct Step
        {
            public readonly Area Tread;
            public readonly float Top, Bottom;

            public Step(Area tread, float top, float bottom)
            {
                Tread = tread; Top = top; Bottom = bottom;
            }
        }

        /// <summary>A railing from A to B (x, z) whose foot is at Y: a top rail at RailHeight and posts.</summary>
        public readonly struct Rail
        {
            public readonly Vector2 A, B;
            public readonly float Y;

            public Rail(float ax, float az, float bx, float bz, float y)
            {
                A = new Vector2(ax, az); B = new Vector2(bx, bz); Y = y;
            }
        }

        /// <summary>A sloping handrail along a flight, from (x, y, z) to (x, y, z).</summary>
        public readonly struct Handrail
        {
            public readonly Vector3 From, To;

            public Handrail(Vector3 from, Vector3 to)
            {
                From = from; To = to;
            }
        }

        public enum FurnitureKind
        {
            Car, Workbench, Shelves, OilDrum, Manhole, FuseBox, Boiler, Crate,
            Sofa, Armchair, CoffeeTable, Fireplace, Rug, Bottle,
            Counter, Sink, Stove, Fridge, KnifeBlock, UpperCabinet,
            DiningTable, Chair, Sideboard, Television, Radiator,
            Toilet, Basin, Bathtub, CoatRack, Plant, SmallTable,
            Bed, DoubleBed, Desk, Bookcase, GlassCase, Wardrobe, Nightstand, Dresser, ToyChest, Poster, Drawing,
            CeilingLamp,
        }

        public readonly struct Furniture
        {
            public readonly FurnitureKind Kind;
            public readonly Vector3 Position;
            public readonly float Yaw;
            public readonly Color Tint;

            /// <summary>What is inside it (docs/LOOTING.md), ItemRegistry's ids, in the part numbered <see cref="StashPart"/> (top drawer first, left door first).</summary>
            public readonly string[] Stash;

            public readonly int StashPart;

            /// <summary>A piece at <paramref name="position"/> (its foot, or its back's middle for a wall piece), facing <paramref name="yaw"/> (0 = +z).</summary>
            public Furniture(FurnitureKind kind, Vector3 position, float yaw, Color tint, int stashPart = 0, params string[] stash)
            {
                Kind = kind; Position = position; Yaw = yaw; Tint = tint; StashPart = stashPart; Stash = stash ?? new string[0];
            }
        }

        /// <summary>Where a weapon lies (<see cref="ItemId"/> is ItemRegistry's), and how it is turned: it stays exactly there until picked up.</summary>
        public readonly struct ItemSpot
        {
            public readonly string ItemId;
            public readonly Vector3 Position;
            public readonly Vector3 Euler;

            public ItemSpot(string itemId, Vector3 position, Vector3 euler)
            {
                ItemId = itemId; Position = position; Euler = euler;
            }
        }

        // ---- colours ----

        private static readonly Color Concrete = new Color(0.62f, 0.62f, 0.60f);
        private static readonly Color DarkConcrete = new Color(0.45f, 0.44f, 0.42f);
        private static readonly Color Oak = new Color(0.62f, 0.44f, 0.27f);
        private static readonly Color DarkOak = new Color(0.40f, 0.27f, 0.16f);
        private static readonly Color Cream = new Color(0.93f, 0.88f, 0.76f);
        private static readonly Color WhiteTile = new Color(0.90f, 0.91f, 0.90f);
        public static readonly Color Render = new Color(0.90f, 0.89f, 0.85f);
        public static readonly Color RoofMetal = new Color(0.23f, 0.24f, 0.26f);
        public static readonly Color Decking = new Color(0.55f, 0.40f, 0.26f);
        public static readonly Color Paving = new Color(0.70f, 0.68f, 0.64f);
        public static readonly Color Ceiling = new Color(0.95f, 0.95f, 0.93f);

        // ---- the layout: three bays across (the living room and the rooms over the garage; the stair core; the east rooms), three rows deep ----

        /// <summary>The west bay (garage, living room, kitchen, the grandson's room) ends here; the stair core's bay here; then the east bay.</summary>
        private const float WestBay = 11.0f, CoreBay = 17.0f;

        /// <summary>The front row (stair core, workshop, WC and coats, study) ends here; the hall strip here; then the back row.</summary>
        private const float FrontRow = 7.4f, HallRow = 12.0f;

        // ---- the rooms ----

        public static readonly Room[] Rooms =
        {
            // Street level: dug in under the house.
            RoomOf("Garage", 0, Finish.Concrete, Concrete, new Color(0.78f, 0.78f, 0.76f), new Area(0, 0, WestBay, 17)),
            RoomOf("Stair core (street)", 0, Finish.Concrete, Concrete, Cream, new Area(WestBay, 0, CoreBay, FrontRow)),
            RoomOf("Stair hall", 0, Finish.Concrete, Concrete, Cream, new Area(WestBay, FrontRow, Width, HallRow)),
            RoomOf("Workshop", 0, Finish.Concrete, Concrete, new Color(0.70f, 0.74f, 0.70f), new Area(CoreBay, 0, Width, FrontRow)),
            RoomOf("Cellar", 0, Finish.Concrete, DarkConcrete, new Color(0.62f, 0.40f, 0.32f), new Area(WestBay, HallRow, Width, 17)),

            // Main floor.
            Tall(RoomOf("Living room", 1, Finish.Wood, Oak, Cream, new Area(0, 0, WestBay, HallRow))),
            RoomOf("Entry hall", 1, Finish.Wood, Oak, new Color(0.82f, 0.86f, 0.80f), new Area(WestBay, FlightTopZ, CoreBay, HallRow), new Area(CoreBay, FrontRow, Width, HallRow)),
            Small(RoomOf("WC", 1, Finish.Tile, WhiteTile, new Color(0.80f, 0.90f, 0.92f), new Area(CoreBay, 0, 21.5f, FrontRow))),
            RoomOf("Coats", 1, Finish.Wood, Oak, new Color(0.82f, 0.86f, 0.80f), new Area(21.5f, 0, Width, FrontRow)),
            RoomOf("Kitchen", 1, Finish.Tile, WhiteTile, new Color(0.96f, 0.90f, 0.62f), new Area(0, HallRow, WestBay, Depth)),
            RoomOf("Dining room", 1, Finish.Wood, Oak, new Color(0.70f, 0.78f, 0.62f), new Area(WestBay, HallRow, 18, Depth)),
            RoomOf("Old Guy's den", 1, Finish.Carpet, new Color(0.36f, 0.30f, 0.22f), new Color(0.80f, 0.68f, 0.36f), new Area(18, HallRow, Width, Depth)),

            // Upper floor.
            Small(RoomOf("Gallery", 2, Finish.Wood, Oak, Cream, new Area(8, 0, WestBay, HallRow), new Area(WestBay, FlightTopZ, CoreBay, HallRow))),
            RoomOf("Study", 2, Finish.Wood, DarkOak, new Color(0.30f, 0.45f, 0.36f), new Area(CoreBay, 0, Width, FrontRow)),
            RoomOf("Corridor", 2, Finish.Wood, Oak, new Color(0.88f, 0.86f, 0.80f), new Area(CoreBay, FrontRow, Width, HallRow)),
            RoomOf("Grandson's room", 2, Finish.Carpet, new Color(0.30f, 0.42f, 0.62f), new Color(0.62f, 0.80f, 0.95f), new Area(0, HallRow, WestBay, Depth)),
            RoomOf("Bathroom", 2, Finish.Tile, WhiteTile, new Color(0.62f, 0.84f, 0.82f), new Area(WestBay, HallRow, CoreBay, Depth)),
            RoomOf("Old Guy's bedroom", 2, Finish.Carpet, new Color(0.70f, 0.62f, 0.52f), new Color(0.86f, 0.70f, 0.70f), new Area(CoreBay, HallRow, Width, Depth)),

            // Outside (no ceiling).
            Outside("Driveway", 0, Finish.Paving, Paving, new Area((float)Plan.TutorialLot.LotX0, (float)Plan.TutorialLot.LotZ0, PorchX1, 0)),
            Outside("Porch", 1, Finish.Deck, Decking, new Area(Width, PorchZ0, PorchX1, PorchZ1), new Area(OutdoorStepsX0, OutdoorStepsTopZ, OutdoorStepsX1, PorchZ0)),
        };

        private static Room RoomOf(string name, int level, Finish floor, Color floorTint, Color wallTint, params Area[] floors) =>
            new Room { Name = name, Level = level, Floors = floors, Floor = floor, FloorTint = floorTint, WallTint = wallTint };

        private static Room Tall(Room room)
        {
            room.DoubleHeight = true;
            return room;
        }

        private static Room Small(Room room)
        {
            room.Small = true;
            return room;
        }

        private static Room Outside(string name, int level, Finish floor, Color floorTint, params Area[] floors) =>
            new Room { Name = name, Level = level, Floors = floors, Floor = floor, FloorTint = floorTint, WallTint = Render, Outdoors = true };

        public static Room RoomNamed(string name)
        {
            foreach (Room r in Rooms) if (r.Name == name) return r;
            return null;
        }

        /// <summary>The room at (x, z) on <paramref name="level"/>, or null.</summary>
        public static Room RoomAt(int level, float x, float z)
        {
            foreach (Room r in Rooms) if (r.Level == level && r.Contains(x, z)) return r;
            return null;
        }

        // ---- the walls ----

        public static readonly Wall[] Walls = MakeWalls();

        private static Wall[] MakeWalls()
        {
            float l1 = LevelY(1), l2 = LevelY(2), c = ClearHeight;
            var w = new List<Wall>
            {
                // Street level: the outer walls stand a whole storey (they run past the slab's edge), the inner ones to the ceiling. The stair core's
                // windows are over its landing, so whoever stands on it looks out.
                X(0, Storey, 0, 0, Width, true, RollUp(1.8f, 9.2f, 0), Window(12.6f, 15.4f, 2.9f, 4.4f), Window(18.8f, 21.6f, 1.6f, 3.8f), Window(23.6f, 26.4f, 1.6f, 3.8f)),
                Z(0, Storey, 0, 0, 17, true, Window(6.5f, 10.5f, 1.8f, 3.8f)),
                Z(0, Storey, Width, 0, 17, true, Window(4.4f, 6.9f, 1.6f, 3.8f), Window(13.5f, 15.5f, 2.4f, 3.8f)),
                X(0, Storey, 17, 0, Width, true),
                Z(0, c, WestBay, 0, 17, false, Door(8.7f, 10.7f, 0)),
                Z(0, c, CoreBay, 0, FrontRow, false),
                X(0, c, FrontRow, CoreBay, Width, false, Door(21.5f, 23.5f, 0)),
                X(0, c, HallRow, WestBay, Width, false, Door(13.0f, 15.0f, 0)),

                // Main floor. The living room's front and side are two storeys tall; the front door is on the east, onto the porch.
                X(l1, EavesY, 0, 0, WestBay, true, Window(2, 9, l1 + 1.1f, l1 + 4.0f), Window(2, 9, l2 + 0.8f, l2 + 4.0f)),
                X(l1, l2, 0, WestBay, Width, true, Window(12.6f, 15.4f, l1 + 2.9f, l1 + 4.4f), Window(18.5f, 20.0f, l1 + 2.4f, l1 + 3.8f), Window(23.5f, 26.5f, l1 + 1.4f, l1 + 3.8f)),
                Z(l1, EavesY, 0, 0, HallRow, true, Window(1.2f, 3.4f, l1 + 1.1f, l1 + 4.0f), Window(8.6f, 10.8f, l1 + 1.1f, l1 + 4.0f)),
                Z(l1, l2, 0, HallRow, Depth, true, Window(14.5f, 19.5f, l1 + 1.6f, l1 + 3.8f)),
                Z(l1, l2, Width, 0, Depth, true, Window(2.2f, 5.2f, l1 + 1.4f, l1 + 3.8f), Front(8.7f, 10.7f, l1), Window(14, 20, l1 + 1.2f, l1 + 3.8f)),
                X(l1, l2, Depth, 0, Width, true, Window(4, 8, l1 + 1.6f, l1 + 3.8f), Window(12.5f, 16.5f, l1 + 1.4f, l1 + 3.8f), Window(20.5f, 25.5f, l1 + 1.4f, l1 + 3.8f)),
                Z(l1, l1 + c, WestBay, 0, Depth, false, Arch(7.8f, 11.6f, l1), Door(16, 18, l1)),
                X(l1, l1 + c, HallRow, 0, WestBay, false, Arch(3.5f, 7.5f, l1)),
                X(l1, l1 + c, HallRow, WestBay, Width, false, Door(13.5f, 15.5f, l1), Door(19, 21, l1)),
                Z(l1, l1 + c, CoreBay, 0, FrontRow, false),
                Z(l1, l1 + c, 21.5f, 0, FrontRow, false),
                X(l1, l1 + c, FrontRow, CoreBay, Width, false, Door(18.75f, 20.75f, l1), Door(23.75f, 25.75f, l1)),
                Z(l1, l1 + c, 18, HallRow, Depth, false),

                // Upper floor.
                X(l2, EavesY, 0, WestBay, Width, true, Window(12.6f, 15.4f, l2 + 1.4f, l2 + 3.8f), Window(19, 22, l2 + 1.2f, l2 + 3.8f), Window(23.5f, 26.5f, l2 + 1.2f, l2 + 3.8f)),
                Z(l2, EavesY, 0, HallRow, Depth, true, Window(14.5f, 19.5f, l2 + 1.2f, l2 + 3.8f)),
                Z(l2, EavesY, Width, 0, Depth, true, Window(2.2f, 5.2f, l2 + 1.2f, l2 + 3.8f), Window(8.7f, 10.7f, l2 + 1.4f, l2 + 3.8f), Window(14, 20, l2 + 1.4f, l2 + 3.8f)),
                X(l2, EavesY, Depth, 0, Width, true, Window(4, 8, l2 + 1.2f, l2 + 3.8f), Window(13, 15, l2 + 2.2f, l2 + 3.8f), Window(22.5f, 26.5f, l2 + 1.2f, l2 + 3.8f)),
                X(l2, l2 + c, HallRow, 0, WestBay, false, Door(8.6f, 10.6f, l2)),
                Z(l2, l2 + c, WestBay, 0, FlightTopZ, false),
                Z(l2, l2 + c, CoreBay, 0, HallRow, false, Arch(7.8f, 11.6f, l2)),
                X(l2, l2 + c, FrontRow, CoreBay, Width, false, Door(21.5f, 23.5f, l2)),
                X(l2, l2 + c, HallRow, WestBay, Width, false, Door(13.0f, 15.0f, l2), Door(24.5f, 26.5f, l2)),
                Z(l2, l2 + c, WestBay, HallRow, Depth, false),
                Z(l2, l2 + c, CoreBay, HallRow, Depth, false),

                // The wall between the two flights of the stair core, all the way up.
                new Wall { AlongX = false, At = (FlightWestX1 + FlightEastX0) * 0.5f, From = FlightTurnZ, To = FlightTopZ, Y0 = 0, Y1 = l2 + c, Thickness = 0.3f, Openings = new Opening[0] },
            };
            return w.ToArray();
        }

        private static Wall X(float y0, float y1, float z, float x0, float x1, bool outer, params Opening[] openings) =>
            new Wall { AlongX = true, At = z, From = x0, To = x1, Y0 = y0, Y1 = y1, Thickness = outer ? OuterWallThickness : InnerWallThickness, Outer = outer, Openings = openings };

        private static Wall Z(float y0, float y1, float x, float z0, float z1, bool outer, params Opening[] openings) =>
            new Wall { AlongX = false, At = x, From = z0, To = z1, Y0 = y0, Y1 = y1, Thickness = outer ? OuterWallThickness : InnerWallThickness, Outer = outer, Openings = openings };

        private static Opening Door(float from, float to, float floorY) => new Opening(from, to, floorY, floorY + DoorHeight, OpeningKind.Door);

        private static Opening Arch(float from, float to, float floorY) => new Opening(from, to, floorY, floorY + DoorHeight + 0.3f, OpeningKind.Arch);

        private static Opening Front(float from, float to, float floorY) => new Opening(from, to, floorY, floorY + DoorHeight, OpeningKind.FrontDoor);

        private static Opening RollUp(float from, float to, float floorY) => new Opening(from, to, floorY, floorY + 3.8f, OpeningKind.RollUpDoor);

        private static Opening Window(float from, float to, float sill, float head) => new Opening(from, to, sill, head, OpeningKind.Window);

        // ---- what breaks: the front door's planks and every window (docs/OLD_GUYS_HOUSE.md), numbered the same on every machine ----

        /// <summary>The planks nailed across the front door, numbered first: two across and one askew (pieces 0, 1, 2).</summary>
        public const int FrontDoorPlanks = 3;

        /// <summary>Every window's pane, numbered after the planks in the order the walls and their openings are written.</summary>
        public static readonly int PaneCount = CountPanes();

        public static int BreakableCount => FrontDoorPlanks + PaneCount;

        private static int CountPanes()
        {
            int n = 0;
            foreach (Wall wall in Walls)
            {
                foreach (Opening o in wall.Openings)
                {
                    if (o.Kind == OpeningKind.Window) n++;
                }
            }
            return n;
        }

        // ---- the stair core: a U stair up all three floors, the same place on each ----

        /// <summary>Each flight starts (or ends) at the core's north edge and turns at a landing against the front wall.</summary>
        public const float FlightTopZ = 7.2f;

        public const float FlightWestX0 = 11.2f, FlightWestX1 = 13.8f, FlightEastX0 = 14.2f, FlightEastX1 = 16.8f;
        public const float LandingZ0 = 0.15f;

        /// <summary>Steps in each flight; the landing between them is the step after the first.</summary>
        public const int FlightSteps = (RisersPerStorey - 2) / 2;

        public const float FlightTurnZ = FlightTopZ - FlightSteps * Tread;

        /// <summary>
        /// Every step and landing of the core, street level to the top: from each floor, up the west flight going south, round the landing,
        /// up the east flight going north onto the next floor. The floors above are cut away over the core (see <see cref="Rooms"/>).
        /// </summary>
        public static List<Step> CoreSteps()
        {
            var steps = new List<Step>();
            for (int level = 0; level < 2; level++)
            {
                float y = LevelY(level), r = Riser;
                for (int i = 1; i <= FlightSteps; i++)
                {
                    float top = y + i * r;
                    steps.Add(new Step(new Area(FlightWestX0, FlightTopZ - i * Tread, FlightWestX1, FlightTopZ - (i - 1) * Tread), top, Mathf.Max(y, top - StepDepth)));
                }
                float landing = y + (FlightSteps + 1) * r;
                steps.Add(new Step(new Area(FlightWestX0, LandingZ0, FlightEastX1, FlightTurnZ), landing, landing - SlabThickness));
                for (int k = 1; k <= FlightSteps; k++)
                {
                    float top = y + (FlightSteps + 1 + k) * r;
                    steps.Add(new Step(new Area(FlightEastX0, FlightTurnZ + (k - 1) * Tread, FlightEastX1, FlightTurnZ + k * Tread), top, Mathf.Max(y, top - StepDepth)));
                }
            }
            return steps;
        }

        /// <summary>The outdoor steps up the east side from the driveway to the porch: a step a riser and a landing at the porch's height.</summary>
        public const float OutdoorStepsX0 = 28.3f, OutdoorStepsX1 = 30.9f, OutdoorStepsTopZ = 2.0f;

        public static float OutdoorStepsFootZ => OutdoorStepsTopZ - (RisersPerStorey - 1) * Tread;

        public static List<Step> OutdoorSteps()
        {
            var steps = new List<Step>();
            for (int i = 1; i < RisersPerStorey; i++)
            {
                steps.Add(new Step(new Area(OutdoorStepsX0, OutdoorStepsFootZ + (i - 1) * Tread, OutdoorStepsX1, OutdoorStepsFootZ + i * Tread), i * Riser, 0));
            }
            return steps;
        }

        /// <summary>The porch at the top of the outdoor steps, before the front door.</summary>
        public const float PorchX1 = 33.0f, PorchZ0 = 6.5f, PorchZ1 = 12.5f;

        public static readonly Handrail[] Handrails = MakeHandrails();

        private static Handrail[] MakeHandrails()
        {
            var rails = new List<Handrail>();
            float r = Riser, up = RailHeight - 0.2f;
            for (int level = 0; level < 2; level++)
            {
                float y = LevelY(level) + up;
                rails.Add(new Handrail(new Vector3(FlightWestX0 + 0.06f, y + r, FlightTopZ), new Vector3(FlightWestX0 + 0.06f, y + FlightSteps * r, FlightTurnZ)));
                rails.Add(new Handrail(new Vector3(FlightEastX1 - 0.06f, y + (FlightSteps + 2) * r, FlightTurnZ), new Vector3(FlightEastX1 - 0.06f, y + (2 * FlightSteps + 1) * r, FlightTopZ)));
            }
            return rails.ToArray();
        }

        /// <summary>The outdoor steps' rails, sloping with them: the open side all the way up, the house side as far as the front wall.</summary>
        public static Handrail[] OutdoorRails()
        {
            float footY = Riser + RailHeight, topY = (RisersPerStorey - 1) * Riser + RailHeight;
            float atWall = ((0.0f - OutdoorStepsFootZ) / Tread + 1.0f) * Riser + RailHeight;
            return new[]
            {
                new Handrail(new Vector3(OutdoorStepsX1, footY, OutdoorStepsFootZ), new Vector3(OutdoorStepsX1, topY, OutdoorStepsTopZ)),
                new Handrail(new Vector3(OutdoorStepsX0, footY, OutdoorStepsFootZ), new Vector3(OutdoorStepsX0, atWall, 0.0f)),
            };
        }

        // ---- solid ground and railings ----

        public static readonly Block[] Blocks =
        {
            // The footing under the back of the main floor (behind the garage and the cellar).
            new Block(new Vector3(0, 0, 17), new Vector3(Width, ClearHeight, Depth), Finish.Concrete, DarkConcrete),
            // The porch and the landing at the top of the outdoor steps, a storey up.
            new Block(new Vector3(Width, 0, PorchZ0), new Vector3(PorchX1, Storey - 0.05f, PorchZ1), Finish.Render, Render),
            new Block(new Vector3(OutdoorStepsX0, 0, OutdoorStepsTopZ), new Vector3(OutdoorStepsX1, Storey - 0.05f, PorchZ0), Finish.Render, Render),
        };

        public static readonly Rail[] Rails =
        {
            new Rail(8, 0.15f, 8, HallRow, LevelY(2)),                                       // the gallery, over the living room
            new Rail(WestBay + 0.1f, FlightTopZ, FlightWestX1 + 0.05f, FlightTopZ, LevelY(2)),  // the stairwell's top, over the flight below
            new Rail(PorchX1, PorchZ0, PorchX1, PorchZ1, LevelY(1)),                         // the porch
            new Rail(OutdoorStepsX1, PorchZ0, PorchX1, PorchZ0, LevelY(1)),
            new Rail(Width + 0.15f, PorchZ1, PorchX1, PorchZ1, LevelY(1)),
            new Rail(OutdoorStepsX1, OutdoorStepsTopZ, OutdoorStepsX1, PorchZ0, LevelY(1)),
        };

        // ---- furniture (low-effort stand-ins for the owner's models) ----

        /// <summary>The kitchen counters' worktop, over the floor (a real 0.9 m at 1.3x).</summary>
        public const float KitchenCounterHeight = 1.17f;

        public const float KnifeBlockZ = 19.6f;

        /// <summary>The wand's glass case in the study. Declared before the furniture: static fields are set in the order they are written.</summary>
        public static readonly Vector3 GlassCase = new Vector3(26.4f, 0, 3.6f);

        public static readonly Furniture[] FurnitureList = MakeFurniture();

        private static Furniture[] MakeFurniture()
        {
            float l1 = LevelY(1), l2 = LevelY(2);
            Color steel = new Color(0.55f, 0.57f, 0.60f), red = new Color(0.60f, 0.12f, 0.10f), plaid = new Color(0.55f, 0.22f, 0.18f);
            Color leather = new Color(0.36f, 0.20f, 0.12f), white = new Color(0.92f, 0.92f, 0.90f), green = new Color(0.30f, 0.45f, 0.25f);
            Color cushion = new Color(0.46f, 0.50f, 0.58f), kidBlue = new Color(0.25f, 0.45f, 0.80f), pine = new Color(0.78f, 0.62f, 0.40f);
            var f = new List<Furniture>
            {
                // Garage, workshop, cellar.
                new Furniture(FurnitureKind.Car, new Vector3(5.5f, 0, 7.5f), 180, new Color(0.25f, 0.33f, 0.45f)),
                new Furniture(FurnitureKind.Workbench, new Vector3(0.15f, 0, 13.0f), 90, DarkOak, 0, "spray_can"),
                new Furniture(FurnitureKind.Shelves, new Vector3(10.85f, 0, 14.5f), 270, steel),
                new Furniture(FurnitureKind.Shelves, new Vector3(3.5f, 0, 16.85f), 180, steel),
                new Furniture(FurnitureKind.OilDrum, new Vector3(1.2f, 0, 16.0f), 0, red),
                new Furniture(FurnitureKind.Manhole, new Vector3(2.8f, 0, 3.0f), 0, DarkConcrete),
                new Furniture(FurnitureKind.FuseBox, new Vector3(Width - 0.15f, 0, 2.0f), 270, new Color(0.40f, 0.44f, 0.40f)),
                new Furniture(FurnitureKind.Workbench, new Vector3(22.5f, 0, 0.15f), 0, DarkOak),
                new Furniture(FurnitureKind.Shelves, new Vector3(14.0f, 0, 16.85f), 180, steel),
                new Furniture(FurnitureKind.Shelves, new Vector3(19.0f, 0, 16.85f), 180, steel),
                new Furniture(FurnitureKind.Boiler, new Vector3(Width - 0.15f, 0, 13.0f), 270, white),
                new Furniture(FurnitureKind.Crate, new Vector3(23.4f, 0, 15.9f), 15, pine),
                // The bat, hidden twice: at the back of the cellar, inside a crate.
                new Furniture(FurnitureKind.Crate, new Vector3(24.8f, 0, 15.9f), -10, pine, 0, "baseball_bat"),
                new Furniture(FurnitureKind.OilDrum, new Vector3(26.5f, 0, 16.1f), 0, red),

                // Living room: the fireplace on the west wall between its windows, the sofa and chairs round it.
                new Furniture(FurnitureKind.Fireplace, new Vector3(0.15f, l1, 6.0f), 90, new Color(0.62f, 0.36f, 0.28f)),
                new Furniture(FurnitureKind.Bottle, new Vector3(0.45f, l1 + 1.62f, 5.2f), 0, new Color(0.25f, 0.50f, 0.30f)),
                new Furniture(FurnitureKind.Bottle, new Vector3(0.45f, l1 + 1.62f, 6.0f), 0, new Color(0.45f, 0.30f, 0.15f)),
                new Furniture(FurnitureKind.Bottle, new Vector3(0.45f, l1 + 1.62f, 6.8f), 0, new Color(0.25f, 0.50f, 0.30f)),
                new Furniture(FurnitureKind.Rug, new Vector3(3.6f, l1, 6.0f), 90, new Color(0.55f, 0.25f, 0.20f)),
                new Furniture(FurnitureKind.Sofa, new Vector3(7.4f, l1, 6.0f), 270, cushion),
                new Furniture(FurnitureKind.Armchair, new Vector3(3.2f, l1, 2.2f), 330, leather),
                new Furniture(FurnitureKind.Armchair, new Vector3(3.2f, l1, 9.8f), 210, leather),
                new Furniture(FurnitureKind.CoffeeTable, new Vector3(4.4f, l1, 6.0f), 90, DarkOak),
                new Furniture(FurnitureKind.Plant, new Vector3(10.3f, l1, 0.8f), 0, green),

                // Entry hall, WC, coats.
                new Furniture(FurnitureKind.SmallTable, new Vector3(26.4f, l1, HallRow - 0.15f), 180, DarkOak),
                new Furniture(FurnitureKind.Rug, new Vector3(22.0f, l1, 9.7f), 90, new Color(0.30f, 0.32f, 0.45f)),
                new Furniture(FurnitureKind.Toilet, new Vector3(19.0f, l1, 0.15f), 0, white),
                new Furniture(FurnitureKind.Basin, new Vector3(21.35f, l1, 4.5f), 270, white),
                new Furniture(FurnitureKind.CoatRack, new Vector3(27.3f, l1, 1.0f), 0, DarkOak),
                new Furniture(FurnitureKind.Shelves, new Vector3(21.65f, l1, 3.5f), 90, Oak),

                // Kitchen: counters along the west wall under the window, the stove, a counter and the fridge along the back wall.
                new Furniture(FurnitureKind.Counter, new Vector3(0.15f, l1, 13.5f), 90, white, 0, "flashlight"),
                new Furniture(FurnitureKind.Counter, new Vector3(0.15f, l1, 15.1f), 90, white),
                new Furniture(FurnitureKind.Sink, new Vector3(0.15f, l1, 16.7f), 90, white),
                new Furniture(FurnitureKind.Counter, new Vector3(0.15f, l1, 18.3f), 90, white),
                new Furniture(FurnitureKind.Counter, new Vector3(0.15f, l1, 19.9f), 90, white),
                new Furniture(FurnitureKind.KnifeBlock, new Vector3(0.65f, l1 + KitchenCounterHeight, KnifeBlockZ), 90, Oak),
                new Furniture(FurnitureKind.UpperCabinet, new Vector3(0.15f, l1 + 2.3f, 13.5f), 90, white),
                new Furniture(FurnitureKind.Stove, new Vector3(1.9f, l1, Depth - 0.15f), 180, white),
                new Furniture(FurnitureKind.Counter, new Vector3(3.5f, l1, Depth - 0.15f), 180, white),
                new Furniture(FurnitureKind.Fridge, new Vector3(9.9f, l1, Depth - 0.15f), 180, white, 0, "glass_bottle"),

                // Dining room.
                new Furniture(FurnitureKind.DiningTable, new Vector3(14.5f, l1, 17.0f), 0, DarkOak),
                new Furniture(FurnitureKind.Chair, new Vector3(13.4f, l1, 16.4f), 90, DarkOak),
                new Furniture(FurnitureKind.Chair, new Vector3(13.4f, l1, 17.6f), 90, DarkOak),
                new Furniture(FurnitureKind.Chair, new Vector3(15.6f, l1, 16.4f), 270, DarkOak),
                new Furniture(FurnitureKind.Chair, new Vector3(15.6f, l1, 17.6f), 270, DarkOak),
                new Furniture(FurnitureKind.Sideboard, new Vector3(14.5f, l1, Depth - 0.15f), 180, DarkOak),

                // Old Guy's den: the dated room (striped wallpaper in gm_house4): the plaid sofa facing the old TV, an armchair, a radiator.
                new Furniture(FurnitureKind.Rug, new Vector3(23.5f, l1, 17.0f), 0, new Color(0.45f, 0.35f, 0.20f)),
                new Furniture(FurnitureKind.Sofa, new Vector3(24.5f, l1, Depth - 0.15f), 180, plaid),
                new Furniture(FurnitureKind.Television, new Vector3(24.5f, l1, HallRow + 0.1f), 0, new Color(0.15f, 0.15f, 0.16f)),
                new Furniture(FurnitureKind.Armchair, new Vector3(21.0f, l1, 17.5f), 100, leather),
                new Furniture(FurnitureKind.Radiator, new Vector3(Width - 0.15f, l1, 17.0f), 270, white),
                new Furniture(FurnitureKind.Sideboard, new Vector3(18.15f, l1, 18.0f), 90, DarkOak),

                // Gallery and corridor.
                new Furniture(FurnitureKind.Rug, new Vector3(22.5f, l2, 9.7f), 90, new Color(0.40f, 0.20f, 0.18f)),

                // The grandson's room: his bed, his desk under the window with the slingshot, a toy chest, a poster, his drawing.
                new Furniture(FurnitureKind.Bed, new Vector3(WestBay - 0.15f, l2, 17.0f), 270, kidBlue),
                new Furniture(FurnitureKind.Desk, new Vector3(3.0f, l2, Depth - 0.15f), 180, pine),
                new Furniture(FurnitureKind.Chair, new Vector3(3.0f, l2, 20.6f), 0, pine),
                new Furniture(FurnitureKind.ToyChest, new Vector3(0.15f, l2, 13.5f), 90, red),
                new Furniture(FurnitureKind.Poster, new Vector3(5.5f, l2 + 1.8f, HallRow + 0.1f), 0, new Color(0.95f, 0.75f, 0.20f)),
                new Furniture(FurnitureKind.Drawing, new Vector3(0.15f, l2 + 1.7f, 20.8f), 90, white),

                // Bathroom.
                new Furniture(FurnitureKind.Bathtub, new Vector3(14.0f, l2, Depth - 0.15f), 180, white),
                new Furniture(FurnitureKind.Toilet, new Vector3(WestBay + 0.15f, l2, 18.5f), 90, white),
                new Furniture(FurnitureKind.Basin, new Vector3(CoreBay - 0.15f, l2, 16.0f), 270, white),

                // The study: books, a desk, and the wand in its glass case.
                new Furniture(FurnitureKind.Bookcase, new Vector3(CoreBay + 0.15f, l2, 2.0f), 90, DarkOak),
                new Furniture(FurnitureKind.Bookcase, new Vector3(CoreBay + 0.15f, l2, 4.4f), 90, DarkOak),
                new Furniture(FurnitureKind.Desk, new Vector3(22.75f, l2, 0.15f), 0, DarkOak),
                new Furniture(FurnitureKind.Chair, new Vector3(22.75f, l2, 1.5f), 180, leather),
                new Furniture(FurnitureKind.GlassCase, new Vector3(GlassCase.x, l2, GlassCase.z), 270, DarkOak),

                // Old Guy's bedroom.
                new Furniture(FurnitureKind.DoubleBed, new Vector3(Width - 0.15f, l2, 17.0f), 270, new Color(0.62f, 0.55f, 0.75f)),
                new Furniture(FurnitureKind.Nightstand, new Vector3(Width - 0.15f, l2, 15.2f), 270, DarkOak, 0, "farmer_hat"),
                new Furniture(FurnitureKind.Nightstand, new Vector3(Width - 0.15f, l2, 18.8f), 270, DarkOak),
                new Furniture(FurnitureKind.Wardrobe, new Vector3(CoreBay + 0.15f, l2, 18.0f), 90, DarkOak, 0, "t_shirt", "blue_jeans"),
                new Furniture(FurnitureKind.Dresser, new Vector3(19.8f, l2, Depth - 0.15f), 180, DarkOak, 1, "white_sneaker"),
            };
            return f.ToArray();
        }

        // ---- the weapons, Old Guy and the swordsman ----

        /// <summary>
        /// The weapons lying in the open: the three starters on the route (the Kitchen Knife in the knife block, the Slingshot on the grandson's
        /// desk, the Magic Wand in its glass case) and the Butter Knife on the counter beside the block. The fifth, the Baseball Bat, is inside a
        /// crate in the cellar (a piece's <see cref="Furniture.Stash"/>). Ids are ItemRegistry's (OldGuysHousePlanTests checks them).
        /// </summary>
        public static readonly ItemSpot[] Items =
        {
            new ItemSpot("kitchen_knife", new Vector3(0.65f, LevelY(1) + KitchenCounterHeight + 0.30f, KnifeBlockZ), new Vector3(0, 90, 180)),
            new ItemSpot("butter_knife", new Vector3(0.75f, LevelY(1) + KitchenCounterHeight + 0.012f, 18.1f), new Vector3(90, 15, 0)),
            new ItemSpot("slingshot", new Vector3(2.6f, LevelY(2) + 1.0f + 0.02f, 21.4f), new Vector3(90, 20, 0)),
            new ItemSpot("magic_wand", new Vector3(GlassCase.x, LevelY(2) + 1.16f, GlassCase.z), new Vector3(90, 90, 0)),
        };

        /// <summary>The one fight: a swordsman in Old Guy's bedroom (owner: "maybe just one swordsman upstairs").</summary>
        public static readonly Vector3 SwordsmanSpot = new Vector3(22.0f, LevelY(2), 16.0f);

        public const float SwordsmanYaw = 200.0f;

        /// <summary>Whether a point on the house's own plan is within its walls or this margin outside them (a sill climbed in by).</summary>
        public static bool WithinWalls(Vector3 local, float margin) =>
            local.x >= -margin && local.x <= Width + margin && local.z >= -margin && local.z <= Depth + margin;

        /// <summary>Where Old Guy waits: at the foot of the outdoor steps, then in his den.</summary>
        public static readonly Vector3 OldGuyAtTheSteps = new Vector3(OutdoorStepsX0 - 1.2f, 0, -5.0f);

        public static readonly Vector3 OldGuyInTheDen = new Vector3(25.0f, LevelY(1), 16.0f);

        /// <summary>Where the player lands (the intro's fall ends here): on his driveway, the house the nearest building, Old Guy at the steps watching.</summary>
        public static readonly Vector3 Landing = new Vector3((float)Plan.TutorialLot.LandingX, 0, (float)Plan.TutorialLot.LandingZ);

        /// <summary>The whole lot, the driveway and the porch included, on the ground plan: the land the city keeps for the house (Plan.TutorialLot).</summary>
        public static readonly Area Lot = new Area((float)Plan.TutorialLot.LotX0, (float)Plan.TutorialLot.LotZ0, (float)Plan.TutorialLot.LotX1, (float)Plan.TutorialLot.LotZ1);
    }
}
