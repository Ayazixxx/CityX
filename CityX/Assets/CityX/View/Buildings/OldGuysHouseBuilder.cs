using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using UnityEngine;
using UnityEngine.Rendering;
using Finish = CityX.World.OldGuysHouse.Finish;
using House = CityX.World.OldGuysHouse;

namespace CityX.View
{
    /// <summary>
    /// Builds the tutorial house (OldGuysHouse, docs/OLD_GUYS_HOUSE.md) under a transform, in the house's own frame: floors and ceilings per
    /// room, walls with their doors and windows cut out and framed, the stair core and the outdoor steps, the porch, railings, the gable roof,
    /// the furniture (FurnitureKit), a lamp and a light in every room, and loose bottles on the mantel. Everything solid collides (a mesh collider
    /// per part); the case over the wand does not. What breaks is an object of its own: every window's pane (HousePane) and the planks across
    /// the front door, with the door behind them (BoardedDoor).
    /// </summary>
    public static class OldGuysHouseBuilder
    {
        public struct Materials
        {
            public Material Structure;
            public Material Glass;

            /// <summary>What a shattered pane's pieces are drawn with (the city's broken glass).</summary>
            public Material Shards;
        }

        private static readonly Color Brick = new Color(0.62f, 0.40f, 0.32f);
        private static readonly Color Stone = new Color(0.66f, 0.64f, 0.60f);
        private static readonly Color Frame = new Color(0.94f, 0.94f, 0.92f);
        private static readonly Color Rail = new Color(0.22f, 0.20f, 0.18f);
        private static readonly Color DoorGreen = new Color(0.20f, 0.34f, 0.26f);
        private static readonly Color Plank = new Color(0.60f, 0.46f, 0.30f);
        private static readonly Color RollUpGrey = new Color(0.58f, 0.60f, 0.62f);

        /// <summary>The frame round a door or window: its width, and how far it stands proud of each face.</summary>
        private const float FrameWidth = 0.1f;

        private const float FrameProud = 0.03f;

        public const float LightIntensity = 1.3f;

        public static readonly Color LightColour = new Color(1.0f, 0.90f, 0.76f);

        /// <summary>
        /// The house under <paramref name="parent"/>, returning its root. <paramref name="footingDepth"/>: how far below its floor the footing under the
        /// whole lot reaches, so on the city's uneven ground no gap shows under its edges (0: none, for a house on flat ground).
        /// </summary>
        public static GameObject Build(Transform parent, Materials materials, float footingDepth = 0.0f)
        {
            var root = new GameObject("OldGuysHouse");
            root.transform.SetParent(parent, false);

            var structure = new MeshBuffer();
            var caseGlass = new MeshBuffer();
            var furniture = new MeshBuffer();

            Floors(structure);
            Walls(structure);
            Stairs(structure);
            Ground(structure, footingDepth);
            Roofs(structure);
            Railings(structure);
            foreach (House.Furniture piece in House.FurnitureList)
            {
                if (piece.Kind != House.FurnitureKind.Bottle)
                {
                    FurnitureKit.Add(furniture, piece);
                }
            }
            Lamps(furniture, root.transform);
            Box(caseGlass, House.GlassCase + new Vector3(-0.38f, House.LevelY(2) + 1.1f, -0.38f), House.GlassCase + new Vector3(0.38f, House.LevelY(2) + 1.7f, 0.38f),
                SurfaceLook.Default);

            Emit(root.transform, "Structure", structure, materials.Structure, true, true);
            Emit(root.transform, "Furniture", furniture, materials.Structure, true, true);
            Containers(root.transform, materials.Structure);
            Panes(root.transform, materials);
            FrontDoor(root.transform, materials.Structure);
            Emit(root.transform, "CaseGlass", caseGlass, materials.Glass, false, false);
            Bottles(root.transform, materials.Structure);
            return root;
        }

        // ---- looks ----

        private static int SliceOf(Finish finish)
        {
            switch (finish)
            {
                case Finish.Brick: return (int)WallMaterial.Brick;
                case Finish.Concrete: return (int)WallMaterial.Concrete;
                case Finish.Tile: return (int)WallMaterial.Tile;
                case Finish.Roof: return (int)WallMaterial.Roof;
                case Finish.Metal: return (int)WallMaterial.Metal;
                case Finish.Paving: return SurfaceLook.Paving;
                default: return (int)WallMaterial.Plaster;
            }
        }

        private static Color32 Look(Finish finish, Color tint, bool indoors, float wear = 0.12f) => SurfaceLook.Pack(tint, SliceOf(finish), wear, indoors);

