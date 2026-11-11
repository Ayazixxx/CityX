using CityX.UI;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>The logo's failing neon (<see cref="NeonFlicker"/>): mostly lit and humming, faltering every few seconds, the same sign every time.</summary>
    public sealed class NeonFlickerTests
    {
        private const float Step = 1.0f / 120.0f;

        [Test]
        public void ItIsMostlyLitAndNeverPastFullOrBelowDark()
        {
            var flicker = new NeonFlicker(3);
            int lit = 0, samples = 0;
            for (float time = 0.0f; time < 120.0f; time += Step, samples++)
            {
                float brightness = flicker.Brightness(time);
                Assert.That(brightness, Is.InRange(0.0f, 1.0f));
                if (brightness >= 1.0f - NeonFlicker.Hum - 1e-4f)
                {
                    lit++;
                }
            }
            Assert.Greater(lit, samples * 0.85f, "a sign that is out more than a moment at a time reads as broken, not failing");
        }

        /// <summary>It falters within every stretch a little longer than the longest lit one.</summary>
        [Test]
        public void ItFaltersEveryFewSeconds()
        {
            var flicker = new NeonFlicker(11);
            float lastDark = 0.0f;
            for (float time = 0.0f; time < 120.0f; time += Step)
            {
                if (flicker.Brightness(time) < 0.5f)
                {
                    lastDark = time;
                }
                Assert.Less(time - lastDark, NeonFlicker.SteadyMax + NeonFlicker.BrownOutMax + 1.0f, $"lit unbroken for too long at {time:0.0} s");
            }
        }

        /// <summary>The faults are counted as each begins, once a fault (its flicker sound plays once, not for each of its flashes).</summary>
        [Test]
        public void EachFaultIsCountedOnceAsItBegins()
        {
            var flicker = new NeonFlicker(7);
            int counted = 0, darkenings = 0;
            bool wasDark = false, darkSinceCount = false;
            for (float time = 0.0f; time < 180.0f; time += Step)
            {
                bool dark = flicker.Brightness(time) < 0.5f;
                if (flicker.FaultsBegun > counted)
                {
                    Assert.AreEqual(counted + 1, flicker.FaultsBegun, $"two faults began at once at {time:0.00} s");
                    counted = flicker.FaultsBegun;
                    darkSinceCount = false;
                }
                if (dark && !wasDark && !darkSinceCount)
                {
                    darkenings++;
                    darkSinceCount = true;
                }
                wasDark = dark;
            }
            Assert.Greater(counted, 5, "three minutes and hardly a fault");
            Assert.AreEqual(counted, darkenings, 1, "a fault counted where the sign did not falter, or one it did missed");
        }

        [Test]
        public void TheSameSeedIsTheSameSignAndAskingAgainFromTheStartGivesTheSame()
        {
            var a = new NeonFlicker(5);
            var b = new NeonFlicker(5);
            var other = new NeonFlicker(6);
            var first = new float[3000];
            bool differs = false;
            for (int i = 0; i < first.Length; i++)
            {
                first[i] = a.Brightness(i * 0.02f);
                Assert.AreEqual(first[i], b.Brightness(i * 0.02f), 1e-6f);
                differs |= System.Math.Abs(first[i] - other.Brightness(i * 0.02f)) > 0.1f;
            }
            Assert.IsTrue(differs, "two signs flicker as one");
            for (int i = 0; i < first.Length; i += 7)
            {
                Assert.AreEqual(first[i], a.Brightness(i * 0.02f), 1e-6f, "going back in time changed the sign");
            }
        }
    }
}
