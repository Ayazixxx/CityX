using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// Arcane Barrier's sounds, made in code (no audio files to ship), as ArcaneOrbSounds are: the wall rising (a swell up into a chord), its hum
    /// while it stands (a loop), a blow on it (a soft deep thud with a ring, nothing glassy: it is aura, not glass), and it fading away (the chord
    /// sinking into breath). Built once, on first use.
    /// </summary>
    public static class ArcaneBarrierSounds
    {
        private const int SampleRate = 44100;
        private static AudioClip _rise;
        private static AudioClip _hum;
        private static AudioClip _struck;
        private static AudioClip _fade;

        public static AudioClip Rise => _rise != null ? _rise : (_rise = MakeRise());

        /// <summary>One second that loops without a seam (every partial a whole number of cycles in it).</summary>
        public static AudioClip Hum => _hum != null ? _hum : (_hum = MakeHum());

        public static AudioClip Struck => _struck != null ? _struck : (_struck = MakeStruck());

        public static AudioClip Fade => _fade != null ? _fade : (_fade = MakeFade());

        /// <summary>A low tone gliding up a fifth under a major triad that blooms in over it, in under half a second.</summary>
        private static AudioClip MakeRise()
        {
            int samples = (int)(0.45f * SampleRate);
            var data = new float[samples];
            float root = 0.0f, third = 0.0f, fifth = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float glide = 1.0f + 0.5f * Mathf.Clamp01(t / 0.2f);
                root += 2.0f * Mathf.PI * 147.0f * glide / SampleRate;
                third += 2.0f * Mathf.PI * 370.0f / SampleRate;
                fifth += 2.0f * Mathf.PI * 440.0f / SampleRate;
                float bloom = Mathf.Clamp01((t - 0.08f) / 0.15f);
                float envelope = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-Mathf.Max(0.0f, t - 0.25f) * 10.0f);
                float tone = Mathf.Sin(root) * 0.45f + (Mathf.Sin(third) * 0.18f + Mathf.Sin(fifth) * 0.18f) * bloom;
                data[i] = Mathf.Clamp(tone * envelope, -1.0f, 1.0f);
            }
            return MakeClip("ArcaneBarrierRise", data);
        }

        /// <summary>A soft drone: a low fundamental, its fifth, and a faint high shimmer beating slowly against itself.</summary>
        private static AudioClip MakeHum()
        {
            int samples = SampleRate;
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float tone = Mathf.Sin(2.0f * Mathf.PI * 110.0f * t) * 0.4f + Mathf.Sin(2.0f * Mathf.PI * 165.0f * t) * 0.2f
                    + (Mathf.Sin(2.0f * Mathf.PI * 880.0f * t) + Mathf.Sin(2.0f * Mathf.PI * 883.0f * t)) * 0.04f;
                data[i] = Mathf.Clamp(tone, -1.0f, 1.0f);
            }
            return MakeClip("ArcaneBarrierHum", data);
        }

        /// <summary>A deep thud falling in pitch, a breath of noise on it and a short ring an octave and a fifth up.</summary>
        private static AudioClip MakeStruck()
        {
            int samples = (int)(0.3f * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(29);
            float thud = 0.0f, smoothed = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                thud += 2.0f * Mathf.PI * (90.0f * Mathf.Exp(-t * 12.0f) + 70.0f) / SampleRate;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                smoothed += (raw - smoothed) * 0.2f;
                float body = Mathf.Sin(thud) * Mathf.Exp(-t * 14.0f) * 0.7f;
                float breath = smoothed * Mathf.Exp(-t * 25.0f) * 0.35f;
                float ring = Mathf.Sin(2.0f * Mathf.PI * 330.0f * t) * Mathf.Exp(-t * 16.0f) * 0.2f;
                data[i] = Mathf.Clamp((body + breath + ring) * Mathf.Clamp01(t / 0.002f), -1.0f, 1.0f);
            }
            return MakeClip("ArcaneBarrierStruck", data);
        }

        /// <summary>The triad sinking a fourth and thinning into soft noise, gone in under a second.</summary>
        private static AudioClip MakeFade()
        {
            int samples = (int)(0.8f * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(31);
            float root = 0.0f, fifth = 0.0f, smoothed = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float sink = 1.0f - 0.25f * (t / 0.8f);
                root += 2.0f * Mathf.PI * 220.0f * sink / SampleRate;
                fifth += 2.0f * Mathf.PI * 330.0f * sink / SampleRate;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                smoothed += (raw - smoothed) * 0.08f;
                float envelope = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 4.0f);
                float tone = (Mathf.Sin(root) * 0.3f + Mathf.Sin(fifth) * 0.2f) * Mathf.Exp(-t * 2.0f) + smoothed * 0.4f * Mathf.Clamp01(t / 0.2f);
                data[i] = Mathf.Clamp(tone * envelope, -1.0f, 1.0f);
            }
            return MakeClip("ArcaneBarrierFade", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
