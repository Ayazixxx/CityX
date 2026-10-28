using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The wings' sounds, made in code (no audio files to ship): a wingbeat (a soft deep whump of air, shaped by a filter opening and closing),
    /// the take-off (a bigger one with a rush after it) and a landing's rustle of feathers. Built once, on first use.
    /// </summary>
    public static class WingSounds
    {
        private const int SampleRate = 44100;
        private static AudioClip _beat;
        private static AudioClip _takeoff;
        private static AudioClip _rustle;

        public static AudioClip Beat => _beat != null ? _beat : (_beat = Make("WingBeat", 0.28f, 0.0f, 0.55f, 0.0f, 11));

        public static AudioClip Takeoff => _takeoff != null ? _takeoff : (_takeoff = Make("WingTakeoff", 0.6f, 0.15f, 0.9f, 0.35f, 17));

        public static AudioClip Rustle => _rustle != null ? _rustle : (_rustle = Make("WingRustle", 0.35f, 0.0f, 0.2f, 0.6f, 23));

        /// <summary>
        /// Noise through a one-pole low-pass whose cutoff rises and falls over the clip (the air pushed, then let go): <paramref name="body"/>
        /// sets how low the whump sits, <paramref name="rush"/> a brighter tail, <paramref name="rustle"/> a crackle of short grains.
        /// </summary>
        private static AudioClip Make(string name, float seconds, float rush, float body, float rustle, int seed)
        {
            int samples = (int)(seconds * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(seed);
            float low = 0.0f, mid = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                // The cutoff: closed, opening over the first fifth, closing again.
                float open = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.6f));
                float cutoff = Mathf.Lerp(0.015f, 0.12f + 0.3f * rush, open);
                low += (raw - low) * cutoff;
                mid += (raw - mid) * Mathf.Min(0.6f, cutoff * 4.0f);
                float grain = rustle > 0.0f && (i % 97) < 9 ? raw * 0.5f : 0.0f;
                float envelope = Mathf.Clamp01(t / 0.05f) * Mathf.Pow(1.0f - t, 1.4f);
                float sample = low * body * 2.2f + mid * rush * 0.5f + grain * rustle;
                data[i] = Mathf.Clamp(sample * envelope, -1.0f, 1.0f);
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