        private static readonly Color32 CeilingLook = SurfaceLook.Pack(House.Ceiling, (int)WallMaterial.Plaster, 0.05f, true);
        private static readonly Color32 RenderLook = SurfaceLook.Pack(House.Render, (int)WallMaterial.Plaster, 0.2f);
        private static readonly Color32 BrickLook = SurfaceLook.Pack(Brick, (int)WallMaterial.Brick, 0.25f);
        private static readonly Color32 StoneLook = SurfaceLook.Pack(Stone, (int)WallMaterial.Stone, 0.3f);
        private static readonly Color32 FrameLook = SurfaceLook.Pack(Frame, (int)WallMaterial.Plaster, 0.05f, true);
        private static readonly Color32 RailLook = SurfaceLook.Pack(Rail, (int)WallMaterial.Metal, 0.1f);
        private static readonly Color32 PlankLook = SurfaceLook.Pack(Plank, (int)WallMaterial.Plaster, 0.3f);

        /// <summary>
        /// The look of a wall face whose room side is at (x, z) at height <paramref name="y"/>: the room's paint indoors; outside, brick at street
        /// level and render above. A face in the open over a room (the living room's upper walls) belongs to the room under it.
        /// </summary>
        private static Color32 FaceLook(float x, float z, float y)
        {
            for (int level = Mathf.Clamp(Mathf.FloorToInt(y / House.Storey), 0, 2); level >= 0; level--)
            {
                House.Room room = House.RoomAt(level, x, z);
                if (room == null)
                {
                    continue;
                }
                if (room.Outdoors)
                {
                    break;
                }
                return Look(room.Floor == Finish.Concrete ? Finish.Concrete : Finish.Plaster, room.WallTint, true);
            }
            return y < House.Storey - 0.01f ? BrickLook : RenderLook;
        }

        // ---- boxes and slabs ----

        /// <summary>An axis-aligned box, every face <paramref name="look"/>, its bottom too.</summary>
        private static void Box(MeshBuffer b, Vector3 min, Vector3 max, Color32 look)
        {
            b.Look = look;
            b.AddBox(min, max, true);
        }

        /// <summary>A box with its own look on top, underneath and round its sides.</summary>
        private static void Slab(MeshBuffer b, Vector3 min, Vector3 max, Color32 top, Color32 bottom, Color32 sides)
        {
            b.Look = sides;
            b.AddBox(min, max, false);
            b.Look = top;
            b.AddQuad(new Vector3(min.x, max.y + 0.0f, min.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), Vector3.up,
                new Vector2(min.x, min.z), new Vector2(min.x, max.z), new Vector2(max.x, max.z), new Vector2(max.x, min.z));
            b.Look = bottom;
            b.AddQuad(new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z), Vector3.down,
                new Vector2(min.x, max.z), new Vector2(max.x, max.z), new Vector2(max.x, min.z), new Vector2(min.x, min.z));
        }

        /// <summary>A beam of square section <paramref name="half"/> from <paramref name="from"/> to <paramref name="to"/>, at any slope.</summary>
        private static void Beam(MeshBuffer b, Vector3 from, Vector3 to, float half, Color32 look)
        {
            Vector3 along = to - from;
            if (along.sqrMagnitude < 1e-6f)
            {
                return;
            }
            Vector3 forward = along.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            side = side.sqrMagnitude > 1e-6f ? side.normalized * half : Vector3.right * half;
            Vector3 up = Vector3.Cross(forward, side).normalized * half;
            b.Look = look;
            Vector3[] c =
            {
                from - side - up, from + side - up, from + side + up, from - side + up,
                to - side - up, to + side - up, to + side + up, to - side + up,
            };
            Quad(b, c[0], c[1], c[5], c[4], -up);
            Quad(b, c[3], c[2], c[6], c[7], up);
            Quad(b, c[0], c[3], c[7], c[4], -side);
            Quad(b, c[1], c[2], c[6], c[5], side);
            Quad(b, c[0], c[1], c[2], c[3], -forward);
            Quad(b, c[4], c[5], c[6], c[7], forward);
        }

        private static void Quad(MeshBuffer b, Vector3 a, Vector3 bb, Vector3 c, Vector3 d, Vector3 normal)
        {
            float u = (bb - a).magnitude, v = (d - a).magnitude;
            b.AddQuad(a, bb, c, d, normal.normalized, Vector2.zero, new Vector2(u, 0), new Vector2(u, v), new Vector2(0, v));
        }

        // ---- floors and ceilings ----

