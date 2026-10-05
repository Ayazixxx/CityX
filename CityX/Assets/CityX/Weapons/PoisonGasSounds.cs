using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>The poison gas's sound, made in code: a soft, breathy "fffsshh" that swells and dies away over about 0.9 s (filtered noise with a slow wobble).</summary>
    public static class PoisonGasSounds
    {
        private const int SampleRate = 44100;
        private static AudioClip _hiss;

        public static AudioClip Hiss => _hiss != null ? _hiss : (_hiss = Make());

        private static AudioClip Make()
        {
            int samples = (int)(0.9f * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(21);
            float low = 0.0f, band = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float swell = Mathf.Clamp01(t / 0.12f) * Mathf.Exp(-Mathf.Max(0.0f, t - 0.12f) * 3.2f);
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                // Two one-pole filters: the difference of a fast and a slow low-pass leaves the middle of the noise, the airy part.
                low += (raw - low) * 0.5f;
                band += (raw - band) * 0.08f;
                float wobble = 0.75f + 0.25f * Mathf.Sin(2.0f * Mathf.PI * 7.0f * t);
                data[i] = Mathf.Clamp((low - band) * 1.1f * swell * wobble, -1.0f, 1.0f);
            }
            AudioClip clip = AudioClip.Create("PoisonGasHiss", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
