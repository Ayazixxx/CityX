using System;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// An apartment block's faces (World/ApartmentFront): a balcony on every floor across the back wall -- the slab, a parapet or a railing along
    /// its edge, a partition between one flat's and the next, a solid side wall closing each end, a laundry pole, now and then a futon airing over
    /// the parapet -- and an open walkway on every floor across the street wall. Every part is a box standing on the wall, in the wall buffer: it
    /// draws with the facade in the same draw call, and the chunk's collider takes it (a balcony is somewhere to land, a parapet a ledge to climb).
    ///
    /// NOTHING CROSSES GLASS: a slab's top is a row line (in the solid band between one row's windows and the next) and a partition stands on a
    /// column line (in the solid strip between two panes). NOTHING CROSSES ANOTHER BLOCK: a flat whose stretch of wall another block of the same
    /// building stands against has no balcony there. FAR AWAY only the slabs, parapets and side walls: a partition is under a pixel there, and
    /// the stripes must already be there when the block comes into full detail, or it would change as you walk up to it.
    /// </summary>
    public static partial class CityMeshBuilder
    {
        private static readonly Color PartitionTint = new Color(0.80f, 0.80f, 0.77f);

        private static readonly Color WhiteParapetTint = new Color(0.86f, 0.86f, 0.83f);

        // What airs over a parapet: a futon, a rug, a towel. Faded colours, mostly pale.
        private static readonly Color[] FutonColours =
        {
            new Color(0.86f, 0.84f, 0.78f), new Color(0.72f, 0.80f, 0.86f), new Color(0.86f, 0.72f, 0.74f),
            new Color(0.80f, 0.84f, 0.70f), new Color(0.62f, 0.66f, 0.78f), new Color(0.88f, 0.80f, 0.62f),
        };

        /// <summary>
        /// False builds every building as it was before apartment fronts: the "before" of the comparison renders (ApartmentFrontCapture). The chunk
        /// builders read it on their own threads, so change it only between rebuilds.
        /// </summary>
        public static bool ApartmentFrontsOn = true;

        /// <summary>The front a building is built with: ApartmentFront.For, or none while <see cref="ApartmentFrontsOn"/> is off.</summary>
        private static ApartmentFront FrontFor(BuildingSite site, in WindowPlan plan)
        {
            return ApartmentFrontsOn ? ApartmentFront.For(site, plan) : ApartmentFront.None;
        }

        /// <summary>The balconies and the walkway of a building that is an apartment block (ApartmentFront.For); nothing on any other.</summary>
        private static void AddApartmentFronts(CityWorld world, BuildingSite site, Vector3 origin, MeshBuffer buffer, SiteLooks looks, bool far)
        {
            WindowPlan plan = WindowPlan.For(world, site);
            ApartmentFront front = FrontFor(site, plan);
            if (!front.Any)
            {
                return;
            }
            float x0 = (float)(site.MinX - origin.x);
            float x1 = (float)(site.MaxX - origin.x);
            float z0 = (float)(site.MinZ - origin.z);
            float z1 = (float)(site.MaxZ - origin.z);
            float lobbyY = site.BaseHeight - origin.y;
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            CellLayout columns = plan.ColumnsFor(x1 - x0);

            Color32 saved = buffer.Look;
            Color32 parapet;
            switch (front.Finish)
            {
                case ApartmentFront.ParapetFinish.White: parapet = SurfaceLook.Pack(WhiteParapetTint, (int)WallMaterial.Plaster, 0.3f); break;
                case ApartmentFront.ParapetFinish.Concrete: parapet = looks.Trim; break;
                default: parapet = looks.Wall; break;
            }
            Span<float> edges = stackalloc float[ApartmentFront.MaxEdges(columns)];
            if (front.Balconies)
            {
                AddApartmentBand(buffer, site, front, new Wall(false, z0, x0, x1), Vector3.back, x0, columns, rows, lobbyY, looks, parapet,
                    far, false, edges);
            }
            if (front.Walkway)
            {
                AddApartmentBand(buffer, site, front, new Wall(false, z1, x0, x1), Vector3.forward, x0, columns, rows, lobbyY, looks, parapet,
                    far, true, edges);
            }
            buffer.Look = saved;
        }

        /// <summary>
        /// One wall's band: a slab on every row line from the first floor's up to the roof's (that last one only a slab, the eave over the top
        /// floor), cut at the flats' edges. A flat against another block of the building is left out; what is left is built in runs.
        /// </summary>
        private static void AddApartmentBand(MeshBuffer buffer, BuildingSite site, in ApartmentFront front, in Wall wall, Vector3 outward,
            float u0, CellLayout columns, CellLayout rows, float lobbyY, SiteLooks looks, Color32 parapet, bool far, bool walkway, Span<float> edges)
        {
            float depth = walkway ? ApartmentFront.WalkwayDepth : front.Depth;
            int n;
            if (walkway)
            {
                // A walkway is one corridor the length of the wall: its only edges are the band's two ends.
                edges[0] = u0 + ApartmentFront.EndInset;
                edges[1] = u0 + columns.Count * columns.Size - ApartmentFront.EndInset;
                n = 2;
            }
            else
            {
                n = front.UnitEdges(u0, columns, edges);
            }
            if (edges[n - 1] - edges[0] < 1.0f)
            {
                return;
            }

            for (int r = 1; r <= rows.Count; r++)
            {
                float floorY = lobbyY + r * rows.Size;
                float fromY = r * rows.Size;
                int runStart = -1;
                for (int k = 0; k < n; k++)
                {
                    bool open = k < n - 1
                        && !TouchesAnotherBlock(site, outward, (edges[k] + edges[k + 1]) * 0.5f, (edges[k + 1] - edges[k]) * 0.5f, fromY);
                    if (open && runStart < 0)
                    {
                        runStart = k;
                    }
                    else if (!open && runStart >= 0)
                    {
                        AddBalconyRun(buffer, site, front, wall, outward, edges, runStart, k, floorY, rows.Size, depth, looks, parapet,
                            far, walkway, r == rows.Count, r);
                        runStart = -1;
                    }
                }
            }
        }

        /// <summary>
        /// One row of balconies (or of walkway) from <paramref name="edges"/>[<paramref name="first"/>] to [<paramref name="end"/>]: the slab, and
        /// unless it is the roof's eave, the side walls, the parapet or railing and, close up, each flat's partition, pole and airing.
        /// </summary>
        private static void AddBalconyRun(MeshBuffer buffer, BuildingSite site, in ApartmentFront front, in Wall wall, Vector3 outward,
            Span<float> edges, int first, int end, float floorY, float rowHeight, float depth, SiteLooks looks, Color32 parapet,
            bool far, bool walkway, bool eave, int row)
        {
            float a = edges[first];
            float b = edges[end];
            const float slab = ApartmentFront.SlabThickness;

            // The slab, its top the floor inside. The eave over the top floor sits a hair under the roof line, so its top is never in one plane
            // with a cornice's step there (two faces in one plane flicker).
            buffer.Look = looks.Trim;
            float top = eave ? floorY - 0.03f : floorY;
            AddWallBox(buffer, wall, outward, a, b, top - slab, top, 0.0f, depth, true, true, false);
            if (eave)
            {
                return;
            }
            float ceiling = floorY + rowHeight - slab;   // the underside of the slab above
            float wallT = ApartmentFront.EndWallThickness;

            // The side walls closing the run, floor to the slab above.
            buffer.Look = parapet;
            AddWallBox(buffer, wall, outward, a, a + wallT, floorY, ceiling, 0.0f, depth, false, false, false);
            AddWallBox(buffer, wall, outward, b - wallT, b, floorY, ceiling, 0.0f, depth, false, false, false);
            float inA = a + wallT;
            float inB = b - wallT;

            // Along the edge: a solid parapet, or a railing (a walkway always has the solid one).
            float edgeTop = floorY + ApartmentFront.ParapetHeight;
            bool railing = front.Railing && !walkway;
            if (!railing)
            {
                AddWallBox(buffer, wall, outward, inA, inB, floorY, edgeTop, depth - ApartmentFront.ParapetThickness, depth, true, false, true);
            }
            else
            {
                // A low cast kerb, the top rail, and close up two lower rails and the posts.
                buffer.Look = looks.Trim;
                AddWallBox(buffer, wall, outward, inA, inB, floorY, floorY + 0.12f, depth - 0.12f, depth, true, false, true);
                buffer.Look = looks.Frame;
                float rail = ApartmentFront.RailSize;
                AddWallBox(buffer, wall, outward, inA, inB, edgeTop - rail, edgeTop, depth - rail, depth, true, true, true);
                if (!far)
                {
                    for (int i = 1; i <= 2; i++)
                    {
                        float y = floorY + 0.12f + (ApartmentFront.ParapetHeight - 0.12f) * (i / 3.0f);
                        AddWallBox(buffer, wall, outward, inA, inB, y - 0.025f, y + 0.025f, depth - 0.045f, depth - 0.01f, true, true, true);
                    }
                    float post = ApartmentFront.PostSize;
                    int posts = Mathf.Max(1, Mathf.CeilToInt((inB - inA) / 1.3f));
                    for (int i = 1; i < posts; i++)
                    {
                        float u = Mathf.Lerp(inA, inB, i / (float)posts);
                        AddWallBox(buffer, wall, outward, u - post * 0.5f, u + post * 0.5f, floorY + 0.12f, edgeTop - rail, depth - post - 0.01f,
                            depth - 0.01f, false, false, true);
                    }
                }
            }
            if (far)
            {
                return;
            }

            // Each flat: the partition from its neighbour, its laundry pole, sometimes a futon airing over the parapet.
            Color32 partition = SurfaceLook.Pack(PartitionTint, (int)WallMaterial.Plaster, 0.25f);
            float half = ApartmentFront.PartitionThickness * 0.5f;
            for (int k = first; k < end; k++)
            {
                float fa = edges[k];
                float fb = edges[k + 1];
                if (k > first)
                {
                    buffer.Look = partition;
                    AddWallBox(buffer, wall, outward, fa - half, fa + half, floorY, Mathf.Min(floorY + ApartmentFront.PartitionHeight, ceiling), 0.0f,
                        depth - ApartmentFront.ParapetThickness * 0.5f, true, false, false);
                }
                if (walkway)
                {
                    continue;
                }
                float left = k == first ? fa + wallT : fa + half;
                float right = k == end - 1 ? fb - wallT : fb - half;
                if (right - left < 0.8f)
                {
                    continue;
                }
                if (ApartmentFront.UnitDie(site.Seed, k, row, ApartmentFront.PoleDie) < ApartmentFront.PoleShare)
                {
                    // From side wall to side wall, high under the slab above (clear of a 2.6 m body standing on the balcony).
                    buffer.Look = looks.Frame;
                    float d = ApartmentFront.PoleFromWall;
                    float y = Mathf.Min(floorY + ApartmentFront.PoleHeight, ceiling - 0.2f);
                    AddWallBox(buffer, wall, outward, left, right, y, y + 0.04f, d - 0.02f, d + 0.02f, true, true, true);
                    if (ApartmentFront.UnitDie(site.Seed, k, row, ApartmentFront.WashingDie) < ApartmentFront.WashingShare)
                    {
                        AddWashing(buffer, site.Seed, k, row, wall, outward, left, right, y, d, floorY + ApartmentFront.ParapetHeight + 0.15f);
                    }
                }
                if (!railing && ApartmentFront.UnitDie(site.Seed, k, row, ApartmentFront.FutonDie) < ApartmentFront.FutonShare)
                {
                    // Draped over the parapet's top, hanging down both its faces.
                    double colour = ApartmentFront.UnitDie(site.Seed, k, row, ApartmentFront.FutonColourDie);
                    buffer.Look = SurfaceLook.Pack(FutonColours[Mathf.Min(FutonColours.Length - 1, (int)(colour * FutonColours.Length))],
                        (int)WallMaterial.Plaster, 0.05f);
                    float width = Mathf.Min(right - left - 0.2f, 1.1f + 0.5f * (float)colour);
                    float centre = Mathf.Lerp(left + width * 0.5f + 0.1f, right - width * 0.5f - 0.1f,
                        (float)ApartmentFront.UnitDie(site.Seed, k, row, ApartmentFront.FutonPlaceDie));
                    AddWallBox(buffer, wall, outward, centre - width * 0.5f, centre + width * 0.5f, edgeTop - 0.75f, edgeTop + 0.05f,
                        depth - ApartmentFront.ParapetThickness - 0.05f, depth + 0.05f, true, true, true);
                }
            }
        }

        // Washing on the line: mostly whites and blues, a dark one, now and then a bright one.
        private static readonly Color[] WashingColours =
        {
            new Color(0.90f, 0.90f, 0.88f), new Color(0.90f, 0.90f, 0.88f), new Color(0.55f, 0.66f, 0.80f), new Color(0.20f, 0.24f, 0.36f),
            new Color(0.82f, 0.62f, 0.66f), new Color(0.58f, 0.58f, 0.60f), new Color(0.86f, 0.80f, 0.55f), new Color(0.62f, 0.16f, 0.15f),
        };

        /// <summary>
        /// A few pieces of washing on a laundry pole at <paramref name="poleY"/>, <paramref name="poleD"/> out from the wall: shirts and towels,
        /// thin flat boxes spread along it, none hanging lower than <paramref name="lowest"/> (over the parapet, so the street sees them).
        /// </summary>
        private static void AddWashing(MeshBuffer buffer, int seed, int unit, int row, in Wall wall, Vector3 outward,
            float left, float right, float poleY, float poleD, float lowest)
        {
            int pieces = 1 + Mathf.Min(ApartmentFront.MaxPieces - 1,
                (int)(ApartmentFront.UnitDie(seed, unit, row, ApartmentFront.WashingCountDie) * ApartmentFront.MaxPieces));
            float slot = (right - left) / pieces;
            for (int i = 0; i < pieces; i++)
            {
                float size = (float)ApartmentFront.UnitDie(seed, unit, row, ApartmentFront.PieceDie + i * 2);
                double colour = ApartmentFront.UnitDie(seed, unit, row, ApartmentFront.PieceDie + i * 2 + 1);
                float width = Mathf.Min(slot - 0.12f, 0.45f + 0.35f * size);
                // A towel is wide and short, a shirt narrower and longer: the drop runs the other way from the width.
                float drop = Mathf.Min(poleY - lowest, 0.95f - 0.4f * size);
                if (width < 0.25f || drop < 0.25f)
                {
                    continue;
                }
                float centre = left + slot * (i + 0.5f);
                buffer.Look = SurfaceLook.Pack(WashingColours[Mathf.Min(WashingColours.Length - 1, (int)(colour * WashingColours.Length))],
                    (int)WallMaterial.Plaster, 0.0f);
                AddWallBox(buffer, wall, outward, centre - width * 0.5f, centre + width * 0.5f, poleY - drop, poleY + 0.02f,
                    poleD - 0.015f, poleD + 0.015f, true, true, true);
            }
        }

        /// <summary>
        /// A box standing out from a wall: from <paramref name="u0"/> to <paramref name="u1"/> along it, <paramref name="y0"/> to
        /// <paramref name="y1"/> up, and <paramref name="near"/> to <paramref name="far"/> metres out. Its front and two ends always; its top,
        /// underside and back only when asked (a part standing on a slab has no seen underside, a part against the wall no seen back).
        /// </summary>
        private static void AddWallBox(MeshBuffer buffer, in Wall wall, Vector3 outward, float u0, float u1, float y0, float y1,
            float near, float far, bool top, bool bottom, bool back)
        {
            if (u1 - u0 <= 0.0001f || y1 - y0 <= 0.0001f || far - near <= 0.0001f)
            {
                return;
            }
            Vector3 along = wall.RunsInZ ? Vector3.forward : Vector3.right;
            Vector3 n0 = outward * near;
            Vector3 n1 = outward * far;
            Vector3 a0 = wall.At(u0, y0), a1 = wall.At(u1, y0), b0 = wall.At(u0, y1), b1 = wall.At(u1, y1);
            AddFace(buffer, outward, a0 + n1, a1 + n1, b1 + n1, b0 + n1);
            AddFace(buffer, -along, a0 + n0, a0 + n1, b0 + n1, b0 + n0);
            AddFace(buffer, along, a1 + n0, a1 + n1, b1 + n1, b1 + n0);
            if (top)
            {
                AddFace(buffer, Vector3.up, b0 + n0, b1 + n0, b1 + n1, b0 + n1);
            }
            if (bottom)
            {
                AddFace(buffer, Vector3.down, a0 + n0, a1 + n0, a1 + n1, a0 + n1);
            }
            if (back)
            {
                AddFace(buffer, -outward, a0 + n0, a1 + n0, b1 + n0, b0 + n0);
            }
        }
    }
}
