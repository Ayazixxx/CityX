using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// THE TUTORIAL HOUSE, designed by hand (docs/OLD_GUYS_HOUSE.md): every other building is generated from the world seed, this one is drawn
    /// room by room. Pure data in the house's own frame: metres, x east, z north (toward the garden), y up, the origin at the front corner of the
    /// house at street level, the street side facing -z. OldGuysHouseBuilder (View) turns it into meshes, colliders and furniture.
    ///
    /// Sizes are for the 2.6 m player with the camera 5 m behind: real sizes times 1.3, wider where the camera needs room (doors 1.6 x 3 m,
    /// halls 3.5 m, storeys 4 m with 3.7 m clear).
    /// </summary>
    public static class OldGuysHouse
    {
        public const float Storey = 4.0f;
        public const float SlabThickness = 0.3f;
        public const float OuterWallThickness = 0.3f;
        public const float InnerWallThickness = 0.2f;
        public const float DoorWidth = 1.6f;
        public const float DoorHeight = 3.0f;
        public const float RailHeight = 1.2f;

        /// <summary>Clear height under a ceiling: a storey less its slab.</summary>
        public const float ClearHeight = Storey - SlabThickness;

        /// <summary>Stairs: the stair kit's tread (BuildingStairs) and a storey in 18 risers of 0.22 m, under 32 degrees.</summary>
        public const float Tread = 0.36f;

        public const int RisersPerStorey = 18;

        public static float Riser => Storey / RisersPerStorey;

        /// <summary>A stringer step hangs this far under its tread, so the flight above another leaves headroom under it.</summary>
        public const float StepDepth = 0.55f;

        public const float GardenWallHeight = 1.0f;
        public const float FenceHeight = 2.6f;

        /// <summary>The top of the walls and the flat ceiling under the roof; the roof's eaves and ridge.</summary>
        public const float EavesY = 3 * Storey;

        public const float RidgeY = 15.6f;
        public const float RidgeZ = 8.0f;
        public const float RoofOverhang = 0.6f;

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
            Grass,
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

            /// <summary>Under the sky (a porch, a balcony, the garden): no ceiling light, no indoor look.</summary>
            public bool Outdoors;

            /// <summary>Open up through the floor above (the living room, two storeys tall under the gallery).</summary>
            public bool DoubleHeight;

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

            /// <summary>A way through for a body (not a window).</summary>
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

        /// <summary>A solid block: the garden terrace, the porch, the footing under the kitchen.</summary>
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
            Tree, Bench, CeilingLamp,
        }

        public readonly struct Furniture
        {
            public readonly FurnitureKind Kind;
            public readonly Vector3 Position;
            public readonly float Yaw;
            public readonly Color Tint;

            /// <summary>A piece at <paramref name="position"/> (its foot, or its back's middle for a wall piece), facing <paramref name="yaw"/> (0 = +z).</summary>
            public Furniture(FurnitureKind kind, Vector3 position, float yaw, Color tint)
            {
                Kind = kind; Position = position; Yaw = yaw; Tint = tint;
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
        private static readonly Color Terracotta = new Color(0.72f, 0.42f, 0.30f);
        public static readonly Color Render = new Color(0.90f, 0.89f, 0.85f);
        public static readonly Color RoofTiles = new Color(0.45f, 0.20f, 0.16f);
        public static readonly Color GrassGreen = new Color(0.36f, 0.55f, 0.25f);
        public static readonly Color Decking = new Color(0.55f, 0.40f, 0.26f);
        public static readonly Color Paving = new Color(0.70f, 0.68f, 0.64f);
        public static readonly Color Ceiling = new Color(0.95f, 0.95f, 0.93f);

        // ---- the rooms ----

        public static readonly Room[] Rooms =
        {
            // Street level: dug in under the house.
            RoomOf("Garage", 0, Finish.Concrete, Concrete, new Color(0.78f, 0.78f, 0.76f), new Area(0, 0, 9, 14)),
            RoomOf("Stair core (street)", 0, Finish.Concrete, Concrete, Cream, new Area(9, 0, 14, 5.5f)),
            RoomOf("Stair hall", 0, Finish.Concrete, Concrete, Cream, new Area(9, 5.5f, 20, 9)),
            RoomOf("Workshop", 0, Finish.Concrete, Concrete, new Color(0.70f, 0.74f, 0.70f), new Area(14, 0, 20, 5.5f)),
            RoomOf("Cellar", 0, Finish.Concrete, DarkConcrete, new Color(0.62f, 0.40f, 0.32f), new Area(9, 9, 20, 14)),

            // Main floor.
            Tall(RoomOf("Living room", 1, Finish.Wood, Oak, Cream, new Area(0, 0, 9, 9))),
            RoomOf("Entry hall", 1, Finish.Wood, Oak, new Color(0.82f, 0.86f, 0.80f), new Area(9, 5.3f, 14, 9), new Area(14, 5.5f, 20, 9)),
            RoomOf("WC", 1, Finish.Tile, WhiteTile, new Color(0.80f, 0.90f, 0.92f), new Area(14, 0, 17, 5.5f)),
            RoomOf("Coats", 1, Finish.Wood, Oak, new Color(0.82f, 0.86f, 0.80f), new Area(17, 0, 20, 5.5f)),
            RoomOf("Kitchen", 1, Finish.Tile, WhiteTile, new Color(0.96f, 0.90f, 0.62f), new Area(0, 9, 9, 16)),
            RoomOf("Dining room", 1, Finish.Wood, Oak, new Color(0.70f, 0.78f, 0.62f), new Area(9, 9, 14, 16)),
            RoomOf("Old Guy's den", 1, Finish.Carpet, new Color(0.36f, 0.30f, 0.22f), new Color(0.80f, 0.68f, 0.36f), new Area(14, 9, 20, 16)),
            RoomOf("Conservatory", 1, Finish.Tile, Terracotta, Render, new Area(0, 16, 9, 20)),

            // Upper floor.
            RoomOf("Gallery", 2, Finish.Wood, Oak, Cream, new Area(7, 0, 9, 9), new Area(9, 5.3f, 14, 9)),
            RoomOf("Study", 2, Finish.Wood, DarkOak, new Color(0.30f, 0.45f, 0.36f), new Area(14, 0, 20, 5.5f)),
            RoomOf("Corridor", 2, Finish.Wood, Oak, new Color(0.88f, 0.86f, 0.80f), new Area(14, 5.5f, 20, 9)),
            RoomOf("Grandson's room", 2, Finish.Carpet, new Color(0.30f, 0.42f, 0.62f), new Color(0.62f, 0.80f, 0.95f), new Area(0, 9, 9, 16)),
            RoomOf("Bathroom", 2, Finish.Tile, WhiteTile, new Color(0.62f, 0.84f, 0.82f), new Area(9, 9, 14, 16)),
            RoomOf("Old Guy's bedroom", 2, Finish.Carpet, new Color(0.70f, 0.62f, 0.52f), new Color(0.86f, 0.70f, 0.70f), new Area(14, 9, 20, 16)),
            RoomOf("Tree house", 2, Finish.Deck, Decking, Decking, new Area(2.5f, 26, 7.5f, 30)),

            // Outside (no ceiling).
            Outside("Driveway", 0, Finish.Paving, Paving, new Area(-4.5f, -6.5f, 24.5f, 0)),
            Outside("Porch", 1, Finish.Deck, Decking, new Area(20, 5, 24.5f, 9.5f)),
            Outside("Side path", 1, Finish.Paving, Paving, new Area(20, 9.5f, 24.5f, 16)),
            Outside("Patio", 1, Finish.Paving, Paving, new Area(9, 16, 20, 18.5f)),
            Outside("Garden", 1, Finish.Grass, GrassGreen, new Area(-4.5f, 16, 0, 35.5f), new Area(0, 20, 9, 35.5f), new Area(9, 18.5f, 20, 35.5f), new Area(20, 16, 27.5f, 35.5f)),
            Outside("Balcony", 2, Finish.Deck, Decking, new Area(9, 16, 20, 18.5f)),
            Outside("Tree house platform", 2, Finish.Deck, Decking, new Area(2, 25.5f, 8, 26), new Area(2, 30, 8, 30.5f), new Area(2, 26, 2.5f, 30), new Area(7.5f, 26, 8, 30)),
        };

        private static Room RoomOf(string name, int level, Finish floor, Color floorTint, Color wallTint, params Area[] floors) =>
            new Room { Name = name, Level = level, Floors = floors, Floor = floor, FloorTint = floorTint, WallTint = wallTint };

        private static Room Tall(Room room)
        {
            room.DoubleHeight = true;
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
            float l1 = LevelY(1), l2 = LevelY(2);
            var w = new List<Wall>
            {
                // Street level: the outer walls stand a whole storey (they run past the slab's edge), the inner ones to the ceiling.
                X(0, Storey, 0, 0, 20, true, RollUp(1.5f, 7.5f, 0), Window(10.6f, 12.4f, 1.4f, 3.0f), Window(15.8f, 18.2f, 1.4f, 3.0f)),
                Z(0, Storey, 0, 0, 14, true),
                Z(0, Storey, 20, 0, 14, true),
                X(0, Storey, 14, 0, 20, true),
                Z(0, ClearHeight, 9, 0, 14, false, Door(6.4f, 8.0f, 0)),
                Z(0, ClearHeight, 14, 0, 5.5f, false),
                X(0, ClearHeight, 5.5f, 14, 20, false, Door(16, 17.6f, 0)),
                X(0, ClearHeight, 9, 9, 20, false, Door(10.4f, 12.0f, 0)),

                // Main floor. The living room's front and side are two storeys tall.
                X(l1, EavesY, 0, 0, 9, true, Window(1.5f, 7.5f, l1 + 0.9f, l1 + 3.4f), Window(1.5f, 7.5f, l2 + 0.6f, l2 + 3.2f)),
                X(l1, l2, 0, 9, 20, true, Window(10.6f, 12.4f, l1 + 1.4f, l1 + 3.0f), Window(15.0f, 16.0f, l1 + 2.0f, l1 + 3.2f)),
                Z(l1, EavesY, 0, 0, 9, true),
                Z(l1, l2, 0, 9, 16, true, Window(11.5f, 14.5f, l1 + 1.4f, l1 + 3.2f)),
                Z(l1, l2, 20, 0, 16, true, Front(6.3f, 7.9f, l1), Window(11.5f, 14.5f, l1 + 1.0f, l1 + 3.2f)),
                X(l1, l2, 16, 0, 20, true, Door(6.0f, 7.6f, l1), Window(10.5f, 12.5f, l1 + 1.0f, l1 + 3.2f), Door(16.5f, 18.1f, l1)),
                Z(l1, l1 + ClearHeight, 9, 0, 16, false, Arch(5.7f, 8.8f, l1), Door(12.2f, 13.8f, l1)),
                X(l1, l1 + ClearHeight, 9, 0, 9, false, Arch(2.5f, 5.5f, l1)),
                X(l1, l1 + ClearHeight, 9, 9, 20, false, Door(10.7f, 12.3f, l1), Door(16.0f, 17.6f, l1)),
                Z(l1, l1 + ClearHeight, 14, 0, 5.5f, false),
                Z(l1, l1 + ClearHeight, 17, 0, 5.5f, false),
                X(l1, l1 + ClearHeight, 5.5f, 14, 20, false, Door(14.7f, 16.3f, l1), Door(17.7f, 19.3f, l1)),
                Z(l1, l1 + ClearHeight, 14, 9, 16, false),

                // Upper floor.
                X(l2, EavesY, 0, 9, 20, true, Window(10.6f, 12.4f, l2 + 1.4f, l2 + 3.0f), Window(15.5f, 18.5f, l2 + 1.0f, l2 + 3.2f)),
                Z(l2, EavesY, 0, 9, 16, true, Window(11.0f, 14.0f, l2 + 1.0f, l2 + 3.2f)),
                Z(l2, EavesY, 20, 0, 16, true, Window(1.5f, 4.0f, l2 + 1.0f, l2 + 3.2f), Window(6.3f, 8.2f, l2 + 1.0f, l2 + 3.2f), Window(11.5f, 14.5f, l2 + 1.0f, l2 + 3.2f)),
                X(l2, EavesY, 16, 0, 20, true, Window(1.5f, 5.0f, l2 + 1.0f, l2 + 3.2f), Window(11.0f, 12.5f, l2 + 1.8f, l2 + 3.0f), Door(15.2f, 16.8f, l2), Window(17.8f, 19.4f, l2 + 1.0f, l2 + 3.2f)),
                X(l2, l2 + ClearHeight, 9, 0, 9, false, Door(7.3f, 8.9f, l2)),
                Z(l2, l2 + ClearHeight, 9, 0, 5.5f, false),
                Z(l2, l2 + ClearHeight, 14, 0, 9, false, Arch(5.8f, 8.7f, l2)),
                X(l2, l2 + ClearHeight, 5.5f, 14, 20, false, Door(16, 17.6f, l2)),
                X(l2, l2 + ClearHeight, 9, 9, 20, false, Door(10.5f, 12.1f, l2), Door(17.6f, 19.2f, l2)),
                Z(l2, l2 + ClearHeight, 9, 9, 16, false),
                Z(l2, l2 + ClearHeight, 14, 9, 16, false),

                // The wall between the two flights of the stair core, all the way up.
                new Wall { AlongX = false, At = 11.5f, From = FlightTurnZ, To = FlightTopZ, Y0 = 0, Y1 = l2 + ClearHeight, Thickness = 0.3f, Openings = new Opening[0] },

                // The tree house: a hut on its platform, its door toward the bridge.
                X(l2, l2 + 3.2f, 26, 2.5f, 7.5f, true, Door(5.9f, 7.5f, l2)),
                X(l2, l2 + 3.2f, 30, 2.5f, 7.5f, true, Window(3.8f, 6.2f, l2 + 1.2f, l2 + 2.4f)),
                Z(l2, l2 + 3.2f, 2.5f, 26, 30, true, Window(27.2f, 28.8f, l2 + 1.2f, l2 + 2.4f)),
                Z(l2, l2 + 3.2f, 7.5f, 26, 30, true),
            };
            return w.ToArray();
        }

        private static Wall X(float y0, float y1, float z, float x0, float x1, bool outer, params Opening[] openings) =>
            new Wall { AlongX = true, At = z, From = x0, To = x1, Y0 = y0, Y1 = y1, Thickness = outer ? OuterWallThickness : InnerWallThickness, Outer = outer, Openings = openings };

        private static Wall Z(float y0, float y1, float x, float z0, float z1, bool outer, params Opening[] openings) =>
            new Wall { AlongX = false, At = x, From = z0, To = z1, Y0 = y0, Y1 = y1, Thickness = outer ? OuterWallThickness : InnerWallThickness, Outer = outer, Openings = openings };

        private static Opening Door(float from, float to, float floorY) => new Opening(from, to, floorY, floorY + DoorHeight, OpeningKind.Door);

        private static Opening Arch(float from, float to, float floorY) => new Opening(from, to, floorY, floorY + DoorHeight + 0.2f, OpeningKind.Arch);

        private static Opening Front(float from, float to, float floorY) => new Opening(from, to, floorY, floorY + DoorHeight, OpeningKind.FrontDoor);

        private static Opening RollUp(float from, float to, float floorY) => new Opening(from, to, floorY, floorY + 3.2f, OpeningKind.RollUpDoor);

        private static Opening Window(float from, float to, float sill, float head) => new Opening(from, to, sill, head, OpeningKind.Window);

        // ---- the stair core: a U stair up all three floors, the same place on each ----

        /// <summary>Each flight starts (or ends) at the core's north edge and turns at a landing against the front wall.</summary>
        public const float FlightTopZ = 5.3f;

        public const float FlightWestX0 = 9.2f, FlightWestX1 = 11.3f, FlightEastX0 = 11.7f, FlightEastX1 = 13.8f;
        public const float LandingZ0 = 0.15f;

        public static float FlightTurnZ => FlightTopZ - 8 * Tread;

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
                for (int i = 1; i <= 8; i++)
                {
                    float top = y + i * r;
                    steps.Add(new Step(new Area(FlightWestX0, FlightTopZ - i * Tread, FlightWestX1, FlightTopZ - (i - 1) * Tread), top, Mathf.Max(y, top - StepDepth)));
                }
                float landing = y + 9 * r;
                steps.Add(new Step(new Area(FlightWestX0, LandingZ0, FlightEastX1, FlightTurnZ), landing, landing - SlabThickness));
                for (int k = 1; k <= 8; k++)
                {
                    float top = y + (9 + k) * r;
                    steps.Add(new Step(new Area(FlightEastX0, FlightTurnZ + (k - 1) * Tread, FlightEastX1, FlightTurnZ + k * Tread), top, Mathf.Max(y, top - StepDepth)));
                }
            }
            return steps;
        }

        /// <summary>The outdoor steps up the east side from the driveway to the porch: 17 steps and a landing at the porch's height.</summary>
        public const float OutdoorStepsX0 = 20.6f, OutdoorStepsX1 = 23.0f, OutdoorStepsTopZ = 2.0f;

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

        public static readonly Handrail[] Handrails =
        {
            new Handrail(new Vector3(FlightWestX0 + 0.06f, RailHeight - 0.2f + Riser, FlightTopZ), new Vector3(FlightWestX0 + 0.06f, RailHeight - 0.2f + 8 * Riser, FlightTurnZ)),
            new Handrail(new Vector3(FlightEastX1 - 0.06f, RailHeight - 0.2f + 10 * Riser, FlightTurnZ), new Vector3(FlightEastX1 - 0.06f, RailHeight - 0.2f + 17 * Riser, FlightTopZ)),
            new Handrail(new Vector3(FlightWestX0 + 0.06f, Storey + RailHeight - 0.2f + Riser, FlightTopZ), new Vector3(FlightWestX0 + 0.06f, Storey + RailHeight - 0.2f + 8 * Riser, FlightTurnZ)),
            new Handrail(new Vector3(FlightEastX1 - 0.06f, Storey + RailHeight - 0.2f + 10 * Riser, FlightTurnZ), new Vector3(FlightEastX1 - 0.06f, Storey + RailHeight - 0.2f + 17 * Riser, FlightTopZ)),
        };

        /// <summary>The outdoor steps' rails, on both sides, sloping with them.</summary>
        public static Handrail[] OutdoorRails()
        {
            float footY = Riser + RailHeight, topY = (RisersPerStorey - 1) * Riser + RailHeight;
            return new[]
            {
                new Handrail(new Vector3(OutdoorStepsX1, footY, OutdoorStepsFootZ), new Vector3(OutdoorStepsX1, topY, OutdoorStepsTopZ)),
                new Handrail(new Vector3(OutdoorStepsX0, footY, OutdoorStepsFootZ), new Vector3(OutdoorStepsX0, Riser * 9 + RailHeight, 0.0f)),
            };
        }

        // ---- solid ground, railings, the fence ----

        public static readonly Block[] Blocks =
        {
            // The footing under the back of the main floor (behind the cellar).
            new Block(new Vector3(0, 0, 14), new Vector3(20, ClearHeight, 16), Finish.Concrete, DarkConcrete),
            // The porch, the landing at the top of the outdoor steps, the side path and the garden: a raised terrace a storey up.
            new Block(new Vector3(20, 0, 5), new Vector3(24.5f, Storey - 0.05f, 9.5f), Finish.Render, Render),
            new Block(new Vector3(OutdoorStepsX0, 0, OutdoorStepsTopZ), new Vector3(OutdoorStepsX1, Storey - 0.05f, 5), Finish.Render, Render),
            new Block(new Vector3(20, 0, 9.5f), new Vector3(24.5f, Storey - 0.05f, 16), Finish.Render, Render),
            new Block(new Vector3(-4.5f, 0, 16), new Vector3(27.5f, Storey - 0.05f, 35.5f), Finish.Render, Render),
        };

        public static readonly Rail[] Rails =
        {
            new Rail(7, 0.15f, 7, 9, LevelY(2)),                       // the gallery, over the living room
            new Rail(9.1f, FlightTopZ, 11.35f, FlightTopZ, LevelY(2)),  // the stairwell's top, over the flight below
            new Rail(11.0f, 18.5f, 20, 18.5f, LevelY(2)),              // the balcony (the bridge leaves from its west end)
            new Rail(20, 16.15f, 20, 18.5f, LevelY(2)),
            new Rail(9, 16.15f, 9, 18.5f, LevelY(2)),
            new Rail(24.5f, 5, 24.5f, 9.5f, LevelY(1)),                // the porch
            new Rail(23, 5, 24.5f, 5, LevelY(1)),
            new Rail(OutdoorStepsX1, OutdoorStepsTopZ, OutdoorStepsX1, 5, LevelY(1)),
            new Rail(2, 30.5f, 8, 30.5f, LevelY(2)),                   // the tree house platform
            new Rail(2, 25.5f, 2, 30.5f, LevelY(2)),
            new Rail(8, 25.5f, 8, 30.5f, LevelY(2)),
            new Rail(2, 25.5f, 5.9f, 25.5f, LevelY(2)),
            new Rail(7.6f, 25.5f, 8, 25.5f, LevelY(2)),
        };

        /// <summary>The garden wall the vault is learnt on: between the porch and the side path, at the porch's height.</summary>
        public static readonly Rail GardenWall = new Rail(20, 9.5f, 24.5f, 9.5f, LevelY(1));

        /// <summary>The fence round the garden: too tall to vault, it keeps the tutorial in.</summary>
        public static readonly Vector2[] Fence =
        {
            new Vector2(0, 16), new Vector2(-4.5f, 16), new Vector2(-4.5f, 35.5f), new Vector2(27.5f, 35.5f), new Vector2(27.5f, 16),
            new Vector2(24.5f, 16), new Vector2(24.5f, 9.5f),
        };

        /// <summary>The plank bridge from the balcony's west end to the tree house, both 8 m up: its two ends' middles and its width.</summary>
        public static readonly Vector2 BridgeFrom = new Vector2(10.1f, 18.5f);

        public static readonly Vector2 BridgeTo = new Vector2(6.7f, 25.5f);
        public const float BridgeWidth = 1.8f;

        /// <summary>The old tree in the garden, its trunk standing on the terrace.</summary>
        public static readonly Vector3 TreeFoot = new Vector3(5, LevelY(1), 28.5f);

        // ---- furniture (low-effort stand-ins for the owner's models) ----

        /// <summary>The kitchen counters' worktop, over the floor (a real 0.9 m at 1.3x).</summary>
        public const float KitchenCounterHeight = 1.17f;

        public const float KnifeBlockZ = 14.6f;

        /// <summary>The wand's glass case in the study. Declared before the furniture: static fields are set in the order they are written.</summary>
        public static readonly Vector3 GlassCase = new Vector3(18.9f, 0, 1.6f);

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
                new Furniture(FurnitureKind.Car, new Vector3(4.5f, 0, 6.5f), 180, new Color(0.25f, 0.33f, 0.45f)),
                new Furniture(FurnitureKind.Workbench, new Vector3(0.15f, 0, 10.5f), 90, DarkOak),
                new Furniture(FurnitureKind.Shelves, new Vector3(8.85f, 0, 11.5f), 270, steel),
                new Furniture(FurnitureKind.OilDrum, new Vector3(1.2f, 0, 13.0f), 0, red),
                new Furniture(FurnitureKind.Manhole, new Vector3(2.4f, 0, 2.6f), 0, DarkConcrete),
                new Furniture(FurnitureKind.FuseBox, new Vector3(19.85f, 0, 2.8f), 270, new Color(0.40f, 0.44f, 0.40f)),
                new Furniture(FurnitureKind.Workbench, new Vector3(16.8f, 0, 0.15f), 0, DarkOak),
                new Furniture(FurnitureKind.Shelves, new Vector3(12.0f, 0, 13.85f), 180, steel),
                new Furniture(FurnitureKind.Shelves, new Vector3(16.0f, 0, 13.85f), 180, steel),
                new Furniture(FurnitureKind.Boiler, new Vector3(19.85f, 0, 10.4f), 270, white),
                new Furniture(FurnitureKind.Crate, new Vector3(10.2f, 0, 10.0f), 15, pine),
                new Furniture(FurnitureKind.Crate, new Vector3(10.4f, 0, 11.3f), -10, pine),

                // Living room.
                new Furniture(FurnitureKind.Fireplace, new Vector3(0.15f, l1, 4.5f), 90, new Color(0.62f, 0.36f, 0.28f)),
                new Furniture(FurnitureKind.Bottle, new Vector3(0.45f, l1 + 1.62f, 3.7f), 0, new Color(0.25f, 0.50f, 0.30f)),
                new Furniture(FurnitureKind.Bottle, new Vector3(0.45f, l1 + 1.62f, 4.5f), 0, new Color(0.45f, 0.30f, 0.15f)),
                new Furniture(FurnitureKind.Bottle, new Vector3(0.45f, l1 + 1.62f, 5.3f), 0, new Color(0.25f, 0.50f, 0.30f)),
                new Furniture(FurnitureKind.Rug, new Vector3(3.6f, l1, 4.5f), 0, new Color(0.55f, 0.25f, 0.20f)),
                new Furniture(FurnitureKind.Sofa, new Vector3(6.0f, l1, 4.5f), 270, cushion),
                new Furniture(FurnitureKind.Armchair, new Vector3(3.0f, l1, 1.4f), 330, leather),
                new Furniture(FurnitureKind.CoffeeTable, new Vector3(3.6f, l1, 4.5f), 90, DarkOak),
                new Furniture(FurnitureKind.Plant, new Vector3(8.2f, l1, 0.8f), 0, green),

                // Entry hall, WC, coats.
                new Furniture(FurnitureKind.SmallTable, new Vector3(18.4f, l1, 8.4f), 180, DarkOak),
                new Furniture(FurnitureKind.Rug, new Vector3(16.5f, l1, 7.2f), 90, new Color(0.30f, 0.32f, 0.45f)),
                new Furniture(FurnitureKind.Toilet, new Vector3(15.5f, l1, 0.15f), 0, white),
                new Furniture(FurnitureKind.Basin, new Vector3(16.85f, l1, 3.0f), 270, white),
                new Furniture(FurnitureKind.CoatRack, new Vector3(19.3f, l1, 1.0f), 0, DarkOak),
                new Furniture(FurnitureKind.Shelves, new Vector3(17.15f, l1, 2.0f), 90, Oak),

                // Kitchen: counters along the west wall under the window, the stove and fridge along the north wall.
                new Furniture(FurnitureKind.Counter, new Vector3(0.15f, l1, 10.1f), 90, white),
                new Furniture(FurnitureKind.Counter, new Vector3(0.15f, l1, 11.7f), 90, white),
                new Furniture(FurnitureKind.Sink, new Vector3(0.15f, l1, 13.3f), 90, white),
                new Furniture(FurnitureKind.Counter, new Vector3(0.15f, l1, 14.9f), 90, white),
                new Furniture(FurnitureKind.KnifeBlock, new Vector3(0.65f, l1 + KitchenCounterHeight, KnifeBlockZ), 90, Oak),
                new Furniture(FurnitureKind.UpperCabinet, new Vector3(0.15f, l1 + 2.3f, 10.1f), 90, white),
                new Furniture(FurnitureKind.Stove, new Vector3(2.4f, l1, 15.85f), 180, white),
                new Furniture(FurnitureKind.Counter, new Vector3(4.0f, l1, 15.85f), 180, white),
                new Furniture(FurnitureKind.Fridge, new Vector3(8.2f, l1, 15.85f), 180, white),

                // Dining room.
                new Furniture(FurnitureKind.DiningTable, new Vector3(11.5f, l1, 12.6f), 0, DarkOak),
                new Furniture(FurnitureKind.Chair, new Vector3(10.4f, l1, 12.0f), 90, DarkOak),
                new Furniture(FurnitureKind.Chair, new Vector3(10.4f, l1, 13.3f), 90, DarkOak),
                new Furniture(FurnitureKind.Chair, new Vector3(12.6f, l1, 12.0f), 270, DarkOak),
                new Furniture(FurnitureKind.Chair, new Vector3(12.6f, l1, 13.3f), 270, DarkOak),
                new Furniture(FurnitureKind.Sideboard, new Vector3(11.5f, l1, 15.85f), 180, DarkOak),

                // Old Guy's den: the dated room (striped wallpaper in gm_house4): a plaid sofa, an armchair, the TV, a radiator.
                new Furniture(FurnitureKind.Rug, new Vector3(17.0f, l1, 12.5f), 0, new Color(0.45f, 0.35f, 0.20f)),
                new Furniture(FurnitureKind.Sofa, new Vector3(17.0f, l1, 15.85f), 180, plaid),
                new Furniture(FurnitureKind.Armchair, new Vector3(15.0f, l1, 11.4f), 45, leather),
                new Furniture(FurnitureKind.Television, new Vector3(19.0f, l1, 9.1f), 0, new Color(0.15f, 0.15f, 0.16f)),
                new Furniture(FurnitureKind.Radiator, new Vector3(19.85f, l1, 13.0f), 270, white),
                new Furniture(FurnitureKind.Sideboard, new Vector3(14.15f, l1, 13.0f), 90, DarkOak),

                // Conservatory.
                new Furniture(FurnitureKind.Plant, new Vector3(0.8f, l1, 19.2f), 0, green),
                new Furniture(FurnitureKind.Plant, new Vector3(8.2f, l1, 19.2f), 0, green),
                new Furniture(FurnitureKind.Armchair, new Vector3(2.4f, l1, 17.6f), 120, pine),
                new Furniture(FurnitureKind.SmallTable, new Vector3(3.6f, l1, 17.4f), 0, pine),

                // Gallery and corridor.
                new Furniture(FurnitureKind.Rug, new Vector3(16.8f, l2, 7.2f), 90, new Color(0.40f, 0.20f, 0.18f)),

                // The grandson's room: his bed, his desk under the window with the slingshot, a toy chest, posters, his drawing of the bridge.
                new Furniture(FurnitureKind.Bed, new Vector3(8.85f, l2, 12.6f), 270, kidBlue),
                new Furniture(FurnitureKind.Desk, new Vector3(3.0f, l2, 15.85f), 180, pine),
                new Furniture(FurnitureKind.Chair, new Vector3(3.0f, l2, 14.6f), 0, pine),
                new Furniture(FurnitureKind.ToyChest, new Vector3(0.15f, l2, 10.4f), 90, red),
                new Furniture(FurnitureKind.Poster, new Vector3(4.5f, l2 + 1.8f, 9.1f), 0, new Color(0.95f, 0.75f, 0.20f)),
                new Furniture(FurnitureKind.Drawing, new Vector3(0.15f, l2 + 1.7f, 15.1f), 90, white),

                // Bathroom.
                new Furniture(FurnitureKind.Bathtub, new Vector3(11.5f, l2, 15.85f), 180, white),
                new Furniture(FurnitureKind.Toilet, new Vector3(9.1f, l2, 10.6f), 90, white),
                new Furniture(FurnitureKind.Basin, new Vector3(13.9f, l2, 11.6f), 270, white),

                // The study: books, a desk, and the wand in its glass case.
                new Furniture(FurnitureKind.Bookcase, new Vector3(14.1f, l2, 1.6f), 90, DarkOak),
                new Furniture(FurnitureKind.Bookcase, new Vector3(14.1f, l2, 3.8f), 90, DarkOak),
                new Furniture(FurnitureKind.Desk, new Vector3(16.6f, l2, 0.15f), 0, DarkOak),
                new Furniture(FurnitureKind.Chair, new Vector3(16.6f, l2, 1.5f), 180, leather),
                new Furniture(FurnitureKind.GlassCase, new Vector3(GlassCase.x, l2, GlassCase.z), 270, DarkOak),

                // Old Guy's bedroom.
                new Furniture(FurnitureKind.DoubleBed, new Vector3(19.85f, l2, 12.5f), 270, new Color(0.62f, 0.55f, 0.75f)),
                new Furniture(FurnitureKind.Nightstand, new Vector3(19.85f, l2, 10.7f), 270, DarkOak),
                new Furniture(FurnitureKind.Nightstand, new Vector3(19.85f, l2, 14.3f), 270, DarkOak),
                new Furniture(FurnitureKind.Wardrobe, new Vector3(14.15f, l2, 13.6f), 90, DarkOak),
                new Furniture(FurnitureKind.Dresser, new Vector3(15.6f, l2, 9.15f), 0, DarkOak),

                // The garden and the tree house.
                new Furniture(FurnitureKind.Tree, TreeFoot, 0, new Color(0.28f, 0.48f, 0.22f)),
                new Furniture(FurnitureKind.Bench, new Vector3(15.0f, l1, 31.0f), 180, DarkOak),
                new Furniture(FurnitureKind.Crate, new Vector3(6.6f, l2, 29.2f), 10, pine),
            };
            return f.ToArray();
        }

        // ---- the weapons, Old Guy and the swordsman ----

        /// <summary>
        /// The five weapons: the three starters on the route (the Kitchen Knife in the knife block, the Slingshot on the grandson's desk, the
        /// Magic Wand in its glass case), the Butter Knife on the counter beside the block, and the Baseball Bat hidden in the tree house.
        /// Ids are ItemRegistry's (OldGuysHousePlanTests checks them).
        /// </summary>
        public static readonly ItemSpot[] Items =
        {
            new ItemSpot("kitchen_knife", new Vector3(0.65f, LevelY(1) + KitchenCounterHeight + 0.30f, KnifeBlockZ), new Vector3(0, 90, 180)),
            new ItemSpot("butter_knife", new Vector3(0.75f, LevelY(1) + KitchenCounterHeight + 0.012f, 12.1f), new Vector3(90, 15, 0)),
            new ItemSpot("slingshot", new Vector3(2.6f, LevelY(2) + 1.0f + 0.02f, 15.4f), new Vector3(90, 20, 0)),
            new ItemSpot("magic_wand", new Vector3(GlassCase.x, LevelY(2) + 1.16f, GlassCase.z), new Vector3(90, 90, 0)),
            new ItemSpot("baseball_bat", new Vector3(3.4f, LevelY(2) + 0.25f, 29.4f), new Vector3(14, 0, 0)),
        };

        /// <summary>The one fight: a swordsman in Old Guy's bedroom (owner: "maybe just one swordsman upstairs").</summary>
        public static readonly Vector3 SwordsmanSpot = new Vector3(16.6f, LevelY(2), 11.6f);

        public const float SwordsmanYaw = 200.0f;

        /// <summary>Where Old Guy waits: at the foot of the outdoor steps, then in his den.</summary>
        public static readonly Vector3 OldGuyAtTheSteps = new Vector3(19.4f, 0, -5.0f);

        public static readonly Vector3 OldGuyInTheDen = new Vector3(17.4f, LevelY(1), 12.4f);

        /// <summary>The whole lot, the fence and the driveway included, on the ground plan.</summary>
        public static readonly Area Lot = new Area(-4.5f, -6.5f, 27.5f, 35.5f);
    }
}
