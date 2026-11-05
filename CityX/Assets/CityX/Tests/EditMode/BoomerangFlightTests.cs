using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>A thrown boomerang's path, as Terraria's (docs/BOOMERANG.md).</summary>
    public sealed class BoomerangFlightTests
    {
        private const float Step = 1.0f / 60.0f;
        private static readonly Vector3 Home = new Vector3(0.0f, 1.5f, 0.0f);

        private static BoomerangFlight Thrown() => new BoomerangFlight(Home, Vector3.forward, BoomerangRules.OutSpeed);

        [Test]
        public void ItFliesStraightWithNoGravityForHalfASecondThenTurnsForHome()
        {
            BoomerangFlight flight = Thrown();
            for (float t = 0.0f; t < BoomerangRules.OutSeconds - 2.0f * Step; t += Step)
            {
                flight.Step(Step, Home);
            }
            Assert.IsFalse(flight.Returning, "it turned early");
            Assert.AreEqual(Home.y, flight.Position.y, 1e-4f, "it fell");
            Assert.AreEqual(0.0f, flight.Position.x, 1e-4f, "it curved");
            Assert.AreEqual(BoomerangRules.OutSeconds * BoomerangRules.OutSpeed, flight.Position.z, 0.7f);

            flight.Step(3.0f * Step, Home);
            Assert.IsTrue(flight.Returning, "half a second on and it has not turned");
        }

        [Test]
        public void AThrowGoesItsRangeAndComesHomeInAboutASecondAndAHalf()
        {
            BoomerangFlight flight = Thrown();
            float furthest = 0.0f, seconds = 0.0f;
            while (!flight.Arrived(Home) && seconds < 5.0f)
            {
                flight.Step(Step, Home);
                seconds += Step;
                furthest = Mathf.Max(furthest, flight.Position.z);
            }
            Assert.IsTrue(flight.Arrived(Home), "it never came home");
            Assert.AreEqual(BoomerangRules.Range, furthest, 0.6f, "not Terraria's 13.6 tiles");
            Assert.AreEqual(12.0f, BoomerangRules.Range, 0.5f);
            Assert.Less(seconds, 2.0f, "it took too long");
            Assert.Greater(seconds, 1.0f, "it was too quick");
        }

        [Test]
        public void ItNeverGoesFasterThanItsTopSpeedComingBack()
        {
            BoomerangFlight flight = Thrown();
            for (float t = 0.0f; t < 1.5f && !flight.Arrived(Home); t += Step)
            {
                flight.Step(Step, Home);
                Assert.LessOrEqual(flight.Velocity.magnitude, BoomerangRules.BackTopSpeed + 1e-3f);
            }
        }

        [Test]
        public void MeetingSomethingGoingOutTurnsItStraightBack()
        {
            BoomerangFlight flight = Thrown();
            flight.Step(0.2f, Home);
            Vector3 was = flight.Velocity;
            flight.TurnBack();
            Assert.IsTrue(flight.Returning);
            Assert.AreEqual(-was.z, flight.Velocity.z, 1e-4f, "it did not turn straight back");

            Vector3 once = flight.Velocity;
            flight.TurnBack();
            Assert.AreEqual(once, flight.Velocity, "a second touch on its way home turned it again");
        }

        [Test]
        public void ItFollowsTheThrowerWhereverTheyGo()
        {
            BoomerangFlight flight = Thrown();
            var moved = new Vector3(40.0f, 1.5f, -10.0f);
            float seconds = 0.0f;
            while (!flight.Arrived(moved) && seconds < 8.0f)
            {
                flight.Step(Step, moved);
                seconds += Step;
            }
            Assert.IsTrue(flight.Arrived(moved), "it did not find the thrower where they went");
        }

        [Test]
        public void ItIsOnlyCaughtOnItsWayBack()
        {
            BoomerangFlight flight = Thrown();
            Assert.IsFalse(flight.Arrived(Home), "caught as it left the hand");
        }

        [Test]
        public void ItIsLostAfterAMinuteOrFarAway()
        {
            BoomerangFlight flight = Thrown();
            Assert.IsTrue(flight.Lost(Home + Vector3.right * (BoomerangRules.LostMetres + 1.0f)), "not lost far from the thrower");
            Assert.IsFalse(flight.Lost(Home));
            flight.Step(BoomerangRules.LifeSeconds, Home);
            Assert.IsTrue(flight.Lost(flight.Position), "not lost after a minute");
        }
    }
}
