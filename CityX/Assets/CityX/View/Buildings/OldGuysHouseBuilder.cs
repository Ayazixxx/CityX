using System.Collections.Generic;
using CityX.World;
using UnityEngine;
using UnityEngine.Rendering;
using Finish = CityX.World.OldGuysHouse.Finish;
using House = CityX.World.OldGuysHouse;

namespace CityX.View
{
    /// <summary>
    /// Builds the tutorial house (OldGuysHouse, docs/OLD_GUYS_HOUSE.md) under a transform, in the house's own frame: floors and ceilings per
    /// room, walls with their doors and windows cut out and framed, the stair core and the outdoor steps, the raised garden, railings, the fence,
    /// the gable roof, the glass conservatory, the balcony, the plank bridge and the tree house, the furniture (FurnitureKit), a lamp and a light
    /// in every room, and loose bottles on the mantel. Everything solid collides (a mesh collider per part); the case over the wand does not.
    /// </summary>
    public static class OldGuysHouseBuilder
    {
        public struct Materials
        {
            public Material Structure;
            public Material Glass;
        }

        private static readonly Color Brick = new Color(0.62f, 0.40f, 0.32f);
        private static readonly Color Stone = new Color(0.66f, 0.64f, 0.60f);
        private static readonly Color Frame = new Color(0.94f, 0.94f, 0.92f);
        private static readonly Color Rail = new Color(0.22f, 0.20f, 0.18f);
        private static readonly Color FenceWood = new Color(0.50f, 0.36f, 0.22f);
        private static readonly Color DoorGreen = new Color(0.20f, 0.34f, 0.26f);
        private static readonly Color Plank = new Color(0.60f, 0.46f, 0.30f);
        private static readonly Color RollUpGrey = new Color(0.58f, 0.60f, 0.62f);

        /// <summary>The frame round a door or window: its width, and how far it stands proud of each face.</summary>
        private const float FrameWidth = 0.1f;

        private const float FrameProud = 0.03f;

        public const float LightIntensity = 1.3f;

        public static readonly Color LightColour = new Color(1.0f, 0.90f, 0.76f);

        /// <summary>Builds the house as children of <paramref name="parent"/> and returns their root.</summary>
        public static GameObject Build(Transform parent, Materials materials)
        {
            var root = new GameObject("OldGuysHouse");
            root.transform.SetParent(parent, false);

            var structure = new MeshBuffer();
            var glass = new MeshBuffer();
            var caseGlass = new MeshBuffer();
            var furniture = new MeshBuffer();

            Floors(structure);
            Walls(structure, glass);
            Stairs(structure);
            Ground(structure);
            Roofs(structure);
            Conservatory(structure, glass);
            Posts(structure);
            Bridge(structure);
            Railings(structure);
            Fence(structure);
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
            Emit(root.transform, "Glass", glass, materials.Glass, true, false);
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
                case Finish.Grass: return SurfaceLook.Earth;
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
            Slab(b, new Vector3(0, House.EavesY - House.SlabThickness, 0), new Vector3(20, House.EavesY, 16), RenderLook, CeilingLook, RenderLook);
        }

        // ---- walls, doors, windows ----

        private static void Walls(MeshBuffer b, MeshBuffer glass)
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
                        Dress(b, glass, wall, openings[i]);
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

        /// <summary>A door's frame, a window's frame, glass and sill, the barricaded front door, the closed roll-up door.</summary>
        private static void Dress(MeshBuffer b, MeshBuffer glass, House.Wall w, House.Opening o)
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
                    WallBox(glass, w, o.From, o.To, o.Sill, o.Head, 0.02f, SurfaceLook.Default);
                    if (w.Outer)
                    {
                        float outside = OutsideSign(w, o);
                        Vector3 p = At(w, o.From - 0.15f, outside * w.Thickness * 0.5f, o.Sill - 0.12f);
                        Vector3 q = At(w, o.To + 0.15f, outside * (w.Thickness * 0.5f + 0.18f), o.Sill - 0.04f);
                        Box(b, Vector3.Min(p, q), Vector3.Max(p, q), StoneLook);
                    }
                    break;
                case House.OpeningKind.FrontDoor:
                {
                    WallBox(b, w, o.From, o.To, o.Sill, o.Head, 0.05f, Look(Finish.Plaster, DoorGreen, false));
                    WallBox(b, w, o.From - FrameWidth, o.From, o.Sill, o.Head + FrameWidth, half, FrameLook);
                    WallBox(b, w, o.To, o.To + FrameWidth, o.Sill, o.Head + FrameWidth, half, FrameLook);
                    WallBox(b, w, o.From, o.To, o.Head, o.Head + FrameWidth, half, FrameLook);
                    // Boarded up: planks nailed across it outside, one of them askew.
                    float outside = OutsideSign(w, o) * (w.Thickness * 0.5f + 0.06f);
                    foreach (float y in new[] { o.Sill + 0.7f, o.Sill + 1.9f })
                    {
                        Vector3 p = At(w, o.From - 0.3f, outside - 0.025f, y), q = At(w, o.To + 0.3f, outside + 0.025f, y + 0.28f);
                        Box(b, Vector3.Min(p, q), Vector3.Max(p, q), PlankLook);
                    }
                    Beam(b, At(w, o.From - 0.2f, outside, o.Sill + 0.3f), At(w, o.To + 0.2f, outside, o.Head - 0.3f), 0.1f, PlankLook);
                    break;
                }
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

