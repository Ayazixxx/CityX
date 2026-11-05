using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A block's paving laid in its sidewalks' own frames (World/Sidewalks, docs/AMERICAN_STREETS.md): the part of each piece that lies on
    /// a sidewalk gets that sidewalk's joints, square to its street, and what lies behind the sidewalks the block's own.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>One side of a block's sidewalk: the strip it covers, the box round it, and the frame its flags are laid in.</summary>
        private readonly struct Sidewalk
        {
            public readonly Vec2[] Strip;
            public readonly PavingFrame Frame;
            private readonly double _x0, _z0, _x1, _z1;

            public Sidewalk(Vec2[] strip, PavingFrame frame)
            {
                Strip = strip;
                Frame = frame;
                Bounds(strip, out _x0, out _z0, out _x1, out _z1);
            }

            public bool MayTouch(double x0, double z0, double x1, double z1) => x1 >= _x0 && x0 <= _x1 && z1 >= _z0 && z0 <= _z1;
        }

        /// <summary>The sidewalks of a block, one for each side that has one to lay (Sidewalks.TryAlong).</summary>
        private static List<Sidewalk> SidewalksOf(PlanBlock block)
        {
            var walks = new List<Sidewalk>(block.Inner.Count);
            for (int side = 0; side < block.Inner.Count; side++)
            {
                if (Sidewalks.TryAlong(block, side, out Vec2[] strip, out PavingFrame frame))
                {
                    walks.Add(new Sidewalk(strip, frame));
                }
            }
            return walks;
        }

        /// <summary>
        /// One convex piece of a block's paving, laid: the part of it on each sidewalk in that sidewalk's frame, the rest in the frame of
        /// what lies behind them.
        /// </summary>
        private static void LayPaving(Context ctx, MeshBuffer buffer, List<Vec2> piece, Func<double, double, double> height, List<Sidewalk> walks,
            PavingFrame behind)
        {
            Bounds(piece, out double x0, out double z0, out double x1, out double z1);
            List<List<Vec2>> left = null;   // what no sidewalk has taken yet: the whole piece until one takes some
            foreach (Sidewalk walk in walks)
            {
                if (!walk.MayTouch(x0, z0, x1, z1))
                {
                    continue;
                }
                var beyond = new List<List<Vec2>>();
                if (left == null)
                {
                    LayOn(ctx, buffer, Split(piece, walk.Strip, beyond), height, walk.Frame);
                }
                else
                {
                    foreach (List<Vec2> part in left)
                    {
                        LayOn(ctx, buffer, Split(part, walk.Strip, beyond), height, walk.Frame);
                    }
                }
                left = beyond;
            }
            if (left == null)
            {
                Fan(ctx, buffer, piece, height, behind);
                return;
            }
            foreach (List<Vec2> part in left)
            {
                Fan(ctx, buffer, part, height, behind);
            }
        }

        private static void LayOn(Context ctx, MeshBuffer buffer, List<Vec2> part, Func<double, double, double> height, PavingFrame frame)
        {
            if (part.Count >= 3 && Math.Abs(PlanMath.SignedArea(part)) > SliverArea)
            {
                Fan(ctx, buffer, part, height, frame);
            }
        }

        /// <summary>
        /// A convex piece split by a convex outline: the part inside it is returned (fewer than three corners when none is), and the
        /// parts outside are added to <paramref name="outside"/>, each convex: what lies beyond each edge of the outline, taken one edge
        /// at a time from what is still inside the ones before.
        /// </summary>
        private static List<Vec2> Split(List<Vec2> piece, Vec2[] outline, List<List<Vec2>> outside)
        {
            bool ccw = PlanMath.SignedArea(outline) > 0.0;
            List<Vec2> inside = piece;
            for (int e = 0; e < outline.Length && inside.Count >= 3; e++)
            {
                Vec2 a = outline[e], b = outline[(e + 1) % outline.Length];
                Vec2 inward = (b - a).Perp * (ccw ? 1.0 : -1.0);   // toward the inside of the outline
                List<Vec2> beyond = ClipHalf(inside, a, -inward);
                if (beyond.Count >= 3 && Math.Abs(PlanMath.SignedArea(beyond)) > SliverArea)
                {
                    outside.Add(beyond);
                }
                inside = ClipHalf(inside, a, inward);
            }
            return inside;
        }

        /// <summary>A point's place in a paving's pattern, as the texture coordinates the paving is drawn by (metres).</summary>
        private static Vector2 PatternAt(PavingFrame frame, Vec2 point)
        {
            frame.At(point, out double along, out double across);
            return new Vector2((float)along, (float)across);
        }
    }
}
