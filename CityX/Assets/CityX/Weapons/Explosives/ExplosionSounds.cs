using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// An explosion's sound, made in code (no audio files to ship), as the spells' are: a hard crack of noise, a deep thump falling away under
    /// it, a rumble that rolls on, and bits of debris pattering down after. Built once, on first use; the grenade's pin and spoon a tick.
    /// </summary>
    public static class ExplosionSounds
    {
        private const int SampleRate = 44100;
        private static AudioClip _boom;
        private static AudioClip _pin;

        public static AudioClip Boom => _boom != null ? _boom : (_boom = MakeBoom());

        /// <summary>The pin pulled and the spoon flying: two little metal ticks.</summary>
        public static AudioClip Pin => _pin != null ? _pin : (_pin = MakePin());

        /// <summary>Crack, thump, rumble and patter, two seconds.</summary>
        private static AudioClip MakeBoom()
        {
            int samples = (int)(2.0f * SampleRate);
            var data = new float[samples];
            var random = new System.Random(1945);
            float thump = 0.0f, low = 0.0f, lower = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2.0f - 1.0f;
                // The crack: bright noise, gone in 40 ms.
                float crack = noise * Mathf.Exp(-t * 90.0f);
                // The thump: a sine falling from 90 Hz to 30, fading over half a second.
                thump += 2.0f * Mathf.PI * Mathf.Lerp(90.0f, 30.0f, Mathf.Clamp01(t / 0.6f)) / SampleRate;
                float body = Mathf.Sin(thump) * Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 4.0f);
                // The rumble: noise twice low-passed, rolling on.
                low += (noise - low) * 0.04f;
                lower += (low - lower) * 0.04f;
                float rumble = lower * 9.0f * Mathf.Exp(-t * 1.6f);
                // The patter: now and then a little tick of debris landing.
                float patter = t > 0.25f && random.NextDouble() < 0.0006 * Mathf.Exp(-(t - 0.25f) * 2.0f) ? noise * 0.5f : 0.0f;
                data[i] = Mathf.Clamp(crack * 0.9f + body * 0.9f + rumble + patter, -1.0f, 1.0f);
            }
            return MakeClip("ExplosionBoom", data);
        }

        private static AudioClip MakePin()
        {
            int samples = (int)(0.18f * SampleRate);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float first = Mathf.Sin(2.0f * Mathf.PI * 3200.0f * t) * Mathf.Exp(-t * 120.0f);
                float second = t > 0.09f ? Mathf.Sin(2.0f * Mathf.PI * 2600.0f * (t - 0.09f)) * Mathf.Exp(-(t - 0.09f) * 90.0f) : 0.0f;
                data[i] = Mathf.Clamp((first + second) * 0.4f, -1.0f, 1.0f);
            }
            return MakeClip("GrenadePin", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
