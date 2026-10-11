using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>Octaslash's timeline against Sephiroth's (SmashWiki): eight cuts on frames 36-50, two apart, the last three times the others.</summary>
    public sealed class OctaslashMoveTests
    {
        [Test]
        public void EightCutsTwoFramesApartLikeSmash()
        {
            Assert.AreEqual(8, OctaslashMove.CutCount);
            CollectionAssert.AreEqual(new[] { 36, 38, 40, 42, 44, 46, 48, 50 }, OctaslashMove.CutFrames);
            for (int i = 1; i < OctaslashMove.CutCount; i++)
            {
                Assert.AreEqual(2.0f / 60.0f, OctaslashMove.CutTime(i) - OctaslashMove.CutTime(i - 1), 1e-5f);
            }
            Assert.AreEqual(OctaslashMove.CutTime(7), OctaslashMove.DashSeconds, 1e-6f, "the dash lasts until the last cut");
        }

        [Test]
        public void TheLastCutIsAboutThreeTimesTheOthers()
        {
            // Smash: 2.3% seven times, then 7% (a ratio of about 3); the percentages themselves do not carry over.
            float ratio = OctaslashMove.CutScale(7) / OctaslashMove.CutScale(0);
            Assert.AreEqual(7.0f / 2.3f, ratio, 0.1f);
            for (int i = 0; i < 7; i++)
            {
                Assert.IsFalse(OctaslashMove.IsFinal(i));
            }
            Assert.IsTrue(OctaslashMove.IsFinal(7));
        }

        [Test]
        public void CutsComeDueOnTime()
        {
            Assert.AreEqual(0, OctaslashMove.CutsDueBy(0.0f));
            Assert.AreEqual(1, OctaslashMove.CutsDueBy(OctaslashMove.CutTime(0)));
            Assert.AreEqual(4, OctaslashMove.CutsDueBy(OctaslashMove.CutTime(3) + 0.001f));
            Assert.AreEqual(8, OctaslashMove.CutsDueBy(10.0f));
        }

        [Test]
        public void TheDashCoversItsDistanceAndTheWindUpIsThreeSeconds()
        {
            Assert.AreEqual(3.0f, OctaslashMove.ChargeSeconds);
            Assert.AreEqual(OctaslashMove.DashMetres, OctaslashMove.DashSpeed * OctaslashMove.DashSeconds, 1e-3f);
        }

        [Test]
        public void TheBodyLungesAlongALevelDashAndStaysUprightGoingStraightUpOrDown()
        {
            Assert.Greater(Vector3.Angle(Vector3.up, OctaslashMove.LeanUp(Vector3.forward)), 40.0f, "a level dash should be a long lunge");
            Assert.Less(Vector3.Angle(Vector3.up, OctaslashMove.LeanUp(Vector3.up)), 0.1f);
            Assert.Less(Vector3.Angle(Vector3.up, OctaslashMove.LeanUp(Vector3.down)), 0.1f, "straight down goes feet first, not head first");
        }
    }
}
