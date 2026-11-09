using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Where a tunnel comes out into a dry channel under its road's bridge (docs/SEWERS.md, <see cref="SewerOutfall"/>): the buried
    /// length, a headwall with the tunnel's mouth in it, a gate of iron bars, and an open cut between two wing walls that step down with
    /// the bank to where it meets the floor. The owner's entrance pictures: a barred round mouth with a door standing ajar.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>How far the open cut's walls stand outside the tunnel's widest, and how thick they are.</summary>
        private const double WingClear = 0.3;
        private const double WingThickness = 0.45;

        /// <summary>How far the headwall reaches past the wings each side, how far its top stands over the bank, and how deep it is.</summary>
        private const double HeadwallPast = 1.2;
        private const double HeadwallOverBank = 0.7;
        private const double HeadwallDepth = 0.5;

        /// <summary>How far the wing walls' tops stand over the ground beside them, and the least they stand over the floor.</summary>
        private const double WingOverGround = 0.3;
        private const double WingLeastHeight = 0.5;

        /// <summary>The gate stands this far inside the mouth; its bars are this far apart and this thick (half).</summary>
        private const double GateInside = 0.4;
        private const double BarSpacing = 0.32;
        private const float BarHalf = 0.035f;
        private const float GatePostHalf = 0.055f;

        /// <summary>The door in a gate: half its width, and its height (a 2.6 m body walks through).</summary>
        private const double GateDoorHalf = 0.85;
        private const double GateDoorHeight = 3.3;

        private static double WingInner(SewerOutfall outfall) => outfall.Tube.Section.HalfWidth + WingClear;

        /// <summary>The ground a mouth's open cut takes away: from its headwall to a little past where its floor runs out, wings and all.</summary>
        private static Vec2[] MouthCut(SewerOutfall outfall)
        {
            SewerTube tube = outfall.Tube;
            Vec2 side = tube.Dir.Perp * (WingInner(outfall) + WingThickness);
            Vec2 from = tube.At(outfall.Portal), to = tube.At(outfall.End + 0.3);
            return new[] { from + side, to + side, to - side, from - side };
        }

        private static void BuildSewerMouth(Context ctx, MeshBuffer buffer, SewerOutfall outfall, int salt, bool far)
        {
            SewerTube tube = outfall.Tube;
            SewerSection section = tube.Section;
            if (!far)
            {
                BuildSewerTube(ctx, buffer, tube, salt);
            }
            Vec2 perp = tube.Dir.Perp;
            double inner = WingInner(outfall), outer = inner + WingThickness;
            double floor = outfall.Floor;
            Vec2 portal = tube.At(outfall.Portal);

            if (ctx.Contains(portal))
            {
                // The headwall, looking out over the channel, and its top.
                double reach = outer + HeadwallPast;
                double top = floor + section.Crown + SewerPlan.PortalCover + HeadwallOverBank;
                buffer.Look = SewerLook(section.Style, SewerSurface.Wall, salt, false, tube.Tone);
                SewerWall(ctx, buffer, portal + perp * reach, portal - perp * reach, floor, top, section.Opening, Level(tube.Dir));
                Vec2 back = portal - tube.Dir * HeadwallDepth;
                buffer.AddQuad(ctx.Local(portal.X + perp.X * reach, top, portal.Y + perp.Y * reach), ctx.Local(portal.X - perp.X * reach, top, portal.Y - perp.Y * reach),
                    ctx.Local(back.X - perp.X * reach, top, back.Y - perp.Y * reach), ctx.Local(back.X + perp.X * reach, top, back.Y + perp.Y * reach), Vector3.up,
                    new Vector2(0.0f, 0.0f), new Vector2((float)(reach * 2.0), 0.0f), new Vector2((float)(reach * 2.0), (float)HeadwallDepth), new Vector2(0.0f, (float)HeadwallDepth));
                if (!far && outfall.Gate != SewerGate.None)
                {
                    BuildSewerGate(ctx, buffer, outfall, salt);
                }
            }

            // The open cut: its floor, and a wing wall each side whose top steps down with the bank.
            double length = outfall.End + 0.3 - outfall.Portal;
            int pieces = Math.Max(1, (int)Math.Ceiling(length / 2.0));
            for (int i = 0; i < pieces; i++)
            {
                double s0 = outfall.Portal + length * i / pieces, s1 = outfall.Portal + length * (i + 1) / pieces;
                Vec2 a = tube.At(s0), b = tube.At(s1);
                if (!ctx.Contains((a + b) * 0.5)) continue;
                buffer.Look = SewerLook(section.Style, SewerSurface.Walkway, salt, false, tube.Tone);
                Fan(ctx, buffer, new List<Vec2> { a + perp * inner, b + perp * inner, b - perp * inner, a - perp * inner }, (x, z) => floor);
                buffer.Look = SewerLook(section.Style, SewerSurface.Wall, salt, false, tube.Tone);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vec2 ia = a + perp * (inner * side), ib = b + perp * (inner * side);
                    Vec2 oa = a + perp * (outer * side), ob = b + perp * (outer * side);
                    double topA = WingTop(ctx, oa, floor), topB = WingTop(ctx, ob, floor);
                    float run = (float)(s1 - s0);
                    Vector3 toMiddle = Level(perp * -side);
                    buffer.AddQuad(ctx.Local(ia.X, floor, ia.Y), ctx.Local(ib.X, floor, ib.Y), ctx.Local(ib.X, topB, ib.Y), ctx.Local(ia.X, topA, ia.Y), toMiddle,
                        new Vector2((float)s0, 0.0f), new Vector2((float)s1, 0.0f), new Vector2((float)s1, (float)(topB - floor)), new Vector2((float)s0, (float)(topA - floor)));
                    buffer.AddQuad(ctx.Local(ia.X, topA, ia.Y), ctx.Local(ib.X, topB, ib.Y), ctx.Local(ob.X, topB, ob.Y), ctx.Local(oa.X, topA, oa.Y), Vector3.up,
                        new Vector2((float)s0, 0.0f), new Vector2((float)s1, 0.0f), new Vector2((float)s1, (float)WingThickness), new Vector2((float)s0, (float)WingThickness));
                    buffer.AddQuad(ctx.Local(oa.X, floor, oa.Y), ctx.Local(ob.X, floor, ob.Y), ctx.Local(ob.X, topB, ob.Y), ctx.Local(oa.X, topA, oa.Y), -toMiddle,
                        new Vector2((float)s0, 0.0f), new Vector2((float)s1, 0.0f), new Vector2((float)s1, (float)(topB - floor)), new Vector2((float)s0, (float)(topA - floor)));
                    if (i == pieces - 1)
                    {
                        // The wing's end, and the floor's edge, looking out along the channel's bank.
                        buffer.AddQuad(ctx.Local(ib.X, floor - 0.6, ib.Y), ctx.Local(ob.X, floor - 0.6, ob.Y), ctx.Local(ob.X, topB, ob.Y), ctx.Local(ib.X, topB, ib.Y),
                            Level(tube.Dir), new Vector2(0.0f, 0.0f), new Vector2(run, 0.0f), new Vector2(run, 1.0f), new Vector2(0.0f, 1.0f));
                    }
                }
                if (i == pieces - 1)
                {
                    buffer.AddQuad(ctx.Local(b.X + perp.X * inner, floor - 0.6, b.Y + perp.Y * inner), ctx.Local(b.X - perp.X * inner, floor - 0.6, b.Y - perp.Y * inner),
                        ctx.Local(b.X - perp.X * inner, floor, b.Y - perp.Y * inner), ctx.Local(b.X + perp.X * inner, floor, b.Y + perp.Y * inner), Level(tube.Dir),
                        new Vector2(0.0f, 0.0f), new Vector2((float)(inner * 2.0), 0.0f), new Vector2((float)(inner * 2.0), 0.6f), new Vector2(0.0f, 0.6f));
                }
            }
        }

        private static double WingTop(Context ctx, Vec2 at, double floor) => Math.Max(floor + WingLeastHeight, ctx.Ground(at.X, at.Y) + WingOverGround);

        /// <summary>
        /// The gate across a mouth: upright iron bars from the floor to the arch, two rails across them, and either a barred door in the
        /// middle standing ajar in a yellow frame, or a few bars rusted through. Both are walked through.
        /// </summary>
        private static void BuildSewerGate(Context ctx, MeshBuffer buffer, SewerOutfall outfall, int salt)
        {
            SewerTube tube = outfall.Tube;
            SewerSection section = tube.Section;
            double along = outfall.Portal - GateInside, floor = outfall.Floor;
            double half = section.FloorHalfWidth - 0.15;
            int bars = Math.Max(2, (int)Math.Floor(half * 2.0 / BarSpacing));
            double step = half * 2.0 / bars;
            bool door = outfall.Gate == SewerGate.DoorAjar;
            // The bars that are gone: the door's width in the middle, or four to one side of it.
            int gapFrom = (int)(LatticeHash(salt, 3, 61) * (bars - 6)) + 1;
            Vector3 P(double x, double y) => TubePoint(ctx, tube, along, floor, new Vec2(x, y));

            buffer.Look = SewerIron(false);
            for (int i = 0; i <= bars; i++)
            {
                double x = -half + step * i;
                double foot = Math.Abs(x) < section.ChannelHalfWidth ? -section.ChannelDepth : 0.0;
                double head = section.HeightAt(x) - 0.03;
                if (door && Math.Abs(x) < GateDoorHalf - 0.01)
                {
                    foot = GateDoorHeight;   // over the door, the bars start at its lintel
                }
                else if (!door && i >= gapFrom && i < gapFrom + 4)
                {
                    // Rusted through: a stub hangs from the arch, another leans out of the floor.
                    CityProps.AddBeam(buffer, P(x, head - 0.55), P(x, head), BarHalf);
                    CityProps.AddBeam(buffer, P(x, foot), P(x + 0.12, foot + 0.4), BarHalf);
                    continue;
                }
                if (head - foot > 0.1)
                {
                    CityProps.AddBeam(buffer, P(x, foot), P(x, head), BarHalf);
                }
            }
            // Two rails across, as far as the arch lets them reach, broken where the way through is.
            foreach (double y in new[] { 1.9, GateDoorHeight + 0.5 })
            {
                double reach = half;
                while (reach > 0.5 && section.HeightAt(reach) < y + 0.1) reach -= 0.1;
                double openFrom = door ? -GateDoorHalf : -half + step * gapFrom - step * 0.5;
                double openTo = door ? GateDoorHalf : -half + step * (gapFrom + 3) + step * 0.5;
                if (door && y > GateDoorHeight)
                {
                    CityProps.AddBeam(buffer, P(-reach, y), P(reach, y), BarHalf);   // over the door
                    continue;
                }
                if (openFrom > -reach) CityProps.AddBeam(buffer, P(-reach, y), P(Math.Min(openFrom, reach), y), BarHalf);
                if (openTo < reach) CityProps.AddBeam(buffer, P(Math.Max(openTo, -reach), y), P(reach, y), BarHalf);
            }
            if (!door) return;

            // The door's frame, painted so it is seen, and the door on its hinges, swung out toward the channel.
            double sill = -section.ChannelDepth;
            buffer.Look = SewerYellow(false);
            CityProps.AddBeam(buffer, P(-GateDoorHalf, sill), P(-GateDoorHalf, GateDoorHeight), GatePostHalf);
            CityProps.AddBeam(buffer, P(GateDoorHalf, sill), P(GateDoorHalf, GateDoorHeight), GatePostHalf);
            CityProps.AddBeam(buffer, P(-GateDoorHalf, GateDoorHeight), P(GateDoorHalf, GateDoorHeight), GatePostHalf);

            double swing = (55.0 + 25.0 * LatticeHash(salt, 5, 67)) * Math.PI / 180.0;
            double cos = Math.Cos(swing), sin = Math.Sin(swing);
            double width = GateDoorHalf * 2.0 - 0.08;
            // A point of the leaf: so far from its hinge, so high. Shut it lies across the mouth; open it swings out along the tunnel.
            Vector3 Leaf(double fromHinge, double y)
            {
                Vec2 hinge = tube.At(along) - tube.Dir.Perp * GateDoorHalf;
                Vec2 p = hinge + tube.Dir.Perp * (fromHinge * cos) + tube.Dir * (fromHinge * sin);
                return ctx.Local(p.X, floor + y, p.Y);
            }
            buffer.Look = SewerIron(false);
            double low = 0.12, high = GateDoorHeight - 0.1;
            CityProps.AddBeam(buffer, Leaf(0.0, low), Leaf(width, low), BarHalf);
            CityProps.AddBeam(buffer, Leaf(0.0, high), Leaf(width, high), BarHalf);
            CityProps.AddBeam(buffer, Leaf(0.0, (low + high) * 0.5), Leaf(width, (low + high) * 0.5), BarHalf);
            for (int i = 0; i <= 5; i++)
            {
                CityProps.AddBeam(buffer, Leaf(width * i / 5.0, low), Leaf(width * i / 5.0, high), BarHalf);
            }
        }
    }
}