        private static void Floors(MeshBuffer b)
        {
            foreach (House.Room room in House.Rooms)
            {
                bool thin = room.Outdoors && room.Level < 2;
                float top = room.FloorY + (room.Outdoors && room.Level == 0 ? 0.03f : 0.0f);
                float thickness = thin ? 0.05f : House.SlabThickness;
                Color32 floor = Look(room.Floor, room.FloorTint, !room.Outdoors);
                Color32 under = room.Outdoors ? RenderLook : CeilingLook;
                foreach (House.Area a in room.Floors)
                {
                    Slab(b, new Vector3(a.X0, top - thickness, a.Z0), new Vector3(a.X1, top, a.Z1), floor, under, under);
                }
            }
            // The ceiling under the roof, over the whole upper floor and the living room.
            Slab(b, new Vector3(0, House.EavesY - House.SlabThickness, 0), new Vector3(House.Width, House.EavesY, House.Depth), RenderLook, CeilingLook, RenderLook);
        }

        // ---- walls, doors, windows ----

        private static void Walls(MeshBuffer b)
        {
            foreach (House.Wall wall in House.Walls)
            {
                // Openings that share a span (the living room's two windows, one over the other) are cut as one column: wall, window, wall, window, wall.
                var openings = new List<House.Opening>(wall.Openings);
                openings.Sort((p, q) => p.From != q.From ? p.From.CompareTo(q.From) : p.Sill.CompareTo(q.Sill));
                float cursor = wall.From;
                for (int i = 0; i < openings.Count;)
                {
                    House.Opening first = openings[i];
                    WallPiece(b, wall, cursor, first.From, wall.Y0, wall.Y1);
                    float y = wall.Y0;
                    for (; i < openings.Count && Mathf.Approximately(openings[i].From, first.From) && Mathf.Approximately(openings[i].To, first.To); i++)
                    {
                        WallPiece(b, wall, first.From, first.To, y, openings[i].Sill);
                        Dress(b, wall, openings[i]);
                        y = openings[i].Head;
                    }
                    WallPiece(b, wall, first.From, first.To, y, wall.Y1);
                    cursor = first.To;
                }
                WallPiece(b, wall, cursor, wall.To, wall.Y0, wall.Y1);
            }
        }

        /// <summary>Where along <paramref name="w"/> a room starts or ends: a wall piece is split there, so each stretch takes the paint of the room it faces.</summary>
        private static List<float> RoomEdgesAlong(House.Wall w, float from, float to)
        {
            var cuts = new List<float>();
            foreach (House.Room room in House.Rooms)
            {
                foreach (House.Area a in room.Floors)
                {
                    foreach (float edge in w.AlongX ? new[] { a.X0, a.X1 } : new[] { a.Z0, a.Z1 })
                    {
                        if (edge > from + 0.05f && edge < to - 0.05f && !cuts.Contains(edge)) cuts.Add(edge);
                    }
                }
            }
            cuts.Sort();
            return cuts;
        }

        /// <summary>A point <paramref name="along"/> the wall, <paramref name="across"/> off its middle, at <paramref name="y"/>.</summary>
        private static Vector3 At(House.Wall w, float along, float across, float y) => w.AlongX ? new Vector3(along, y, w.At + across) : new Vector3(w.At + across, y, along);

        /// <summary>A box of <paramref name="w"/> from <paramref name="from"/> to <paramref name="to"/> along it, <paramref name="y0"/> to <paramref name="y1"/>, +-<paramref name="half"/> across.</summary>
        private static void WallBox(MeshBuffer b, House.Wall w, float from, float to, float y0, float y1, float half, Color32 look)
        {
            Vector3 p = At(w, from, -half, y0), q = At(w, to, half, y1);
            Box(b, Vector3.Min(p, q), Vector3.Max(p, q), look);
        }

        private static void WallPiece(MeshBuffer b, House.Wall w, float from, float to, float y0, float y1)
        {
            if (to - from < 0.01f || y1 - y0 < 0.01f)
            {
                return;
            }
            float start = from;
            foreach (float cut in RoomEdgesAlong(w, from, to))
            {
                WallStretch(b, w, start, cut, y0, y1);
                start = cut;
            }
            WallStretch(b, w, start, to, y0, y1);
        }

