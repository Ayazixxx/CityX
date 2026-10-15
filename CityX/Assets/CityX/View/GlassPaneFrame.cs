using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>The glass of one window in the world: its bottom-left corner on the glass, the way across and up it, its size, its outward normal.</summary>
    public readonly struct PaneFrame
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Across;
        public readonly Vector3 Up;
        public readonly Vector3 Normal;
        public readonly float Width;
        public readonly float Height;

        public PaneFrame(Vector3 origin, Vector3 across, Vector3 up, Vector3 normal, float width, float height)
        {
            Origin = origin;
            Across = across;
            Up = up;
            Normal = normal;
            Width = width;
            Height = height;
        }

        /// <summary>A point of the pane (metres across and up from its bottom-left corner) in the world.</summary>
        public Vector3 At(Vector2 p) => Origin + Across * p.x + Up * p.y;

        /// <summary>A world point as metres across and up the pane.</summary>
        public Vector2 Of(Vector3 world)
        {
            Vector3 d = world - Origin;
            return new Vector2(Vector3.Dot(d, Across), Vector3.Dot(d, Up));
        }
    }

    /// <summary>
    /// WHERE A WINDOW'S GLASS IS, for the pane that just broke (GlassFractureBurst cuts that rectangle into pieces). The same layout the city's
    /// mesh is built from (CityMeshBuilder.AddWindowedWall and AddDoorRow): the cell from the window grid, less the frame's insets -- a shop
    /// window on the entrance row keeps only thin piers, a stall riser under it and a transom over it. The glass's depth in the wall is
    /// where the blow landed on it.
    /// </summary>
    public static class GlassPaneFrame
    {
        public static bool TryFrame(CityWorld world, BuildingSite site, PaneKey key, Vector3 onGlass, out PaneFrame frame)
        {
            frame = default;
            if (site.Sides >= 3)
            {
                return false;   // a round building's facets: the old burst
            }
            bool runsInZ = key.Wall == WindowGrid.Wall.West || key.Wall == WindowGrid.Wall.East;
            float span = runsInZ ? site.Params.Depth : site.Params.Width;
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            WindowPlan plan = WindowPlan.For(world, site);
            CellLayout columns = plan.ColumnsFor(span);
            if (key.Column < 0 || key.Column >= columns.Count || key.Row < 0 || key.Row >= rows.Count)
            {
                return false;
            }

            float rowHeight = rows.Size;
            float y0 = site.BaseHeight + key.Row * rowHeight;
            float y1 = y0 + rowHeight;
            float cellU0 = key.Column * columns.Size;
            float cellU1 = cellU0 + columns.Size;
            float inset, paneY0, paneY1;
            if (key.Wall == WindowGrid.Wall.North && key.Row == 0)
            {
                // The entrance row (CityMeshBuilder.AddDoorRow): a shopfront's big glass, or the facade's own windows with clear margins.
                bool storefront = plan.Storefront;
                inset = storefront ? CityMeshBuilder.StorefrontPierShare : WindowGrid.PaneInset;
                paneY0 = storefront ? y0 + CityMeshBuilder.StallRiser : y0 + rowHeight * WindowGrid.PaneInsetVerticalClear;
                paneY1 = storefront ? y0 + rowHeight * CityMeshBuilder.StorefrontGlassTop : y1 - rowHeight * WindowGrid.PaneInsetVerticalClear;
            }
            else
            {
                inset = WindowGrid.PaneInset;
                plan.Insets(WindowGrid.TopEdgeHasSlab(key.Row, rowHeight, site.Params.Height), out float insetBottom, out float insetTop);
                paneY0 = y0 + rowHeight * insetBottom;
                paneY1 = y1 - rowHeight * insetTop;
            }
            float paneU0 = cellU0 + columns.Size * inset;
            float paneU1 = cellU1 - columns.Size * inset;
            if (paneU1 - paneU0 < 0.05f || paneY1 - paneY0 < 0.05f)
            {
                return false;
            }

            Vector3 start = WallPoint(site, key.Wall, paneU0, paneY0);
            Vector3 end = WallPoint(site, key.Wall, paneU1, paneY0);
            Vector3 across = end - start;
            float width = across.magnitude;
            if (width < 1e-3f)
            {
                return false;
            }
            across /= width;
            Vector3 normal = CityMeshBuilder.WallNormal(site, key.Wall);
            // The glass is where the blow landed on it: the wall's face is in front of it by the depth of the reveal.
            start += normal * Vector3.Dot(onGlass - start, normal);
            frame = new PaneFrame(start, across, Vector3.up, normal, width, paneY1 - paneY0);
            return true;
        }

        /// <summary>A point on a wall, <paramref name="u"/> metres along it from its start (the builder's MinX or MinZ end) and at height y.</summary>
        private static Vector3 WallPoint(BuildingSite site, WindowGrid.Wall wall, float u, float y)
        {
            double bx, bz;
            switch (wall)
            {
                case WindowGrid.Wall.West:
                    bx = site.MinX;
                    bz = site.MinZ + u;
                    break;
                case WindowGrid.Wall.East:
                    bx = site.MaxX;
                    bz = site.MinZ + u;
                    break;
                case WindowGrid.Wall.South:
                    bx = site.MinX + u;
                    bz = site.MinZ;
                    break;
                default:
                    bx = site.MinX + u;
                    bz = site.MaxZ;
                    break;
            }
            site.LocalToWorld(bx - site.CentreX, bz - site.CentreZ, out double wx, out double wz);
            return new Vector3((float)wx, y, (float)wz);
        }
    }
}
