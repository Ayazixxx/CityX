using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Crimson Claw's swipe as one flowing motion (owner, 2026-11-07: "make it look more fluid less robotic"): from rest and back to it,
    /// no part jumping or stopping dead between the phases, the claw still drawn back through its tell, the blow carrying past where it aims,
    /// the body turning and lunging into it, and the other hand the mirror of it.
    /// </summary>
    public sealed class ClawStrikeMotionTests
    {
        private const float Step = 0.001f;

        private static Vector3[] Parts(ClawStrikeMotion.Pose pose) =>
            new[] { pose.StrikeTurn, pose.StrikeShift * 100.0f, pose.OtherTurn, pose.OtherShift * 100.0f, pose.BodyTurn, pose.BodyShift * 100.0f };

        [Test]
        public void ItStartsAndEndsAtRest()
        {
            foreach (float t in new[] { -1.0f, 0.0f, CrimsonClaw.AttackSeconds, CrimsonClaw.AttackSeconds + 1.0f })
            {
                foreach (Vector3 part in Parts(ClawStrikeMotion.At(t)))
                {
                    Assert.Less(part.magnitude, 1e-4f, $"not at rest at {t:0.00} s");
                }
            }
        }

        /// <summary>
        /// Fluid: no part moves further in a millisecond than its fastest swipe allows, and none changes speed all at once (the old swipe
        /// eased each phase on its own, so the claw stopped dead at the end of each and jumped into the next at full speed).
        /// </summary>
        [Test]
        public void NoPartJumpsOrStopsDead()
        {
            Vector3[] before = Parts(ClawStrikeMotion.At(0.0f));
            Vector3[] speedBefore = new Vector3[before.Length];
            for (float t = Step; t <= CrimsonClaw.AttackSeconds + Step; t += Step)
            {
                Vector3[] now = Parts(ClawStrikeMotion.At(t));
                for (int i = 0; i < now.Length; i++)
                {
                    Vector3 speed = (now[i] - before[i]) / Step;
                    Assert.Less(speed.magnitude * Step, 1.2f, $"part {i} jumped at {t:0.000} s");
                    Assert.Less((speed - speedBefore[i]).magnitude, 60.0f, $"part {i} changed speed all at once at {t:0.000} s");
                    speedBefore[i] = speed;
                }
                before = now;
            }
        }

        [Test]
        public void ThroughItsTellTheClawStaysDrawnBack()
        {
            for (float t = CrimsonClaw.WindupSeconds; t <= CrimsonClaw.WindupSeconds + CrimsonClaw.TellSeconds; t += Step)
            {
                Assert.LessOrEqual(ClawStrikeMotion.At(t).StrikeTurn.y, -CrimsonClaw.WindupAngleDegrees * 0.9f, $"on its way at {t:0.000} s");
            }
        }

        [Test]
        public void TheBlowCarriesPastWhereItAimsAndSettles()
        {
            float swipeEnd = CrimsonClaw.WindupSeconds + CrimsonClaw.TellSeconds + CrimsonClaw.SwingSeconds;
            Assert.GreaterOrEqual(ClawStrikeMotion.At(swipeEnd).StrikeTurn.y, CrimsonClaw.SwingAngleDegrees * 0.95f, "the swipe fell short");
            float furthest = 0.0f;
            for (float t = swipeEnd; t <= CrimsonClaw.AttackSeconds; t += Step)
            {
                furthest = Mathf.Max(furthest, ClawStrikeMotion.At(t).StrikeTurn.y);
            }
            Assert.Greater(furthest, CrimsonClaw.SwingAngleDegrees + 3.0f, "no follow-through");
            Assert.Less(furthest, CrimsonClaw.SwingAngleDegrees + 20.0f, "it flung far past its aim");
        }

        [Test]
        public void TheBodyTurnsBackThenLungesIntoTheBlow()
        {
            ClawStrikeMotion.Pose coiled = ClawStrikeMotion.At(CrimsonClaw.WindupSeconds + CrimsonClaw.TellSeconds);
            ClawStrikeMotion.Pose struck = ClawStrikeMotion.At(CrimsonClaw.WindupSeconds + CrimsonClaw.TellSeconds + CrimsonClaw.SwingSeconds);
            Assert.Less(coiled.BodyTurn.y, -5.0f, "the body did not turn the striking side back");
            Assert.Less(coiled.BodyShift.z, 0.0f, "the body did not draw back");
            Assert.Greater(struck.BodyTurn.y, 5.0f, "the body did not turn into the blow");
            Assert.Greater(struck.BodyShift.z, 0.03f, "the body did not lunge after it");
        }

        [Test]
        public void TheOtherHandIsTheMirrorOfIt()
        {
            ClawStrikeMotion.Pose left = ClawStrikeMotion.At(0.6f);
            ClawStrikeMotion.Pose right = left.Mirrored();
            Assert.AreEqual(left.StrikeTurn.x, right.StrikeTurn.x, 1e-5f);
            Assert.AreEqual(-left.StrikeTurn.y, right.StrikeTurn.y, 1e-5f);
            Assert.AreEqual(-left.StrikeTurn.z, right.StrikeTurn.z, 1e-5f);
            Assert.AreEqual(-left.StrikeShift.x, right.StrikeShift.x, 1e-5f);
            Assert.AreEqual(left.StrikeShift.z, right.StrikeShift.z, 1e-5f);
            Assert.AreEqual(-left.BodyTurn.y, right.BodyTurn.y, 1e-5f);
        }
    }
}
