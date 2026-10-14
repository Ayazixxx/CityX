using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The recoil from a blow on paper: four directions by name, thrown away from the blow, held an instant, back to nothing by the end; a stagger bigger and longer.</summary>
    public sealed class HitReactionsTests
    {
        [Test]
        public void NamesRoundTripForEveryDirectionAndBothWeights()
        {
            foreach (HitDirection direction in new[] { HitDirection.Front, HitDirection.Back, HitDirection.Left, HitDirection.Right })
            {
                foreach (bool stagger in new[] { false, true })
                {
                    string name = HitReactions.NameFor(direction, stagger);
                    Assert.IsTrue(HitReactions.TryGet(name, out HitDirection back, out bool backStagger), name);
                    Assert.AreEqual(direction, back);
                    Assert.AreEqual(stagger, backStagger);
                    Assert.IsTrue(HitReactions.Has(name));
                }
            }
            Assert.IsFalse(HitReactions.Has("sword1h_slash"));
            Assert.IsFalse(HitReactions.Has("punch_jab"));
        }

        [Test]
        public void TheDirectionIsWhereTheBlowCameFrom()
        {
            Assert.AreEqual(HitDirection.Front, HitReactions.DirectionOf(new Vector3(0.2f, 1.0f, 2.0f)));
            Assert.AreEqual(HitDirection.Back, HitReactions.DirectionOf(new Vector3(-0.5f, 0.0f, -1.5f)));
            Assert.AreEqual(HitDirection.Right, HitReactions.DirectionOf(new Vector3(2.0f, 0.0f, 0.5f)));
            Assert.AreEqual(HitDirection.Left, HitReactions.DirectionOf(new Vector3(-2.0f, 0.0f, -0.5f)));
        }

        [Test]
        public void ABlowFromTheFrontThrowsTheTorsoBackHoldsThenRecovers()
        {
            FlinchFrame thrown = HitReactions.Evaluate(HitDirection.Front, false, HitReactions.SnapEnd);
            Assert.Less(thrown.LeanDegrees, -10.0f, "the torso should be thrown back (a negative lean)");
            Assert.AreEqual(0.0f, thrown.TiltRightDegrees, 0.001f);
            Assert.Greater(thrown.HeadDegrees, 0.0f, "the head whips further than the torso");

            FlinchFrame held = HitReactions.Evaluate(HitDirection.Front, false, (HitReactions.SnapEnd + HitReactions.HoldEnd) * 0.5f);
            Assert.AreEqual(thrown.LeanDegrees, held.LeanDegrees, 0.001f, "the recoil should hold still after the snap");

            FlinchFrame end = HitReactions.Evaluate(HitDirection.Front, false, 1.0f);
            Assert.AreEqual(0.0f, end.LeanDegrees, 0.001f, "the recoil should be gone by the end");
            Assert.AreEqual(0.0f, end.HeadDegrees, 0.001f);

            FlinchFrame start = HitReactions.Evaluate(HitDirection.Front, false, 0.0f);
            Assert.AreEqual(0.0f, start.LeanDegrees, 0.001f, "nothing has happened at t=0");
        }

        [Test]
        public void ABlowFromBehindFoldsForwardAndFromTheSidesTipsAway()
        {
            Assert.Greater(HitReactions.Evaluate(HitDirection.Back, false, 0.25f).LeanDegrees, 10.0f, "a blow from behind folds the body forward");
            Assert.Greater(HitReactions.Evaluate(HitDirection.Left, false, 0.25f).TiltRightDegrees, 10.0f, "a blow from the left tips the body right");
            Assert.Less(HitReactions.Evaluate(HitDirection.Right, false, 0.25f).TiltRightDegrees, -10.0f, "a blow from the right tips the body left");
        }

        [Test]
        public void AStaggerIsBiggerAndLongerThanAFlinch()
        {
            Assert.Greater(HitReactions.Seconds(true), HitReactions.Seconds(false));
            Assert.Greater(HitReactions.Amplitude(true), HitReactions.Amplitude(false) * 1.5f);
            Assert.Less(HitReactions.Evaluate(HitDirection.Front, true, 0.25f).LeanDegrees, HitReactions.Evaluate(HitDirection.Front, false, 0.25f).LeanDegrees);
        }
    }
}
