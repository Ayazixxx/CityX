using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// THE SEWERS UNDER THE STREETS (docs/SEWERS.md), built from <see cref="SewerPlan"/> into the chunk's own mesh, so they collide as the
    /// street does: the tunnels (this file), the rooms under the junctions with their shafts and ladders (SewerRooms), the mouths in the
    /// dry channels' banks (SewerMouths). Every face looks INTO the tunnel: nothing sees or strikes one from behind.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>
        /// What the sewers take out of the surface in this chunk: the open cuts of the mouths out of the ground, and the open manholes
        /// (returned) out of the junctions' asphalt. A far chunk keeps its manholes whole (nobody sees one from there), but is cut for
        /// a mouth as a near one is, so the bank does not change shape as it comes near.
        /// </summary>
        private static List<Vec2[]> SewerCuts(Context ctx, int chunkX, int chunkY, bool far, List<Vec2[]> terrainCuts)
        {
            SewerPlan sewers = SewerPlan.For(ctx.Plan);
            foreach (int edgeId in ctx.Plan.EdgesIn(chunkX, chunkY))
            {
                foreach (SewerOutfall outfall in sewers.Outfalls(edgeId))
                {
                    Vec2[] cut = MouthCut(outfall);
                    Bounds(cut, out double mx, out double mz, out double Mx, out double Mz);
                    if (ctx.Rect.Overlaps(mx, mz, Mx, Mz)) terrainCuts.Add(cut);
                }
            }
            if (far) return null;
            List<Vec2[]> manholes = null;
            foreach (int nodeId in ctx.Plan.JunctionsIn(chunkX, chunkY))
            {
                SewerShaft shaft = sewers.Room(nodeId)?.Shaft;
                if (shaft == null || !shaft.Open) continue;
                (manholes ??= new List<Vec2[]>()).Add(shaft.Ring);
            }
            return manholes;
        }

        private static void BuildSewers(Context ctx, int chunkX, int chunkY, MeshBuffer buffer, bool far)
        {
            SewerPlan sewers = SewerPlan.For(ctx.Plan);
            foreach (int edgeId in ctx.Plan.EdgesIn(chunkX, chunkY))
            {
                SewerTube tube = sewers.Tube(edgeId);
                if (tube != null && !far)
                {
                    BuildSewerTube(ctx, buffer, tube, edgeId);
                }
                foreach (SewerOutfall outfall in sewers.Outfalls(edgeId))
                {
                    BuildSewerMouth(ctx, buffer, outfall, edgeId, far);
                }
            }
            if (far) return;
            foreach (int nodeId in ctx.Plan.JunctionsIn(chunkX, chunkY))
            {
                SewerRoom room = sewers.Room(nodeId);
                if (room != null && ctx.Contains(room.Node.P))
                {
                    BuildSewerRoom(ctx, buffer, room);
                }
            }
        }

        // ---------------------------------------------------------------- looks

        /// <summary>
        /// What a tunnel's face looks like: old brick, wet stone or plain concrete by its style, darker in the channel, all of it marked as
        /// inside so the sky does not light it (SurfaceLook). The brick and the stone come in the colour of their neighbourhood
        /// (<paramref name="tone"/>, SewerPlan.ToneAt): old brick grey, blue, grey-yellow or green-black, never a house's red.
        /// </summary>
        private static Color32 SewerLook(SewerStyle style, SewerSurface surface, int salt, bool inside = true, int tone = 0)
        {
            float shade = 0.86f + 0.14f * LatticeHash(salt, (int)style, 43);
            bool channel = surface == SewerSurface.ChannelBed || surface == SewerSurface.ChannelSide;
            bool floor = surface == SewerSurface.Walkway;
            switch (style)
            {
                case SewerStyle.BrickVault:
                    if (channel) return SurfaceLook.Pack(new Color(0.34f, 0.36f, 0.31f) * shade, (int)WallMaterial.Concrete, 0.95f, inside);
                    if (floor) return SurfaceLook.Pack(new Color(0.56f, 0.55f, 0.52f) * shade, (int)WallMaterial.Stone, 0.8f, inside);
                    return SurfaceLook.Pack(new Color(0.92f, 0.92f, 0.92f) * shade, SurfaceLook.OldBrick + tone % SurfaceLook.OldBrickColours, 0.7f, inside);
                case SewerStyle.StoneArch:
                    if (channel) return SurfaceLook.Pack(new Color(0.36f, 0.40f, 0.35f) * shade, (int)WallMaterial.Stone, 0.95f, inside);
                    if (floor) return SurfaceLook.Pack(new Color(0.58f, 0.59f, 0.56f) * shade, SurfaceLook.Setts, 0.8f, inside);
                    return SurfaceLook.Pack(StoneTones[tone % StoneTones.Length] * shade, (int)WallMaterial.Stone, 0.8f, inside);
                case SewerStyle.RoundPipe:
                    if (channel) return SurfaceLook.Pack(new Color(0.38f, 0.40f, 0.37f) * shade, (int)WallMaterial.Concrete, 0.9f, inside);
                    return SurfaceLook.Pack(new Color(0.60f, 0.61f, 0.60f) * shade, (int)WallMaterial.Concrete, floor ? 0.6f : 0.75f, inside);
                default:
                    if (channel) return SurfaceLook.Pack(new Color(0.42f, 0.43f, 0.41f) * shade, (int)WallMaterial.Concrete, 0.85f, inside);
                    return SurfaceLook.Pack(new Color(0.72f, 0.72f, 0.70f) * shade, (int)WallMaterial.Concrete, floor ? 0.45f : 0.6f, inside);
            }
        }

        /// <summary>The stone arches' tints over the stone's own warm grey: cool grey, green-grey, blue-grey, dark.</summary>
        private static readonly Color[] StoneTones =
        {
            new Color(0.56f, 0.60f, 0.64f), new Color(0.50f, 0.58f, 0.52f), new Color(0.46f, 0.52f, 0.60f), new Color(0.40f, 0.42f, 0.44f),
        };

        /// <summary>Ladders, bars and lids: dark iron, rusting (under the ground, and out in the daylight).</summary>
        private static Color32 SewerIron(bool inside) => SurfaceLook.Pack(new Color(0.30f, 0.25f, 0.22f), (int)WallMaterial.Metal, 0.9f, inside);

        /// <summary>The paint that says "here": a ladder's top rungs, a door's frame, the ring round an open manhole (players walk past what does not stand out).</summary>
        private static Color32 SewerYellow(bool inside) => SurfaceLook.Pack(new Color(0.95f, 0.74f, 0.10f), (int)WallMaterial.Metal, 0.35f, inside);

        // ---------------------------------------------------------------- tunnels

        /// <summary>A point of a tunnel's ring: so far along it, so far across from its middle, so high over its walkways there.</summary>
        private static Vector3 TubePoint(Context ctx, SewerTube tube, double along, double floor, Vec2 section)
        {
            // Across is the right of the way the distances run.
            Vec2 p = tube.At(along) - tube.Dir.Perp * section.X;
            return ctx.Local(p.X, floor + section.Y, p.Y);
        }

        /// <summary>
        /// A tunnel: its section drawn from ring to ring, each piece by the chunk its middle is in. Walled where it ends with no room, and
        /// with a step up out of the channel where it opens into one.
        /// </summary>
        private static void BuildSewerTube(Context ctx, MeshBuffer buffer, SewerTube tube, int salt)
        {
            SewerSection section = tube.Section;
            int rings = tube.Stations.Length;
            var across = new Vector3((float)tube.Dir.Y, 0.0f, (float)-tube.Dir.X);
            for (int i = 0; i + 1 < rings; i++)
            {
                double s0 = tube.Stations[i], s1 = tube.Stations[i + 1];
                if (!ctx.Contains(tube.At((s0 + s1) * 0.5))) continue;
                double f0 = tube.Floors[i], f1 = tube.Floors[i + 1];
                float run = 0.0f;
                for (int k = 0; k < section.Outline.Length; k++)
                {
                    Vec2 p = section.Outline[k], q = section.Outline[(k + 1) % section.Outline.Length];
                    float width = (float)(q - p).Length;
                    Vector3 a = TubePoint(ctx, tube, s0, f0, p), b = TubePoint(ctx, tube, s1, f1, p);
                    Vector3 c = TubePoint(ctx, tube, s1, f1, q), d = TubePoint(ctx, tube, s0, f0, q);
                    // Into the tunnel: the outline goes round anticlockwise seen along the tunnel, so its inside is on the left of each face.
                    Vector3 inward = across * (float)-(q.Y - p.Y) + Vector3.up * (float)(q.X - p.X);
                    Vector3 normal = Vector3.Cross(b - a, d - a);
                    if (normal.sqrMagnitude > 1e-10f)
                    {
                        normal.Normalize();
                        if (Vector3.Dot(normal, inward) < 0.0f) normal = -normal;
                        buffer.Look = SewerLook(section.Style, section.Surfaces[k], salt, true, tube.Tone);
                        buffer.AddQuad(a, b, c, d, normal, new Vector2((float)s0, run), new Vector2((float)s1, run),
                            new Vector2((float)s1, run + width), new Vector2((float)s0, run + width));
                    }
                    run += width;
                }
                if (i == 0)
                {
                    SewerTubeEnd(ctx, buffer, tube, s0, f0, tube.WallAtStart, 1.0, salt);
                }
                if (i + 2 == rings)
                {
                    SewerTubeEnd(ctx, buffer, tube, s1, f1, tube.WallAtEnd, -1.0, salt);
                }
            }
        }

        /// <summary>
        /// The end of a tunnel, looking back into it (<paramref name="facing"/> is +1 at its start, -1 at its end): a wall across the whole
        /// section where it ends blind; where it opens, only the step up out of the channel.
        /// </summary>
        private static void SewerTubeEnd(Context ctx, MeshBuffer buffer, SewerTube tube, double along, double floor, bool wall, double facing, int salt)
        {
            SewerSection section = tube.Section;
            var normal = new Vector3((float)(tube.Dir.X * facing), 0.0f, (float)(tube.Dir.Y * facing));
            double half = section.ChannelHalfWidth, depth = section.ChannelDepth;
            buffer.Look = SewerLook(section.Style, SewerSurface.ChannelSide, salt, true, tube.Tone);
            buffer.AddQuad(TubePoint(ctx, tube, along, floor, new Vec2(-half, -depth)), TubePoint(ctx, tube, along, floor, new Vec2(half, -depth)),
                TubePoint(ctx, tube, along, floor, new Vec2(half, 0.0)), TubePoint(ctx, tube, along, floor, new Vec2(-half, 0.0)), normal,
                new Vector2((float)-half, (float)-depth), new Vector2((float)half, (float)-depth), new Vector2((float)half, 0.0f), new Vector2((float)-half, 0.0f));
            if (!wall) return;
            buffer.Look = SewerLook(section.Style, SewerSurface.Wall, salt, true, tube.Tone);
            Vec2[] opening = section.Opening;
            Vector3 hub = TubePoint(ctx, tube, along, floor, new Vec2(0.0, 0.0));
            for (int k = 0; k + 1 < opening.Length; k++)
            {
                buffer.AddTriangle(hub, TubePoint(ctx, tube, along, floor, opening[k]), TubePoint(ctx, tube, along, floor, opening[k + 1]), normal,
                    Vector2.zero, new Vector2((float)opening[k].X, (float)opening[k].Y), new Vector2((float)opening[k + 1].X, (float)opening[k + 1].Y));
            }
        }

        // ---------------------------------------------------------------- walls with a tunnel's mouth in them

        /// <summary>
        /// An upright wall from <paramref name="a"/> to <paramref name="b"/>, level along its foot and its top, seen from the side
        /// <paramref name="facing"/> points to, with a tunnel's mouth left open in its middle (none: a whole wall). Laid in the wall's own
        /// plane: so far along from its middle, so far up from its foot.
        /// </summary>
        private static void SewerWall(Context ctx, MeshBuffer buffer, Vec2 a, Vec2 b, double foot, double top, Vec2[] opening, Vector3 facing)
        {
            double half = (b - a).Length * 0.5;
            if (half < 0.01 || top - foot < 0.01) return;
            Vec2 middle = (a + b) * 0.5, dir = (b - a) / (half * 2.0);
            var whole = new List<Vec2> { new Vec2(-half, 0.0), new Vec2(half, 0.0), new Vec2(half, top - foot), new Vec2(-half, top - foot) };
            List<List<Vec2>> pieces = opening == null ? new List<List<Vec2>> { whole } : Subtract(whole, new List<Vec2[]> { opening });
            foreach (List<Vec2> piece in pieces)
            {
                Vec2 first = middle + dir * piece[0].X;
                Vector3 p0 = ctx.Local(first.X, foot + piece[0].Y, first.Y);
                for (int i = 1; i + 1 < piece.Count; i++)
                {
                    Vec2 pi = middle + dir * piece[i].X, pj = middle + dir * piece[i + 1].X;
                    buffer.AddTriangle(p0, ctx.Local(pi.X, foot + piece[i].Y, pi.Y), ctx.Local(pj.X, foot + piece[i + 1].Y, pj.Y), facing,
                        new Vector2((float)piece[0].X, (float)piece[0].Y), new Vector2((float)piece[i].X, (float)piece[i].Y),
                        new Vector2((float)piece[i + 1].X, (float)piece[i + 1].Y));
                }
            }
        }

        /// <summary>A level vector in the world from a plan direction.</summary>
        private static Vector3 Level(Vec2 v) => new Vector3((float)v.X, 0.0f, (float)v.Y);
    }
}