        private static void WallStretch(MeshBuffer b, House.Wall w, float from, float to, float y0, float y1)
        {
            float half = w.Thickness * 0.5f, mid = (from + to) * 0.5f, midY = (y0 + y1) * 0.5f;
            Vector3 minusSide = At(w, mid, -half - 0.3f, midY), plusSide = At(w, mid, half + 0.3f, midY);
            // Each face takes the room it faces; the ends, the top and the bottom are plain.
            Face(b, w, from, to, y0, y1, -half, FaceLook(minusSide.x, minusSide.z, midY));
            Face(b, w, from, to, y0, y1, half, FaceLook(plusSide.x, plusSide.z, midY));
            Vector3 along = w.AlongX ? Vector3.right : Vector3.forward, across = w.AlongX ? Vector3.forward : Vector3.right;
            b.Look = FrameLook;
            Quad(b, At(w, from, -half, y1), At(w, to, -half, y1), At(w, to, half, y1), At(w, from, half, y1), Vector3.up);
            Quad(b, At(w, from, -half, y0), At(w, to, -half, y0), At(w, to, half, y0), At(w, from, half, y0), Vector3.down);
            Quad(b, At(w, from, -half, y0), At(w, from, half, y0), At(w, from, half, y1), At(w, from, -half, y1), -along);
            Quad(b, At(w, to, -half, y0), At(w, to, half, y0), At(w, to, half, y1), At(w, to, -half, y1), along);
        }

        /// <summary>One side of a wall piece.</summary>
        private static void Face(MeshBuffer b, House.Wall w, float from, float to, float y0, float y1, float across, Color32 look)
        {
            float s = Mathf.Sign(across);
            Vector3 normal = w.AlongX ? new Vector3(0, 0, s) : new Vector3(s, 0, 0);
            b.Look = look;
            b.AddQuad(At(w, from, across, y0), At(w, to, across, y0), At(w, to, across, y1), At(w, from, across, y1), normal,
                new Vector2(from, y0), new Vector2(to, y0), new Vector2(to, y1), new Vector2(from, y1));
        }

        /// <summary>A door's frame, a window's frame and sill (its pane is an object of its own, see <see cref="Panes"/>), the front door's frame, the closed roll-up door.</summary>
        private static void Dress(MeshBuffer b, House.Wall w, House.Opening o)
        {
            float half = w.Thickness * 0.5f + FrameProud;
            switch (o.Kind)
            {
                case House.OpeningKind.Door:
                case House.OpeningKind.Arch:
                    WallBox(b, w, o.From - FrameWidth, o.From, o.Sill, o.Head + FrameWidth, half, FrameLook);
                    WallBox(b, w, o.To, o.To + FrameWidth, o.Sill, o.Head + FrameWidth, half, FrameLook);
                    WallBox(b, w, o.From, o.To, o.Head, o.Head + FrameWidth, half, FrameLook);
                    break;
                case House.OpeningKind.Window:
                    WallBox(b, w, o.From - FrameWidth, o.From, o.Sill - FrameWidth, o.Head + FrameWidth, half, FrameLook);
                    WallBox(b, w, o.To, o.To + FrameWidth, o.Sill - FrameWidth, o.Head + FrameWidth, half, FrameLook);
                    WallBox(b, w, o.From, o.To, o.Head, o.Head + FrameWidth, half, FrameLook);
                    WallBox(b, w, o.From, o.To, o.Sill - FrameWidth, o.Sill, half, FrameLook);
                    WallBox(b, w, (o.From + o.To) * 0.5f - 0.03f, (o.From + o.To) * 0.5f + 0.03f, o.Sill, o.Head, 0.04f, FrameLook);   // the mullion
                    if (w.Outer)
                    {
                        float outside = OutsideSign(w, o);
                        Vector3 p = At(w, o.From - 0.15f, outside * w.Thickness * 0.5f, o.Sill - 0.12f);
                        Vector3 q = At(w, o.To + 0.15f, outside * (w.Thickness * 0.5f + 0.18f), o.Sill - 0.04f);
                        Box(b, Vector3.Min(p, q), Vector3.Max(p, q), StoneLook);
                    }
                    break;
                case House.OpeningKind.FrontDoor:
                    // Its leaf and the planks across it are objects of their own (FrontDoor).
                    WallBox(b, w, o.From - FrameWidth, o.From, o.Sill, o.Head + FrameWidth, half, FrameLook);
                    WallBox(b, w, o.To, o.To + FrameWidth, o.Sill, o.Head + FrameWidth, half, FrameLook);
                    WallBox(b, w, o.From, o.To, o.Head, o.Head + FrameWidth, half, FrameLook);
                    break;
                case House.OpeningKind.RollUpDoor:
                {
                    WallBox(b, w, o.From, o.To, o.Sill, o.Head, 0.04f, Look(Finish.Metal, RollUpGrey, false));
                    float outside = OutsideSign(w, o);
                    for (float y = o.Sill + 0.2f; y < o.Head - 0.1f; y += 0.28f)
                    {
                        Vector3 p = At(w, o.From, outside * 0.04f, y), q = At(w, o.To, outside * 0.08f, y + 0.05f);
                        Box(b, Vector3.Min(p, q), Vector3.Max(p, q), Look(Finish.Metal, Color.Lerp(RollUpGrey, Color.black, 0.2f), false));
                    }
                    break;
                }
            }
        }

