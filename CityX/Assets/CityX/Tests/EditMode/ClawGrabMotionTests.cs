using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Crimson Claw's one-handed grab, from its swipe's catch (owner, 2026-11-08: "i wanted it to be a 1 handed grab... you can use the swing
    /// attack animation but he just grabs his and the raises his arm and throws him"): it runs on from wherever the swipe had every part, with
    /// nothing jumping or stopping dead, raises the arm high, rears back, flings forward, and comes to rest.
    /// </summary>
    public sealed class ClawGrabMotionTests
    {
        private const float Step = 0.001f;
        private const float Reared = ClawGrabMotion.LiftSeconds + ClawGrabMotion.RearSeconds;

        /// <summary>The swipe's pose at its middle, where a catch is likeliest.</summary>
        private static ClawStrikeMotion.Pose Caught() =>
            ClawStrikeMotion.At(CrimsonClaw.WindupSeconds + CrimsonClaw.TellSeconds + CrimsonClaw.SwingSeconds * 0.5f);

        private static Vector3[] Parts(ClawStrikeMotion.Pose pose) =>
            new[] { pose.StrikeTurn, pose.StrikeShift * 100.0f, pose.OtherTurn, pose.OtherShift * 100.0f, pose.BodyTurn, pose.BodyShift * 100.0f };

        [Test]
        public void ItRunsOnFromWhereTheSwipeCaughtAndEndsAtRest()
        {
            var grab = new ClawGrabMotion(Caught());
            Vector3[] start = Parts(grab.At(0.0f));
            Vector3[] caught = Parts(Caught());
            for (int i = 0; i < start.Length; i++)
            {
                Assert.Less((start[i] - caught[i]).magnitude, 1e-4f, $"part {i} jumped away from where the swipe had it");
            }
            foreach (Vector3 part in Parts(grab.At(ClawGrabMotion.Seconds)))
            {
                Assert.Less(part.magnitude, 1e-4f, "not at rest at the end");
            }
        }

        [Test]
        public void NoPartJumpsOrStopsDead()
        {
            var grab = new ClawGrabMotion(Caught());
            Vector3[] before = Parts(grab.At(0.0f));
            Vector3[] speedBefore = new Vector3[before.Length];
            for (float t = Step; t <= ClawGrabMotion.Seconds + Step; t += Step)
            {
                Vector3[] now = Parts(grab.At(t));
                for (int i = 0; i < now.Length; i++)
                {
                    Vector3 speed = (now[i] - before[i]) / Step;
                    Assert.Less(speed.magnitude * Step, 1.2f, $"part {i} jumped at {t:0.000} s");
                    if (t > Step)
                    {
                        Assert.Less((speed - speedBefore[i]).magnitude, 60.0f, $"part {i} changed speed all at once at {t:0.000} s");
                    }
                    speedBefore[i] = speed;
                }
                before = now;
            }
        }

        /// <summary>"raises his arm": the striking claw lifted high, little tipped (the fist stays upright round what it holds), then drawn back.</summary>
        [Test]
        public void ItRaisesItsArmThenRearsBack()
        {
            var grab = new ClawGrabMotion(Caught());
            ClawStrikeMotion.Pose raised = grab.At(ClawGrabMotion.LiftSeconds);
            ClawStrikeMotion.Pose reared = grab.At(Reared);
            Assert.Greater(raised.StrikeShift.y, 0.4f, "the arm is not raised");
            Assert.Less(Mathf.Abs(raised.StrikeTurn.x), 20.0f, "the fist is tipped over, not held upright");
            Assert.Less(reared.StrikeTurn.x, raised.StrikeTurn.x, "the arm does not draw back further to throw");
            Assert.Less(reared.BodyTurn.x, -5.0f, "the head does not rear back with the arm");
        }

        [Test]
        public void ItThrowsForwardAndDown()
        {
            var grab = new ClawGrabMotion(Caught());
            ClawStrikeMotion.Pose reared = grab.At(Reared);
            ClawStrikeMotion.Pose flung = grab.At(Reared + ClawGrabMotion.ThrowSeconds);
            Assert.Greater(flung.StrikeTurn.x, 0.0f, "the claw does not come down past level");
            Assert.Greater(flung.StrikeShift.z, reared.StrikeShift.z + 0.2f, "the claw does not fling forward");
            Assert.Greater(flung.BodyTurn.x, 5.0f, "the head does not lunge into the throw");
            Assert.That(ClawGrabMotion.LetGoSeconds, Is.GreaterThan(Reared).And.LessThan(Reared + ClawGrabMotion.ThrowSeconds),
                "it lets go outside its throw");
        }
    }
}