        // ---- the terrace, the garden wall ----

        private static void Ground(MeshBuffer b)
        {
            foreach (House.Block block in House.Blocks)
            {
                Slab(b, block.Min, block.Max, Look(block.Top, block.TopTint, false), StoneLook, block.Top == Finish.Concrete ? Look(Finish.Concrete, block.TopTint, true) : StoneLook);
            }
            House.Rail wall = House.GardenWall;
            Box(b, new Vector3(wall.A.x, wall.Y, wall.A.y - 0.15f), new Vector3(wall.B.x, wall.Y + House.GardenWallHeight - 0.06f, wall.B.y + 0.15f), BrickLook);
            Box(b, new Vector3(wall.A.x, wall.Y + House.GardenWallHeight - 0.06f, wall.A.y - 0.2f), new Vector3(wall.B.x, wall.Y + House.GardenWallHeight, wall.B.y + 0.2f), StoneLook);
        }

        // ---- roofs ----

        private static void Roofs(MeshBuffer b)
        {
            float o = House.RoofOverhang, wall = House.OuterWallThickness * 0.5f;
            Gable(b, -o, 20 + o, -o, 16 + o, House.RidgeZ, House.EavesY, House.RidgeY, -wall, 20 + wall, 0, 16);
            // The tree house's little roof over its hut.
            float hutTop = House.LevelY(2) + 3.2f;
            Gable(b, 2.2f, 7.8f, 25.7f, 30.3f, 28.0f, hutTop, hutTop + 1.1f, 2.5f - wall, 7.5f + wall, 26, 30);
        }

        /// <summary>
        /// A gable roof: two planes from eaves at z0 and z1 (height <paramref name="eaves"/>) up to a ridge along x at <paramref name="ridgeZ"/>,
        /// spanning x0 to x1, with the triangular gable walls (at gx0 and gx1, over the walls from gz0 to gz1) under it.
        /// </summary>
        private static void Gable(MeshBuffer b, float x0, float x1, float z0, float z1, float ridgeZ, float eaves, float ridge, float gx0, float gx1, float gz0, float gz1)
        {
            const float thickness = 0.25f;
            Color32 tiles = SurfaceLook.Pack(House.RoofTiles, (int)WallMaterial.Roof, 0.35f);
            foreach (bool front in new[] { true, false })
            {
                float ze = front ? z0 : z1;
                Vector3 e0 = new Vector3(x0, eaves, ze), e1 = new Vector3(x1, eaves, ze), r0 = new Vector3(x0, ridge, ridgeZ), r1 = new Vector3(x1, ridge, ridgeZ);
                Vector3 normal = Vector3.Cross(r0 - e0, e1 - e0).normalized;
                if (normal.y < 0) normal = -normal;
                Vector3 down = Vector3.down * thickness;
                b.Look = tiles;
                Quad(b, e0, e1, r1, r0, normal);
                b.Look = RenderLook;
                Quad(b, e0 + down, e1 + down, r1 + down, r0 + down, -normal);
                Quad(b, e0, e1, e1 + down, e0 + down, front ? Vector3.back : Vector3.forward);
                Quad(b, e0, r0, r0 + down, e0 + down, Vector3.left);
                Quad(b, e1, r1, r1 + down, e1 + down, Vector3.right);
            }
            // The gable walls fill the triangle under the roof at each end, from the walls' top up to the roof's underside.
            float apex = ridge - thickness;
            foreach (float gx in new[] { gx0, gx1 })
            {
                Vector3 n = new Vector3(gx <= (gx0 + gx1) * 0.5f ? -1.0f : 1.0f, 0, 0);
                Vector3 a = new Vector3(gx, eaves, gz0), c = new Vector3(gx, eaves, gz1), top = new Vector3(gx, apex, ridgeZ);
                b.Look = RenderLook;
                b.AddTriangle(a, c, top, n, new Vector2(gz0, eaves), new Vector2(gz1, eaves), new Vector2(ridgeZ, apex));
                b.AddTriangle(a, c, top, -n, new Vector2(gz0, eaves), new Vector2(gz1, eaves), new Vector2(ridgeZ, apex));
            }
        }

