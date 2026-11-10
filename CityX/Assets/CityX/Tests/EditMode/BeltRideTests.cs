using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The ride on the conveyor belt a character is made on (BeltRide, docs/BELT_INTRO.md; owner, 2026-11-10: "when you hold right, it
    /// rolls out a plain white playermodel from the left of the screen ... once you have rolled him to the center of the screen the
    /// conveyer belt stops ... once you complete your character. the conveyer belt start moving to the right again ... until it rolls
    /// your character off the conveyer belt").
    /// </summary>
    public sealed class BeltRideTests
    {
        private const float Start = -6.5f;
        private const float Cut = 6.5f;
        private const float End = 12.5f;
        private const float Frame = 1.0f / 60.0f;

        private static BeltRide Ride() => new BeltRide(Start, Cut, End);

        /// <summary>Steps until the stage changes or <paramref name="seconds"/> pass; the seconds it took.</summary>
        private static float Until(BeltRide ride, float push, float seconds)
        {
            BeltRide.Stage from = ride.Now;
            float elapsed = 0.0f;
            while (ride.Now == from && elapsed < seconds)
            {
                ride.Step(push, Frame);
                elapsed += Frame;
            }
            return elapsed;
        }

        [Test]
        public void TheBeltStandsEmptyAndStillUntilAKeyIsHeld()
        {
            BeltRide ride = Ride();
            Until(ride, 0.0f, 3.0f);
            Assert.AreEqual(BeltRide.Stage.RollingIn, ride.Now);
            Assert.AreEqual(Start, ride.BodyAt, 1e-4f, "the body came in by itself");
            Assert.AreEqual(0.0f, ride.Travel, 1e-4f, "the belt ran by itself");
        }

        [Test]
        public void HoldingRightRollsTheBodyInAndSetsItDownInTheMiddle()
        {
            BeltRide ride = Ride();
            float fastest = 0.0f, last = ride.BodyAt;
            float took = 0.0f;
            while (ride.Now == BeltRide.Stage.RollingIn && took < 20.0f)
            {
                ride.Step(1.0f, Frame);
                took += Frame;
                Assert.GreaterOrEqual(ride.BodyAt, last - 1e-5f, "the body went back while right was held");
                Assert.LessOrEqual(ride.BodyAt, 1e-4f, "the body was carried past the middle");
                fastest = Mathf.Max(fastest, ride.BeltSpeed);
                last = ride.BodyAt;
            }
            Assert.AreEqual(BeltRide.Stage.Making, ride.Now, "it never arrived");
            Assert.AreEqual(0.0f, ride.BodyAt, 1e-4f, "it was not set down in the middle");
            Assert.AreEqual(0.0f, ride.BeltSpeed, 1e-4f, "the belt did not stop with it");
            Assert.AreEqual(BeltRide.Speed, fastest, 0.05f, "the belt never reached its full run");
            Assert.That(took, Is.InRange(2.0f, 6.0f), "rolling in takes a moment, not an age");
            Assert.AreEqual(-Start, ride.Travel, 0.01f, "the belt ran further or less far than it carried the body");
        }

        [Test]
        public void LettingGoStopsTheBeltWithTheBodyWhereItIs()
        {
            BeltRide ride = Ride();
            Until(ride, 1.0f, 1.0f);
            float moving = ride.BodyAt;
            Assert.Greater(moving, Start + 0.5f);
            for (int i = 0; i < 120; i++)
            {
                ride.Step(0.0f, Frame);
            }
            Assert.AreEqual(0.0f, ride.BeltSpeed, 1e-4f, "the belt runs on with no key held");
            float stopped = ride.BodyAt;
            Assert.That(stopped - moving, Is.InRange(0.0f, 1.0f), "it stops in a stride or so");
            for (int i = 0; i < 60; i++)
            {
                ride.Step(0.0f, Frame);
            }
            Assert.AreEqual(stopped, ride.BodyAt, 1e-5f);
        }

        [Test]
        public void HoldingLeftRunsTheBeltBackAndTheBodyNeverPastWhereItWaited()
        {
            BeltRide ride = Ride();
            Until(ride, 1.0f, 1.2f);
            float travelled = ride.Travel;
            for (int i = 0; i < 600; i++)
            {
                ride.Step(-1.0f, Frame);
            }
            Assert.Less(ride.Travel, travelled, "the belt did not run back");
            Assert.Less(ride.BeltSpeed, 0.0f);
            Assert.AreEqual(Start, ride.BodyAt, 1e-4f, "the body was carried off beyond where it waited, or not back to it");
            Assert.AreEqual(BeltRide.Stage.RollingIn, ride.Now);
        }

        [Test]
        public void InTheMiddleTheKeysDoNothingUntilTheCharacterIsFinished()
        {
            BeltRide ride = Ride();
            Until(ride, 1.0f, 20.0f);
            for (int i = 0; i < 120; i++)
            {
                ride.Step(i < 60 ? 1.0f : -1.0f, Frame);
            }
            Assert.AreEqual(BeltRide.Stage.Making, ride.Now);
            Assert.AreEqual(0.0f, ride.BodyAt, 1e-5f, "a key moved the body while it was being made");
            Assert.AreEqual(0.0f, ride.BeltSpeed, 1e-5f);
        }

        /// <summary>Owner, 2026-11-10: "make this noise play when going right or left on the conveyer belt, and at the end when the
        /// conveyer belt rolls your character off".</summary>
        [Test]
        public void TheBeltIsHeardRunningEitherWayAndNotStandingStill()
        {
            BeltRide ride = Ride();
            Assert.AreEqual(0.0f, ride.Running, "an empty belt nobody runs is heard");
            for (int i = 0; i < 60; i++)
            {
                ride.Step(1.0f, Frame);
            }
            Assert.AreEqual(1.0f, ride.Running, 1e-4f, "run right, the belt is not heard at its full run");
            for (int i = 0; i < 120; i++)
            {
                ride.Step(-1.0f, Frame);
            }
            Assert.Less(ride.BeltSpeed, 0.0f);
            Assert.AreEqual(1.0f, ride.Running, 1e-4f, "run left, the belt is not heard at its full run");
            for (int i = 0; i < 60; i++)
            {
                ride.Step(0.0f, Frame);
            }
            Assert.AreEqual(0.0f, ride.Running, "let go, the belt is still heard");

            Until(ride, 1.0f, 20.0f);
            Assert.AreEqual(BeltRide.Stage.Making, ride.Now);
            Assert.AreEqual(0.0f, ride.Running, "the belt is heard while the character is made");
            ride.Finish();
            Until(ride, 0.0f, 20.0f);
            Until(ride, 0.0f, 20.0f);
            Assert.AreEqual(BeltRide.Stage.Fallen, ride.Now);
            Assert.AreEqual(1.0f, ride.Running, 1e-4f, "the belt is not heard as it rolls the body off its end");
        }

        [Test]
        public void FinishedItIsCarriedOffPastTheEdgeAndOffTheEndWithNoKeyHeld()
        {
            BeltRide ride = Ride();
            ride.Finish();
            Assert.AreEqual(BeltRide.Stage.RollingIn, ride.Now, "a body still off the screen was carried off as finished");
            Until(ride, 1.0f, 20.0f);
            ride.Finish();
            Assert.AreEqual(BeltRide.Stage.RollingOut, ride.Now);

            float toTheEdge = Until(ride, 0.0f, 20.0f);
            Assert.AreEqual(BeltRide.Stage.OnTheEdge, ride.Now, "the view never changed");
            Assert.GreaterOrEqual(ride.BodyAt, Cut);
            Assert.That(toTheEdge, Is.InRange(2.0f, 5.0f));

            float toTheEnd = Until(ride, 0.0f, 20.0f);
            Assert.AreEqual(BeltRide.Stage.Fallen, ride.Now, "it never reached the belt's end");
            Assert.AreEqual(End, ride.BodyAt, BeltRide.Speed * Frame + 1e-4f, "it was let go somewhere other than the end");
            Assert.AreEqual((End - Cut) / BeltRide.Speed, toTheEnd, 0.1f, "the last ride is at the belt's full run");

            float let = ride.BodyAt, travelled = ride.Travel;
            for (int i = 0; i < 60; i++)
            {
                ride.Step(0.0f, Frame);
            }
            Assert.AreEqual(let, ride.BodyAt, 1e-5f, "the belt still carries a body that has fallen off it");
            Assert.Greater(ride.Travel, travelled + 1.0f, "the belt stopped when the body fell");
        }
    }
}
