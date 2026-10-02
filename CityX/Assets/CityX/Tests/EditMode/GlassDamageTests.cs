using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Breaking windows: which one a blow lands on, and what a blow does to it.
    ///
    /// The addressing half is the part worth testing hardest. Getting the state
    /// machine wrong is visible the first time you punch a window; getting the
    /// addressing wrong breaks a DIFFERENT window somewhere along the wall, and
    /// that is the kind of bug you would put down to the animation being off.
    /// </summary>
    public class GlassDamageTests
    {
        private const int Seed = 20260810;

        /// <summary>
        /// A building whose door wall has plenty of windows AND plenty of blank wall, so a test of "finds the
        /// window" and one of "finds nothing on concrete" both mean something. Not every building qualifies
        /// now: a curtain-wall tower has no blank cells and a warehouse has almost no windows.
        /// </summary>
        private static BuildingSite AnyBuilding(CityWorld world)
        {
            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    BuildingSite site = TestSites.FirstOwnedBy(world, x, y);
                    if (!site.Exists || site.Params.Width <= 14.0f)
                    {
                        continue;
                    }

                    WindowPlan plan = WindowPlan.For(world, site);
                    CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                    CellLayout columns = plan.ColumnsFor(site.Params.Width);
                    int windows = 0;
                    int blanks = 0;
                    for (int row = 1; row < rows.Count; row++)
                    {
                        for (int column = 0; column < columns.Count; column++)
                        {
                            if (plan.IsGlazed(WindowGrid.Wall.North, column, row))
                            {
                                windows++;
                            }
                            else
                            {
                                blanks++;
                            }
                        }
                    }
                    if (windows > 20 && blanks > 20)
                    {
                        return site;
                    }
                }
            }
            Assert.Fail("no building near the origin with both windows and blank wall to punch");
            return default;
        }

        [Test]
        public void OneHitCracksAWindowAndASecondShattersIt()
        {
            var damage = new GlassDamage();
            var key = new PaneKey(3, WindowGrid.Wall.North, 4, 2);

            Assert.AreEqual(PaneState.Intact, damage.StateOf(key));
            Assert.AreEqual(0, damage.BrokenCount, "an untouched city stores nothing");

            Assert.AreEqual(PaneState.Cracked, damage.Hit(key));
            Assert.AreEqual(PaneState.Shattered, damage.Hit(key));

            // And it stays gone however hard you keep hitting it.
            Assert.AreEqual(PaneState.Shattered, damage.Hit(key));
            Assert.AreEqual(PaneState.Shattered, damage.StateOf(key));
            Assert.AreEqual(1, damage.BrokenCount, "one window took more than one slot");
        }

        /// <summary>
        /// Hitting a window changes THAT window. The pane key carries a chunk, a
        /// wall, a column and a row, and confusing any two of them would break
        /// something across the street.
        /// </summary>
        [Test]
        public void BreakingOneWindowLeavesItsNeighboursAlone()
        {
            var damage = new GlassDamage();
            var key = new PaneKey(3, WindowGrid.Wall.North, 4, 2);
            damage.Hit(key);

            Assert.AreEqual(PaneState.Intact,
                damage.StateOf(new PaneKey(3, WindowGrid.Wall.North, 5, 2)), "next along");
            Assert.AreEqual(PaneState.Intact,
                damage.StateOf(new PaneKey(3, WindowGrid.Wall.North, 4, 3)), "the floor above");
            Assert.AreEqual(PaneState.Intact,
                damage.StateOf(new PaneKey(3, WindowGrid.Wall.South, 4, 2)), "round the back");
            Assert.AreEqual(PaneState.Intact,
                damage.StateOf(new PaneKey(4, WindowGrid.Wall.North, 4, 2)), "next building");
        }

        /// <summary>
        /// Every change tells whatever is drawing the city, once. A chunk that
        /// missed the message would show an intact window you can walk through.
        /// </summary>
        [Test]
        public void EveryChangeIsAnnouncedAndNoNonChangeIs()
        {
            var damage = new GlassDamage();
            var key = new PaneKey(0, WindowGrid.Wall.West, 1, 1);

            int announced = 0;
            PaneState last = PaneState.Intact;
            damage.Changed += (k, state) =>
            {
                Assert.AreEqual(key, k);
                last = state;
                announced++;
            };

            damage.Hit(key);
            Assert.AreEqual(1, announced);
            Assert.AreEqual(PaneState.Cracked, last);

            damage.Hit(key);
            Assert.AreEqual(2, announced);
            Assert.AreEqual(PaneState.Shattered, last);

            // Already gone. Nothing changed, so nothing is said -- a chunk
            // rebuilding itself for a hit that did nothing is a hitch for free.
            damage.Hit(key);
            Assert.AreEqual(2, announced);

            damage.Set(key, PaneState.Shattered);
            Assert.AreEqual(2, announced, "setting a pane to what it already was said something");
        }

        /// <summary>
        /// A blow on a wall finds the window it actually landed on. Checked by
        /// walking every glazed cell of a real building, aiming at its centre,
        /// and asking what the city says is there.
        /// </summary>
        [Test]
        public void APointOnAWallResolvesToTheWindowItIsOn()
        {
            var world = new CityWorld(Seed);
            BuildingSite site = AnyBuilding(world);
            WindowPlan plan = WindowPlan.For(world, site);

            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            CellLayout columns = plan.ColumnsFor(site.Params.Width);
            int checkedCount = 0;

            for (int row = 1; row < rows.Count; row++)
            {
                for (int column = 0; column < columns.Count; column++)
                {
                    if (!plan.IsGlazed(WindowGrid.Wall.North, column, row))
                    {
                        continue;
                    }

                    // Dead centre of that pane on the wall at maximum Z, a
                    // couple of centimetres proud of it like a real contact.
                    var point = TestSites.At(site, 
                        (float)site.MinX + (column + 0.5f) * columns.Size,
                        site.BaseHeight + (row + 0.5f) * rows.Size,
                        (float)site.MaxZ + 0.02f);

                    Assert.IsTrue(GlassDamage.TryPaneAt(world, site, point, out PaneKey key),
                        $"the middle of the window at column {column}, row {row} is not a window");
                    Assert.AreEqual(site.Id, key.Building);
                    Assert.AreEqual(WindowGrid.Wall.North, key.Wall);
                    Assert.AreEqual(column, key.Column);
                    Assert.AreEqual(row, key.Row);
                    checkedCount++;
                }
            }

            Assert.Greater(checkedCount, 20, "not enough windows on this building to mean anything");
        }

        /// <summary>
        /// AND A PUNCH ON CONCRETE BREAKS NOTHING. Half of every facade is
        /// blank wall, so a hit resolver that said yes everywhere would let a
        /// player punch a hole through solid concrete.
        /// </summary>
        [Test]
        public void APunchOnBlankWallOrPastTheBuildingFindsNothing()
        {
            var world = new CityWorld(Seed);
            BuildingSite site = AnyBuilding(world);
            WindowPlan plan = WindowPlan.For(world, site);

            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            CellLayout columns = plan.ColumnsFor(site.Params.Width);
            int blanks = 0;

            for (int row = 1; row < rows.Count; row++)
            {
                for (int column = 0; column < columns.Count; column++)
                {
                    if (plan.IsGlazed(WindowGrid.Wall.North, column, row))
                    {
                        continue;
                    }
                    var point = TestSites.At(site, 
                        (float)site.MinX + (column + 0.5f) * columns.Size,
                        site.BaseHeight + (row + 0.5f) * rows.Size,
                        (float)site.MaxZ + 0.02f);
                    Assert.IsFalse(GlassDamage.TryPaneAt(world, site, point, out _),
                        $"blank concrete at column {column}, row {row} claims to be glass");
                    blanks++;
                }
            }
            Assert.Greater(blanks, 20, "this building has no blank wall, so this proved nothing");

            float midX = (float)site.CentreX;
            float midY = site.BaseHeight + site.Params.Height * 0.5f;

            // Standing well back from the wall.
            Assert.IsFalse(GlassDamage.TryPaneAt(world, site,
                TestSites.At(site, midX, midY, (float)site.MaxZ + 3.0f), out _), "a punch at thin air");

            // Under the lobby floor, which is podium, and over the roof.
            Assert.IsFalse(GlassDamage.TryPaneAt(world, site,
                TestSites.At(site, midX, site.BaseHeight - 2.0f, (float)site.MaxZ), out _), "the podium");
            Assert.IsFalse(GlassDamage.TryPaneAt(world, site,
                TestSites.At(site, midX, site.Top + 2.0f, (float)site.MaxZ), out _), "above the roof");

            // Off the end of the wall.
            Assert.IsFalse(GlassDamage.TryPaneAt(world, site,
                TestSites.At(site, (float)site.MaxX + 4.0f, midY, (float)site.MaxZ), out _), "past the corner");
        }

        /// <summary>
        /// THE DOORWAY IS NOT A WINDOW. No glass was ever hung there, so a
        /// punch at the entrance has to find nothing rather than crack a pane
        /// that is not drawn -- which would leave a window in the damage record
        /// that nothing on screen could ever match.
        /// </summary>
        [Test]
        public void PunchingTheDoorwayBreaksNothing()
        {
            var world = new CityWorld(Seed);
            int found = 0;

            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    BuildingSite site = TestSites.FirstOwnedBy(world, x, y);
                    if (!site.Exists)
                    {
                        continue;
                    }
                    found++;

                    var inTheDoorway = TestSites.At(site, 
                        (float)site.CentreX,
                        site.BaseHeight + BuildingInterior.DoorHeight(site.Params.Height) * 0.5f,
                        (float)site.MaxZ);
                    Assert.IsFalse(GlassDamage.TryPaneAt(world, site, inTheDoorway, out PaneKey key),
                        $"({x},{y}) has breakable glass in its own doorway at {key}");
                }
            }

            Assert.Greater(found, 20, "not enough buildings sampled");
        }
    }
}
