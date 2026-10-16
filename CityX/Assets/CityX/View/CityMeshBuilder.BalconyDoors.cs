using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The doors onto the balconies (World/BalconyDoors): in the back wall of an apartment block, where a flat's door takes the place of a window in
    /// every row, the doorway is cut through both faces of the wall and the door is built in it -- its frame (jambs, head, a threshold barely proud
    /// of the floor) and its panels or leaves: a sliding door's fixed panel on the outer track and its sliding one on the inner; a pair of glazed
    /// doors hinged at the jambs; or one solid door like the street doors, with a lever handle on each face.
    ///
    /// SHUT OR OPEN, IN THE CHUNK'S OWN MESH. There are thousands of these doors (a block of flats has fifty), so none of them is an object of its
    /// own: each is drawn the way BalconyDoorState has it -- shut, or open (the sliding panel run across behind the fixed one; a leaf turned a
    /// quarter round into the flat) -- and opening one rebuilds its chunk in the background, the way a broken window does (BalconyDoorInteractor,
    /// CityStreamer). The frame, the leaves and the handles are in the wall buffer, the glass in the dark glass buffer (double-sided, clear where a
    /// room is behind it); both are in the chunk's collider, so a shut door is something you walk into.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        /// <summary>The single door's leaf and handles: the street doors' own colours (CityMassingSceneBuilder's BuildingDoor and its handle).</summary>
        private static readonly Color SolidDoorTint = new Color(0.42f, 0.28f, 0.16f);

        private static readonly Color DoorHandleTint = new Color(0.72f, 0.68f, 0.55f);

        [System.ThreadStatic] private static List<BalconyDoor> s_wallDoors;

        /// <summary>
        /// The doors in a wall: the flats' balcony doors (only the back wall of a building with balconies has any) and the footbridges' doorways
        /// (Footbridges, worked out before the walls by BuildSite), or null. One list per thread, refilled.
        /// </summary>
        private static List<BalconyDoor> BalconyDoorsOf(BuildingSite site, in WindowPlan plan, WindowGrid.Wall which)
        {
            List<BalconyDoor> doors = s_wallDoors ?? (s_wallDoors = new List<BalconyDoor>());
            doors.Clear();
            if (which == ApartmentFront.BalconyWall && FrontFor(site, plan).Balconies)
            {
                BalconyDoors.For(site, plan, doors);
            }
            Footbridge[] bridges = Footbridges.Cached(site);
            if (bridges.Length > 0)
            {
                int before = doors.Count;
                Footbridges.DoorsOf(site, plan, bridges, doors);
                // Only this wall's.
                for (int i = doors.Count - 1; i >= before; i--)
                {
                    if (doors[i].Wall != which) doors.RemoveAt(i);
                }
            }
            return doors.Count > 0 ? doors : null;
        }

        /// <summary>Whether a stretch of the wall (<paramref name="u0"/>..<paramref name="u1"/>) runs into any of the doorways (on any row).</summary>
        private static bool CrossesABalconyDoor(List<BalconyDoor> doors, float u0, float u1)
        {
            if (doors == null)
            {
                return false;
            }
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i].U0 < u1 && doors[i].U1 > u0)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool RowHasBalconyDoor(List<BalconyDoor> doors, int row)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i].Row == row)
                {
                    return true;
                }
            }
            return false;
        }

        private static int BalconyDoorStartingAt(List<BalconyDoor> doors, int column, int row)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                if (doors[i].Row == row && doors[i].Column == column)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// One row of the back wall with flats' doors in it, cell by cell (as the street door's row is, AddDoorRow): a door's cells keep what is left
        /// of them either side of its doorway and over its head; every other cell is the window or wall it would have been, with the row's sill and
        /// head strips in the band look as in any other row.
        /// </summary>
        private static void AddBalconyDoorRow(MeshBuffer wallBuffer, in Glazing glazing, BuildingSite site, in Wall wall, float u0, float y0, float y1,
            Vector3 normal, in WindowPlan plan, WindowGrid.Wall which, CellLayout columns, CellLayout rows, int row, List<BalconyDoor> doors,
            SiteLooks? looks, float standOn)
        {
            float inset = WindowGrid.PaneInset;
            plan.Insets(WindowGrid.TopEdgeHasSlab(row, rows.Size, site.Params.Height), out float insetBottom, out float insetTop);
            float paneY0 = y0 + rows.Size * insetBottom;
            float paneY1 = y1 - rows.Size * insetTop;
            Color32 rowLook = wallBuffer.Look;
            Color32 bandLook = looks.HasValue ? looks.Value.Band : rowLook;

            for (int column = 0; column < columns.Count; column++)
            {
                float cellU0 = u0 + column * columns.Size;
                float cellU1 = cellU0 + columns.Size;
                int d = BalconyDoorStartingAt(doors, column, row);
                if (d >= 0)
                {
                    BalconyDoor door = doors[d];
                    float groupU1 = u0 + (column + door.Columns) * columns.Size;
                    float doorTop = y0 + door.Height;
                    AddBandedPanel(wallBuffer, wall, cellU0, door.U0, y0, y1, paneY0, paneY1, normal, bandLook, rowLook);
                    AddBandedPanel(wallBuffer, wall, door.U1, groupU1, y0, y1, paneY0, paneY1, normal, bandLook, rowLook);
                    wallBuffer.Look = bandLook;   // over the head: the strip under the slab
                    AddWallPanel(wallBuffer, wall, door.U0, door.U1, doorTop, y1, normal);
                    wallBuffer.Look = rowLook;
                    if (glazing.HangsGlass)
                    {
                        // The doorway is a recess the thickness of the wall (no floor face: the balcony slab and the room's floor are there).
                        AddWindowReveals(wallBuffer, wall, door.U0, door.U1, y0, doorTop, normal, null, false, false);
                        if (looks.HasValue)
                        {
                            AddBalconyDoor(wallBuffer, glazing, site, wall, door, y0, normal, looks.Value);
                            wallBuffer.Look = rowLook;
                        }
                    }
                    column += door.Columns - 1;
                    continue;
                }
                bool glazed = plan.IsGlazed(which, column, row);
                float paneU0 = cellU0 + columns.Size * inset;
                float paneU1 = cellU1 - columns.Size * inset;
                wallBuffer.Look = bandLook;   // the strips over and under the windows, as in an ordinary row
                AddWallPanel(wallBuffer, wall, cellU0, cellU1, y0, paneY0, normal);
                AddWallPanel(wallBuffer, wall, cellU0, cellU1, paneY1, y1, normal);
                wallBuffer.Look = rowLook;
                if (!glazed)
                {
                    AddWallPanel(wallBuffer, wall, cellU0, cellU1, paneY0, paneY1, normal);
                    continue;
                }
                AddWallPanel(wallBuffer, wall, cellU0, paneU0, paneY0, paneY1, normal);
                AddWallPanel(wallBuffer, wall, paneU1, cellU1, paneY0, paneY1, normal);
                AddOpening(wallBuffer, glazing, site, wall, paneU0, paneU1, paneY0, paneY1, normal, plan, which, column, row, looks, standOn);
            }
        }

        /// <summary>A stretch of a row's wall from bottom to top: its sill and head strips in the band look, the middle in the row's.</summary>
        private static void AddBandedPanel(MeshBuffer buffer, in Wall wall, float u0, float u1, float y0, float y1, float paneY0, float paneY1,
            Vector3 normal, Color32 band, Color32 middle)
        {
            buffer.Look = band;
            AddWallPanel(buffer, wall, u0, u1, y0, paneY0, normal);
            AddWallPanel(buffer, wall, u0, u1, paneY1, y1, normal);
            buffer.Look = middle;
            AddWallPanel(buffer, wall, u0, u1, paneY0, paneY1, normal);
        }

        /// <summary>One flat's door in its doorway: the frame, then the panels or leaves, shut or open as BalconyDoorState has them.</summary>
        private static void AddBalconyDoor(MeshBuffer wallBuffer, in Glazing glazing, BuildingSite site, in Wall wall, in BalconyDoor door, float y0,
            Vector3 outward, SiteLooks looks)
        {
            bool open = door.Opens && BalconyDoorState.Shared.IsOpen(site.Id, door.Unit, door.Row);
            Vector3 along = wall.RunsInZ ? Vector3.forward : Vector3.right;
            Vector3 inward = -outward;
            Color32 frame = looks.Frame;
            Color32 glass = SurfaceLook.PackGlass(looks.GlassTint, 0, 0.0f, y0 + door.Height * 0.5f < glazing.ClearBelow);
            MeshBuffer glassBuffer = glazing.Dark ?? glazing.Lit;

            // The frame: the jambs and the head the full depth of the tracks, and a threshold barely proud of the floor.
            const float jamb = BalconyDoors.Jamb;
            float width = door.Width, height = door.Height;
            float f0 = BalconyDoors.OuterTrack - 0.05f, f1 = BalconyDoors.InnerTrack + 0.05f;
            Vector3 low = wall.At(door.U0, y0);
            wallBuffer.Look = frame;
            AddFrameBox(wallBuffer, low, along, inward, 0.0f, jamb, 0.0f, height, f0, f1);
            AddFrameBox(wallBuffer, low, along, inward, width - jamb, width, 0.0f, height, f0, f1);
            AddFrameBox(wallBuffer, low, along, inward, jamb, width - jamb, height - BalconyDoors.HeadBar, height, f0, f1);
            AddFrameBox(wallBuffer, low, along, inward, jamb, width - jamb, 0.0f, BalconyDoors.Threshold, f0, f1);

            float clear = width - 2.0f * jamb;   // between the jambs
            float panelHeight = height - BalconyDoors.HeadBar - BalconyDoors.Threshold;
            Vector3 sill = low + Vector3.up * BalconyDoors.Threshold;   // the panels stand on the threshold
            bool lowSide = BalconyDoors.FixedOnLow(site, door.Unit);
            switch (door.Kind)
            {
                case BalconyDoorKind.Sliding:
                {
                    float panel = clear * 0.5f + BalconyDoors.SlidingOverlap * 0.5f;
                    float fixedAt = lowSide ? jamb : width - jamb - panel;
                    float slidingShut = lowSide ? width - jamb - panel : jamb;
                    // Open, the sliding panel stands behind the fixed one (a little short of all the way: its handle stops it).
                    float slidingAt = open ? fixedAt + (lowSide ? BalconyDoors.SlidingStop : -BalconyDoors.SlidingStop) : slidingShut;
                    AddGlazedPanel(wallBuffer, glassBuffer, sill + along * fixedAt + inward * BalconyDoors.OuterTrack, along, inward, panel,
                        panelHeight, frame, glass);
                    AddGlazedPanel(wallBuffer, glassBuffer, sill + along * slidingAt + inward * BalconyDoors.InnerTrack, along, inward, panel,
                        panelHeight, frame, glass);
                    break;
                }
                case BalconyDoorKind.Double:
                {
                    // Each leaf turns about its own jamb; open, it stands against the jamb, square to the wall, into the flat.
                    float leaf = clear * 0.5f;
                    float t = BalconyDoors.PanelThickness * 0.5f;
                    Vector3 plane = inward * BalconyDoors.LeafPlane;
                    if (open)
                    {
                        AddGlazedPanel(wallBuffer, glassBuffer, sill + along * (jamb + t) + plane, inward, along, leaf, panelHeight, frame, glass);
                        AddGlazedPanel(wallBuffer, glassBuffer, sill + along * (width - jamb - t) + plane, inward, -along, leaf, panelHeight, frame,
                            glass);
                    }
                    else
                    {
                        AddGlazedPanel(wallBuffer, glassBuffer, sill + along * jamb + plane, along, inward, leaf, panelHeight, frame, glass);
                        AddGlazedPanel(wallBuffer, glassBuffer, sill + along * (width - jamb) + plane, -along, inward, leaf, panelHeight, frame, glass);
                    }
                    break;
                }
                default:
                {
                    // One solid leaf like the street doors, hinged at the jamb FixedOnLow names; open, it stands against that jamb, into the flat.
                    float t = BalconyDoors.SolidLeafThickness * 0.5f;
                    Vector3 plane = inward * BalconyDoors.LeafPlane;
                    Vector3 hinge = sill + along * (lowSide ? jamb : width - jamb) + plane;
                    Vector3 run = lowSide ? along : -along;   // from the hinge across the doorway, shut
                    if (open)
                    {
                        AddSolidLeaf(wallBuffer, hinge + run * t, inward, run, clear, panelHeight);
                    }
                    else
                    {
                        AddSolidLeaf(wallBuffer, hinge, run, inward, clear, panelHeight);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// A glazed panel or leaf: a frame of four bars round a sheet of glass, standing on <paramref name="origin"/> (its bottom corner on the hinge
        /// or track side), running <paramref name="width"/> along <paramref name="along"/>, <paramref name="height"/> up, its thickness centred on
        /// its plane (<paramref name="across"/> is the plane's normal). The bars in <paramref name="frameBuffer"/>, the glass in
        /// <paramref name="glassBuffer"/>, both sides.
        /// </summary>
        private static void AddGlazedPanel(MeshBuffer frameBuffer, MeshBuffer glassBuffer, Vector3 origin, Vector3 along, Vector3 across, float width,
            float height, Color32 frameLook, Color32 glassLook)
        {
            const float bar = 0.06f;
            float t = BalconyDoors.PanelThickness * 0.5f;
            frameBuffer.Look = frameLook;
            AddFrameBox(frameBuffer, origin, along, across, 0.0f, bar, 0.0f, height, -t, t);
            AddFrameBox(frameBuffer, origin, along, across, width - bar, width, 0.0f, height, -t, t);
            AddFrameBox(frameBuffer, origin, along, across, bar, width - bar, 0.0f, bar, -t, t);
            AddFrameBox(frameBuffer, origin, along, across, bar, width - bar, height - bar, height, -t, t);
            if (glassBuffer == null)
            {
                return;
            }
            Color32 saved = glassBuffer.Look;
            glassBuffer.Look = glassLook;
            Vector3 a = origin + along * bar + Vector3.up * bar;
            Vector3 b = origin + along * (width - bar) + Vector3.up * bar;
            Vector3 c = origin + along * (width - bar) + Vector3.up * (height - bar);
            Vector3 d = origin + along * bar + Vector3.up * (height - bar);
            float w = width - 2.0f * bar, h = height - 2.0f * bar;
            glassBuffer.AddQuad(a, b, c, d, across, new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
            glassBuffer.AddQuad(a, b, c, d, -across, new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h));
            glassBuffer.Look = saved;
        }

        /// <summary>
        /// One solid door like the street doors (BuildingDoor.Hang): a wooden leaf from its hinge at <paramref name="origin"/> along
        /// <paramref name="along"/>, <paramref name="width"/> wide, and a lever handle on each face by its free edge, at the street door's height.
        /// </summary>
        private static void AddSolidLeaf(MeshBuffer buffer, Vector3 origin, Vector3 along, Vector3 across, float width, float height)
        {
            float t = BalconyDoors.SolidLeafThickness * 0.5f;
            buffer.Look = SurfaceLook.Pack(SolidDoorTint, (int)WallMaterial.Plaster, 0.1f);
            AddFrameBox(buffer, origin, along, across, 0.0f, width, 0.0f, height, -t, t);
            buffer.Look = SurfaceLook.Pack(DoorHandleTint, (int)WallMaterial.Metal, 0.2f);
            float x = width - 0.14f, y = height * 0.45f;
            for (int side = -1; side <= 1; side += 2)
            {
                float face = side * t;
                AddFrameBox(buffer, origin, along, across, x - 0.05f, x + 0.05f, y - 0.05f, y + 0.05f, Mathf.Min(face, face + side * 0.012f),
                    Mathf.Max(face, face + side * 0.012f));   // the rose
                AddFrameBox(buffer, origin, along, across, x - 0.018f, x + 0.018f, y - 0.018f, y + 0.018f, Mathf.Min(face, face + side * 0.06f),
                    Mathf.Max(face, face + side * 0.06f));    // the neck
                float l0 = face + side * 0.05f, l1 = face + side * 0.085f;
                AddFrameBox(buffer, origin, along, across, x - 0.17f, x + 0.01f, y - 0.018f, y + 0.018f, Mathf.Min(l0, l1), Mathf.Max(l0, l1));   // the lever
            }
        }

        /// <summary>
        /// A box in a turned frame: from <paramref name="origin"/>, <paramref name="a0"/>..<paramref name="a1"/> along <paramref name="along"/>,
        /// <paramref name="y0"/>..<paramref name="y1"/> up and <paramref name="n0"/>..<paramref name="n1"/> along <paramref name="across"/>; all six
        /// faces (a door's parts are seen from every side, in the doorway and turned open).
        /// </summary>
        private static void AddFrameBox(MeshBuffer buffer, Vector3 origin, Vector3 along, Vector3 across, float a0, float a1, float y0, float y1,
            float n0, float n1)
        {
            if (a1 - a0 <= 0.0001f || y1 - y0 <= 0.0001f || n1 - n0 <= 0.0001f)
            {
                return;
            }
            Vector3 P(float a, float y, float n) => origin + along * a + Vector3.up * y + across * n;
            AddFace(buffer, across, P(a0, y0, n1), P(a1, y0, n1), P(a1, y1, n1), P(a0, y1, n1));
            AddFace(buffer, -across, P(a0, y0, n0), P(a1, y0, n0), P(a1, y1, n0), P(a0, y1, n0));
            AddFace(buffer, -along, P(a0, y0, n0), P(a0, y0, n1), P(a0, y1, n1), P(a0, y1, n0));
            AddFace(buffer, along, P(a1, y0, n0), P(a1, y0, n1), P(a1, y1, n1), P(a1, y1, n0));
            AddFace(buffer, Vector3.up, P(a0, y1, n0), P(a1, y1, n0), P(a1, y1, n1), P(a0, y1, n1));
            AddFace(buffer, Vector3.down, P(a0, y0, n0), P(a1, y0, n0), P(a1, y0, n1), P(a0, y0, n1));
        }
    }
}