        /// <summary>+1 if the outside of an outer wall is on its +z (or +x) side at this opening, else -1.</summary>
        private static float OutsideSign(House.Wall w, House.Opening o)
        {
            float mid = (o.From + o.To) * 0.5f, y = (o.Sill + o.Head) * 0.5f;
            Vector3 plus = At(w, mid, w.Thickness * 0.5f + 0.4f, y);
            int level = Mathf.Clamp(Mathf.FloorToInt(y / House.Storey), 0, 2);
            for (int l = level; l >= 0; l--)
            {
                House.Room room = House.RoomAt(l, plus.x, plus.z);
                if (room != null)
                {
                    return room.Outdoors ? 1.0f : -1.0f;
                }
            }
            return 1.0f;
        }

        // ---- stairs ----

        private static void Stairs(MeshBuffer b)
        {
            foreach (House.Step s in House.CoreSteps())
            {
                Color tint = s.Top > House.Storey ? new Color(0.62f, 0.44f, 0.27f) : new Color(0.62f, 0.62f, 0.60f);
                Slab(b, new Vector3(s.Tread.X0, s.Bottom, s.Tread.Z0), new Vector3(s.Tread.X1, s.Top, s.Tread.Z1), Look(Finish.Wood, tint, true), CeilingLook, Look(Finish.Plaster, Color.Lerp(tint, Color.black, 0.15f), true));
            }
            foreach (House.Step s in House.OutdoorSteps())
            {
                Slab(b, new Vector3(s.Tread.X0, s.Bottom, s.Tread.Z0), new Vector3(s.Tread.X1, s.Top, s.Tread.Z1), Look(Finish.Paving, House.Paving, false), StoneLook, StoneLook);
            }
            foreach (House.Handrail h in House.Handrails)
            {
                Beam(b, h.From, h.To, 0.035f, RailLook);
            }
            foreach (House.Handrail h in House.OutdoorRails())
            {
                Beam(b, h.From, h.To, 0.04f, RailLook);
                int posts = Mathf.Max(2, Mathf.CeilToInt((h.To - h.From).magnitude / 1.4f) + 1);
                for (int i = 0; i < posts; i++)
                {
                    Vector3 top = Vector3.Lerp(h.From, h.To, i / (posts - 1.0f));
                    float foot = top.y - House.RailHeight;
                    Box(b, new Vector3(top.x - 0.03f, foot, top.z - 0.03f), new Vector3(top.x + 0.03f, top.y, top.z + 0.03f), RailLook);
                }
            }
        }

        // ---- the footing and the solid blocks ----

        /// <summary>The footing's top stands this far under the floor, clear of the floors' own undersides and the paving the city lays round it.</summary>
        private const float FootingTopBelowFloor = 0.03f;

        private static void Ground(MeshBuffer b, float footingDepth)
        {
            if (footingDepth > FootingTopBelowFloor)
            {
                House.Area lot = House.Lot;
                Slab(b, new Vector3(lot.X0, -footingDepth, lot.Z0), new Vector3(lot.X1, -FootingTopBelowFloor, lot.Z1), Look(Finish.Paving, House.Paving, false), StoneLook, StoneLook);
            }
            foreach (House.Block block in House.Blocks)
            {
                Slab(b, block.Min, block.Max, Look(block.Top, block.TopTint, false), StoneLook, block.Top == Finish.Concrete ? BrickLook : StoneLook);   // the footing's sides are the street level's brick outside
            }
        }

        // ---- roofs ----

