using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>A boomerang's whirr, made in code (no audio files to ship): air cut twice a turn, four turns a second. Built once, on first use.</summary>
    public static class BoomerangSounds
    {
        private const int SampleRate = 44100;
        private const float SecondsPerCut = 0.13f;
        private const int CutsInTheLoop = 8;

        private static AudioClip _whirr;
        private static AudioClip _bang;

        public static AudioClip Whirr => _whirr != null ? _whirr : (_whirr = MakeWhirr());

        /// <summary>
        /// The bang of a boomerang striking a wall, the floor or anything hard (owner, 2026-11-17: "some sort of bang noise"): a sharp crack and a
        /// low thump under it, dying in a quarter of a second. The giant one plays it slower, deeper (<see cref="GiantBangPitch"/>).
        /// </summary>
        public static AudioClip Bang => _bang != null ? _bang : (_bang = MakeBang());

        public const float BangSeconds = 0.32f;

        public const float GiantBangPitch = 0.62f;

        /// <summary>The bang's samples: a crack of bright noise over 4 ms falling away fast, a thump from 110 Hz down to 55 dying slower. Peaks at once, quiet by its end.</summary>
        public static float[] BangSamples()
        {
            int samples = (int)(BangSeconds * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(91);
            float phase = 0.0f, previous = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                float bright = raw - previous;   // the change between samples: the high end of the noise, a crack not a hiss
                previous = raw;
                float attack = Mathf.Clamp01(t / 0.0015f);
                float crack = bright * 0.55f * attack * Mathf.Exp(-t / 0.018f);
                float hertz = Mathf.Lerp(110.0f, 55.0f, Mathf.Clamp01(t / 0.12f));
                phase += 2.0f * Mathf.PI * hertz / SampleRate;
                float thump = Mathf.Sin(phase) * 0.8f * attack * Mathf.Exp(-t / 0.07f);
                data[i] = Mathf.Clamp(crack + thump, -1.0f, 1.0f);
            }
            return data;
        }

        private static AudioClip MakeBang()
        {
            float[] data = BangSamples();
            AudioClip clip = AudioClip.Create("BoomerangBang", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Soft noise that swells and dies with each cut: silent at every cut's end, so the loop has no seam.</summary>
        private static AudioClip MakeWhirr()
        {
            int samples = (int)(SecondsPerCut * CutsInTheLoop * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(77);
            float smoothed = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                smoothed += (raw - smoothed) * 0.18f;
                float cut = Mathf.Sin(Mathf.PI * Mathf.Repeat(t, SecondsPerCut) / SecondsPerCut);
                data[i] = smoothed * cut * cut * 0.9f;
            }
            AudioClip clip = AudioClip.Create("BoomerangWhirr", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
