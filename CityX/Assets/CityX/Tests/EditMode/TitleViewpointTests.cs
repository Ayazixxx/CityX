using CityX.UI;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The roof the title's character stands on (TitleViewpoint; owner, 2026-11-11: the city behind them "like the ingame cities that generate
    /// ingame"): a real building's, near where the game starts, the character near a corner, looking out over its edge at the city.
    /// </summary>
    public sealed class TitleViewpointTests
    {
        private const int Seed = 95998845;

        [Test]
        public void ItStandsJustInFromTheEdgeOfARealRoofLookingOutOverIt()
        {
            PlanWorld plan = new CityWorld(Seed).Plan;
            TitleViewpoint.Pick pick = TitleViewpoint.Choose(plan);
            Assert.IsTrue(pick.Found, "no roof in the world to stand on");

            BuildingSite site = plan.Buildings[pick.Building];
            Assert.IsTrue(TitleViewpoint.Standable(site), "not a plain roof of the right height with room on it");
            Assert.GreaterOrEqual(pick.Feet.y, site.Top + TitleViewpoint.AboveAround - 1e-3f, "the title's rooftop does not stand over the real one");
            site.WorldToLocal(pick.Feet.x, pick.Feet.z, out double x, out double z);
            double halfWidth = site.Params.Width * 0.5, halfDepth = site.Params.Depth * 0.5;
            Assert.LessOrEqual(System.Math.Abs(x), halfWidth, "off the roof");
            Assert.LessOrEqual(System.Math.Abs(z), halfDepth, "off the roof");
            // Near a corner: just in from the front edge and from the one on the camera's left.
            double[] gaps = { halfWidth - System.Math.Abs(x), halfDepth - System.Math.Abs(z) };
            System.Array.Sort(gaps);
            Assert.AreEqual(System.Math.Min(TitleViewpoint.EdgeGap, TitleViewpoint.SideGap), gaps[0], 0.01, "not near the corner");
            Assert.AreEqual(System.Math.Max(TitleViewpoint.EdgeGap, TitleViewpoint.SideGap), gaps[1], 0.01, "not near the corner");

            var fromMiddle = new Vector3(pick.Feet.x - (float)site.CentreX, 0.0f, pick.Feet.z - (float)site.CentreZ);
            Assert.Greater(Vector3.Dot(fromMiddle, pick.Out), 0.0f, "looking in over the roof, not out over its edge");
            Assert.Greater(Vector3.Dot(fromMiddle, Vector3.Cross(pick.Out, Vector3.up)), 0.0f,
                "the corner is on the camera's right: the roof would fill the view, not drop away on the left");
            Assert.AreEqual(1.0f, pick.Out.magnitude, 1e-4f);
            Assert.AreEqual(0.0f, pick.Out.y);
            Assert.IsFalse(pick.Walled, "every roof in reach looks at a wall: the view would be a building's face");
            Assert.Greater(pick.Score, 0.0, "nothing to look at");
        }

        /// <summary>
        /// In the city the game plays (not the tests' classic one) too: the title once found no roof there to stand on and showed a wall
        /// filling the screen (owner, 2026-11-11: "its not working properly").
        /// </summary>
        [Test]
        public void InTheCityTheGamePlaysItFindsASpotToo()
        {
            CityScale was = CityScale.Active;
            CityScale.Active = CityScale.Default;
            try
            {
                TitleViewpoint.Pick pick = TitleViewpoint.Choose(new CityWorld(Seed).Plan);
                Assert.IsTrue(pick.Found, "no spot in the game's city for the title's rooftop");
                Assert.AreEqual(1.0f, pick.Out.magnitude, 1e-4f);
            }
            finally
            {
                CityScale.Active = was;
            }
        }

        [Test]
        public void TheSameWorldChoosesTheSameRoof()
        {
            TitleViewpoint.Pick a = TitleViewpoint.Choose(new CityWorld(Seed).Plan);
            TitleViewpoint.Pick b = TitleViewpoint.Choose(new CityWorld(Seed).Plan);
            Assert.AreEqual(a.Building, b.Building);
            Assert.AreEqual(a.Feet, b.Feet);
            Assert.AreEqual(a.Out, b.Out);
        }
    }
}
