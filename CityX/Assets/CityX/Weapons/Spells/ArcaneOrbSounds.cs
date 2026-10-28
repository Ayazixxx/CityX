using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// Arcane Orb's sounds, made in code (no audio files to ship), as IceShardSounds are: the ball leaving (a low swell with a shimmer over it), a
    /// chime as a staff's charge reaches each orb, the curse taking hold (a dark wavering pair of tones), and an orb striking (a thump and a
    /// falling ring). Built once, on first use.
    /// </summary>
    public static class ArcaneOrbSounds
    {
        private const int SampleRate = 44100;
        private static AudioClip _cast;
        private static AudioClip _charge;
        private static AudioClip _curse;
        private static AudioClip _strike;

        public static AudioClip Cast => _cast != null ? _cast : (_cast = MakeCast());

        /// <summary>One chime; played higher for each orb the charge reaches.</summary>
        public static AudioClip Charge => _charge != null ? _charge : (_charge = MakeCharge());

        public static AudioClip Curse => _curse != null ? _curse : (_curse = MakeCurse());

        public static AudioClip Strike => _strike != null ? _strike : (_strike = MakeStrike());

        /// <summary>A hum swelling up an octave, a bright shimmer riding it, gone in a third of a second.</summary>
        private static AudioClip MakeCast()
        {
            int samples = (int)(0.34f * SampleRate);
            var data = new float[samples];
            float hum = 0.0f, shimmer = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 9.0f);
                hum += 2.0f * Mathf.PI * (180.0f + 360.0f * t / 0.34f) / SampleRate;
                shimmer += 2.0f * Mathf.PI * (1400.0f + 900.0f * Mathf.Sin(t * 70.0f)) / SampleRate;
                float tone = Mathf.Sin(hum) * 0.5f + Mathf.Sin(hum * 2.0f) * 0.2f + Mathf.Sin(shimmer) * 0.18f;
                data[i] = Mathf.Clamp(tone * envelope, -1.0f, 1.0f);
            }
            return MakeClip("ArcaneOrbCast", data);
        }

        /// <summary>A short bell: two partials a fifth apart, ringing away.</summary>
        private static AudioClip MakeCharge()
        {
            int samples = (int)(0.3f * SampleRate);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float ring = Mathf.Sin(2.0f * Mathf.PI * 880.0f * t) * 0.4f + Mathf.Sin(2.0f * Mathf.PI * 1320.0f * t) * 0.25f;
                data[i] = Mathf.Clamp(ring * Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 14.0f), -1.0f, 1.0f);
            }
            return MakeClip("ArcaneOrbCharge", data);
        }

        /// <summary>Two low tones a tritone apart, beating against each other, wavering down.</summary>
        private static AudioClip MakeCurse()
        {
            int samples = (int)(0.6f * SampleRate);
            var data = new float[samples];
            float a = 0.0f, b = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float bend = 1.0f - 0.25f * t;
                a += 2.0f * Mathf.PI * 233.0f * bend / SampleRate;
                b += 2.0f * Mathf.PI * 330.0f * bend * (1.0f + 0.01f * Mathf.Sin(t * 40.0f)) / SampleRate;
                float envelope = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t * 5.0f);
                data[i] = Mathf.Clamp((Mathf.Sin(a) * 0.35f + Mathf.Sin(b) * 0.3f) * envelope, -1.0f, 1.0f);
            }
            return MakeClip("ArcaneOrbCurse", data);
        }

        /// <summary>A low thump falling in pitch under a burst of noise, and a ring falling with it.</summary>
        private static AudioClip MakeStrike()
        {
            int samples = (int)(0.5f * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(13);
            float thump = 0.0f, ring = 0.0f, smoothed = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                thump += 2.0f * Mathf.PI * (140.0f * Mathf.Exp(-t * 6.0f) + 45.0f) / SampleRate;
                ring += 2.0f * Mathf.PI * (1100.0f - 600.0f * t) / SampleRate;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                smoothed += (raw - smoothed) * 0.35f;
                float body = Mathf.Sin(thump) * Mathf.Exp(-t * 7.0f) * 0.7f;
                float hiss = smoothed * Mathf.Exp(-t * 18.0f) * 0.6f;
                float bell = Mathf.Sin(ring) * Mathf.Exp(-t * 9.0f) * 0.2f;
                data[i] = Mathf.Clamp((body + hiss + bell) * Mathf.Clamp01(t / 0.002f), -1.0f, 1.0f);
            }
            return MakeClip("ArcaneOrbStrike", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