        /// <summary>
        /// The single slope: a metal slab from its high front edge down to its low back edge (House.RoofTopAt), its edges a dark band, and the
        /// walls risen under it to meet its underside: the front and back as rectangles, each side as the slope's trapezoid.
        /// </summary>
        private static void Roofs(MeshBuffer b)
        {
            float x0 = -House.RoofOverhang, x1 = House.Width + House.RoofOverhang;
            float z0 = -House.RoofFrontOverhang, z1 = House.Depth + House.RoofOverhang;
            Vector3 down = Vector3.down * House.RoofThickness;
            Vector3 f0 = new Vector3(x0, House.RoofTopAt(z0), z0), f1 = new Vector3(x1, f0.y, z0);
            Vector3 k0 = new Vector3(x0, House.RoofTopAt(z1), z1), k1 = new Vector3(x1, k0.y, z1);
            Vector3 up = Vector3.Cross(k0 - f0, f1 - f0).normalized;
            if (up.y < 0) up = -up;
            Color32 metal = SurfaceLook.Pack(House.RoofMetal, (int)WallMaterial.Metal, 0.15f);
            b.Look = metal;
            Quad(b, f0, f1, k1, k0, up);
            Quad(b, f0, f1, f1 + down, f0 + down, Vector3.back);
            Quad(b, k0, k1, k1 + down, k0 + down, Vector3.forward);
            Quad(b, f0, k0, k0 + down, f0 + down, Vector3.left);
            Quad(b, f1, k1, k1 + down, f1 + down, Vector3.right);
            b.Look = RenderLook;
            Quad(b, f0 + down, f1 + down, k1 + down, k0 + down, -up);

            float wall = House.OuterWallThickness * 0.5f, eaves = House.EavesY;
            float Under(float z) => House.RoofTopAt(z) - House.RoofThickness;
            foreach (float z in new[] { -wall, House.Depth + wall })
            {
                float top = Under(z);
                Vector3 a = new Vector3(-wall, eaves, z), c = new Vector3(House.Width + wall, eaves, z);
                Vector3 n = z < 0 ? Vector3.back : Vector3.forward;
                Quad(b, a, c, c + Vector3.up * (top - eaves), a + Vector3.up * (top - eaves), n);
                Quad(b, c, a, a + Vector3.up * (top - eaves), c + Vector3.up * (top - eaves), -n);
            }
            foreach (float x in new[] { -wall, House.Width + wall })
            {
                Vector3 n = x < 0 ? Vector3.left : Vector3.right;
                Vector3 a = new Vector3(x, eaves, -wall), c = new Vector3(x, eaves, House.Depth + wall);
                Vector3 at = new Vector3(x, Under(-wall), -wall), ct = new Vector3(x, Under(House.Depth + wall), House.Depth + wall);
                Quad(b, a, c, ct, at, n);
                Quad(b, c, a, at, ct, -n);
            }
        }

        // ---- railings ----

        private static void Railings(MeshBuffer b)
        {
            foreach (House.Rail rail in House.Rails)
            {
                Vector3 a = new Vector3(rail.A.x, rail.Y, rail.A.y), c = new Vector3(rail.B.x, rail.Y, rail.B.y);
                Beam(b, a + Vector3.up * House.RailHeight, c + Vector3.up * House.RailHeight, 0.045f, RailLook);
                Beam(b, a + Vector3.up * 0.15f, c + Vector3.up * 0.15f, 0.03f, RailLook);
                int balusters = Mathf.Max(2, Mathf.CeilToInt((c - a).magnitude / 0.4f) + 1);
                for (int i = 0; i < balusters; i++)
                {
                    Vector3 p = Vector3.Lerp(a, c, i / (balusters - 1.0f));
                    Box(b, new Vector3(p.x - 0.025f, p.y, p.z - 0.025f), new Vector3(p.x + 0.025f, p.y + House.RailHeight, p.z + 0.025f), RailLook);
                }
            }
        }

        // ---- lamps and lights ----

        private static void Lamps(MeshBuffer furniture, Transform root)
        {
            var lights = new GameObject("Lights");
            lights.transform.SetParent(root, false);
            foreach (House.Room room in House.Rooms)
            {
                if (room.Outdoors)
                {
                    continue;
                }
                House.Area biggest = room.Floors[0];
                foreach (House.Area a in room.Floors)
                {
                    if ((a.X1 - a.X0) * (a.Z1 - a.Z0) > (biggest.X1 - biggest.X0) * (biggest.Z1 - biggest.Z0)) biggest = a;
                }
                Vector2 c = biggest.Centre;
                if (room.Name.StartsWith("Stair core"))
                {
                    c = new Vector2(c.x, House.FlightTopZ - 0.3f);
                }
                FurnitureKit.Add(furniture, new House.Furniture(House.FurnitureKind.CeilingLamp, new Vector3(c.x, room.CeilingY, c.y), 0, new Color(0.96f, 0.92f, 0.80f)));
                var lightObject = new GameObject("Light " + room.Name);
                lightObject.transform.SetParent(lights.transform, false);
                lightObject.transform.localPosition = new Vector3(c.x, room.CeilingY - 1.1f, c.y);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = LightColour;
                light.intensity = LightIntensity;
                light.range = Mathf.Clamp(Mathf.Max(biggest.X1 - biggest.X0, biggest.Z1 - biggest.Z0) * 1.1f, 6.0f, 12.0f);
                light.shadows = LightShadows.None;
            }
        }

        // ---- what breaks: the windows' panes and the front door's planks ----

        /// <summary>Thickness of a pane of glass, and of a plank.</summary>
        private const float PaneThickness = 0.04f, PlankThickness = 0.05f, PlankHeight = 0.28f;

