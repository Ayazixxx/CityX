using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// What stands along each sidewalk's kerb (World/StreetFurniture, docs/AMERICAN_STREETS.md), stood chunk by chunk, and the marks of
    /// the parking stalls in the road beside the meters.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>Red paint on the kerb by a hydrant, as American streets have it: no parking.</summary>
        public static readonly Color32 RedKerbLook = SurfaceLook.Pack(new Color(0.72f, 0.12f, 0.09f), (int)WallMaterial.Plaster, 0.45f);

        /// <summary>The red paint stands this far proud of the kerb's face and top, and reaches this far back over its top, metres.</summary>
        private const double RedKerbProud = 0.006, RedKerbOverTheTop = 0.2;

        /// <summary>The red kerb is laid in pieces this long, to follow the ground.</summary>
        private const double RedKerbPiece = 1.0;

        /// <summary>A stall's mark: half its line's width, how far off the kerb its stem starts, and how far its bar reaches each way along the road, metres.</summary>
        public const double StallMarkHalfWidth = 0.05, StallMarkFromKerb = 0.15, StallMarkBar = 0.55;

        /// <summary>
        /// One road's furnishings that stand in this chunk. None where the pavement is cut away (a ramp's foot, a garage's drive, a
        /// rounded corner), at a stairway's mouth, or on Old Guy's lot.
        /// </summary>
        private static void BuildStreetFurniture(Context ctx, EdgeGeometry g, MeshBuffer structures, List<Vec2[]> cuts, List<StairLane> stairways,
            List<StreetFurnishing> scratch)
        {
            scratch.Clear();
            StreetFurniture.Along(ctx.Plan, g, scratch);
            foreach (StreetFurnishing thing in scratch)
            {
                if (!ctx.Contains(thing.At) || InCut(thing.At, cuts) || BeforeStairway(stairways, thing.At) || ctx.Plan.OnTutorialLot(thing.At.X, thing.At.Y, 1.0))
                {
                    continue;
                }
                double y = ctx.Plan.GroundHeight(thing.At.X, thing.At.Y) + RoadLift + KerbHeight;
                CityProps.AddStreetFurnishing(structures, ctx.Local(thing.At.X, y, thing.At.Y), thing);
            }
            foreach (StreetFurnishing thing in scratch)
            {
                // Each chunk paints the part of a hydrant's red kerb that lies in it, the hydrant itself in this chunk or the next.
                if (thing.Kind == StreetFurnitureKind.Hydrant && !InCut(thing.At, cuts) && !BeforeStairway(stairways, thing.At)
                    && !ctx.Plan.OnTutorialLot(thing.At.X, thing.At.Y, 1.0))
                {
                    PaintRedKerb(ctx, g, thing, structures, cuts);
                }
            }
        }

        /// <summary>The kerb by a hydrant painted red, its face and the edge of its top, <see cref="StreetFurniture.RedKerbEach"/> either way.</summary>
        private static void PaintRedKerb(Context ctx, EdgeGeometry g, StreetFurnishing hydrant, MeshBuffer structures, List<Vec2[]> cuts)
        {
            Vec2 toRoad = hydrant.ToRoad;
            double along = Vec2.Dot(hydrant.At - g.A, g.Dir);
            Vec2 kerbAt = g.A + g.Dir * along - toRoad * g.HalfWidth;
            Vec2 from = kerbAt - g.Dir * StreetFurniture.RedKerbEach, to = kerbAt + g.Dir * StreetFurniture.RedKerbEach;
            if (!ClipSegment(from, to, ctx.Rect, out Vec2 ka, out Vec2 kb) || (kb - ka).Length < 0.02)
            {
                return;
            }
            Color32 saved = structures.Look;
            structures.Look = RedKerbLook;
            var road = new Vector3((float)toRoad.X, 0.0f, (float)toRoad.Y);
            int pieces = Math.Max(1, (int)Math.Ceiling((kb - ka).Length / RedKerbPiece));
            for (int i = 0; i < pieces; i++)
            {
                Vec2 a = ka + (kb - ka) * ((double)i / pieces), b = ka + (kb - ka) * ((double)(i + 1) / pieces);
                if (InCut((a + b) * 0.5 - toRoad * 0.1, cuts))
                {
                    continue;   // a ramp's foot or a drive: no kerb here to paint
                }
                Vec2 fa = a + toRoad * RedKerbProud, fb = b + toRoad * RedKerbProud;
                Vec2 ba = a - toRoad * RedKerbOverTheTop, bb = b - toRoad * RedKerbOverTheTop;
                double ya = ctx.Plan.GroundHeight(a.X, a.Y) + RoadLift, yb = ctx.Plan.GroundHeight(b.X, b.Y) + RoadLift;
                double topA = ya + KerbHeight + RedKerbProud, topB = yb + KerbHeight + RedKerbProud;
                float length = (float)(b - a).Length;
                FacingQuad(structures, road, ctx.Local(fa.X, ya, fa.Y), ctx.Local(fb.X, yb, fb.Y), ctx.Local(fb.X, topB, fb.Y), ctx.Local(fa.X, topA, fa.Y), length);
                FacingQuad(structures, Vector3.up, ctx.Local(fa.X, topA, fa.Y), ctx.Local(fb.X, topB, fb.Y), ctx.Local(bb.X, topB, bb.Y), ctx.Local(ba.X, topA, ba.Y), length);
            }
            structures.Look = saved;
        }

        /// <summary>A quad seen from the side <paramref name="facing"/> points to, whichever way round its corners were given.</summary>
        private static void FacingQuad(MeshBuffer buffer, Vector3 facing, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float length)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0.0f)
            {
                (a, b, c, d) = (b, a, d, c);
            }
            buffer.AddQuad(a, b, c, d, facing, new Vector2(0.0f, 0.0f), new Vector2(length, 0.0f), new Vector2(length, 0.2f), new Vector2(0.0f, 0.2f));
        }

        /// <summary>
        /// The parking stalls of a road's metered sides: a white T at each stall line, its stem from the kerb out into the road and its bar
        /// along the edge of the lane.
        /// </summary>
        private static void PaintStalls(Context ctx, EdgeGeometry g, MeshBuffer white, Func<double, double, double> height)
        {
            if (white == null)
            {
                return;
            }
            var lines = new List<double>();
            for (int side = 1; side >= -1; side -= 2)
            {
                lines.Clear();
                StreetFurniture.StallLines(ctx.Plan, g, side, lines);
                Vec2 aside = g.Dir.Perp * side;
                foreach (double along in lines)
                {
                    Vec2 atTheKerb = g.A + g.Dir * along + aside * (g.HalfWidth - StallMarkFromKerb);
                    Vec2 end = g.A + g.Dir * along + aside * (g.HalfWidth - StreetFurniture.StallDepth);
                    StripeAlong(ctx, white, atTheKerb, end, g.Dir, StallMarkHalfWidth, height);
                    StripeAlong(ctx, white, end - g.Dir * StallMarkBar, end + g.Dir * StallMarkBar, aside, StallMarkHalfWidth, height);
                }
            }
        }
    }
}
