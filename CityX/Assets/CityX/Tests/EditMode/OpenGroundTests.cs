using CityX.Character;
using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Where the new character's fall lands (OpenGround; owner, 2026-11-10: "make him fall far enough away from the buildings that the camera
    /// changes dont clip into the building").
    /// </summary>
    public sealed class OpenGroundTests
    {
        private const int Seed = 95998845;

        [Test]
        public void TheFallLandsNearTheStartFurtherFromEveryBuildingThanItsCamerasSwingOut()
        {
            var world = new CityWorld(Seed);
            Assert.IsTrue(world.Plan.TryFindSpawn(out double sx, out double sz), "the world has no start");
            (double x, double z, double clear) = OpenGround.Near(world, sx, sz, IntroCutscene.LandingReach);

            double away = System.Math.Sqrt((x - sx) * (x - sx) + (z - sz) * (z - sz));
            Assert.LessOrEqual(away, IntroCutscene.LandingReach + 1e-6, "it lands too far from where the character starts");
            Assert.GreaterOrEqual(clear, OpenGround.Clearance(world, sx, sz), "it lands somewhere closer to a building than the start");
            Assert.Greater(clear, IntroCutscene.OverheadSpringLength, "a building is within reach of the cameras where it lands");
            Assert.IsFalse(world.TryBuildingAt(x, z, out _, IntroCutscene.OverheadSpringLength), "it lands beside a building");
            Assert.IsFalse(world.Plan.TutorialHouse.Contains(x, z, IntroCutscene.OverheadSpringLength), "it lands beside Old Guy's house");
            Assert.IsTrue(world.Plan.TutorialHouse.Contains(x, z, IntroCutscene.LandingReach), "it lands out of sight of Old Guy's house, where the tutorial is");
        }

        [Test]
        public void ABuildingWallIsNotOpenGroundAndOpenGroundIsMeasuredToTheWall()
        {
            var world = new CityWorld(Seed);
            BuildingSite site = world.Building(0);
            Assert.AreEqual(0.0, OpenGround.Clearance(world, site.CentreX, site.CentreZ), "the middle of a building is open ground");
            Assert.IsTrue(world.Plan.TryFindSpawn(out double sx, out double sz));
            double clear = OpenGround.Clearance(world, sx, sz);
            Assert.IsFalse(world.TryBuildingAt(sx, sz, out _, clear), "a building nearer than the clearance given");
        }
    }
}