        /// <summary>Every window's pane as an object of its own (HousePane), numbered as the plan numbers them, so each one cracks and shatters by itself.</summary>
        private static void Panes(Transform root, Materials materials)
        {
            var parent = new GameObject("Panes").transform;
            parent.SetParent(root, false);
            int piece = House.FrontDoorPlanks;
            foreach (House.Wall w in House.Walls)
            {
                foreach (House.Opening o in w.Openings)
                {
                    if (o.Kind != House.OpeningKind.Window)
                    {
                        continue;
                    }
                    float height = o.Head - o.Sill;
                    Vector3 size = w.AlongX ? new Vector3(o.Width, height, PaneThickness) : new Vector3(PaneThickness, height, o.Width);
                    var shape = new MeshBuffer();
                    Box(shape, -size * 0.5f, size * 0.5f, SurfaceLook.Default);
                    GameObject pane = Emit(parent, "Pane " + piece, shape, materials.Glass, false, false);
                    pane.transform.localPosition = At(w, (o.From + o.To) * 0.5f, 0, (o.Sill + o.Head) * 0.5f);
                    pane.AddComponent<BoxCollider>().size = size;
                    var glass = pane.AddComponent<HousePane>();
                    glass.Piece = piece++;
                    glass.Size = new Vector2(o.Width, height);
                    glass.AlongLocalX = w.AlongX;
                    glass.ShardMaterial = materials.Shards;
                }
            }
        }

        /// <summary>
        /// The front door (docs/OLD_GUYS_HOUSE.md): its green leaf on a hinge, shut, and three planks nailed across it outside (pieces 0 to 2: two
        /// across, one askew), each an object that takes blows and falls (BoardedPlank); with all three gone the door swings open (BoardedDoor).
        /// </summary>
        private static void FrontDoor(Transform root, Material material)
        {
            foreach (House.Wall w in House.Walls)
            {
                foreach (House.Opening o in w.Openings)
                {
                    if (o.Kind == House.OpeningKind.FrontDoor)
                    {
                        BuildFrontDoor(root, material, w, o);
                    }
                }
            }
        }

        private static void BuildFrontDoor(Transform root, Material material, House.Wall w, House.Opening o)
        {
            var door = new GameObject("Front door");
            door.transform.SetParent(root, false);
            float outside = OutsideSign(w, o);
            Vector3 along = w.AlongX ? Vector3.right : Vector3.forward, outward = (w.AlongX ? Vector3.forward : Vector3.right) * outside;

            // The leaf, its hinge at the door's From side, in the wall's middle; it swings in, away from the planks.
            var hinge = new GameObject("Leaf hinge");
            hinge.transform.SetParent(door.transform, false);
            hinge.transform.localPosition = At(w, o.From, 0, o.Sill);
            var leafShape = new MeshBuffer();
            Vector3 leafMin = Vector3.Min(Vector3.zero - outward * 0.05f, along * o.Width + Vector3.up * (o.Head - o.Sill) + outward * 0.05f);
            Vector3 leafMax = Vector3.Max(Vector3.zero - outward * 0.05f, along * o.Width + Vector3.up * (o.Head - o.Sill) + outward * 0.05f);
            Box(leafShape, leafMin, leafMax, Look(Finish.Plaster, DoorGreen, false));
            GameObject leaf = Emit(hinge.transform, "Leaf", leafShape, material, false, true);
            var leafBox = leaf.AddComponent<BoxCollider>();
            leafBox.center = (leafMin + leafMax) * 0.5f;
            leafBox.size = leafMax - leafMin;

            var boarded = door.AddComponent<BoardedDoor>();
            boarded.Leaf = hinge.transform;
            // Turning about +y takes +along toward -outward when the turn is positive for an along-x wall facing +z (and the mirror otherwise).
            Vector3 turned = Quaternion.Euler(0, BoardedDoor.OpenDegrees, 0) * along;
            boarded.OpenSign = Vector3.Dot(turned, outward) < 0.0f ? 1.0f : -1.0f;

            // The planks, outside the leaf: two across at waist and chest height, one from low on one side to high on the other.
            float off = w.Thickness * 0.5f + 0.06f;
            float mid = (o.From + o.To) * 0.5f;
            Vector3 low = At(w, o.From - 0.2f, outside * off, o.Sill + 0.5f), high = At(w, o.To + 0.2f, outside * off, o.Head - 0.5f);
            (Vector3 centre, float length, float tilt)[] planks =
            {
                (At(w, mid, outside * off, o.Sill + 1.0f), o.Width + 0.6f, 0.0f),
                (At(w, mid, outside * off, o.Sill + 2.5f), o.Width + 0.6f, 0.0f),
                ((low + high) * 0.5f, (high - low).magnitude, Mathf.Atan2(o.Head - o.Sill - 1.0f, o.Width + 0.4f) * Mathf.Rad2Deg),
            };
            for (int i = 0; i < planks.Length; i++)
            {
                var shape = new MeshBuffer();
                Vector3 half = new Vector3(planks[i].length * 0.5f, PlankHeight * 0.5f, PlankThickness * 0.5f);
                Box(shape, -half, half, PlankLook);
                GameObject plank = Emit(door.transform, "Plank " + i, shape, material, false, true);
                // The plank's own x along the wall, z out of it, then tilted in the wall's plane (the askew one, nailed toward the From side's foot).
                Quaternion lay = Quaternion.LookRotation(outward, Vector3.up);
                Vector3 alongLocal = Quaternion.Inverse(lay) * along;
                float tilt = alongLocal.x > 0.0f ? planks[i].tilt : -planks[i].tilt;
                plank.transform.localPosition = planks[i].centre;
                plank.transform.localRotation = lay * Quaternion.Euler(0, 0, tilt);
                plank.AddComponent<BoxCollider>().size = half * 2.0f;
                plank.AddComponent<BoardedPlank>().Piece = i;
            }
        }