        // ---- the conservatory ----

        private static void Conservatory(MeshBuffer b, MeshBuffer glass)
        {
            float floor = House.LevelY(1), top = House.LevelY(2), low = top - 0.6f, sill = floor + 0.6f;
            Color32 white = FrameLook;
            // A low brick sill round its three glass sides (the north side's doorway left open), posts at the corners and every 1.5 m.
            Box(b, new Vector3(-0.15f, floor, 16), new Vector3(0.15f, sill, 20.15f), BrickLook);
            Box(b, new Vector3(8.85f, floor, 16), new Vector3(9.15f, sill, 20.15f), BrickLook);
            Box(b, new Vector3(-0.15f, floor, 19.85f), new Vector3(2.9f, sill, 20.15f), BrickLook);
            Box(b, new Vector3(4.7f, floor, 19.85f), new Vector3(9.15f, sill, 20.15f), BrickLook);
            foreach (float x in new[] { 0f, 1.5f, 2.9f, 4.7f, 6.0f, 7.5f, 9f })
            {
                Box(b, new Vector3(x - 0.07f, floor, 19.93f), new Vector3(x + 0.07f, low, 20.07f), white);
            }
            foreach (float z in new[] { 16f, 17.3f, 18.6f, 20f })
            {
                float h = Mathf.Lerp(top, low, (z - 16) / 4f);
                Box(b, new Vector3(-0.07f, floor, z - 0.07f), new Vector3(0.07f, h, z + 0.07f), white);
                Box(b, new Vector3(8.93f, floor, z - 0.07f), new Vector3(9.07f, h, z + 0.07f), white);
            }
            // The doorway's head, and the beam along the low edge of the roof.
            Box(b, new Vector3(2.9f, floor + House.DoorHeight, 19.93f), new Vector3(4.7f, floor + House.DoorHeight + 0.1f, 20.07f), white);
            Box(b, new Vector3(-0.07f, low, 19.9f), new Vector3(9.07f, low + 0.12f, 20.1f), white);
            // Glass: above the sill on three sides (over the doorway's head on the north), and the sloping roof.
            Box(glass, new Vector3(-0.02f, sill, 16.07f), new Vector3(0.02f, low, 19.93f), SurfaceLook.Default);
            Box(glass, new Vector3(8.98f, sill, 16.07f), new Vector3(9.02f, low, 19.93f), SurfaceLook.Default);
            Box(glass, new Vector3(0.07f, sill, 19.98f), new Vector3(2.83f, low, 20.02f), SurfaceLook.Default);
            Box(glass, new Vector3(4.77f, sill, 19.98f), new Vector3(8.93f, low, 20.02f), SurfaceLook.Default);
            Box(glass, new Vector3(2.97f, floor + House.DoorHeight + 0.1f, 19.98f), new Vector3(4.63f, low, 20.02f), SurfaceLook.Default);
            Vector3 ra = new Vector3(-0.1f, top, 16), rb = new Vector3(9.1f, top, 16), rc = new Vector3(9.1f, low + 0.12f, 20.1f), rd = new Vector3(-0.1f, low + 0.12f, 20.1f);
            Vector3 up = Vector3.Cross(rb - ra, rd - ra).normalized;
            if (up.y < 0) up = -up;
            glass.Look = SurfaceLook.Default;
            Quad(glass, ra, rb, rc, rd, up);
            Quad(glass, ra - up * 0.03f, rb - up * 0.03f, rc - up * 0.03f, rd - up * 0.03f, -up);
            for (float x = 0; x <= 9.01f; x += 1.5f)
            {
                Beam(b, new Vector3(x, top - 0.05f, 16), new Vector3(x, low + 0.1f, 20.05f), 0.05f, white);
            }
        }

        // ---- posts under the balcony and the tree house ----

