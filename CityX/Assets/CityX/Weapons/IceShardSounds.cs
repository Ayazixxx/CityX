using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The two sounds of the ice shards spell, made in code (no audio files to ship): a quick bright "shing" as a shard leaves the hand, and a
    /// glassy "tink" as one lodges. Built once, on first use.
    /// </summary>
    public static class IceShardSounds
    {
        private const int SampleRate = 44100;
        private static AudioClip _cast;
        private static AudioClip _impact;

        public static AudioClip Cast => _cast != null ? _cast : (_cast = MakeCast());

        public static AudioClip Impact => _impact != null ? _impact : (_impact = MakeImpact());

        /// <summary>A rising breath of high noise over a thin sweeping tone, gone in a quarter of a second.</summary>
        private static AudioClip MakeCast()
        {
            int samples = (int)(0.26f * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(5);
            float last = 0.0f;
            float phase = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t * 14.0f);

                // High-passed noise (the difference between neighbours leaves the top of it).
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                float hiss = raw - last;
                last = raw;

                // A tone that climbs as it fades.
                phase += 2.0f * Mathf.PI * (1800.0f + 3200.0f * t) / SampleRate;
                float tone = Mathf.Sin(phase);

                data[i] = Mathf.Clamp((hiss * 0.45f + tone * 0.35f) * envelope, -1.0f, 1.0f);
            }
            return MakeClip("IceShardCast", data);
        }

        /// <summary>A hard little click and a ring of inharmonic high partials, the way a small piece of ice or glass rings.</summary>
        private static AudioClip MakeImpact()
        {
            int samples = (int)(0.55f * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(9);
            float[] partials = { 2130.0f, 3370.0f, 4960.0f, 6280.0f };
            float[] decay = { 7.0f, 9.0f, 12.0f, 16.0f };
            float[] weight = { 0.35f, 0.3f, 0.22f, 0.15f };
            float lowpass = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float ring = 0.0f;
                for (int p = 0; p < partials.Length; p++)
                {
                    ring += Mathf.Sin(2.0f * Mathf.PI * partials[p] * t) * Mathf.Exp(-t * decay[p]) * weight[p];
                }

                // The click: a few milliseconds of noise at the very start.
                float raw = ((float)noise.NextDouble() * 2.0f - 1.0f) * Mathf.Exp(-t * 220.0f);
                lowpass += (raw - lowpass) * 0.6f;

                data[i] = Mathf.Clamp(ring * Mathf.Clamp01(t / 0.001f) + lowpass * 0.8f, -1.0f, 1.0f);
            }
            return MakeClip("IceShardImpact", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
