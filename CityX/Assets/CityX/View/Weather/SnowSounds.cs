using UnityEngine;

namespace CityX.View
{
    /// <summary>Snow's sounds, made in code (no audio files to ship): the wind that carries it, and a foot pressing into it. Built once, on first use.</summary>
    public static class SnowSounds
    {
        private const int SampleRate = 44100;
        private const float WindSeconds = 8.0f;
        private const float CrunchSeconds = 0.16f;

        private static AudioClip _wind;
        private static AudioClip _crunch;

        public static AudioClip Wind => _wind != null ? _wind : (_wind = MakeWind());

        public static AudioClip Crunch => _crunch != null ? _crunch : (_crunch = MakeCrunch());

        /// <summary>Low noise that swells and falls: two passes of the same filter, the loop's end crossfaded into its start so it has no seam.</summary>
        private static AudioClip MakeWind()
        {
            int samples = (int)(WindSeconds * SampleRate);
            int fade = SampleRate;
            var raw = new float[samples + fade];
            var noise = new System.Random(19);
            float first = 0.0f, second = 0.0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SampleRate;
                first += ((float)noise.NextDouble() * 2.0f - 1.0f - first) * 0.03f;
                second += (first - second) * 0.12f;
                float swell = 0.6f + 0.4f * Mathf.Sin(2.0f * Mathf.PI * t / WindSeconds * 2.0f) * Mathf.Sin(2.0f * Mathf.PI * t / WindSeconds * 3.0f);
                raw[i] = second * swell * 6.0f;
            }
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float share = i < fade ? i / (float)fade : 1.0f;
                data[i] = i < fade ? Mathf.Lerp(raw[samples + i], raw[i], share) : raw[i];
            }
            AudioClip clip = AudioClip.Create("SnowWind", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A short rattle of noise grains under a soft thud.</summary>
        private static AudioClip MakeCrunch()
        {
            int samples = (int)(CrunchSeconds * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(5);
            float held = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                if (i % 90 == 0)
                {
                    held = (float)noise.NextDouble() < 0.55 ? (float)noise.NextDouble() * 2.0f - 1.0f : 0.0f;
                }
                float grains = ((float)noise.NextDouble() * 2.0f - 1.0f) * Mathf.Abs(held) * Mathf.Exp(-t * 18.0f);
                float thud = Mathf.Sin(2.0f * Mathf.PI * 90.0f * t) * Mathf.Exp(-t * 40.0f) * 0.5f;
                data[i] = grains * 0.7f + thud;
            }
            AudioClip clip = AudioClip.Create("SnowCrunch", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
