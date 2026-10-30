using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The speck's sounds, made in code as the arcane orb's are: leaving the wand (a glassy flick, two high partials sliding up with a glitter
    /// over them) and striking (a soft sparkle: a short crackle under a falling ring). Built once, on first use.
    /// </summary>
    public static class MagicSpeckSounds
    {
        private const int SampleRate = 44100;
        private static AudioClip _cast;
        private static AudioClip _hit;

        public static AudioClip Cast => _cast != null ? _cast : (_cast = MakeCast());

        public static AudioClip Hit => _hit != null ? _hit : (_hit = MakeHit());

        private static AudioClip MakeCast()
        {
            const float seconds = 0.22f;
            int samples = (int)(seconds * SampleRate);
            var data = new float[samples];
            float low = 0.0f, high = 0.0f, glitter = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float slide = t / seconds;
                float envelope = Mathf.Clamp01(t / 0.006f) * Mathf.Exp(-t * 16.0f);
                low += 2.0f * Mathf.PI * (900.0f + 700.0f * slide) / SampleRate;
                high += 2.0f * Mathf.PI * (1800.0f + 1500.0f * slide) / SampleRate;
                glitter += 2.0f * Mathf.PI * (4200.0f + 1200.0f * Mathf.Sin(t * 260.0f)) / SampleRate;
                float tone = Mathf.Sin(low) * 0.35f + Mathf.Sin(high) * 0.22f + Mathf.Sin(glitter) * 0.1f;
                data[i] = Mathf.Clamp(tone * envelope, -1.0f, 1.0f);
            }
            return MakeClip("MagicSpeckCast", data);
        }

        private static AudioClip MakeHit()
        {
            const float seconds = 0.25f;
            int samples = (int)(seconds * SampleRate);
            var data = new float[samples];
            var random = new System.Random(7);
            float ring = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                ring += 2.0f * Mathf.PI * (2600.0f - 1400.0f * t / seconds) / SampleRate;
                float crackle = ((float)random.NextDouble() * 2.0f - 1.0f) * Mathf.Exp(-t * 60.0f) * 0.35f;
                float tone = Mathf.Sin(ring) * 0.3f * Mathf.Exp(-t * 14.0f);
                data[i] = Mathf.Clamp((tone + crackle) * Mathf.Clamp01(t / 0.002f), -1.0f, 1.0f);
            }
            return MakeClip("MagicSpeckHit", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
