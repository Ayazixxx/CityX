using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The open spaces drawn (docs/OPEN_SPACE.md): a plaza block's layout (World/OpenSpace, pure) as paving in the role's look, dry lawns with a
    /// kerb round them, the pieces (CityProps), and bollards along the kerb. Each chunk draws the pieces standing in it and the parts of the
    /// lawns and kerbs inside its square, so a block that spans chunks comes out whole.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>Dry grass: straw over earth (nothing has grown since the rapture).</summary>
        private static readonly Color32 DryGrass = SurfaceLook.Pack(new Color(0.64f, 0.57f, 0.33f), SurfaceLook.Earth, 0.5f);

        /// <summary>A lawn stands this much over the paving (no z-fight) inside a kerb this high.</summary>
        private const float LawnLift = 0.02f, LawnKerbHeight = 0.12f;

        /// <summary>The paving of an open block by its role: earth for a vacant lot, warm setts for the civic square and the quiet court, plain paving for the rest.</summary>
        private static Color32 OpenSpacePavingLook(PlanWorld plan, int blockIndex)
        {
            OpenSpaceRole role = OpenSpace.RoleOf(plan, blockIndex);
            float h = LatticeHash(blockIndex, 9, 23);
            switch (role)
            {
                case OpenSpaceRole.VacantLot:
                    return SurfaceLook.Pack(new Color(0.50f + 0.08f * h, 0.45f, 0.38f), SurfaceLook.Earth, 0.7f);
                case OpenSpaceRole.Plaza:
                case OpenSpaceRole.QuietCourt:
                    return SurfaceLook.Pack(new Color(0.84f + 0.06f * h, 0.80f + 0.05f * h, 0.74f), SurfaceLook.Setts, 0.35f);
                default:
                    return SurfaceLook.Pack(new Color(0.80f + 0.08f * h, 0.79f + 0.06f * h, 0.76f), SurfaceLook.Paving, 0.3f + 0.3f * h);
            }
        }

        private static void BuildOpenSpace(Context ctx, PlanBlock block, int blockIndex, MeshBuffer structures, MeshBuffer lit)
        {
            OpenSpaceLayout layout = OpenSpace.LayoutFor(ctx.Plan, blockIndex);
            if (layout == null) return;
            List<Vec2> ring = block.Inner;
            Bounds(ring, out double bx0, out double bz0, out double bx1, out double bz1);
            if (!ctx.Rect.Overlaps(bx0 - 1.0, bz0 - 1.0, bx1 + 1.0, bz1 + 1.0)) return;
            Func<double, double, double> height = (x, z) => ctx.Plan.GroundHeight(x, z) + RoadLift + KerbHeight;

            // The lawns: straw ground a little over the paving, cut to this chunk, and the kerb round it (the segments in this chunk).
            foreach (Vec2[] lawn in layout.Lawns)
            {
                Bounds(lawn, out double lx0, out double lz0, out double lx1, out double lz1);
                if (!ctx.Rect.Overlaps(lx0, lz0, lx1, lz1)) continue;
                List<Vec2> piece = Clip(lawn, ctx.Rect);
                if (piece.Count >= 3 && PlanMath.SignedArea(piece) > SliverArea)
                {
                    structures.Look = DryGrass;
                    int[] t = PlanMath.Triangulate(piece);
                    for (int k = 0; k + 2 < t.Length; k += 3)
                    {
                        Fan(ctx, structures, new List<Vec2> { piece[t[k]], piece[t[k + 1]], piece[t[k + 2]] }, (x, z) => height(x, z) + LawnLift);
                    }
                }
                for (int i = 0; i < lawn.Length; i++)
                {
                    Vec2 a = lawn[i], c = lawn[(i + 1) % lawn.Length];
                    if (!ClipSegment(a, c, ctx.Rect, out Vec2 ka, out Vec2 kb) || (kb - ka).Length < 0.02) continue;
                    var local = new[] { ctx.Local(ka.X, height(ka.X, ka.Y), ka.Y), ctx.Local(kb.X, height(kb.X, kb.Y), kb.Y) };
                    Color32 saved = structures.Look;
                    structures.Look = CityProps.Concrete;
                    CityProps.AddBeam(structures, local[0] + Vector3.up * (LawnKerbHeight * 0.5f), local[1] + Vector3.up * (LawnKerbHeight * 0.5f), LawnKerbHeight * 0.5f);
                    structures.Look = saved;
                }
            }

            // The pieces, each by the chunk it stands in (DrawPiece, CitySurfaceBuilder.Pedestrian.cs: the streets share the kit).
            foreach (OpenPiece p in layout.Pieces)
            {
                if (!ctx.Contains(p.At)) continue;
                DrawPiece(ctx, p, height(p.At.X, p.At.Y), structures, lit, layout.Powered);
            }

            // Bollards along the kerb, every 3 m, where the role keeps the street out.
            if (layout.KerbBollards)
            {
                int n = ring.Count;
                for (int e = 0; e < n; e++)
                {
                    Vec2 i0 = ring[e], i1 = ring[(e + 1) % n];
                    double len = (i1 - i0).Length;
                    if (len < 6.0) continue;
                    Vec2 dir = (i1 - i0) / len;
                    Vec2 inward = dir.Perp;   // the kerb ring is counter-clockwise: the block is on the left of each edge
                    for (double t = 1.6; t < len - 1.2; t += 3.0)
                    {
                        Vec2 p = i0 + dir * t + inward * 0.45;
                        if (!ctx.Contains(p)) continue;
                        Vector3 at = ctx.Local(p.X, 0.0, p.Y);
                        CityProps.AddBollard(structures, at.x, at.z, ctx.LocalY(height(p.X, p.Y)));
                    }
                }
            }
            structures.Look = SurfaceLook.Default;
        }
    }
}
