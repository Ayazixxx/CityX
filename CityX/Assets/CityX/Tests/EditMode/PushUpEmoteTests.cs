using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>The /pushups emote's phases and its count (PushUpEmote): down, reps until called off, up, a rep's XP each.</summary>
    public sealed class PushUpEmoteTests
    {
        private const float Dt = 1.0f / 60.0f;

        private static int Run(PushUpEmote emote, float seconds, bool stop = false)
        {
            int reps = 0;
            for (float t = 0.0f; t < seconds - 1e-4f; t += Dt)
            {
                reps += emote.Tick(Dt, stop);
            }
            return reps;
        }

        [Test]
        public void ItGoesDownRepsOnRepeatAndCountsEveryOne()
        {
            var emote = new PushUpEmote(1.0f, 1.2f, 1.0f);
            Assert.IsFalse(emote.HoldsTheBody);
            Assert.IsTrue(emote.Start());
            Assert.AreEqual(PushUpEmote.Phase.GoingDown, emote.Current);
            Assert.IsTrue(emote.AnimatorPushingUp);
            Assert.AreEqual(1, emote.AnimatorPhase, "the Animator goes down");
            Assert.IsTrue(emote.HoldsTheBody, "the body is held from the start: no walking off mid-way down");

            Assert.AreEqual(0, Run(emote, 1.02f), "going down is no push-up");
            Assert.AreEqual(PushUpEmote.Phase.PushingUp, emote.Current);
            Assert.AreEqual(3, Run(emote, 3.65f), "a rep every 1.2 s");
            Assert.AreEqual(3, emote.Reps);
            Assert.IsFalse(emote.Start(), "a second /pushups mid-way changes nothing");
        }

        [Test]
        public void AKeyOrAClickGetsItUpAndOnlyThenTheBodyIsFree()
        {
            var emote = new PushUpEmote(1.0f, 1.2f, 0.8f);
            emote.Start();
            Run(emote, 2.5f);
            Assert.AreEqual(PushUpEmote.Phase.PushingUp, emote.Current);

            Assert.AreEqual(0, emote.Tick(Dt, true), "a rep cut short is not counted");
            Assert.AreEqual(PushUpEmote.Phase.GettingUp, emote.Current);
            Assert.IsFalse(emote.AnimatorPushingUp, "the Animator is told to get up");
            Assert.AreEqual(3, emote.AnimatorPhase);
            Assert.IsTrue(emote.HoldsTheBody, "still held while getting up");
            Run(emote, 0.75f);
            Assert.IsTrue(emote.HoldsTheBody);
            Run(emote, 0.1f);
            Assert.AreEqual(PushUpEmote.Phase.Off, emote.Current, "standing: free to move");
            Assert.IsFalse(emote.HoldsTheBody);
        }

        [Test]
        public void CalledOffGoingDownItFinishesGoingDownThenGetsUpWithNoRep()
        {
            var emote = new PushUpEmote(1.0f, 1.2f, 0.8f);
            emote.Start();
            emote.Tick(Dt, true);
            Assert.AreEqual(PushUpEmote.Phase.GoingDown, emote.Current, "no snapping back up halfway down");
            Assert.AreEqual(0, Run(emote, 1.0f));
            Assert.AreEqual(PushUpEmote.Phase.GettingUp, emote.Current);
            Assert.AreEqual(0, emote.Reps);
        }

        [Test]
        public void AbortStandsItUpAtOnce()
        {
            var emote = new PushUpEmote(1.0f, 1.2f, 0.8f);
            emote.Start();
            Run(emote, 2.0f);
            emote.Abort();
            Assert.AreEqual(PushUpEmote.Phase.Off, emote.Current);
            Assert.IsTrue(emote.Start(), "and it can start again");
        }
    }
}
