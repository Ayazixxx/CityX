using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>How far a climber leans out round what juts from a wall (ClimbJut.LeanFor).</summary>
    public sealed class ClimbJutTests
    {
        [Test]
        public void AFlatWallNeedsNoLean()
        {
            Assert.AreEqual(0.0f, ClimbJut.LeanFor(0.0f));
            Assert.AreEqual(0.0f, ClimbJut.LeanFor(ClimbJut.Ignore * 0.5f), "a frame or the wall's own texture is not a ledge");
        }

        [Test]
        public void ACourseOrACorniceIsLeanedRoundByItsOwnDepth()
        {
            Assert.AreEqual(0.24f, ClimbJut.LeanFor(0.24f), 1e-6f, "the base course over a shop front");
            Assert.AreEqual(0.85f, ClimbJut.LeanFor(0.85f), 1e-6f, "the deepest cornice");
            Assert.GreaterOrEqual(ClimbJut.MaxLean, World.FacadeOrnament.MaxCorniceDepth, "a cornice the city builds must be one a climber can lean round");
        }

        [Test]
        public void ABalconyIsTooDeepToLeanRound()
        {
            Assert.AreEqual(0.0f, ClimbJut.LeanFor(1.4f));
        }
    }
}
