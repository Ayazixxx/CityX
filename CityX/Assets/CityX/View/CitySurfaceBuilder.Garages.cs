using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The drives into the parking garages (GarageEntrances, owner 2026-10-16: "there was physically no way for the car to drive into it"): from
    /// the edge of the carriageway, level with the road, across the pavement to each opening at a garage ramp's grade. The pavement, the paving
    /// and the ground are cut away under each drive (and under the whole garage, whose lowest deck is dug into its hill); the drive is asphalt,
    /// with a concrete face down each side to the bank where the drive is cut into it, or up to the drive where it is built over a dip.
    /// </summary>
    public static partial class CitySurfaceBuilder
    {
        /// <summary>The cut under a drive reaches this far past the kerb line, so the kerb across it goes too.</summary>
        private const float DrivewayCutPastKerb = 0.3f;

        /// <summary>The faces down a drive's sides are drawn in pieces this long, metres.</summary>
        private const float DrivewaySideStep = 1.0f;

        private static readonly Color32 DrivewaySide = SurfaceLook.Pack(new Color(0.58f, 0.57f, 0.55f), (int)WallMaterial.Concrete, 0.6f);

        /// <summary>A garage drive to build in this chunk: the garage and the entrance it leads to.</summary>
        private readonly struct Driveway
        {
            public readonly BuildingSite Site;
            public readonly GarageEntrance Entrance;

            public Driveway(BuildingSite site, GarageEntrance entrance)
            {
                Site = site;
                Entrance = entrance;
            }
        }

        /// <summary>
        /// A garage reaching into this chunk: its footprint cut out of the ground (its lowest deck is dug into the hill), and each drive's ground
        /// too, out past the kerb; the drives to build.
        /// </summary>
        private static void AddGarageCuts(Context ctx, BuildingSite site, List<Vec2[]> cuts, List<Driveway> drives)
        {
            Vec2[] garage = Footprint(site);
            Bounds(garage, out double gx0, out double gz0, out double gx1, out double gz1);
            if (ctx.Rect.Overlaps(gx0, gz0, gx1, gz1)) cuts.Add(garage);
            foreach (GarageEntrance e in GarageEntrances.For(ctx.Plan, site))
            {
                if (!e.HasDriveway) continue;
                Vec2[] drive = GarageEntrances.Driveway(site, e, DrivewayCutPastKerb);
                Bounds(drive, out double dx0, out double dz0, out double dx1, out double dz1);
                if (!ctx.Rect.Overlaps(dx0, dz0, dx1, dz1)) continue;
                cuts.Add(drive);
                drives.Add(new Driveway(site, e));
            }
        }

        /// <summary>The mouth of a drive is laid in strips this wide across the opening where the road's edge is not level across it, metres.</summary>
        private const float DriveMouthStrip = 0.25f;

        /// <summary>
        /// A drive's surface as drawn at <paramref name="x"/> across the frontage (the building's frame), <paramref name="inFromFront"/> metres in
        /// from the front wall: out on the road just under the road's surface as drawn (<see cref="DrawnRoadAt"/>), sinking as it goes on; from the
        /// kerb in, the drive's own rise (GarageEntrances.DrawnHeight on the opening's middle) with the road's edge across its mouth easing off to
        /// nothing at the wall, where the deck is level across. (2026-10-16, seed 19077151: a mouth level across a street sloping along the frontage
        /// stood a step over the road at one side and the car stopped at it; and where a road's 5 m piece spans a crease in the hill, the road as
        /// drawn stands up to a metre off the ground under it, so the mouth meets the road as drawn, not the ground.)
        /// </summary>
        public static float DriveHeight(PlanWorld plan, BuildingSite site, in GarageEntrance e, float x, float inFromFront)
        {
            GarageEntrances.CarriagewayOut(plan, site, (e.X0 + e.X1) * 0.5f, out int road);
            return DriveHeight(plan, site, e, road, x, inFromFront);
        }

        private static float DriveHeight(PlanWorld plan, BuildingSite site, in GarageEntrance e, int road, float x, float inFromFront)
        {
            float start = -e.KerbOut;
            if (road < 0 || start >= -1e-3f) return GarageEntrances.DrawnHeight(e, inFromFront);
            float front = site.Params.Depth * 0.5f;
            if (inFromFront < start)
            {
                site.LocalToWorld(x, front - inFromFront, out double rx, out double rz);
                float sink = GarageEntrances.SinkIntoRoad * Mathf.Min(1.0f, (start - inFromFront) / GarageEntrances.IntoRoad);
                return (float)DrawnRoadAt(plan, road, new Vec2(rx, rz)) - GarageEntrances.UnderRoad - sink;
            }
            site.LocalToWorld(x, front + e.KerbOut, out double kx, out double kz);
            float mouth = (float)DrawnRoadAt(plan, road, new Vec2(kx, kz)) - GarageEntrances.UnderRoad;
            float w = Mathf.Clamp01(inFromFront / start);
            return GarageEntrances.DrawnHeight(e, inFromFront) + (mouth - (e.RoadY - GarageEntrances.UnderRoad)) * w;
        }

        /// <summary>
        /// The road's surface as BuildRoad and BuildPlate draw it at a point of a road (the ribbon of <paramref name="edgeId"/>, or the junction
        /// plate at its trimmed end): the ribbon's PieceLength piece the point is in, clipped to the point's chunk and fanned on the road's height at
        /// its corners exactly as BuildRoad lays it, or the plate's triangle as BuildPlate fans it. A point off the ribbon is taken onto its edge.
        /// </summary>
        private static double DrawnRoadAt(PlanWorld plan, int edgeId, Vec2 p)
        {
            EdgeGeometry g = plan.Geometry.Edges[edgeId];
            double size = PlanWorld.ChunkSize;
            int cx = PlanWorld.ChunkOf(p.X), cy = PlanWorld.ChunkOf(p.Y);
            var rect = new Rect2(cx * size, cy * size, (cx + 1) * size, (cy + 1) * size);
            Func<double, double, double> height = RoadHeight(plan, g);
            if (PedestrianStreets.Is(plan, g.Edge))
            {
                Func<double, double, double> road = height;
                height = (x, z) => road(x, z) + KerbHeight;
            }
            double t0 = g.TrimA, t1 = g.Length - g.TrimB;
            double t = Vec2.Dot(p - g.A, g.Dir);
            if (t < t0 || t > t1)
            {
                JunctionGeometry j = plan.Geometry.Junctions[(t < t0 ? g.Edge.A : g.Edge.B).Id];
                if (j != null && j.Plate != null)
                {
                    Func<double, double, double> plate = (x, z) => plan.GroundHeight(x, z) + RoadLift;
                    for (int i = 0; i < j.Plate.Length; i++)
                    {
                        var tri = new List<Vec2> { j.Node.P, j.Plate[i], j.Plate[(i + 1) % j.Plate.Length] };
                        if (OnFan(Clip(tri, rect), p, plate, out double y)) return y;
                    }
                }
                t = t < t0 ? t0 : t1;   // not on the plate: the ribbon's end
            }
            double across = Vec2.Dot(p - g.A, g.Dir.Perp), half = g.HalfWidth - 1e-3;
            Vec2 q = g.A + g.Dir * t + g.Dir.Perp * (across < -half ? -half : (across > half ? half : across));
            int pieces = Math.Max(1, (int)Math.Ceiling((t1 - t0) / PieceLength));
            int k = Math.Min(pieces - 1, Math.Max(0, (int)((t - t0) / (t1 - t0) * pieces)));
            Vec2 perp = g.Dir.Perp * g.HalfWidth;
            Vec2 ca = g.A + g.Dir * (t0 + (t1 - t0) * k / pieces), cb = g.A + g.Dir * (t0 + (t1 - t0) * (k + 1) / pieces);
            var quad = new List<Vec2> { ca + perp, cb + perp, cb - perp, ca - perp };
            return OnFan(Clip(quad, rect), q, height, out double h) ? h : height(q.X, q.Y);
        }

        /// <summary>Whether a point lies on a convex polygon fanned from its first corner (as Fan draws it), and the height there.</summary>
        private static bool OnFan(List<Vec2> poly, Vec2 p, Func<double, double, double> height, out double y)
        {
            y = 0.0;
            for (int i = 1; i + 1 < poly.Count; i++)
            {
                Vec2 a = poly[0], b = poly[i], c = poly[i + 1];
                double d = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
                if (Math.Abs(d) < 1e-12) continue;
                double wa = ((b.Y - c.Y) * (p.X - c.X) + (c.X - b.X) * (p.Y - c.Y)) / d;
                double wb = ((c.Y - a.Y) * (p.X - c.X) + (a.X - c.X) * (p.Y - c.Y)) / d;
                double wc = 1.0 - wa - wb;
                if (wa < -1e-7 || wb < -1e-7 || wc < -1e-7) continue;
                y = wa * height(a.X, a.Y) + wb * height(b.X, b.Y) + wc * height(c.X, c.Y);
                return true;
            }
            return false;
        }

        /// <summary>Each drive's surface (clipped to the chunk) and the faces down its sides (each piece in the chunk its middle is in).</summary>
        private static void BuildDriveways(Context ctx, List<Driveway> drives, MeshBuffer pavement)
        {
            foreach (Driveway d in drives)
            {
                BuildingSite site = d.Site;
                GarageEntrance e = d.Entrance;
                float front = site.Params.Depth * 0.5f;
                GarageEntrances.CarriagewayOut(ctx.Plan, site, (e.X0 + e.X1) * 0.5f, out int road);
                double Height(double x, double z)
                {
                    site.WorldToLocal(x, z, out double lx, out double lz);
                    return DriveHeight(ctx.Plan, site, e, road, (float)lx, front - (float)lz);
                }

                // The surface: tucked under the road's edge past the kerb, the sloped piece from the kerb, then the level piece to the wall (the ramp
                // may end out here, or run on inside). Where the road's edge is not level across the mouth (a street sloping along the frontage, a
                // crease in it) the drive untwists from it to level at the wall, so each piece is laid in strips across and along it.
                pavement.Look = AsphaltLook(RoadClass.Street, site.Id);
                float start = -e.KerbOut;
                float mid = (e.X0 + e.X1) * 0.5f, mouthMid = DriveHeight(ctx.Plan, site, e, road, mid, start);
                int stripsAcross = 1;
                for (float x = e.X0; x <= e.X1 + 1e-3f; x += DriveMouthStrip)
                {
                    if (Mathf.Abs(DriveHeight(ctx.Plan, site, e, road, x, start) - mouthMid) > 0.02f)
                    {
                        stripsAcross = Mathf.CeilToInt((e.X1 - e.X0) / DriveMouthStrip);
                        break;
                    }
                }
                void Strips(float near, float far)
                {
                    int along = stripsAcross > 1 ? Mathf.Max(1, Mathf.CeilToInt((far - near) / 1.5f)) : 1;
                    for (int i = 0; i < stripsAcross; i++)
                    {
                        float xa = Mathf.Lerp(e.X0, e.X1, i / (float)stripsAcross), xb = Mathf.Lerp(e.X0, e.X1, (i + 1) / (float)stripsAcross);
                        for (int j = 0; j < along; j++)
                        {
                            float za = Mathf.Lerp(near, far, j / (float)along), zb = Mathf.Lerp(near, far, (j + 1) / (float)along);
                            Fan(ctx, pavement, Clip(GarageEntrances.Rectangle(site, xa, xb, za, zb), ctx.Rect), Height);
                        }
                    }
                }
                Strips(front - start, front - start + GarageEntrances.IntoRoad);
                float slopeEnd = Mathf.Min(0.0f, e.RampEnd);
                if (slopeEnd > start + 0.01f)
                {
                    Strips(front - slopeEnd, front - start);
                }
                if (slopeEnd < -0.01f)
                {
                    float levelFrom = Mathf.Max(start, slopeEnd);
                    Strips(front, front - levelFrom);
                }

                // Down each side, between the drive and the bank beside it (the pavement and paving, PavingRise over the ground), both ways round.
                pavement.Look = DrivewaySide;
                int pieces = Mathf.Max(1, Mathf.CeilToInt(e.KerbOut / DrivewaySideStep));
                foreach (float x in new[] { e.X0, e.X1 })
                {
                    for (int i = 0; i < pieces; i++)
                    {
                        float s0 = start + e.KerbOut * i / pieces, s1 = start + e.KerbOut * (i + 1) / pieces;
                        site.LocalToWorld(x, front - s0, out double ax, out double az);
                        site.LocalToWorld(x, front - s1, out double bx, out double bz);
                        double mx = (ax + bx) * 0.5, mz = (az + bz) * 0.5;
                        if (mx < ctx.Rect.X0 || mx >= ctx.Rect.X1 || mz < ctx.Rect.Z0 || mz >= ctx.Rect.Z1) continue;
                        float driveA = DriveHeight(ctx.Plan, site, e, road, x, s0), driveB = DriveHeight(ctx.Plan, site, e, road, x, s1);
                        float bankA = (float)(ctx.Plan.GroundHeight(ax, az) + PlanBuildings.PavingRise);
                        float bankB = (float)(ctx.Plan.GroundHeight(bx, bz) + PlanBuildings.PavingRise);
                        if (Mathf.Abs(bankA - driveA) < 0.02f && Mathf.Abs(bankB - driveB) < 0.02f) continue;
                        Vector3 a0 = ctx.Local(ax, driveA, az), b0 = ctx.Local(bx, driveB, bz);
                        Vector3 a1 = ctx.Local(ax, bankA, az), b1 = ctx.Local(bx, bankB, bz);
                        Vector3 across = Vector3.Cross(b0 - a0, Vector3.up).normalized;
                        float len = Vector3.Distance(a0, b0);
                        pavement.AddQuad(a0, b0, b1, a1, across, new Vector2(0, driveA), new Vector2(len, driveB), new Vector2(len, bankB), new Vector2(0, bankA));
                        pavement.AddQuad(a0, b0, b1, a1, -across, new Vector2(0, driveA), new Vector2(len, driveB), new Vector2(len, bankB), new Vector2(0, bankA));
                    }
                }
            }
        }
    }
}
