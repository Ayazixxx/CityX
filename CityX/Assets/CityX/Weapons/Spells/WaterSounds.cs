using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>The water staff's sound, made in code: the hiss and rush of a jet of water, a seamless second that loops while it sprays.</summary>
    public static class WaterSounds
    {
        private const int SampleRate = 44100;
        private static AudioClip _hiss;

        public static AudioClip Hiss => _hiss != null ? _hiss : (_hiss = MakeHiss());

        /// <summary>Noise shaped by two filters: a low rush and a bright hiss, each wavering a little, cross-faded at the loop point.</summary>
        private static AudioClip MakeHiss()
        {
            int samples = SampleRate;
            var data = new float[samples];
            var noise = new System.Random(41);
            float low = 0.0f, high = 0.0f, lowered = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                float waver = 1.0f + 0.15f * Mathf.Sin(2.0f * Mathf.PI * 3.0f * t) + 0.1f * Mathf.Sin(2.0f * Mathf.PI * 7.0f * t);
                low += (raw - low) * 0.06f * waver;
                lowered += (raw - lowered) * 0.45f;
                high = raw - lowered;
                data[i] = Mathf.Clamp(low * 1.8f + high * 0.25f * waver, -1.0f, 1.0f);
            }
            // The last tenth cross-faded into the first, so the loop has no seam.
            int fade = samples / 10;
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                data[samples - fade + i] = Mathf.Lerp(data[samples - fade + i], data[i], t);
            }
            AudioClip clip = AudioClip.Create("WaterJetHiss", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
