using System;
using System.Collections.Generic;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A building wears one whole facade, and the city has all the kinds a real one does: glass towers, ribbon
    /// bands, punched windows, irregular openings, clerestory sheds and blank walls -- with party walls and
    /// shopfronts on top. These check that the mix exists, that it follows where a building stands and how
    /// big it is, and that a glass tower really is glass all the way round.
    /// </summary>
    public sealed class FacadeVarietyTests
    {
        private const int Seed = 20260810;

        private static IEnumerable<(BuildingSite site, WindowPlan plan, ZoneKind zone)> Sample(int radius)
        {
            var world = new CityWorld(Seed);
            // "radius" was once a half-width in 24 m chunks of the old grid; it is now how many chunks around the spawn to look.
            foreach (BuildingSite site in TestSites.NearSpawn(world, radius * 2))
            {
                yield return (site, WindowPlan.For(world, site), site.Zone);
            }
        }

        private static BuildingParams Params(BuildingMass mass, float height = 60.0f, string special = "")
        {
            return new BuildingParams(true, 20.0f, height, 20.0f, 0.0f, 0.0f, "", 0, special, false, mass);
        }

        private static double GlazedShare(WindowPlan plan, float height, float width, float depth)
        {
            CellLayout rows = WindowGrid.RowsFor(height);
            int glazed = 0;
            int total = 0;
            foreach ((WindowGrid.Wall wall, float span) in new[]
                     {
                         (WindowGrid.Wall.West, depth), (WindowGrid.Wall.East, depth),
                         (WindowGrid.Wall.South, width), (WindowGrid.Wall.North, width),
                     })
            {
                CellLayout columns = plan.ColumnsFor(span);
                for (int row = 0; row < rows.Count; row++)
                {
                    for (int column = 0; column < columns.Count; column++)
                    {
                        total++;
                        if (plan.IsGlazed(wall, column, row))
                        {
                            glazed++;
                        }
                    }
                }
            }
            return glazed / (double)total;
        }

        [Test]
        public void TheCityContainsEveryKindOfFacade()
        {
            var counts = new int[Enum.GetValues(typeof(FacadeStyle)).Length];
            foreach ((BuildingSite site, WindowPlan plan, _) in Sample(60))
            {
                counts[(int)plan.Style]++;
            }

            foreach (FacadeStyle style in Enum.GetValues(typeof(FacadeStyle)))
            {
                Assert.Greater(counts[(int)style], 0, $"no {style} building within 40 chunks of the origin");
            }
        }

        [Test]
        public void NoStyleTakesOverTheCity()
        {
            var counts = new int[Enum.GetValues(typeof(FacadeStyle)).Length];
            int total = 0;
            foreach ((BuildingSite site, WindowPlan plan, _) in Sample(40))
            {
                counts[(int)plan.Style]++;
                total++;
            }

            Assert.Greater(total, 60, "too few buildings sampled to mean anything");
            foreach (FacadeStyle style in Enum.GetValues(typeof(FacadeStyle)))
            {
                Assert.Less(counts[(int)style] / (double)total, 0.6, $"{style} is most of the city");
            }
        }

        [Test]
        public void TheSamePlaceGivesTheSamePlan()
        {
            WindowPlan a = WindowGrid.PlanFor(12345, ZoneKind.Urban, Params(BuildingMass.MidRise));
            WindowPlan b = WindowGrid.PlanFor(12345, ZoneKind.Urban, Params(BuildingMass.MidRise));

            Assert.AreEqual(a.Style, b.Style);
            for (int row = 0; row < 12; row++)
            {
                for (int column = 0; column < 12; column++)
                {
                    foreach (WindowGrid.Wall wall in Enum.GetValues(typeof(WindowGrid.Wall)))
                    {
                        Assert.AreEqual(a.IsGlazed(wall, column, row), b.IsGlazed(wall, column, row));
                    }
                }
            }
        }

        [Test]
        public void ACurtainWallIsGlassOnEveryCellOfEveryWall()
        {
            int checkedBuildings = 0;
            for (int seed = 1; seed < 4000 && checkedBuildings < 20; seed++)
            {
                WindowPlan plan = WindowGrid.PlanFor(seed, ZoneKind.Core, Params(BuildingMass.Tower, 150.0f));
                if (plan.Style != FacadeStyle.Curtain)
                {
                    continue;
                }
                checkedBuildings++;
                Assert.AreEqual(1.0, GlazedShare(plan, 150.0f, 40.0f, 40.0f), 1e-9,
                    $"seed {seed}: a curtain wall has a solid cell");
            }
            Assert.AreEqual(20, checkedBuildings, "no curtain-wall towers turned up in a downtown");
        }

        [Test]
        public void TowersAreMostlyGlassAndSheds_NeverAre()
        {
            int towerCurtain = 0;
            int shedCurtain = 0;
            const int N = 600;
            for (int seed = 0; seed < N; seed++)
            {
                if (WindowGrid.PlanFor(seed, ZoneKind.Core, Params(BuildingMass.Tower, 150.0f)).Style == FacadeStyle.Curtain)
                {
                    towerCurtain++;
                }
                if (WindowGrid.PlanFor(seed, ZoneKind.Industrial, Params(BuildingMass.Warehouse, 12.0f)).Style == FacadeStyle.Curtain)
                {
                    shedCurtain++;
                }
            }

            Assert.Greater(towerCurtain / (double)N, 0.5, "downtown towers should mostly be glass");
            Assert.AreEqual(0, shedCurtain, "a warehouse is never a glass curtain wall");
        }

        [Test]
        public void DowntownIsGlassierThanTheOldQuarter()
        {
            int core = 0;
            int old = 0;
            const int N = 800;
            for (int seed = 0; seed < N; seed++)
            {
                if (WindowGrid.PlanFor(seed, ZoneKind.Core, Params(BuildingMass.MidRise)).Style == FacadeStyle.Curtain)
                {
                    core++;
                }
                if (WindowGrid.PlanFor(seed, ZoneKind.OldTown, Params(BuildingMass.MidRise)).Style == FacadeStyle.Curtain)
                {
                    old++;
                }
            }
            Assert.Greater(core, old * 3, $"downtown curtain {core} vs old town {old}");
        }

        [Test]
        public void IndustrialShedsAreMostlyBlankOrHighBand()
        {
            int sparse = 0;
            const int N = 400;
            for (int seed = 0; seed < N; seed++)
            {
                FacadeStyle style = WindowGrid.PlanFor(seed, ZoneKind.Industrial, Params(BuildingMass.Warehouse, 12.0f)).Style;
                if (style == FacadeStyle.HighBand || style == FacadeStyle.Blank)
                {
                    sparse++;
                }
            }
            Assert.Greater(sparse / (double)N, 0.8);
        }

        [Test]
        public void TheCityAsAWholeHasLessGlassThanTheOldEveryBuildingSpeckle()
        {
            double sum = 0.0;
            int count = 0;
            foreach ((BuildingSite site, WindowPlan plan, _) in Sample(40))
            {
                sum += GlassArea(plan, site.Params.Height, site.Params.Width, site.Params.Depth);
                count++;
            }
            double mean = sum / count;

            // Every building used to be about 27% glass by area. The mix now runs from blank sheds to full
            // glass towers and should average lower -- which is also fewer panes to draw.
            Assert.Less(mean, 0.28, $"mean glass area share {mean:0.000}");
            Assert.Greater(mean, 0.12, $"mean glass area share {mean:0.000}");
        }

        /// <summary>
        /// The number that matters for frame rate: every pane is geometry, a collider triangle and a
        /// transparent draw. A glass tower has thousands, so glass towers use wide units (see
        /// WindowPlan.ColumnMetres) and the whole city has to hang no more panes than the old
        /// every-building-42% scatter did.
        /// </summary>
        [Test]
        public void TheCityHangsNoMorePanesThanTheOldSpeckleDid()
        {
            double panes = 0.0;
            double oldPanes = 0.0;
            foreach ((BuildingSite site, WindowPlan plan, _) in Sample(40))
            {
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                foreach ((WindowGrid.Wall wall, float span) in new[]
                         {
                             (WindowGrid.Wall.West, site.Params.Depth), (WindowGrid.Wall.East, site.Params.Depth),
                             (WindowGrid.Wall.South, site.Params.Width), (WindowGrid.Wall.North, site.Params.Width),
                         })
                {
                    CellLayout columns = plan.ColumnsFor(span);
                    for (int row = 0; row < rows.Count; row++)
                    {
                        for (int column = 0; column < columns.Count; column++)
                        {
                            if (plan.IsGlazed(wall, column, row))
                            {
                                panes++;
                            }
                        }
                    }
                    oldPanes += rows.Count * WindowGrid.ColumnsFor(span).Count * 0.42;
                }
            }
            Assert.LessOrEqual(panes, oldPanes * 1.0, $"{panes:0} panes vs {oldPanes:0} before ({panes / oldPanes:0.00}x)");
        }

        /// <summary>The fraction of wall AREA that is glass: glazed cells weighted by how much of their row the window fills.</summary>
        private static double GlassArea(WindowPlan plan, float height, float width, float depth)
        {
            CellLayout rows = WindowGrid.RowsFor(height);
            double area = 0.0;
            double total = 0.0;
            foreach ((WindowGrid.Wall wall, float span) in new[]
                     {
                         (WindowGrid.Wall.West, depth), (WindowGrid.Wall.East, depth),
                         (WindowGrid.Wall.South, width), (WindowGrid.Wall.North, width),
                     })
            {
                CellLayout columns = plan.ColumnsFor(span);
                for (int row = 0; row < rows.Count; row++)
                {
                    for (int column = 0; column < columns.Count; column++)
                    {
                        total += columns.Size * rows.Size;
                        if (plan.IsGlazed(wall, column, row))
                        {
                            plan.Insets(false, out float bottom, out float top);
                            area += (1.0 - bottom - top) * (1.0 - 2.0 * WindowGrid.PaneInset) * columns.Size * rows.Size;
                        }
                    }
                }
            }
            return area / total;
        }

        [Test]
        public void GlazingVariesWidelyFromBuildingToBuilding()
        {
            double min = 1.0;
            double max = 0.0;
            foreach ((BuildingSite site, WindowPlan plan, _) in Sample(40))
            {
                double share = GlazedShare(plan, site.Params.Height, site.Params.Width, site.Params.Depth);
                min = Math.Min(min, share);
                max = Math.Max(max, share);
            }
            Assert.Less(min, 0.10, "no nearly blank building");
            Assert.Greater(max, 0.90, "no fully glazed building");
        }

        [Test]
        public void APartyWallHasNoWindowsAndTheDoorWallIsNeverOne()
        {
            int partyWalls = 0;
            for (int seed = 0; seed < 600; seed++)
            {
                WindowPlan plan = WindowGrid.PlanFor(seed, ZoneKind.OldTown, Params(BuildingMass.MidRise, 40.0f));
                Assert.IsFalse(plan.IsBlankWall(WindowGrid.Wall.North), "the door wall must never be a party wall");

                foreach (WindowGrid.Wall wall in new[] { WindowGrid.Wall.West, WindowGrid.Wall.East, WindowGrid.Wall.South })
                {
                    if (!plan.IsBlankWall(wall))
                    {
                        continue;
                    }
                    partyWalls++;
                    for (int row = 0; row < 12; row++)
                    {
                        for (int column = 0; column < 12; column++)
                        {
                            Assert.IsFalse(plan.IsGlazed(wall, column, row), $"seed {seed}: window on a party wall");
                        }
                    }
                }
            }
            Assert.Greater(partyWalls, 30, "the old quarter has no party walls at all");
        }

        [Test]
        public void GlassFacesAndShedsNeverGetPartyWalls()
        {
            for (int seed = 0; seed < 600; seed++)
            {
                foreach (WindowPlan plan in new[]
                         {
                             WindowGrid.PlanFor(seed, ZoneKind.Core, Params(BuildingMass.Supertall, 280.0f)),
                             WindowGrid.PlanFor(seed, ZoneKind.Industrial, Params(BuildingMass.Warehouse, 12.0f)),
                         })
                {
                    foreach (WindowGrid.Wall wall in Enum.GetValues(typeof(WindowGrid.Wall)))
                    {
                        Assert.IsFalse(plan.IsBlankWall(wall), $"seed {seed} {plan.Style}: {wall} is a party wall");
                    }
                }
            }
        }

        [Test]
        public void AShopfrontGlazesTheWholeGroundFloorOfTheDoorWall()
        {
            int shops = 0;
            for (int seed = 0; seed < 600; seed++)
            {
                WindowPlan plan = WindowGrid.PlanFor(seed, ZoneKind.Urban, Params(BuildingMass.MidRise, 40.0f));
                if (!plan.Storefront)
                {
                    continue;
                }
                shops++;
                for (int column = 0; column < 12; column++)
                {
                    Assert.IsTrue(plan.IsGlazed(WindowGrid.Wall.North, column, 0), $"seed {seed}: shopfront cell {column} is solid");
                }
            }
            Assert.Greater(shops, 100, "an urban street has too few shopfronts");
        }

        [Test]
        public void AGlassOfficeIsLitAStoreyAtATime()
        {
            WindowPlan plan = default;
            for (int seed = 1; seed < 4000; seed++)
            {
                plan = WindowGrid.PlanFor(seed, ZoneKind.Core, Params(BuildingMass.Tower, 150.0f));
                if (plan.Style == FacadeStyle.Curtain)
                {
                    break;
                }
            }
            Assert.AreEqual(FacadeStyle.Curtain, plan.Style);

            const double Share = 0.5;
            int litFloors = 0;
            int darkFloors = 0;
            for (int storey = 0; storey < 24; storey++)
            {
                int lit = 0;
                int total = 0;
                for (int row = storey * 2; row < storey * 2 + 2; row++)
                {
                    for (int column = 0; column < 20; column++)
                    {
                        total++;
                        if (plan.IsLit(WindowGrid.Wall.South, column, row, Share))
                        {
                            lit++;
                        }
                    }
                }
                double fraction = lit / (double)total;
                if (fraction > 0.7)
                {
                    litFloors++;
                }
                else if (fraction < 0.2)
                {
                    darkFloors++;
                }
                else
                {
                    Assert.Fail($"storey {storey} is half lit ({fraction:0.00}) -- a glass office is lit floor by floor");
                }
            }
            Assert.Greater(litFloors, 4);
            Assert.Greater(darkFloors, 4);
        }

        [Test]
        public void AFloorIsLitAllTheWayRoundTheBuilding()
        {
            WindowPlan plan = default;
            for (int seed = 1; seed < 4000; seed++)
            {
                plan = WindowGrid.PlanFor(seed, ZoneKind.Core, Params(BuildingMass.Tower, 150.0f));
                if (plan.Style == FacadeStyle.Curtain)
                {
                    break;
                }
            }

            for (int storey = 0; storey < 24; storey++)
            {
                int row = storey * 2;
                int south = 0;
                int east = 0;
                for (int column = 0; column < 20; column++)
                {
                    south += plan.IsLit(WindowGrid.Wall.South, column, row, 0.5) ? 1 : 0;
                    east += plan.IsLit(WindowGrid.Wall.East, column, row, 0.5) ? 1 : 0;
                }
                Assert.AreEqual(south > 10, east > 10, $"storey {storey} is lit on one face and dark on another");
            }
        }

        [Test]
        public void PunchedWindowsAreShorterThanTheRowAndGlassIsFullHeight()
        {
            WindowPlan punched = default;
            WindowPlan curtain = default;
            for (int seed = 1; seed < 4000; seed++)
            {
                WindowPlan p = WindowGrid.PlanFor(seed, ZoneKind.Residential, Params(BuildingMass.MidRise));
                if (p.Style == FacadeStyle.Punched) { punched = p; }
                if (WindowGrid.PlanFor(seed, ZoneKind.Core, Params(BuildingMass.Tower, 150.0f)).Style == FacadeStyle.Curtain)
                {
                    curtain = WindowGrid.PlanFor(seed, ZoneKind.Core, Params(BuildingMass.Tower, 150.0f));
                }
            }

            punched.Insets(false, out float pBottom, out float pTop);
            curtain.Insets(false, out float cBottom, out float cTop);

            Assert.Greater(pBottom + pTop, 0.3f, "a punched window should be a small opening in a solid wall");
            Assert.Less(cBottom + cTop, 0.15f, "a curtain wall runs floor to ceiling");
        }

        [Test]
        public void AWindowNeverReachesIntoTheFloorSlabAboveIt()
        {
            foreach (FacadeStyle wanted in Enum.GetValues(typeof(FacadeStyle)))
            {
                for (int seed = 1; seed < 6000; seed++)
                {
                    WindowPlan plan = WindowGrid.PlanFor(seed, ZoneKind.Urban, Params(BuildingMass.MidRise));
                    if (plan.Style != wanted)
                    {
                        continue;
                    }
                    plan.Insets(true, out _, out float top);
                    Assert.GreaterOrEqual(top, WindowGrid.PaneInsetVerticalSlab, $"{wanted}: the window pokes into the slab");
                    break;
                }
            }
        }
    }
}
