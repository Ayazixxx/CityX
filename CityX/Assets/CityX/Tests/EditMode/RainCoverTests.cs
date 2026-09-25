using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The sky-to-surface map, with a fake caster standing in for physics: a
    /// flat ground at y=0, and optionally a roof over a square of the city.
    /// What matters is what the map SAYS -- where rain can fall, who is
    /// sheltered -- and that keeping it current as the player walks costs a
    /// row of rays, not the whole window.
    /// </summary>
    public sealed class RainCoverTests
    {
        private const float RoofHeight = 8.0f;

        private int _rays;
        private bool _roofPresent;
        private Rect _roofArea;
        private float _groundHeight;

        [SetUp]
        public void SetUp()
        {
            _rays = 0;
            _roofPresent = false;
            _roofArea = new Rect(-10.0f, -10.0f, 20.0f, 20.0f);   // x -10..10, z -10..10
            _groundHeight = 0.0f;
        }

        private float Caster(float x, float z, float fromY)
        {
            _rays++;
            if (_roofPresent && _roofArea.Contains(new Vector2(x, z)))
            {
                return RoofHeight;
            }
            return _groundHeight;
        }

        private RainCover NewCover()
        {
            return new RainCover(Caster);
        }

        [Test]
        public void TheFirstRefreshCastsTheWholeWindow()
        {
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);

            Assert.AreEqual(RainCover.Cells * RainCover.Cells, cover.LastRefreshRays);
            Assert.AreEqual(0.0f, cover.SurfaceHeightAt(3.0f, 3.0f));
        }

        [Test]
        public void OpenSkyIsNotSheltered()
        {
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);

            Assert.AreEqual(0.0f, cover.Shelter(new Vector3(1.0f, 0.0f, 1.0f)));
        }

        [Test]
        public void GroundUnderARoofIsShelteredAndGroundBesideItIsNot()
        {
            _roofPresent = true;
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);

            Assert.AreEqual(RoofHeight, cover.SurfaceHeightAt(0.5f, 0.5f), "rain falling here would land on the roof");
            Assert.AreEqual(1.0f, cover.Shelter(new Vector3(0.5f, 0.0f, 0.5f)), 1e-4f, "standing under it");
            Assert.AreEqual(0.0f, cover.Shelter(new Vector3(15.0f, 0.0f, 0.5f)), 1e-4f, "well outside it");
        }

        [Test]
        public void TheEdgeOfARoofFadesTheShelterInsteadOfFlippingIt()
        {
            _roofPresent = true;
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);

            // Walk from well under the roof, out across its edge at x=10.
            float previous = 2.0f;
            bool sawPartial = false;
            for (float x = 6.0f; x <= 14.0f; x += 0.25f)
            {
                float shelter = cover.Shelter(new Vector3(x, 0.0f, 0.0f));
                Assert.LessOrEqual(shelter, previous + 1e-4f, $"shelter rose walking OUT from under the roof at x={x}");
                if (shelter > 0.05f && shelter < 0.95f)
                {
                    sawPartial = true;
                }
                previous = shelter;
            }
            Assert.IsTrue(sawPartial, "the shelter flipped from 1 to 0 at a cell edge instead of easing out over a couple of metres");
        }

        [Test]
        public void ARoofIsNoShelterToSomeoneStandingOnIt()
        {
            _roofPresent = true;
            RainCover cover = NewCover();
            cover.Refresh(new Vector3(0.0f, RoofHeight, 0.0f), 0.016f);

            Assert.AreEqual(0.0f, cover.Shelter(new Vector3(0.0f, RoofHeight, 0.0f)),
                "the surface is under their feet, not over their head");
        }

        [Test]
        public void ASurfaceCountsAsARoofOnlyIfItIsAboveHeadHeight()
        {
            _roofPresent = true;
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);

            // Standing on the ground, on a 2m platform, or 5.7m up a stairwell:
            // the 8m roof is still more than HeadClearance (2.2m) over the feet.
            Assert.AreEqual(1.0f, cover.Shelter(new Vector3(0.0f, 0.0f, 0.0f)), 1e-4f);
            Assert.AreEqual(1.0f, cover.Shelter(new Vector3(0.0f, 2.0f, 0.0f)), 1e-4f);
            Assert.AreEqual(1.0f, cover.Shelter(new Vector3(0.0f, RoofHeight - RainCover.HeadClearance - 0.1f, 0.0f)), 1e-4f);
            // Closer than that, the 'roof' is at the person's own head: it is what
            // they are standing beside, not what is over them.
            Assert.AreEqual(0.0f, cover.Shelter(new Vector3(0.0f, RoofHeight - RainCover.HeadClearance + 0.1f, 0.0f)), 1e-4f);
        }

        [Test]
        public void WalkingOnlyRecastsTheRowThatEnteredTheWindow()
        {
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);

            // One cell (2m) along +x: exactly one new column of cells.
            cover.Refresh(new Vector3(RainCover.CellSize, 0.0f, 0.0f), 0.016f);

            Assert.AreEqual(RainCover.Cells, cover.LastRefreshRays, "one column of new cells, nothing else");
        }

        [Test]
        public void ADiagonalStepRecastsAnLShapeNotTheWholeWindow()
        {
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);

            cover.Refresh(new Vector3(RainCover.CellSize * 3.0f, 0.0f, RainCover.CellSize * 2.0f), 0.016f);

            Assert.Less(cover.LastRefreshRays, RainCover.Cells * RainCover.Cells / 2);
            Assert.Greater(cover.LastRefreshRays, 0);
        }

        [Test]
        public void StandingStillCastsNothingUntilACellAgesOut()
        {
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);

            cover.Refresh(Vector3.zero, 0.016f);
            Assert.AreEqual(0, cover.LastRefreshRays, "nothing changed and nothing is old");
        }

        [Test]
        public void ARoofThatAppearsIsNoticedWithinASecondOrTwoAtABoundedCostPerFrame()
        {
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);
            Assert.AreEqual(0.0f, cover.Shelter(new Vector3(0.5f, 0.0f, 0.5f)));

            _roofPresent = true;   // a door closes, a car pulls up, a skybridge is built...
            int mostInOneFrame = 0;
            for (int frame = 0; frame < 400; frame++)
            {
                cover.Refresh(Vector3.zero, 0.016f);
                mostInOneFrame = Mathf.Max(mostInOneFrame, cover.LastRefreshRays);
            }

            Assert.AreEqual(1.0f, cover.Shelter(new Vector3(0.5f, 0.0f, 0.5f)), 1e-4f, "the new roof was never noticed");
            Assert.LessOrEqual(mostInOneFrame, RainCover.AgingBudgetPerRefresh, "ageing recast more cells in one frame than its budget");
        }

        [Test]
        public void APointOutsideTheWindowIsAnsweredByOneDirectCast()
        {
            _roofPresent = true;
            _roofArea = new Rect(200.0f, 200.0f, 20.0f, 20.0f);
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);
            int before = _rays;

            float shelter = cover.Shelter(new Vector3(205.0f, 0.0f, 205.0f));

            Assert.AreEqual(1.0f, shelter, "a point far outside the window under a roof is still sheltered");
            Assert.AreEqual(1, _rays - before, "one ray, not a refresh");
        }

        [Test]
        public void InvalidateForgetsEverythingSoTheNextRefreshCastsItAllAgain()
        {
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);
            cover.Invalidate();

            Assert.IsTrue(float.IsNaN(cover.SurfaceHeightAt(1.0f, 1.0f)));
            cover.Refresh(Vector3.zero, 0.016f);
            Assert.AreEqual(RainCover.Cells * RainCover.Cells, cover.LastRefreshRays);
        }

        [Test]
        public void CellsOutsideTheWindowAreUnknownNotZero()
        {
            RainCover cover = NewCover();
            cover.Refresh(Vector3.zero, 0.016f);

            Assert.IsTrue(float.IsNaN(cover.SurfaceHeightAt(500.0f, 0.0f)));
        }

        [Test]
        public void NegativeCoordinatesMapToTheirOwnCells()
        {
            _roofPresent = true;
            RainCover cover = NewCover();
            cover.Refresh(new Vector3(-3.0f, 0.0f, -3.0f), 0.016f);

            Assert.AreEqual(RoofHeight, cover.SurfaceHeightAt(-3.0f, -3.0f));
            Assert.AreEqual(RoofHeight, cover.SurfaceHeightAt(-9.0f, -0.5f));
            Assert.AreEqual(0.0f, cover.SurfaceHeightAt(-19.0f, -19.0f));   // outside the roof, inside the window
        }

        [Test]
        public void AnUnknownSurfaceIsTreatedAsOpenSkyNotAHole()
        {
            var cover = new RainCover((x, z, y) => float.NaN);
            cover.Refresh(Vector3.zero, 0.016f);

            Assert.AreEqual(0.0f, cover.Shelter(new Vector3(1.0f, 0.0f, 1.0f)));
        }

        [Test]
        public void RaysStartAboveTheTallestPossibleBuilding()
        {
            // 72m of terrain relief + a 320m supertall, with room to spare.
            Assert.Greater(RainCover.SkyHeight, 72.0f + 320.0f);

            float lastFrom = 0.0f;
            var cover = new RainCover((x, z, fromY) => { lastFrom = fromY; return 0.0f; });
            cover.Refresh(new Vector3(0.0f, 700.0f, 0.0f), 0.016f);
            Assert.Greater(lastFrom, 700.0f, "a player above the sky height still casts from above themselves");
        }
    }
}
