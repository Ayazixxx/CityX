using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The tutorial's steps in order (TutorialProgress, docs/TUTORIAL.md): a step done by doing passes when the world shows it done, early or
    /// not, one step at a time; a talk step only when its conversation ends; and every step on the way has an objective to show.
    /// </summary>
    public sealed class TutorialProgressTests
    {
        private static TutorialProgress Accepted()
        {
            var progress = new TutorialProgress();
            progress.Accept();
            return progress;
        }

        [Test]
        public void ItWaitsOnTheOfferUntilItIsTakenUp()
        {
            var progress = new TutorialProgress();
            progress.Observe(new TutorialFacts { Inside = true, HasWeapon = true, IntruderDown = true, PowerOn = true });
            progress.Talked();
            Assert.AreEqual(TutorialStep.Offered, progress.Step);
            Assert.IsFalse(progress.Running);
            progress.Accept();
            Assert.AreEqual(TutorialStep.GetIn, progress.Step);
            Assert.IsTrue(progress.Running);
        }

        [Test]
        public void TheWholeWayThroughInOrder()
        {
            TutorialProgress progress = Accepted();
            var facts = new TutorialFacts { StatPointsLeft = true, SkillPointsLeft = true };

            facts.Inside = true;
            progress.Observe(facts);
            Assert.AreEqual(TutorialStep.FindWeapon, progress.Step);
            facts.HasWeapon = true;
            progress.Observe(facts);
            Assert.AreEqual(TutorialStep.Intruder, progress.Step);
            facts.IntruderDown = true;
            progress.Observe(facts);
            Assert.AreEqual(TutorialStep.ReportBack, progress.Step);

            progress.Observe(facts);
            Assert.AreEqual(TutorialStep.ReportBack, progress.Step, "a talk step passed without a talk");
            progress.Talked();
            Assert.AreEqual(TutorialStep.Power, progress.Step);
            facts.PowerOn = true;
            progress.Observe(facts);
            Assert.AreEqual(TutorialStep.BackToOldGuy, progress.Step);
            progress.Talked();
            Assert.AreEqual(TutorialStep.Stats, progress.Step);
            progress.Observe(facts);
            Assert.AreEqual(TutorialStep.Stats, progress.Step, "the stat points were not spent");
            facts.StatPointsLeft = false;
            progress.Observe(facts);
            Assert.AreEqual(TutorialStep.Skill, progress.Step);
            facts.SkillPointsLeft = false;
            progress.Observe(facts);
            Assert.AreEqual(TutorialStep.LastTalk, progress.Step);
            progress.Talked();
            Assert.AreEqual(TutorialStep.Ambush, progress.Step);
            progress.Observe(facts);
            Assert.AreEqual(TutorialStep.Ambush, progress.Step);
            progress.Finish();
            Assert.AreEqual(TutorialStep.Done, progress.Step);
            Assert.IsFalse(progress.Running);
        }

        [Test]
        public void WhatWasDoneEarlyIsPassedOneStepAtATime()
        {
            TutorialProgress progress = Accepted();
            var everything = new TutorialFacts { Inside = true, HasWeapon = true, IntruderDown = true, PowerOn = true };
            progress.Observe(everything);
            Assert.AreEqual(TutorialStep.FindWeapon, progress.Step);
            progress.Observe(everything);
            Assert.AreEqual(TutorialStep.Intruder, progress.Step);
            progress.Observe(everything);
            Assert.AreEqual(TutorialStep.ReportBack, progress.Step);
            progress.Talked();
            progress.Observe(everything);
            Assert.AreEqual(TutorialStep.BackToOldGuy, progress.Step, "the power turned on early did not pass its step");
        }

        [Test]
        public void BeatingTheIntruderBarehandedPassesGettingInAndArming()
        {
            TutorialProgress progress = Accepted();
            var facts = new TutorialFacts { IntruderDown = true };
            for (int i = 0; i < 3; i++)
            {
                progress.Observe(facts);
            }
            Assert.AreEqual(TutorialStep.ReportBack, progress.Step);
        }

        [Test]
        public void TalkingOutsideATalkStepPassesNothing()
        {
            TutorialProgress progress = Accepted();
            progress.Talked();
            Assert.AreEqual(TutorialStep.GetIn, progress.Step);
            progress.Finish();
            Assert.AreEqual(TutorialStep.GetIn, progress.Step, "the tutorial finished before its ending");
        }

        [Test]
        public void EachChangeIsAnnouncedOnce()
        {
            TutorialProgress progress = Accepted();
            int heard = 0;
            TutorialStep last = TutorialStep.Offered;
            progress.StepChanged += step => { heard++; last = step; };
            progress.Observe(new TutorialFacts { Inside = true });
            progress.Observe(new TutorialFacts { Inside = true });
            progress.JumpTo(TutorialStep.Stats);
            progress.JumpTo(TutorialStep.Stats);
            Assert.AreEqual(2, heard);
            Assert.AreEqual(TutorialStep.Stats, last);
        }

        [Test]
        public void EveryStepOnTheWayHasAnObjectiveAndAHint()
        {
            for (TutorialStep step = TutorialStep.GetIn; step < TutorialStep.Ambush; step++)
            {
                Assert.IsNotEmpty(TutorialScript.Objective(step), $"{step} has no objective");
                Assert.IsNotEmpty(TutorialScript.Hint(step), $"{step} has no hint");
            }
            Assert.IsEmpty(TutorialScript.Objective(TutorialStep.Offered));
            Assert.IsEmpty(TutorialScript.Objective(TutorialStep.Ambush), "an objective stays up through the ending");
            Assert.IsEmpty(TutorialScript.Objective(TutorialStep.Done));
        }
    }
}
