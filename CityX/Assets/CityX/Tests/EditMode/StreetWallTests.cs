using System.Collections.Generic;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Downtown is a street wall (PlanLot.StreetWall): buildings stand shoulder to shoulder with blank party walls between them, not as detached
    /// blocks with gaps. And the relief that dresses every facade (FacadeOrnament) stays on its own wall.
    /// </summary>
    public sealed class StreetWallTests
    {
        private const int Seed = 58203917;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        [Test]
        public void DowntownBuildingsStandShoulderToShoulder()
        {
            int dense = 0, touching = 0;
            foreach (BuildingSite site in World.Plan.Buildings)
            {
                if (site.OnRoof || site.Sides >= 3 || World.Plan.Data.Fields.Population(new Vec2(site.CentreX, site.CentreZ)) < 0.65)
                {
                    continue;
                }
                dense++;
                if (site.Contacts != null && site.Contacts.Length > 0)
                {
                    touching++;
                }
            }
            Assert.Greater(dense, 300, "need a real downtown");
            Assert.Greater(touching, dense * 0.6, $"only {touching} of {dense} downtown buildings share a wall with a neighbour: the street is still a row of detached blocks");
        }

        [Test]
        public void EveryBlankWallHasSomethingStandingAgainstIt()
        {
            // A contact blanks the windows of a stretch of wall; just outside that stretch there must be another block (of the same building or a
            // neighbour), or a wall was blanked for nothing -- and a contact on the wrong wall, or measured from the wrong end, shows up here.
            BuildingSite[] all = World.Plan.Buildings;
            int checkedContacts = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 18))
            {
                if (site.Contacts == null || site.OnRoof || site.Sides >= 3)
                {
                    continue;
                }
                foreach (WallContact c in site.Contacts)
                {
                    float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
                    float u = (c.U0 + c.U1) * 0.5f;
                    float probe = (float)PlanBuildings.PartyWallReach + 0.1f;
                    float lx, lz;
                    switch (c.Wall)
                    {
                        case WindowGrid.Wall.North: lx = u - hw; lz = hd + probe; break;
                        case WindowGrid.Wall.South: lx = u - hw; lz = -hd - probe; break;
                        case WindowGrid.Wall.East: lx = hw + probe; lz = u - hd; break;
                        default: lx = -hw - probe; lz = u - hd; break;
                    }
                    site.LocalToWorld(lx, lz, out double wx, out double wz);
                    bool found = false;
                    foreach (BuildingSite other in all)
                    {
                        if (other.Id == site.Id || other.OnRoof || System.Math.Abs(other.CentreX - wx) > 200.0 || System.Math.Abs(other.CentreZ - wz) > 200.0)
                        {
                            continue;
                        }
                        other.WorldToLocal(wx, wz, out double ox, out double oz);
                        if (System.Math.Abs(ox) <= other.Params.Width * 0.5 + 0.05 && System.Math.Abs(oz) <= other.Params.Depth * 0.5 + 0.05)
                        {
                            found = true;
                            break;
                        }
                    }
                    Assert.IsTrue(found, $"building {site.Id}: its {c.Wall} wall is blank from {c.U0:0.0} to {c.U1:0.0} m, but nothing stands against it there");
                    checkedContacts++;
                }
            }
            Assert.Greater(checkedContacts, 50, "need a real sample");
        }

        [Test]
        public void NoWindowLooksIntoTheNeighbour()
        {
            int checkedCells = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 12))   // 8 chunks held only 26 walls against a neighbour on seed 74102958
            {
                if (site.Contacts == null || site.OnRoof || site.Sides >= 3)
                {
                    continue;
                }
                WindowPlan plan = WindowPlan.For(World, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                foreach (WallContact c in site.Contacts)
                {
                    bool northSouth = c.Wall == WindowGrid.Wall.North || c.Wall == WindowGrid.Wall.South;
                    CellLayout columns = plan.ColumnsFor(northSouth ? site.Params.Width : site.Params.Depth);
                    int column = Mathf.Clamp(Mathf.FloorToInt((c.U0 + c.U1) * 0.5f / columns.Size), 0, columns.Count - 1);
                    if (c.Top < rows.Size * 1.5f)
                    {
                        continue;
                    }
                    Assert.IsTrue(plan.IsAgainstAnotherBlock(c.Wall, column, 0),
                        $"building {site.Id}: the window in column {column} of its {c.Wall} wall opens onto the wall of the building next door");
                    checkedCells++;
                }
            }
            Assert.Greater(checkedCells, 30, "need a real sample");
        }

        [Test]
        public void EveryBuildingFitsInsideItsLot()
        {
            // The rectangle a building is built in lies inside the lot's footprint: downtown, where the neighbour stands right at the lot line, a
            // building poking past it would stand inside the next one.
            int fitted = 0;
            foreach (PlanLot lot in World.Plan.Data.Lots)
            {
                double margin = lot.StreetWall ? PlanBuildings.StreetWallFitMargin : PlanBuildings.FitMargin;
                if (!PlanBuildings.FitRectangle(lot.Footprint, lot.FrontEdge, out Vec2 c, out Vec2 ex, out double width, out double depth, lot.Scale, margin))
                {
                    continue;
                }
                var ez = new Vec2(-ex.Y, ex.X);
                for (int k = 0; k < 4; k++)
                {
                    Vec2 p = c + ex * (((k & 1) == 0 ? -0.5 : 0.5) * width) + ez * (((k & 2) == 0 ? -0.5 : 0.5) * depth);
                    Assert.Less(OutsideBy(lot.Footprint, p), 0.02, $"lot {lot.Id}: its building's corner {k} stands outside the lot");
                }
                fitted++;
            }
            Assert.Greater(fitted, 1000, "need a real sample");
        }

        [Test]
        public void ADowntownLotLeavesNoGapToItsNeighbours()
        {
            int streetWall = 0;
            foreach (PlanLot lot in World.Plan.Data.Lots)
            {
                if (lot.StreetWall)
                {
                    streetWall++;
                    Assert.Greater(lot.Population, LotSubdivider.StreetWallPopulation - 0.1, $"lot {lot.Id} is a street wall out in the suburbs");
                }
            }
            Assert.Greater(streetWall, 500, "downtown has no street walls");
        }

        [Test]
        public void TheReliefStaysOnItsOwnWall()
        {
            // Round the roof line, nothing projects further than the deepest cornice (lower down, shop signs, awnings and balconies rightly reach
            // further); and on a party wall nothing projects at all past the joint, at any height.
            int checkedBuildings = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 6))
            {
                if (site.OnRoof || site.Sides >= 3 || site.Shell || site.Params.Height < 12.0f)
                {
                    continue;
                }
                var structures = new MeshBuffer();
                var origin = new Vector3(site.OwnerChunkX * CityLayout.ChunkSize, 0, site.OwnerChunkY * CityLayout.ChunkSize);
                CityMeshBuilder.BuildSite(World, site, origin, structures, new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(),
                    null, null, new MeshBuffer(), false);
                float hw = site.Params.Width * 0.5f, hd = site.Params.Depth * 0.5f;
                ApartmentFront front = ApartmentFront.For(site, WindowPlan.For(World, site));
                foreach (Vector3 v in structures.Vertices)
                {
                    if (v.y < site.BaseHeight + 3.0f || v.y > site.Top + 2.5f)
                    {
                        continue;
                    }
                    site.WorldToLocal(origin.x + v.x, origin.z + v.z, out double lx, out double lz);
                    double outX = System.Math.Abs(lx) - hw, outZ = System.Math.Abs(lz) - hd;
                    double out_ = System.Math.Max(outX, outZ);
                    // (An apartment block's balcony band (ApartmentFront) ends in an eave over its top floor, as far out as the balconies below.)
                    if (v.y > site.Top - 1.0f && !(outZ > outX && front.Covers(lz > 0 ? WindowGrid.Wall.North : WindowGrid.Wall.South)))
                    {
                        Assert.Less(out_, FacadeOrnament.MaxCorniceDepth + 0.05f,
                            $"building {site.Id}: something projects {out_:0.00} m from its wall at the roof line ({v.y - site.Top:0.0} m from the top)");
                    }
                    // Into a joint, anything deeper than half of it (a roof coping's few centimetres are not) would meet the neighbour's.
                    if (out_ > PlanBuildings.StreetWallFitMargin)
                    {
                        // Which wall it stands off, and where along it.
                        WindowGrid.Wall wall = outX >= outZ ? (lx > 0 ? WindowGrid.Wall.East : WindowGrid.Wall.West) : (lz > 0 ? WindowGrid.Wall.North : WindowGrid.Wall.South);
                        float along = wall == WindowGrid.Wall.East || wall == WindowGrid.Wall.West ? (float)lz + hd : (float)lx + hw;
                        float up = v.y - site.BaseHeight;
                        // Strictly inside a stretch something stands against (a band may run right up to where the neighbour starts).
                        bool inJoint = false;
                        foreach (WallContact c in site.Contacts ?? new WallContact[0])
                        {
                            inJoint |= c.Wall == wall && along > c.U0 + 0.05f && along < c.U1 - 0.05f && c.Top > up + 0.5f;
                        }
                        Assert.IsFalse(inJoint, $"building {site.Id}: relief projects {out_:0.00} m into the party wall on its {wall} side, {along:0.00} m along, {up:0.0} m up");
                    }
                }
                checkedBuildings++;
            }
            Assert.Greater(checkedBuildings, 20, "need a real sample");
        }

        [Test]
        public void MostOfTheCityWearsSomeRelief()
        {
            int sites = 0, cornices = 0, bases = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(World, 20))
            {
                if (site.OnRoof || site.Shell)
                {
                    continue;
                }
                WindowPlan plan = WindowPlan.For(World, site);
                FacadeOrnament o = FacadeOrnament.For(site, plan.Style, BuildingLook.For(site, plan.Style).Wall);
                Assert.AreEqual(o.HasCornice, FacadeOrnament.For(site, plan.Style, BuildingLook.For(site, plan.Style).Wall).HasCornice, "the ornament is rolled, not random");
                sites++;
                if (o.HasCornice) cornices++;
                if (o.BaseCourse) bases++;
            }
            Assert.Greater(sites, 100, "need a real sample");
            Assert.Greater(cornices, sites * 0.35, $"only {cornices} of {sites} buildings have a cornice: the skyline is flat boxes");
            Assert.Greater(bases, sites * 0.35, $"only {bases} of {sites} buildings set their street storey apart");
        }

        /// <summary>How far outside a polygon a point is (0 inside).</summary>
        private static double OutsideBy(Vec2[] poly, Vec2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                {
                    inside = !inside;
                }
            }
            if (inside)
            {
                return 0.0;
            }
            double best = double.MaxValue;
            for (int i = 0; i < poly.Length; i++)
            {
                Vec2 a = poly[i], d = poly[(i + 1) % poly.Length] - a;
                double t = System.Math.Max(0.0, System.Math.Min(1.0, Vec2.Dot(p - a, d) / Vec2.Dot(d, d)));
                best = System.Math.Min(best, (a + d * t - p).Length);
            }
            return best;
        }
    }
}
