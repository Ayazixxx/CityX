using System;
using System.Collections.Generic;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The wider family of building shapes (Massing.Compose): L, T and U blocks, staggered pairs, twisted, wonky and cantilevered towers. Asked:
    /// "all sorts of different building shapes and sizes ... some normal some wonky some round some square some L shaped".
    /// </summary>
    public sealed class BuildingShapeTests
    {
        private const int Seed = 74102958;

        private static CityWorld _world;
        private static CityWorld World => _world ?? (_world = new CityWorld(Seed));

        private static readonly BuildingMass[] Masses =
            { BuildingMass.LowRise, BuildingMass.MidRise, BuildingMass.Slab, BuildingMass.Tower, BuildingMass.Supertall };

        private static readonly ZoneKind[] Zones = { ZoneKind.Core, ZoneKind.Urban, ZoneKind.Residential, ZoneKind.OldTown, ZoneKind.Outskirts };

        private static IEnumerable<(int seed, double w, double d, double h, List<MassVolume> volumes, BuildingForm form)> Samples(int count)
        {
            for (int i = 0; i < count; i++)
            {
                BuildingMass mass = Masses[i % Masses.Length];
                double w = 18.0 + (i * 7) % 40;
                double d = 18.0 + (i * 11) % 44;
                double h = mass == BuildingMass.Supertall ? 260.0 + i % 90
                    : mass == BuildingMass.Tower ? 115.0 + (i * 3) % 120
                    : mass == BuildingMass.LowRise ? 12.0 + i % 16
                    : 30.0 + (i * 5) % 70;
                List<MassVolume> v = Massing.Compose(Zones[(i / 5) % Zones.Length], mass, w, d, h, new DeterministicRandom(i), out _, out BuildingForm form);
                yield return (i, w, d, h, v, form);
            }
        }

        [Test]
        public void EveryFormTurnsUp()
        {
            // The odd stacks are rare on purpose (Massing.OddStackChance), so it takes a lot of lots to meet every form.
            var seen = new HashSet<BuildingForm>();
            foreach (var s in Samples(40000))
            {
                seen.Add(s.form);
            }
            foreach (BuildingForm form in Enum.GetValues(typeof(BuildingForm)))
            {
                Assert.IsTrue(seen.Contains(form), $"no {form} in 3000 lots");
            }
        }

        [Test]
        public void EveryVolumeStaysInsideItsLot()
        {
            var xs = new double[4];
            var zs = new double[4];
            foreach (var s in Samples(3000))
            {
                foreach (MassVolume v in s.volumes)
                {
                    v.Corners(xs, zs);
                    for (int k = 0; k < 4; k++)
                    {
                        Assert.LessOrEqual(Math.Abs(xs[k]), s.w * 0.5 + 1e-6, $"seed {s.seed} {s.form}: reaches past the lot sideways");
                        Assert.LessOrEqual(Math.Abs(zs[k]), s.d * 0.5 + 1e-6, $"seed {s.seed} {s.form}: reaches past the lot front or back");
                    }
                }
            }
        }

        [Test]
        public void EveryVolumeAboveTheGroundStandsOnTheOneUnderIt()
        {
            foreach (var s in Samples(3000))
            {
                for (int i = 0; i < s.volumes.Count; i++)
                {
                    MassVolume v = s.volumes[i];
                    if (v.Support < 0)
                    {
                        continue;
                    }
                    Assert.Less(v.Support, i, $"seed {s.seed}: a volume's support comes before it");
                    Assert.IsTrue(Massing.CentreOver(v, s.volumes[v.Support], 0.0), $"seed {s.seed} {s.form} volume {i}: its centre is off the one below");
                }
            }
        }

        [Test]
        public void TheBlocksAtStreetLevelTouchButNeverOverlap()
        {
            var ax = new double[4];
            var az = new double[4];
            foreach (var s in Samples(3000))
            {
                for (int a = 0; a < s.volumes.Count; a++)
                {
                    for (int b = a + 1; b < s.volumes.Count; b++)
                    {
                        MassVolume va = s.volumes[a], vb = s.volumes[b];
                        if (va.Support >= 0 || vb.Support >= 0)
                        {
                            continue;
                        }
                        double ox = Math.Min(va.CentreX + HalfX(va), vb.CentreX + HalfX(vb)) - Math.Max(va.CentreX - HalfX(va), vb.CentreX - HalfX(vb));
                        double oz = Math.Min(va.CentreZ + HalfZ(va), vb.CentreZ + HalfZ(vb)) - Math.Max(va.CentreZ - HalfZ(va), vb.CentreZ - HalfZ(vb));
                        Assert.IsFalse(ox > 0.05 && oz > 0.05, $"seed {s.seed} {s.form}: blocks {a} and {b} overlap");
                    }
                }
            }
        }

        private static double HalfX(MassVolume v) => Math.Abs(v.TurnX) * v.Width * 0.5 + Math.Abs(v.TurnZ) * v.Depth * 0.5;
        private static double HalfZ(MassVolume v) => Math.Abs(v.TurnZ) * v.Width * 0.5 + Math.Abs(v.TurnX) * v.Depth * 0.5;

        [Test]
        public void TheSameLotAlwaysGivesTheSameShape()
        {
            for (int i = 0; i < 200; i++)
            {
                List<MassVolume> a = Massing.Compose(ZoneKind.Urban, Masses[i % Masses.Length], 34.0, 40.0, 150.0, new DeterministicRandom(i), out _, out BuildingForm fa);
                List<MassVolume> b = Massing.Compose(ZoneKind.Urban, Masses[i % Masses.Length], 34.0, 40.0, 150.0, new DeterministicRandom(i), out _, out BuildingForm fb);
                Assert.AreEqual(fa, fb);
                Assert.AreEqual(a.Count, b.Count);
                for (int k = 0; k < a.Count; k++)
                {
                    Assert.AreEqual(a[k].CentreX, b[k].CentreX);
                    Assert.AreEqual(a[k].TurnZ, b[k].TurnZ);
                    Assert.AreEqual(a[k].Height, b[k].Height);
                }
            }
        }

        [Test]
        public void AnLHasAWingWhoseDoorOpensIntoItsYardNotIntoTheBar()
        {
            int checkedL = 0;
            foreach (var s in Samples(3000))
            {
                if (s.form != BuildingForm.LShape && s.form != BuildingForm.UShape && s.form != BuildingForm.TShape)
                {
                    continue;
                }
                List<WallContact>[] contacts = PlanBuildings.ContactsBetween(s.volumes);
                for (int i = 0; i < s.volumes.Count; i++)
                {
                    if (contacts[i] == null)
                    {
                        continue;
                    }
                    foreach (WallContact c in contacts[i])
                    {
                        if (c.Wall != WindowGrid.Wall.North)
                        {
                            continue;
                        }
                        // The door is in the middle of the north wall.
                        float mid = (float)s.volumes[i].Width * 0.5f;
                        Assert.IsFalse(c.U0 < mid + 1.0f && c.U1 > mid - 1.0f, $"seed {s.seed} {s.form}: block {i}'s door is against another block");
                    }
                }
                Assert.IsNotNull(contacts[0], $"seed {s.seed} {s.form}: the bar touches nothing");
                checkedL++;
            }
            Assert.Greater(checkedL, 50);
        }

        [Test]
        public void ABlockWithAnIslandOfRoadInsideItIsMarkedAndItsNeighboursAreNot()
        {
            PlanBlock Square(double x0, double z0, double size)
            {
                var b = new PlanBlock { Area = size * size };
                b.Poly.Add(new Vec2(x0, z0));
                b.Poly.Add(new Vec2(x0 + size, z0));
                b.Poly.Add(new Vec2(x0 + size, z0 + size));
                b.Poly.Add(new Vec2(x0, z0 + size));
                return b;
            }
            var outer = Square(0.0, 0.0, 100.0);
            var island = Square(45.0, 45.0, 10.0);
            var neighbour = Square(100.0, 0.0, 60.0);
            var blocks = new List<PlanBlock> { outer, island, neighbour };
            CityPlan.MarkEnclosing(blocks);
            Assert.IsTrue(outer.Encloses, "the block round the island");
            Assert.IsFalse(island.Encloses);
            Assert.IsFalse(neighbour.Encloses, "sharing an edge is not enclosing");
        }

        // ---- in the real city ----

        [Test]
        public void TheCityHasEveryKindOfShape()
        {
            // Whole buildings: a street-level volume that is not a wing, and the wings and volumes after it.
            int buildings = 0, winged = 0, odd = 0, round = 0, plain = 0;
            for (int id = 0; id < World.BuildingCount; id++)
            {
                BuildingSite first = World.Building(id);
                if (first.OnRoof || first.Wing)
                {
                    continue;
                }
                buildings++;
                bool hasWing = false, hasShell = false, stacked = false;
                for (int k = id + 1; k < World.BuildingCount && (World.Building(k).OnRoof || World.Building(k).Wing); k++)
                {
                    hasWing |= World.Building(k).Wing;
                    hasShell |= World.Building(k).Shell;
                    stacked = true;
                }
                if (first.Sides >= 3) round++;
                else if (hasShell) odd++;
                else if (hasWing) winged++;
                else if (!stacked) plain++;
            }
            Debug.Log($"CityX shapes: {buildings} buildings: {plain} plain boxes, {winged} with wings, {round} round, {odd} odd stacks");
            Assert.Greater(winged, 200, "L, T, U and staggered blocks");
            Assert.Greater(round, 20, "round towers");
            Assert.Greater(odd, 0, "a twisted, wonky or cantilevered tower somewhere");
            Assert.Less(odd, buildings * 0.01, "the odd stacks are rare (Massing.OddStackChance)");
            Assert.Greater(plain, winged, "plain boxes are still the most common building");
        }

        [Test]
        public void NoWindowLooksIntoAnotherBlockOfTheSameBuilding()
        {
            int checkedCells = 0;
            for (int id = 0; id < World.BuildingCount; id++)
            {
                BuildingSite site = World.Building(id);
                if (site.Contacts == null)
                {
                    continue;
                }
                WindowPlan plan = WindowPlan.For(World, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                foreach (WallContact c in site.Contacts)
                {
                    bool northSouth = c.Wall == WindowGrid.Wall.North || c.Wall == WindowGrid.Wall.South;
                    CellLayout columns = plan.ColumnsFor(northSouth ? site.Params.Width : site.Params.Depth);
                    for (int col = 0; col < columns.Count; col++)
                    {
                        float u0 = col * columns.Size, u1 = u0 + columns.Size;
                        // A column the other block only grazes (2% of it, well inside the pane's own inset) keeps its window.
                        if (u1 <= c.U0 + columns.Size * 0.02f || u0 >= c.U1 - columns.Size * 0.02f)
                        {
                            continue;
                        }
                        for (int row = 0; row < rows.Count && row * rows.Size < c.Top - 0.2f; row++)
                        {
                            Assert.IsFalse(plan.IsGlazed(c.Wall, col, row), $"#{site.Id} {c.Wall} column {col} row {row}: a window in the wall another block stands against");
                            checkedCells++;
                        }
                    }
                }
            }
            Assert.Greater(checkedCells, 1000);
        }

        [Test]
        public void EveryStreetLevelDoorOpensOntoOpenGround()
        {
            int doors = 0, blocked = 0;
            for (int id = 0; id < World.BuildingCount; id++)
            {
                BuildingSite site = World.Building(id);
                if (!site.Wing)
                {
                    continue;
                }
                site.LocalToWorld(0.0, site.Params.Depth * 0.5 + 1.5, out double x, out double z);
                doors++;
                if (World.Plan.BuildingAtHeight(x, site.BaseHeight + 1.0, z) >= 0)
                {
                    blocked++;
                }
            }
            Assert.Greater(doors, 200);
            Assert.AreEqual(0, blocked, $"{blocked} of {doors} wing doors open into a building");
        }

        [Test]
        public void ASealedBlockHasNoDoorAndCannotBeShotThrough()
        {
            int shells = 0;
            for (int id = 0; id < World.BuildingCount && shells < 200; id++)
            {
                BuildingSite site = World.Building(id);
                if (!site.Shell)
                {
                    continue;
                }
                shells++;
                Assert.IsTrue(site.OnRoof, $"#{id}: a sealed block always stands on the building you can go into");
                site.LocalToWorld(0.0, site.Params.Depth * 0.5, out double x, out double z);
                Assert.IsFalse(GlassDamage.TryPaneAt(World, site, new Vector3((float)x, site.BaseHeight + site.Params.Height * 0.5f, (float)z), out _));
            }
            Assert.Greater(shells, 50);
        }
    }
}
