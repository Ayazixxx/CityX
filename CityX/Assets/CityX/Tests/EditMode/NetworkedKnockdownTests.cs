using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>A knockdown forwarded to another player's body stands only if the blow just before it landed (bug hunt M5).</summary>
    public sealed class NetworkedKnockdownTests
    {
        [Test]
        public void AKnockdownRightAfterADefendedBlowIsDropped()
        {
            foreach (HitResponse defended in new[] { HitResponse.Ignored, HitResponse.Blocked, HitResponse.Guarded })
            {
                Assert.IsFalse(NetworkedDamage.KnockdownStands(defended, 0.05f), $"a knockdown after a {defended} blow threw the body down");
            }
            Assert.IsTrue(NetworkedDamage.KnockdownStands(HitResponse.Landed, 0.05f), "a landed blow's knockdown was dropped");
        }

        [Test]
        public void AKnockdownLongAfterAnyBlowStands()
        {
            float later = NetworkedDamage.KnockdownFollowsHitSeconds + 0.1f;
            Assert.IsTrue(NetworkedDamage.KnockdownStands(HitResponse.Blocked, later), "an old block stopped a later knockdown that came with no blow");
            Assert.IsTrue(NetworkedDamage.KnockdownStands(HitResponse.Landed, float.PositiveInfinity), "a first knockdown with no blow before it was dropped");
        }
    }
}
