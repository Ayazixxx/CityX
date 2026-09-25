using CityX.View;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Synthesised thunder, checked by MEASURING it -- there is no ear here, so
    /// what these pin is the physics the sound is meant to have: a clip is
    /// finite, loud enough but never clipped, the same every time for a seed,
    /// and distance strips its high end, softens its attack and lengthens it.
    /// </summary>
    public sealed class ThunderSynthTests
    {
        /// <summary>Fraction of the clip's energy that is high-frequency, by the first difference -- a crude, monotone brightness measure.</summary>
        private static double Brightness(float[] samples)
        {
            double signal = 0.0, difference = 0.0;
            for (int i = 1; i < samples.Length; i++)
            {
                signal += samples[i] * (double)samples[i];
                double d = samples[i] - samples[i - 1];
                difference += d * d;
            }
            return signal > 0.0 ? difference / signal : 0.0;
        }

        private static int PeakIndex(float[] samples)
        {
            int best = 0;
            float peak = 0.0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = samples[i] < 0.0f ? -samples[i] : samples[i];
                if (a > peak)
                {
                    peak = a;
                    best = i;
                }
            }
            return best;
        }

        private static float Peak(float[] samples)
        {
            float peak = 0.0f;
            foreach (float s in samples)
            {
                float a = s < 0.0f ? -s : s;
                if (a > peak)
                {
                    peak = a;
                }
            }
            return peak;
        }

        [TestCase(120.0f)]
        [TestCase(500.0f)]
        [TestCase(1000.0f)]
        [TestCase(1800.0f)]
        public void EveryClipIsFiniteAndLoudButNeverClipped(float distance)
        {
            float[] clip = ThunderSynth.Generate(distance, 7);

            Assert.Greater(clip.Length, ThunderSynth.SampleRate * 3, "a strike rumbles for seconds");
            foreach (float sample in clip)
            {
                Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
            }
            Assert.AreEqual(ThunderSynth.PeakLevel, Peak(clip), 1e-3f, "normalised to the shipped peak level");
            Assert.LessOrEqual(Peak(clip), 1.0f);
        }

        [Test]
        public void TheSameStrikeAlwaysSoundsTheSame()
        {
            float[] a = ThunderSynth.Generate(600.0f, 42);
            float[] b = ThunderSynth.Generate(600.0f, 42);

            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i += 97)
            {
                Assert.AreEqual(a[i], b[i]);
            }
        }

        [Test]
        public void DifferentSeedsGiveDifferentThunder()
        {
            float[] a = ThunderSynth.Generate(600.0f, 1);
            float[] b = ThunderSynth.Generate(600.0f, 2);

            int differing = 0;
            for (int i = 0; i < a.Length && i < b.Length; i += 101)
            {
                if (a[i] != b[i])
                {
                    differing++;
                }
            }
            Assert.Greater(differing, 100);
        }

        [Test]
        public void CloseThunderIsBrighterThanDistantThunder()
        {
            double close = Brightness(ThunderSynth.Generate(150.0f, 5));
            double mid = Brightness(ThunderSynth.Generate(800.0f, 5));
            double far = Brightness(ThunderSynth.Generate(1700.0f, 5));

            TestContext.WriteLine($"brightness: close {close:0.0000}, mid {mid:0.0000}, far {far:0.0000}");
            Assert.Greater(close, mid, "the high end should thin out with distance");
            Assert.Greater(mid, far);
        }

        [Test]
        public void DistantThunderLastsLongerThanCloseThunder()
        {
            Assert.Greater(ThunderSynth.DurationSeconds(1700.0f), ThunderSynth.DurationSeconds(150.0f));
            Assert.AreEqual(ThunderSynth.DurationSeconds(150.0f) * ThunderSynth.SampleRate,
                ThunderSynth.Generate(150.0f, 3).Length, 1.0);
        }

        [Test]
        public void ACloseStrikeHasASharpCrackAtTheStartAndAFarOneDoesNot()
        {
            float[] close = ThunderSynth.Generate(150.0f, 9);
            float[] far = ThunderSynth.Generate(1700.0f, 9);

            int closePeak = PeakIndex(close);
            int farPeak = PeakIndex(far);
            TestContext.WriteLine($"peak at {closePeak / (float)ThunderSynth.SampleRate:0.00}s (close), {farPeak / (float)ThunderSynth.SampleRate:0.00}s (far)");

            Assert.Less(closePeak, ThunderSynth.SampleRate * 0.4f, "the crack is the loudest moment of a close strike, and it comes first");
            Assert.Greater(farPeak, ThunderSynth.SampleRate * 0.4f, "a far strike has no crack: it swells");
        }

        [Test]
        public void EveryClipFadesOutInsteadOfCuttingOff()
        {
            float[] clip = ThunderSynth.Generate(700.0f, 11);
            int tail = clip.Length - 1;

            Assert.Less(System.Math.Abs(clip[tail]), 0.02f);
            Assert.Less(System.Math.Abs(clip[tail - 200]), 0.05f);
        }

        [Test]
        public void NearnessIsOneClosePastAndZeroFarAway()
        {
            Assert.AreEqual(1.0f, ThunderSynth.Nearness(50.0f));
            Assert.AreEqual(1.0f, ThunderSynth.Nearness(100.0f));
            Assert.AreEqual(0.0f, ThunderSynth.Nearness(1700.0f), 1e-6f);
            Assert.AreEqual(0.0f, ThunderSynth.Nearness(9000.0f));
        }

        [Test]
        public void GeneratingAClipIsCheapEnoughToDoTheFirstTimeItIsNeeded()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            ThunderSynth.Generate(500.0f, 1);
            watch.Stop();
            TestContext.WriteLine($"one clip: {watch.Elapsed.TotalMilliseconds:0.0}ms");
            Assert.Less(watch.Elapsed.TotalMilliseconds, 250.0, "building a clip must not be a visible hitch");
        }
    }
}