        private static void Posts(MeshBuffer b)
        {
            Color32 wood = SurfaceLook.Pack(House.Decking, (int)WallMaterial.Plaster, 0.3f);
            float from = House.LevelY(1), to = House.LevelY(2) - House.SlabThickness;
            foreach (float x in new[] { 9.15f, 14.5f, 19.85f })
            {
                Box(b, new Vector3(x - 0.15f, from, 18.2f), new Vector3(x + 0.15f, to, 18.5f), RenderLook);
            }
            foreach (Vector2 p in new[] { new Vector2(2.15f, 25.65f), new Vector2(7.85f, 25.65f), new Vector2(2.15f, 30.35f), new Vector2(7.85f, 30.35f) })
            {
                Box(b, new Vector3(p.x - 0.12f, from, p.y - 0.12f), new Vector3(p.x + 0.12f, to, p.y + 0.12f), wood);
            }
            // The rope ladder, pulled up and coiled on the platform's edge.
            Box(b, new Vector3(2.05f, House.LevelY(2), 27.0f), new Vector3(2.45f, House.LevelY(2) + 0.25f, 28.2f), SurfaceLook.Pack(new Color(0.70f, 0.60f, 0.40f), (int)WallMaterial.Plaster, 0.3f));
        }

        // ---- the plank bridge ----

        private static void Bridge(MeshBuffer b)
        {
            Vector2 d = House.BridgeTo - House.BridgeFrom;
            float length = d.magnitude, yaw = Mathf.Atan2(d.x, d.y);
            float y = House.LevelY(2), w = House.BridgeWidth * 0.5f;
            b.SetFrame(new Vector3(House.BridgeFrom.x, 0, House.BridgeFrom.y), Mathf.Cos(yaw), -Mathf.Sin(yaw));
            // The boards run on past both ends onto the balcony and the platform, so the slanting ends leave no gap.
            for (float s = -0.5f; s < length + 0.5f; s += 0.32f)
            {
                Box(b, new Vector3(-w, y - 0.06f, s + 0.02f), new Vector3(w, y + 0.02f, s + 0.29f), PlankLook);
            }
            Box(b, new Vector3(-w + 0.1f, y - 0.3f, 0), new Vector3(-w + 0.25f, y - 0.06f, length), PlankLook);
            Box(b, new Vector3(w - 0.25f, y - 0.3f, 0), new Vector3(w - 0.1f, y - 0.06f, length), PlankLook);
            Color32 rope = SurfaceLook.Pack(new Color(0.75f, 0.65f, 0.45f), (int)WallMaterial.Plaster, 0.2f);
            foreach (float x in new[] { -w + 0.05f, w - 0.05f })
            {
                for (float s = 0.3f; s < length; s += 1.3f)
                {
                    Box(b, new Vector3(x - 0.05f, y, s - 0.05f), new Vector3(x + 0.05f, y + 1.15f, s + 0.05f), PlankLook);
                }
                Box(b, new Vector3(x - 0.03f, y + 1.05f, 0.3f), new Vector3(x + 0.03f, y + 1.11f, length - 0.2f), rope);
                Box(b, new Vector3(x - 0.03f, y + 0.55f, 0.3f), new Vector3(x + 0.03f, y + 0.6f, length - 0.2f), rope);
            }
            b.ClearFrame();
        }

        // ---- railings and the fence ----

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

        private static void Fence(MeshBuffer b)
        {
            Color32 boards = SurfaceLook.Pack(FenceWood, (int)WallMaterial.Plaster, 0.35f);
            float y = House.LevelY(1);
            for (int i = 0; i + 1 < House.Fence.Length; i++)
            {
                Vector2 p = House.Fence[i], q = House.Fence[i + 1];
                Box(b, new Vector3(Mathf.Min(p.x, q.x) - 0.06f, y, Mathf.Min(p.y, q.y) - 0.06f), new Vector3(Mathf.Max(p.x, q.x) + 0.06f, y + House.FenceHeight, Mathf.Max(p.y, q.y) + 0.06f), boards);
                int posts = Mathf.Max(2, Mathf.CeilToInt((q - p).magnitude / 2.0f) + 1);
                for (int k = 0; k < posts; k++)
                {
                    Vector2 at = Vector2.Lerp(p, q, k / (posts - 1.0f));
                    Box(b, new Vector3(at.x - 0.1f, y, at.y - 0.1f), new Vector3(at.x + 0.1f, y + House.FenceHeight + 0.15f, at.y + 0.1f), boards);
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
