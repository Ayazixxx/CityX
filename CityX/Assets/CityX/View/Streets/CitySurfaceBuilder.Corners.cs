using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The kerb going round each street corner in an arc (World/KerbCorners, docs/AMERICAN_STREETS.md). The plan's blocks have sharp corners;
    /// here the pavement and the ground are cut away between the corner and the arc, the road's surface is laid in the cut, and the kerb
    /// is stood round it.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>The cuts reach this far past the sharp corner, metres, so the pavement's own edge is inside them and not along their edge.</summary>
        private const double CornerCutPast = 0.05;

        private static readonly Color32 KerbLook = SurfaceLook.Pack(new Color32(214, 212, 206, 255), (int)WallMaterial.Concrete, 0.45f);

        /// <summary>
        /// What the rounded corners of these blocks take out of the pavement and the ground under it, where they touch this chunk: one
        /// triangle for each straight piece of each arc (Subtract takes convex cuts; the whole piece between a corner and its arc is not).
        /// </summary>
        private static List<Vec2[]> CornerCuts(Context ctx, List<int> blockIndices)
        {
            var cuts = new List<Vec2[]>();
            foreach (int blockIndex in blockIndices)
            {
                PlanBlock block = ctx.Plan.Data.Blocks[blockIndex];
                for (int v = 0; block.Inner != null && v < block.Inner.Count; v++)
                {
                    if (!KerbCorners.TryAt(ctx.Plan, block, v, out KerbArc arc) || !Near(ctx, arc))
                    {
                        continue;
                    }
                    Vec2 past = arc.Corner - arc.Inward * CornerCutPast;
                    int pieces = arc.Pieces;
                    for (int i = 0; i < pieces; i++)
                    {
                        cuts.Add(new[] { past, arc.PointAt((double)i / pieces), arc.PointAt((double)(i + 1) / pieces) });
                    }
                }
            }
            return cuts;
        }

        /// <summary>Whether any of a corner's cut can lie in this chunk.</summary>
        private static bool Near(Context ctx, KerbArc arc)
        {
            double reach = arc.TangentLength + CornerCutPast;
            return ctx.Rect.Overlaps(arc.Corner.X - reach, arc.Corner.Y - reach, arc.Corner.X + reach, arc.Corner.Y + reach);
        }

        /// <summary>The road's surface in each rounded corner of a block, at the road's height, and the kerb round the arc.</summary>
        private static void BuildKerbCorners(Context ctx, PlanBlock block, MeshBuffer ground, MeshBuffer pavement)
        {
            for (int v = 0; block.Inner != null && v < block.Inner.Count; v++)
            {
                if (!KerbCorners.TryAt(ctx.Plan, block, v, out KerbArc arc) || !Near(ctx, arc))
                {
                    continue;
                }
                Func<double, double, double> road = (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift;
                // The junction's own asphalt (BuildPlate): the corner reads as part of the crossing.
                RoadEdge into = ctx.Plan.EdgeBetween(block.Poly[(v + block.Poly.Count - 1) % block.Poly.Count], block.Poly[v]);
                RoadNode node = (into.A.P - block.Poly[v]).Length < (into.B.P - block.Poly[v]).Length ? into.A : into.B;
                Color32 asphalt = AsphaltLook(RoadClass.Street, node.Id);
                int pieces = arc.Pieces;
                Vec2 from = arc.PointAt(0.0);
                for (int i = 0; i < pieces; i++)
                {
                    Vec2 to = arc.PointAt((double)(i + 1) / pieces);
                    var triangle = new List<Vec2> { arc.Corner, from, to };
                    if (PlanMath.SignedArea(triangle) < 0.0)
                    {
                        triangle.Reverse();
                    }
                    ground.Look = asphalt;
                    Fan(ctx, ground, Clip(triangle, ctx.Rect), road);

                    if (ClipSegment(from, to, ctx.Rect, out Vec2 ka, out Vec2 kb) && (kb - ka).Length > 0.02)
                    {
                        pavement.Look = KerbLook;
                        KerbFace(ctx, pavement, ka, kb);
                    }
                    from = to;
                }
            }
        }

        /// <summary>A kerb's face along a stretch of the block's edge, taken in the block's own order: from the road up to the pavement, facing the road.</summary>
        private static void KerbFace(Context ctx, MeshBuffer buffer, Vec2 ka, Vec2 kb)
        {
            Vec2 d = (kb - ka).Normalized;
            var normal = new Vector3((float)d.Y, 0.0f, (float)-d.X);   // to the right of the edge: toward the road
            double ya = ctx.Plan.GroundHeight(ka.X, ka.Y) + RoadLift;
            double yb = ctx.Plan.GroundHeight(kb.X, kb.Y) + RoadLift;
            Vector3 b0 = ctx.Local(ka.X, ya, ka.Y);
            Vector3 b1 = ctx.Local(kb.X, yb, kb.Y);
            Vector3 t1 = ctx.Local(kb.X, yb + KerbHeight, kb.Y);
            Vector3 t0 = ctx.Local(ka.X, ya + KerbHeight, ka.Y);
            float len = (float)(kb - ka).Length;
            buffer.AddQuad(b0, b1, t1, t0, normal,
                new Vector2(0.0f, 0.0f), new Vector2(len, 0.0f), new Vector2(len, KerbHeight), new Vector2(0.0f, KerbHeight));
        }

        /// <summary>
        /// How far from each end of one edge of a block its straight kerb begins and ends: past the arcs of the corners at its ends, where
        /// those are rounded (0 where a corner is sharp).
        /// </summary>
        private static void KerbInsets(Context ctx, PlanBlock block, int edge, out double fromStart, out double fromEnd)
        {
            int next = (edge + 1) % block.Poly.Count;
            fromStart = KerbCorners.TryAt(ctx.Plan, block, edge, out KerbArc first) ? first.TangentLength : 0.0;
            fromEnd = KerbCorners.TryAt(ctx.Plan, block, next, out KerbArc last) ? last.TangentLength : 0.0;
        }
    }
}
