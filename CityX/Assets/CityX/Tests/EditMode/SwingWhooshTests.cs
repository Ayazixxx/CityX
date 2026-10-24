using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The sword's swish, made in code (<see cref="SwingWhoosh"/>): short and high for a light, longer, lower and louder for a heavy, loudest at the contact.</summary>
    public sealed class SwingWhooshTests
    {
        private static float[] SamplesOf(AudioClip clip)
        {
            var samples = new float[clip.samples];
            clip.GetData(samples, 0);
            return samples;
        }

        /// <summary>How often the wave crosses zero per second: a rough pitch.</summary>
        private static float CrossingsPerSecond(AudioClip clip)
        {
            float[] samples = SamplesOf(clip);
            int crossings = 0;
            for (int i = 1; i < samples.Length; i++)
            {
                if (samples[i - 1] < 0.0f != samples[i] < 0.0f)
                {
                    crossings++;
                }
            }
            return crossings / clip.length;
        }

        /// <summary>Where in the clip (0-1) its loudest twentieth sits.</summary>
        private static float LoudestAt(AudioClip clip)
        {
            float[] samples = SamplesOf(clip);
            const int windows = 20;
            int size = samples.Length / windows;
            float loudest = -1.0f, at = 0.0f;
            for (int w = 0; w < windows; w++)
            {
                float sum = 0.0f;
                for (int i = w * size; i < (w + 1) * size; i++)
                {
                    sum += samples[i] * samples[i];
                }
                if (sum > loudest)
                {
                    loudest = sum;
                    at = (w + 0.5f) / windows;
                }
            }
            return at;
        }

        [Test]
        public void TheLightSwishIsShortAndHighTheHeavyLongerLowerAndLouder()
        {
            AudioClip light = SwingWhoosh.Clip(false);
            AudioClip heavy = SwingWhoosh.Clip(true);
            Assert.AreEqual(SwingWhoosh.LightSeconds, light.length, 0.01f);
            Assert.Greater(heavy.length, light.length * 1.5f, "the heavy's swish is not longer");
            Assert.Greater(CrossingsPerSecond(light), CrossingsPerSecond(heavy) * 1.5f, "the heavy's swish is not lower");
            Assert.Greater(SwingWhoosh.Volume(true), SwingWhoosh.Volume(false), "the heavy's swish is not louder");
            foreach (AudioClip clip in new[] { light, heavy })
            {
                float peak = 0.0f;
                foreach (float s in SamplesOf(clip))
                {
                    peak = Mathf.Max(peak, Mathf.Abs(s));
                }
                Assert.That(peak, Is.InRange(0.8f, 1.0f), $"{clip.name} peaks at {peak:0.00}: not normalised");
            }
        }

        [Test]
        public void TheSwishPeaksWhereTheBladeIsFastest()
        {
            // Silent as the blade leaves the hold, loudest and highest at the contact (past the middle), gone at the end of the follow-through.
            float best = -1.0f, bestAt = 0.0f;
            for (float s = 0.0f; s <= 1.0f; s += 0.001f)
            {
                float e = SwingWhoosh.Envelope(s);
                if (e > best)
                {
                    best = e;
                    bestAt = s;
                }
            }
            Assert.AreEqual(SwingWhoosh.PeakAt, bestAt, 0.01f, "the envelope does not peak where it says");
            Assert.AreEqual(1.0f, best, 0.001f);
            Assert.Less(SwingWhoosh.Envelope(0.0f), 0.001f);
            Assert.Less(SwingWhoosh.Envelope(1.0f), 0.001f);
            Assert.That(LoudestAt(SwingWhoosh.Clip(false)), Is.InRange(0.45f, 0.75f), "the light swish is not loudest past the middle");
            Assert.That(LoudestAt(SwingWhoosh.Clip(true)), Is.InRange(0.45f, 0.75f), "the heavy swish is not loudest past the middle");
        }
    }
}
