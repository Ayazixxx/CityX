using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SOUND OF A SWORD SWUNG THROUGH THE AIR, made in code like the hit's thud (<see cref="HitImpactEffects.Thud"/>): a burst of noise through
    /// a resonant filter whose pitch rises with the blade's speed and falls away as it slows, a swish shaped like the strike itself (slow off the
    /// mark, fastest at the contact, gone at the end of the follow-through). A light's is short and high; a heavy's is longer, lower and louder.
    /// Until 2026-10-23 a swing made no sound unless it landed, and a miss felt like nothing had happened; the game-feel research (mocaponline,
    /// "Animation polish and game feel") has the whoosh carry a swing's weight as much as the picture does.
    ///
    /// Played by <see cref="SwordPoseDriver"/> on every body the attack plays on, so other players hear each other's swings.
    /// </summary>
    public static class SwingWhoosh
    {
        public const float LightSeconds = 0.18f;

        public const float HeavySeconds = 0.32f;

        public const float LightVolume = 0.35f;

        public const float HeavyVolume = 0.55f;

        /// <summary>Where in the sound (0-1) the blade is fastest and the swish loudest and highest: the contact, past the middle.</summary>
        public const float PeakAt = 0.6f;

        private static AudioClip _light;
        private static AudioClip _heavy;

        public static AudioClip Clip(bool heavy) => heavy
            ? _heavy ?? (_heavy = Build("SwingWhooshHeavy", HeavySeconds, 180.0f, 950.0f, 23))
            : _light ?? (_light = Build("SwingWhooshLight", LightSeconds, 450.0f, 2300.0f, 29));

        public static float Volume(bool heavy) => heavy ? HeavyVolume : LightVolume;

        /// <summary>Plays the swish at <paramref name="point"/> (the blade), 3D like the thud.</summary>
        public static void Play(bool heavy, Vector3 point)
        {
            AudioSource.PlayClipAtPoint(Clip(heavy), point, Volume(heavy));
        }

        /// <summary>The swish's loudness and pitch through the sound: the blade's speed, slow off the mark, peaking at <see cref="PeakAt"/>, gone at the end.</summary>
        public static float Envelope(float s01)
        {
            // sin(pi * s^k) peaks where s^k = 0.5: k is picked so that is at PeakAt.
            float k = Mathf.Log(0.5f) / Mathf.Log(PeakAt);
            float e = Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(s01), k));
            return e * e;
        }

        private static AudioClip Build(string name, float seconds, float lowHz, float highHz, int seed)
        {
            const int rate = 44100;
            const float resonance = 0.4f;   // the filter's 1/Q: narrow enough to whistle, wide enough to still be air
            int count = Mathf.RoundToInt(rate * seconds);
            var samples = new float[count];
            var noise = new System.Random(seed);
            float low = 0.0f, band = 0.0f, peak = 0.0f;
            for (int i = 0; i < count; i++)
            {
                float envelope = Envelope(i / (float)count);
                // A state-variable band-pass (Chamberlin) swept up the blade's speed and down again.
                float centre = Mathf.Lerp(lowHz, highHz, envelope);
                float f = 2.0f * Mathf.Sin(Mathf.PI * centre / rate);
                float input = (float)(noise.NextDouble() * 2.0 - 1.0);
                low += f * band;
                float high = input - low - resonance * band;
                band += f * high;
                samples[i] = band * envelope;
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }
            float gain = peak > 1e-5f ? 0.9f / peak : 1.0f;
            for (int i = 0; i < count; i++)
            {
                samples[i] *= gain;
            }
            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
