using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The carried one-handed sword's hand on its own (owner, 2026-10-17: "make it fluid motion no jumps too no weird bends").</summary>
    public sealed class CarryHandTests
    {
        private static readonly Vector3 BladeInHand = new Vector3(0.26f, 0.0f, 0.966f).normalized;

        [Test]
        public void TheNaturalHandRunsAlongTheForearmWithTheThumbForward()
        {
            Quaternion hand = CarryHand.Natural(Vector3.down, Vector3.forward);
            Assert.Less(Vector3.Angle(hand * Vector3.right, Vector3.down), 0.1f);
            Assert.Less(Vector3.Angle(hand * Vector3.forward, Vector3.forward), 0.1f);
        }

        [Test]
        public void TheWristNeverBendsMoreThanItsLimit()
        {
            for (int i = 0; i < 360; i += 5)
            {
                Vector3 fingers = Quaternion.AngleAxis(i * 0.25f - 45.0f, Vector3.right) * Vector3.down;   // the arm swung 45 degrees each way
                Quaternion natural = CarryHand.Natural(fingers, Vector3.forward);
                Quaternion hand = CarryHand.Turn(natural, BladeInHand, new Vector3(0.0f, -0.7f, 0.7f));
                Assert.LessOrEqual(Quaternion.Angle(natural, hand), CarryHand.MaxWristDegrees + 0.05f);
            }
        }

        [Test]
        public void ASwingingArmMovesTheHandSmoothlyWithNoJumps()
        {
            Vector3 wanted = new Vector3(0.15f, -0.55f, -0.82f);
            Quaternion last = Quaternion.identity;
            for (int i = 0; i <= 600; i++)
            {
                float swing = 50.0f * Mathf.Sin(i * 0.02f);
                Vector3 fingers = Quaternion.AngleAxis(swing, Vector3.right) * Vector3.down;
                Quaternion hand = CarryHand.Turn(CarryHand.Natural(fingers, Vector3.forward), BladeInHand, wanted);
                if (i > 0)
                {
                    // the arm turns at most 1 degree a step; the hand may not turn much faster than that
                    Assert.Less(Quaternion.Angle(last, hand), 2.5f, $"the hand jumps at step {i}");
                }
                last = hand;
            }
        }

        [Test]
        public void WithinReachTheBladePointsExactlyWhereTheCarryWantsIt()
        {
            Quaternion natural = CarryHand.Natural(Vector3.down, Vector3.forward);
            Vector3 wanted = Quaternion.AngleAxis(20.0f, Vector3.right) * (natural * BladeInHand);
            Quaternion hand = CarryHand.Turn(natural, BladeInHand, wanted);
            Assert.Less(Vector3.Angle(hand * BladeInHand, wanted), 0.1f);
        }
    }
}