        // ---- drawers, doors and lids (docs/LOOTING.md) ----

        /// <summary>
        /// Every part of the furniture that opens, as an object of its own on its hinge (LootContainer), with what the plan puts inside it: the
        /// furniture mesh holds only the carcasses.
        /// </summary>
        private static void Containers(Transform root, Material material)
        {
            var parent = new GameObject("Containers").transform;
            parent.SetParent(root, false);
            foreach (House.Furniture piece in House.FurnitureList)
            {
                List<FurnitureKit.ContainerPart> parts = FurnitureKit.PartsOf(piece.Kind, piece.Tint);
                Quaternion turn = Quaternion.Euler(0, piece.Yaw, 0);
                for (int i = 0; i < parts.Count; i++)
                {
                    FurnitureKit.ContainerPart part = parts[i];
                    var hinge = new GameObject(part.Label);
                    hinge.transform.SetParent(parent, false);
                    hinge.transform.localPosition = piece.Position + turn * part.Pivot;
                    hinge.transform.localRotation = turn;
                    var shape = new MeshBuffer();
                    FurnitureKit.BuildPart(shape, part);
                    GameObject mesh = Emit(hinge.transform, "Part", shape, material, false, true);
                    var box = mesh.AddComponent<BoxCollider>();
                    box.center = (part.Min + part.Max) * 0.5f - part.Pivot;
                    box.size = part.Max - part.Min;
                    // Added once its hinge is in place: it takes the closed pose from where it stands when it wakes.
                    var container = hinge.AddComponent<LootContainer>();
                    container.Kind = part.Kind;
                    container.Slide = part.Slide;
                    container.Label = part.Label;
                    container.LookPoint = part.Front - part.Pivot;
                    if (i == piece.StashPart)
                    {
                        container.Items.AddRange(piece.Stash);
                    }
                }
            }
        }

        // ---- loose bottles on the mantel (the slingshot's first targets) ----

        private static void Bottles(Transform root, Material material)
        {
            foreach (House.Furniture piece in House.FurnitureList)
            {
                if (piece.Kind != House.FurnitureKind.Bottle)
                {
                    continue;
                }
                var shape = new MeshBuffer();
                FurnitureKit.Add(shape, new House.Furniture(piece.Kind, Vector3.zero, 0, piece.Tint));
                GameObject bottle = Emit(root, "Bottle", shape, material, false, true);
                bottle.transform.localPosition = piece.Position;
                var box = bottle.AddComponent<BoxCollider>();
                box.center = new Vector3(0, 0.21f, 0);
                box.size = new Vector3(0.12f, 0.42f, 0.12f);
                var body = bottle.AddComponent<Rigidbody>();
                body.mass = 0.4f;
            }
        }

        // ---- meshes ----

        private static GameObject Emit(Transform parent, string name, MeshBuffer buffer, Material material, bool collider, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (buffer.IsEmpty)
            {
                return go;
            }
            var mesh = new Mesh { name = "OldGuysHouse-" + name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(buffer.Vertices);
            mesh.SetNormals(buffer.Normals);
            mesh.SetUVs(0, buffer.Uvs);
            mesh.SetColors(buffer.Colors);
            mesh.SetTriangles(buffer.Triangles, 0);
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider)
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            return go;
        }
    }
}
