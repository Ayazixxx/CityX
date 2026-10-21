using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The relief on a building's walls (FacadeOrnament): base course, string courses, cornice, piers between bays, corner piers. Every part is a box
    /// standing proud of the wall plane, in the wall buffer so it draws with the facade material in the same draw call. See FacadeOrnament for why.
    ///
    /// NOTHING CROSSES GLASS: courses sit on the row lines (inside the solid band between one row's windows and the next), piers on the column lines
    /// (inside the solid strip between panes), the cornice on the parapet. NOTHING CROSSES ANOTHER BLOCK: a stretch of wall another block of the same
    /// building stands against (BuildingSite.Contacts) gets none.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        /// <summary>
        /// The ornament on one wall, outside face: the wall (in the builder's frame), its span, its outward normal, which window wall it is, the floor and
        /// roof heights, the rows and columns of its window grid. <paramref name="far"/> keeps only what reads at a distance (base course and cornice);
        /// <paramref name="round"/> is a side of a round building (no piers: its corners are the polygon's).
        /// </summary>
        private static void AddWallOrnament(MeshBuffer buffer, BuildingSite site, SiteLooks looks, in Wall wall, float u0, float u1, Vector3 normal,
            WindowGrid.Wall which, float floorY, float roofY, CellLayout rows, CellLayout columns, bool far, bool round = false)
        {
            FacadeOrnament o = looks.Ornament;
            Color32 saved = buffer.Look;

            if (o.BaseCourse && rows.Count > 1)
            {
                float y = floorY + rows.Size;
                AddBand(buffer, site, wall, normal, which, u0, u1, y - 0.10f, y - 0.10f + FacadeOrnament.BaseCourseHeight,
                    FacadeOrnament.BaseCourseDepth, looks.Trim, floorY, round);
            }

            float corniceBottom = roofY;
            if (o.HasCornice)
            {
                // The cornice CAPS THE PARAPET: its top is flush with the coping's, so the roof edge is one flat top a climber can pull up onto
                // (LedgeProbe's thin ledge). A cornice a step below the coping was two tops at two heights, and every climb hung under it. A glass
                // crown runs from the roof line up; a masonry cornice hangs down from the top by its own height.
                float yt = roofY + ParapetWallHeight + CopingHeight;
                float yb = o.CorniceAboveRoof ? roofY : yt - o.CorniceHeight;
                corniceBottom = yb;
                AddBand(buffer, site, wall, normal, which, u0, u1, yb, yt, o.CorniceDepth, looks.Trim, floorY, round);
                // A smaller step under it, so the cornice reads as a moulding rather than a plank.
                AddBand(buffer, site, wall, normal, which, u0, u1, yb - 0.16f, yb, o.CorniceDepth * 0.55f, looks.Trim, floorY, round);
                corniceBottom = yb - 0.16f;
            }

            if (!far && o.CourseEveryRows > 0)
            {
                for (int r = o.CourseEveryRows; r < rows.Count; r += o.CourseEveryRows)
                {
                    if (o.BaseCourse && r == 1)
                    {
                        continue;
                    }
                    float y = floorY + r * rows.Size;
                    if (y > corniceBottom - 1.0f)
                    {
                        break;
                    }
                    AddBand(buffer, site, wall, normal, which, u0, u1, y - FacadeOrnament.CourseHeight * 0.5f, y + FacadeOrnament.CourseHeight * 0.5f,
                        FacadeOrnament.CourseDepth, looks.Band, floorY, round);
                }
            }

            if (!far && !round && o.PierEveryColumns > 0 && rows.Count > 2)
            {
                // Piers run from the top of the street storey to under the cornice, on every Nth column line.
                float y0 = floorY + rows.Size + (o.BaseCourse ? FacadeOrnament.BaseCourseHeight - 0.10f : 0.0f);
                float y1 = corniceBottom;
                buffer.Look = looks.Wall;
                // Not in front of a flat's door onto its balcony (BalconyDoors): the doorway of a pair or a sliding door can span a column line. And
                // broken round a footbridge's landing on any wall (Footbridges, owner 2026-10-16: the bridge's door was "blocked by the buildings
                // decals/architecture"): the pier stops under the deck and starts again over the door.
                List<BalconyDoor> doors = BalconyDoorsOf(site, WindowPlan.For(site), which);
                List<FacadeClearing> clearings = BridgeClearings(site, which);
                for (int c = o.PierEveryColumns; c < columns.Count; c += o.PierEveryColumns)
                {
                    float u = u0 + c * columns.Size;
                    float half = FacadeOrnament.PierWidth * 0.5f;
                    if (site.ContactTop(which, u - u0 - half, u - u0 + half) > y0 - floorY || CrossesABalconyDoor(doors, u - half, u + half, false))
                    {
                        continue;
                    }
                    float from = y0;
                    while (from < y1 - 0.01f)
                    {
                        float to = NextBridgeLanding(clearings, floorY, u - half, u + half, from, y1, out float resume);
                        if (to > from + 0.01f)
                        {
                            AddOutset(buffer, wall, normal, u - half, u + half, from, to, FacadeOrnament.PierDepth, false);
                        }
                        from = resume;
                    }
                }
            }
            buffer.Look = saved;
        }

        /// <summary>
        /// A horizontal band along a wall, wrapped round the corners at its ends (it runs on past each end by its own depth, so two walls' bands meet as a
        /// solid corner), and broken where another block of the building stands against the wall above the band's bottom.
        /// </summary>
        private static void AddBand(MeshBuffer buffer, BuildingSite site, in Wall wall, Vector3 normal, WindowGrid.Wall which,
            float u0, float u1, float yBottom, float yTop, float depth, Color32 look, float floorY, bool round)
        {
            if (yTop - yBottom < 0.01f || depth < 0.01f)
            {
                return;
            }
            Color32 saved = buffer.Look;
            buffer.Look = look;
            float above = yBottom - floorY;
            // Round a corner only where the wall round it is open: into a party wall (or another block of the building) the band would push into
            // the neighbour, and two neighbours' cornices would fight for one face.
            float start = u0 - (!round && AgainstCorner(site, which, false, above) ? 0.0f : depth);
            float end = u1 + (!round && AgainstCorner(site, which, true, above) ? 0.0f : depth);
            if (site.Contacts != null && !round)
            {
                // Walk the wall, emitting the stretches no contact covers.
                float cursor = start;
                while (cursor < end - 0.01f)
                {
                    // The next contact starting at or after the cursor (or covering it).
                    float blockFrom = end, blockTo = end;
                    foreach (WallContact c in site.Contacts)
                    {
                        if (c.Wall != which || c.Top <= above)
                        {
                            continue;
                        }
                        float cs = u0 + c.U0 - (c.U0 <= 0.01f ? depth : 0.0f);
                        float ce = u0 + c.U1 + (c.U1 >= u1 - u0 - 0.01f ? depth : 0.0f);
                        if (ce > cursor && cs < blockFrom)
                        {
                            blockFrom = Mathf.Max(cs, cursor);
                            blockTo = ce;
                        }
                    }
                    if (blockFrom > cursor + 0.01f)
                    {
                        AddOutset(buffer, wall, normal, cursor, blockFrom, yBottom, yTop, depth, true);
                    }
                    cursor = Mathf.Max(blockTo, cursor + 0.01f);
                }
            }
            else
            {
                AddOutset(buffer, wall, normal, start, end, yBottom, yTop, depth, true);
            }
            buffer.Look = saved;
        }

        /// <summary>
        /// Whether something stands against the wall that meets <paramref name="which"/> at its start (or end), at that corner, higher than
        /// <paramref name="above"/> metres over the floor.
        /// </summary>
        private static bool AgainstCorner(BuildingSite site, WindowGrid.Wall which, bool atEnd, float above)
        {
            if (site.Contacts == null)
            {
                return false;
            }
            CornerOf(site, which, atEnd, out WindowGrid.Wall other, out float at);
            return site.ContactTop(other, at - CornerReach, at + CornerReach) > above;
        }

        /// <summary>How close to a corner a contact on the wall round it has to come to count as standing at the corner.</summary>
        private const float CornerReach = 1.0f;

        /// <summary>
        /// The wall that meets a box's wall at its start (u = 0) or its end, and where along that other wall the corner is. North and south run from
        /// -x to +x, east and west from -z to +z (the convention BuildingSite.Contacts and the corner piers use).
        /// </summary>
        private static void CornerOf(BuildingSite site, WindowGrid.Wall which, bool atEnd, out WindowGrid.Wall other, out float at)
        {
            float width = site.Params.Width, depth = site.Params.Depth;
            switch (which)
            {
                case WindowGrid.Wall.North:
                    other = atEnd ? WindowGrid.Wall.East : WindowGrid.Wall.West;
                    at = depth;
                    break;
                case WindowGrid.Wall.South:
                    other = atEnd ? WindowGrid.Wall.East : WindowGrid.Wall.West;
                    at = 0.0f;
                    break;
                case WindowGrid.Wall.East:
                    other = atEnd ? WindowGrid.Wall.North : WindowGrid.Wall.South;
                    at = width;
                    break;
                default:
                    other = atEnd ? WindowGrid.Wall.North : WindowGrid.Wall.South;
                    at = 0.0f;
                    break;
            }
        }

        /// <summary>A box standing proud of a wall: along it from u0 to u1, from y0 to y1, out by depth (with its underside when asked).</summary>
        private static void AddOutset(MeshBuffer buffer, in Wall wall, Vector3 normal, float u0, float u1, float y0, float y1, float depth, bool underside)
        {
            Vector3 a = wall.At(u0, y0);
            Vector3 b = wall.At(u1, y1) + normal * depth;
            buffer.AddBox(Vector3.Min(a, b), Vector3.Max(a, b), underside);
        }

        /// <summary>
        /// Piers up the four corners of a box building, from the bottom of its podium to the top of its parapet, unless another block of the building
        /// stands against either wall at that corner. A square post centred on the corner: half of it is in the wall, half stands out on both faces.
        /// </summary>
        private static void AddCornerPiers(MeshBuffer buffer, BuildingSite site, SiteLooks looks, float x0, float x1, float z0, float z1,
            float bottomY, float topY)
        {
            if (!looks.Ornament.CornerPiers)
            {
                return;
            }
            Color32 saved = buffer.Look;
            buffer.Look = looks.Wall;
            float h = FacadeOrnament.CornerPierHalf;
            float width = x1 - x0, depth = z1 - z0;
            // Each corner, with the two walls that meet there and where along each the corner is (start or end).
            CornerPier(buffer, site, x0, z0, h, bottomY, topY, WindowGrid.Wall.West, 0.0f, WindowGrid.Wall.South, 0.0f);
            CornerPier(buffer, site, x1, z0, h, bottomY, topY, WindowGrid.Wall.East, 0.0f, WindowGrid.Wall.South, width);
            CornerPier(buffer, site, x1, z1, h, bottomY, topY, WindowGrid.Wall.East, depth, WindowGrid.Wall.North, width);
            CornerPier(buffer, site, x0, z1, h, bottomY, topY, WindowGrid.Wall.West, depth, WindowGrid.Wall.North, 0.0f);
            buffer.Look = saved;
        }

        private static void CornerPier(MeshBuffer buffer, BuildingSite site, float x, float z, float h, float bottomY, float topY,
            WindowGrid.Wall wallA, float atA, WindowGrid.Wall wallB, float atB)
        {
            if (site.ContactTop(wallA, atA - 0.5f, atA + 0.5f) > 0.0f || site.ContactTop(wallB, atB - 0.5f, atB + 0.5f) > 0.0f)
            {
                return;
            }
            buffer.AddBox(new Vector3(x - h, bottomY, z - h), new Vector3(x + h, topY, z + h), false);
        }
    }
}
