using CityX.Character;
using CityX.World;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Pedestrians in the rain: they hurry, and when it is really falling on
    /// them they head for cover and wait it out (Breath of the Wild's villagers
    /// "run for shelter"). Stepped with a fake sense of the weather -- and with
    /// NONE, where the brain must behave exactly as it always did.
    /// </summary>
    public sealed class CivilianBrainRainTests
    {
        /// <summary>A weather where a chosen predicate says which points are under a roof (nowhere, by default).</summary>
        private sealed class FakeWeather : IRainSense
        {
            public float Rain;
            public System.Func<Vector3, bool> Covered = p => false;

            public float Precipitation => Rain;

            public float Shelter(Vector3 worldPoint) => Covered(worldPoint) ? 1.0f : 0.0f;

            public float RainOn(Vector3 worldPoint) => Rain * (1.0f - Shelter(worldPoint));
        }

        private static Vector3 Run(CivilianBrain brain, Vector3 start, float seconds, out Vector3 last)
        {
            Vector3 position = start;
            last = Vector3.zero;
            for (float t = 0.0f; t < seconds; t += 0.05f)
            {
                Vector3 direction = brain.Tick(position, 0.05f);
                if (direction.sqrMagnitude > 0.0f)
                {
                    position += direction * CivilianBrain.WalkSpeed * brain.SpeedMultiplier * 0.05f;
                    last = direction;
                }
            }
            return position;
        }

        [Test]
        public void WithNoWeatherSenseThePedestrianIsExactlyWhatItAlwaysWas()
        {
            var plain = new CivilianBrain(1234);
            var withNoSense = new CivilianBrain(1234);
            withNoSense.SetRainSense(null);
            Vector3 a = new Vector3(4.0f, 0.0f, 4.0f), b = a;

            for (int i = 0; i < 2000; i++)
            {
                Vector3 da = plain.Tick(a, 0.05f);
                Vector3 db = withNoSense.Tick(b, 0.05f);
                Assert.AreEqual(da, db, $"step {i}");
                a += da * 0.05f * 1.5f;
                b += db * 0.05f * 1.5f;
            }
            Assert.AreEqual(1.0f, plain.SpeedMultiplier);
        }

        [Test]
        public void ADryDayLeavesTheWeatherAwarePedestrianWalkingAtNormalSpeed()
        {
            var brain = new CivilianBrain(5);
            brain.SetRainSense(new FakeWeather { Rain = 0.0f });
            brain.Tick(Vector3.zero, 0.05f);

            Assert.AreEqual(1.0f, brain.SpeedMultiplier);
            Assert.IsFalse(brain.IsSeekingShelter);
            Assert.IsFalse(brain.IsWaitingOutRain);
        }

        [Test]
        public void RainFallingOnAPedestrianMakesThemHurry()
        {
            var weather = new FakeWeather { Rain = 1.0f };
            var brain = new CivilianBrain(5);
            brain.SetRainSense(weather);
            brain.Tick(Vector3.zero, 0.05f);

            Assert.AreEqual(Weather.PedestrianHurry(1.0f), brain.SpeedMultiplier, 1e-5f);
            Assert.Greater(brain.SpeedMultiplier, 1.4f);
        }

        [Test]
        public void ADrizzleDoesNotSendAnyoneLookingForCover()
        {
            // Cover exists a few metres away, but a drizzle is not worth walking to it for.
            var weather = new FakeWeather { Rain = Weather.LightRain, Covered = p => p.magnitude > 12.0f };
            var brain = new CivilianBrain(9);
            brain.SetRainSense(weather);

            for (int i = 0; i < 600; i++)
            {
                brain.Tick(Vector3.zero, 0.05f);
                Assert.IsFalse(brain.IsSeekingShelter, "a drizzle was enough to make someone run for cover");
            }
        }

        [Test]
        public void ADownpourSendsAnExposedPedestrianToTheNearestCoverAndTheyWaitThereWhileItLasts()
        {
            // Everything more than 12m from where they start is under a roof; the
            // spot they start on is open. (Which pavement spots the street network
            // offers as candidates is the network's business -- any of them past 12m
            // counts.)
            var weather = new FakeWeather { Rain = 1.0f, Covered = p => p.magnitude > 12.0f };
            var brain = new CivilianBrain(21);
            brain.SetRainSense(weather);

            Vector3 start = Vector3.zero;
            Vector3 position = start;
            bool sawSeeking = false;
            for (float t = 0.0f; t < 120.0f; t += 0.05f)
            {
                Vector3 direction = brain.Tick(position, 0.05f);
                if (brain.IsSeekingShelter)
                {
                    sawSeeking = true;
                }
                if (direction.sqrMagnitude > 0.0f)
                {
                    position += direction * CivilianBrain.WalkSpeed * brain.SpeedMultiplier * 0.05f;
                }
            }

            Assert.IsTrue(sawSeeking, "an exposed pedestrian in a downpour never went looking for cover");
            Assert.GreaterOrEqual(weather.Shelter(position), 0.9f, "after two minutes of downpour they are not under cover");
            Assert.IsTrue(brain.IsWaitingOutRain, "and are standing there waiting it out");
        }

        [Test]
        public void WhenTheRainStopsTheyLeaveCoverAndWanderAgain()
        {
            var weather = new FakeWeather { Rain = 1.0f, Covered = p => true };
            var brain = new CivilianBrain(3);
            brain.SetRainSense(weather);

            Vector3 position = Vector3.zero;
            for (float t = 0.0f; t < 20.0f; t += 0.05f)
            {
                brain.Tick(position, 0.05f);
            }
            Assert.IsTrue(brain.IsWaitingOutRain, "setup: they should be sheltering while it pours");

            weather.Rain = 0.0f;
            bool walked = false;
            for (float t = 0.0f; t < 30.0f; t += 0.05f)
            {
                Vector3 direction = brain.Tick(position, 0.05f);
                if (direction.sqrMagnitude > 0.0f)
                {
                    walked = true;
                    position += direction * CivilianBrain.WalkSpeed * 0.05f;
                }
            }
            Assert.IsTrue(walked, "the rain stopped and they never set off again");
            Assert.IsFalse(brain.IsWaitingOutRain);
        }

        [Test]
        public void IfThereIsNoCoverAnywhereTheyKeepWalkingHurriedAndNeverFreeze()
        {
            var brain = new CivilianBrain(17);
            brain.SetRainSense(new FakeWeather { Rain = 1.0f });   // nowhere covered

            Vector3 last;
            Vector3 end = Run(brain, new Vector3(3.0f, 0.0f, 3.0f), 60.0f, out last);

            Assert.Greater((end - new Vector3(3.0f, 0.0f, 3.0f)).magnitude, 5.0f, "with nowhere to shelter a pedestrian should still get on with it");
            Assert.IsFalse(brain.IsWaitingOutRain);
        }
    }
}
