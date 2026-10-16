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

        /// <summary>Each drive's surface (clipped to the chunk) and the faces down its sides (each piece in the chunk its middle is in).</summary>
        private static void BuildDriveways(Context ctx, List<Driveway> drives, MeshBuffer pavement)
        {
            foreach (Driveway d in drives)
            {
                BuildingSite site = d.Site;
                GarageEntrance e = d.Entrance;
                float front = site.Params.Depth * 0.5f;
                double Height(double x, double z)
                {
                    site.WorldToLocal(x, z, out _, out double lz);
                    return GarageEntrances.DrawnHeight(e, front - (float)lz);
                }

                // The surface, in pieces each flat (GarageEntrances.DrawnHeight is straight between their ends): tucked under the road's edge past the
                // kerb, the sloped piece from the kerb, then the level piece to the wall (the ramp may end out here, or run on inside).
                pavement.Look = AsphaltLook(RoadClass.Street, site.Id);
                float start = -e.KerbOut;
                Fan(ctx, pavement, Clip(GarageEntrances.Rectangle(site, e.X0, e.X1, front - start, front - start + GarageEntrances.IntoRoad), ctx.Rect), Height);
                float slopeEnd = Mathf.Min(0.0f, e.RampEnd);
                if (slopeEnd > start + 0.01f)
                {
                    Fan(ctx, pavement, Clip(GarageEntrances.Rectangle(site, e.X0, e.X1, front - slopeEnd, front - start), ctx.Rect), Height);
                }
                if (slopeEnd < -0.01f)
                {
                    float levelFrom = Mathf.Max(start, slopeEnd);
                    Fan(ctx, pavement, Clip(GarageEntrances.Rectangle(site, e.X0, e.X1, front, front - levelFrom), ctx.Rect), Height);
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
                        float driveA = GarageEntrances.DrawnHeight(e, s0), driveB = GarageEntrances.DrawnHeight(e, s1);
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
