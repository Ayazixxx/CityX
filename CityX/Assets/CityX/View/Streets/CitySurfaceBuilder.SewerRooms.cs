using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The rooms where the sewers' tunnels meet (docs/SEWERS.md): a hall as tall as the street's slab allows under every junction, each
    /// tunnel coming in through its own mouth, and over its middle the neck of a manhole with a ladder down to the floor.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>How far a shaft's collar hangs under the room's ceiling, and how thick it is.</summary>
        private const double CollarDrop = 0.35;
        private const double CollarThickness = 1.25;

        /// <summary>A shut lid's underside is this far under the road; an open one's lid lies this far from the hole.</summary>
        private const double LidUnderRoad = 0.3;
        private const double LidAside = 1.7;

        /// <summary>The height everywhere on the plane through three points.</summary>
        private static Func<double, double, double> PlaneThrough(Vec2 a, double ha, Vec2 b, double hb, Vec2 c, double hc)
        {
            double det = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (Math.Abs(det) < 1e-9) return (x, z) => ha;
            double gx = ((hb - ha) * (c.Y - a.Y) - (hc - ha) * (b.Y - a.Y)) / det;
            double gz = ((hc - ha) * (b.X - a.X) - (hb - ha) * (c.X - a.X)) / det;
            return (x, z) => ha + gx * (x - a.X) + gz * (z - a.Y);
        }

        private static void BuildSewerRoom(Context ctx, MeshBuffer buffer, SewerRoom room)
        {
            int n = room.Plate.Length;
            Vec2 centre = room.Node.P;
            int salt = room.Node.Id;
            // The room is built as the first tunnel into it is.
            SewerStyle style = SewerStyle.BrickVault;
            for (int i = 0; i < n; i++)
            {
                if (room.Opening[i] != null)
                {
                    style = room.Opening[i].Style;
                    break;
                }
            }
            List<Vec2[]> neck = room.Shaft == null ? null : new List<Vec2[]> { Grown(room.Shaft.Ring, room.Shaft.Centre, CollarThickness) };

            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vec2 a = room.Plate[i], b = room.Plate[next];
                var wedge = new List<Vec2> { centre, a, b };
                if (Math.Abs(PlanMath.SignedArea(wedge)) > 1e-4)
                {
                    buffer.Look = SewerLook(style, SewerSurface.Walkway, salt, true, room.Tone);
                    Fan(ctx, buffer, wedge, PlaneThrough(centre, room.CentreFloor, a, room.Floor[i], b, room.Floor[next]));
                    buffer.Look = SewerLook(style, SewerSurface.Vault, salt, true, room.Tone);
                    Func<double, double, double> ceiling = PlaneThrough(centre, room.CentreCeiling, a, room.Ceiling[i], b, room.Ceiling[next]);
                    foreach (List<Vec2> part in Subtract(wedge, neck))
                    {
                        Fan(ctx, buffer, part, ceiling, false);
                    }
                }

                if ((b - a).Length < 0.02) continue;
                Vec2 inward = (b - a).Perp.Normalized;
                if (Vec2.Dot(inward, centre - a) < 0.0) inward = -inward;
                SewerSection opening = room.Opening[i];
                buffer.Look = SewerLook(opening?.Style ?? style, SewerSurface.Wall, salt, true, room.Tone);
                if (opening != null)
                {
                    SewerWall(ctx, buffer, a, b, room.Floor[i], room.Ceiling[i], opening.Opening, Level(inward));
                }
                else
                {
                    float length = (float)(b - a).Length, heightA = (float)(room.Ceiling[i] - room.Floor[i]), heightB = (float)(room.Ceiling[next] - room.Floor[next]);
                    buffer.AddQuad(ctx.Local(a.X, room.Floor[i], a.Y), ctx.Local(b.X, room.Floor[next], b.Y),
                        ctx.Local(b.X, room.Ceiling[next], b.Y), ctx.Local(a.X, room.Ceiling[i], a.Y), Level(inward),
                        new Vector2(0.0f, 0.0f), new Vector2(length, 0.0f), new Vector2(length, heightB), new Vector2(0.0f, heightA));
                }
            }

            if (room.Shaft != null)
            {
                BuildSewerShaft(ctx, buffer, room, style, salt);
            }
        }

        /// <summary>An outline grown out from a middle by a factor.</summary>
        private static Vec2[] Grown(Vec2[] ring, Vec2 centre, double factor)
        {
            var grown = new Vec2[ring.Length];
            for (int i = 0; i < ring.Length; i++)
            {
                grown[i] = centre + (ring[i] - centre) * factor;
            }
            return grown;
        }

        /// <summary>
        /// The way up out of a room: the neck through the road's slab, its collar standing down out of the ceiling, the ladder from the
        /// floor to the street, and the lid: on the hole and shut, or lying beside it with a yellow ring round the open hole.
        /// </summary>
        private static void BuildSewerShaft(Context ctx, MeshBuffer buffer, SewerRoom room, SewerStyle style, int salt)
        {
            SewerShaft shaft = room.Shaft;
            Vec2[] ring = shaft.Ring;
            Vec2[] outer = Grown(ring, shaft.Centre, CollarThickness);
            double low = room.CentreCeiling - CollarDrop;
            double collarTop = room.CentreCeiling + 1.0;   // well into the slab, whatever the ceiling's slope there
            int n = ring.Length;
            var top = new double[n];
            for (int i = 0; i < n; i++)
            {
                double surface = ctx.Ground(ring[i].X, ring[i].Y) + RoadLift;
                top[i] = shaft.Open ? surface : surface - LidUnderRoad;
            }
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Vec2 a = ring[i], b = ring[next], oa = outer[i], ob = outer[next];
                Vec2 inward = (shaft.Centre - (a + b) * 0.5).Normalized;
                float side = (float)(b - a).Length;
                // The neck's inside, the collar's outside (seen from the room), and the rim under it.
                buffer.Look = SewerLook(style, SewerSurface.Wall, salt, true, room.Tone);
                buffer.AddQuad(ctx.Local(a.X, low, a.Y), ctx.Local(b.X, low, b.Y), ctx.Local(b.X, top[next], b.Y), ctx.Local(a.X, top[i], a.Y), Level(inward),
                    new Vector2(0.0f, 0.0f), new Vector2(side, 0.0f), new Vector2(side, (float)(top[next] - low)), new Vector2(0.0f, (float)(top[i] - low)));
                buffer.AddQuad(ctx.Local(oa.X, low, oa.Y), ctx.Local(ob.X, low, ob.Y), ctx.Local(ob.X, collarTop, ob.Y), ctx.Local(oa.X, collarTop, oa.Y), Level(-inward),
                    new Vector2(0.0f, 0.0f), new Vector2(side, 0.0f), new Vector2(side, (float)(collarTop - low)), new Vector2(0.0f, (float)(collarTop - low)));
                buffer.AddQuad(ctx.Local(a.X, low, a.Y), ctx.Local(b.X, low, b.Y), ctx.Local(ob.X, low, ob.Y), ctx.Local(oa.X, low, oa.Y), Vector3.down,
                    new Vector2((float)a.X, (float)a.Y), new Vector2((float)b.X, (float)b.Y), new Vector2((float)ob.X, (float)ob.Y), new Vector2((float)oa.X, (float)oa.Y));
            }

            Func<double, double, double> road = (x, z) => ctx.Ground(x, z) + RoadLift;
            if (shaft.Open)
            {
                // The lid lies on the road beside the hole, and a ring of yellow paint is round the hole.
                Vec2 aside = shaft.Centre + shaft.Normal.Perp * LidAside;
                var lid = new Vec2[n];
                for (int i = 0; i < n; i++) lid[i] = aside + (ring[i] - shaft.Centre) * 1.06;
                SewerLid(ctx, buffer, lid, aside, (x, z) => road(x, z) + 0.06, true);
                buffer.Look = SewerYellow(false);
                Vec2[] paint = Grown(ring, shaft.Centre, 1.4);
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    Fan(ctx, buffer, new List<Vec2> { ring[i], ring[next], paint[next], paint[i] }, (x, z) => road(x, z) + 0.012);
                }
            }
            else
            {
                // Shut: the lid in the road, and its underside closing the neck.
                SewerLid(ctx, buffer, Grown(ring, shaft.Centre, 1.06), shaft.Centre, (x, z) => road(x, z) + 0.015, false);
                buffer.Look = SewerIron(true);
                Fan(ctx, buffer, new List<Vec2>(ring), (x, z) => road(x, z) - LidUnderRoad, false);
            }

            BuildLadder(ctx, buffer, Ladders.Of(shaft), 0.3f, true);
        }

        /// <summary>A manhole's iron lid lying at a height: its top, and its edge where it lies proud of the road.</summary>
        private static void SewerLid(Context ctx, MeshBuffer buffer, Vec2[] outline, Vec2 centre, Func<double, double, double> height, bool edge)
        {
            buffer.Look = SewerIron(false);
            for (int i = 0; i < outline.Length; i++)
            {
                Vec2 a = outline[i], b = outline[(i + 1) % outline.Length];
                Fan(ctx, buffer, new List<Vec2> { centre, a, b }, height);
                if (!edge) continue;
                Vec2 outward = ((a + b) * 0.5 - centre).Normalized;
                float side = (float)(b - a).Length;
                buffer.AddQuad(ctx.Local(a.X, height(a.X, a.Y) - 0.06, a.Y), ctx.Local(b.X, height(b.X, b.Y) - 0.06, b.Y),
                    ctx.Local(b.X, height(b.X, b.Y), b.Y), ctx.Local(a.X, height(a.X, a.Y), a.Y), Level(outward),
                    new Vector2(0.0f, 0.0f), new Vector2(side, 0.0f), new Vector2(side, 0.06f), new Vector2(0.0f, 0.06f));
            }
        }

        /// <summary>Half the thickness of a ladder's rails and of its rungs.</summary>
        private const float LadderRailHalf = 0.03f;
        private const float LadderRungHalf = 0.022f;

        /// <summary>
        /// A ladder: two rails and a rung every <see cref="Ladders.Pitch"/>, its rails set <paramref name="sunk"/> into the floor, its top
        /// three rungs painted yellow so it is seen from above.
        /// </summary>
        private static void BuildLadder(Context ctx, MeshBuffer buffer, Ladder ladder, float sunk, bool inside)
        {
            Vector3 foot = ladder.Foot - ctx.Origin;
            Vector3 across = Vector3.Cross(Vector3.up, ladder.Normal).normalized * Ladders.RungHalfWidth;
            int rungs = ladder.RungCount;
            float railTop = Ladders.FirstRung + (rungs - 1) * Ladders.Pitch + 0.1f;
            buffer.Look = SewerIron(inside);
            CityProps.AddBeam(buffer, foot - across - Vector3.up * sunk, foot - across + Vector3.up * railTop, LadderRailHalf);
            CityProps.AddBeam(buffer, foot + across - Vector3.up * sunk, foot + across + Vector3.up * railTop, LadderRailHalf);
            for (int i = 0; i < rungs; i++)
            {
                Vector3 rung = foot + Vector3.up * (Ladders.FirstRung + i * Ladders.Pitch);
                buffer.Look = i >= rungs - 3 ? SewerYellow(inside) : SewerIron(inside);
                CityProps.AddBeam(buffer, rung - across, rung + across, LadderRungHalf);
            }
        }
    }
}
