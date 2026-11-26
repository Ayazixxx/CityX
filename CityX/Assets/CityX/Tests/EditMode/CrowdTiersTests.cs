using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The crowd's level of detail (owner, 2026-11-25: "something that will not tank performance in large crowds of people remember there is a
    /// hub"; docs/CROWD_TEST.md): another player's body in full near the camera and among the nearest, lighter past that, unseen off the screen.
    /// </summary>
    public sealed class CrowdTiersTests
    {
        [Test]
        public void NearAndAmongTheNearestInFullFurtherOrPastThemLighterOffTheScreenUnseen()
        {
            Assert.AreEqual(CrowdTier.Full, CrowdTiers.For(5.0f, 0, true));
            Assert.AreEqual(CrowdTier.Full, CrowdTiers.For(CrowdTiers.FullWithin, CrowdTiers.MostInFull - 1, true));
            Assert.AreEqual(CrowdTier.Light, CrowdTiers.For(CrowdTiers.FullWithin + 0.1f, 0, true), "a body far off is drawn in full");
            Assert.AreEqual(CrowdTier.Light, CrowdTiers.For(5.0f, CrowdTiers.MostInFull, true), "a packed crowd is all drawn in full");
            Assert.AreEqual(CrowdTier.Unseen, CrowdTiers.For(2.0f, 0, false), "a body off the screen is worked out");
        }
    }
}
