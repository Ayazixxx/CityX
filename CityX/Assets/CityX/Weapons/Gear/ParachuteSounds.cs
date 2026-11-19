using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The canopy's sounds, made in code as the rocket's are (RocketSounds): the CRACK as it fills (a whip of bright noise on a low thump, the
    /// fabric snapping taut) and the FLUTTER it makes flying (low noise beating a dozen times a second, one second that loops without a seam).
    /// </summary>
    public static class ParachuteSounds
    {
        private const int SampleRate = 44100;

        /// <summary>How many times a second the fabric beats.</summary>
        private const float FlutterBeats = 12.0f;

        private static AudioClip _crack;
        private static AudioClip _flutter;

        public static AudioClip OpenCrack => _crack != null ? _crack : (_crack = MakeCrack());

        public static AudioClip Flutter => _flutter != null ? _flutter : (_flutter = MakeFlutter());

        private static AudioClip MakeCrack()
        {
            int samples = (int)(0.6f * SampleRate);
            var data = new float[samples];
            var random = new System.Random(5);
            float low = 0.0f, band = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2.0f - 1.0f;
                low += (noise - low) * 0.03f;
                band += (noise - band) * 0.5f;
                float snap = (band - low) * Mathf.Exp(-t * 28.0f);                          // the fabric snapping taut
                float thump = Mathf.Sin(2.0f * Mathf.PI * 70.0f * t) * Mathf.Exp(-t * 9.0f);   // the canopy's weight taking the body
                float rush = low * 3.0f * Mathf.Exp(-t * 5.0f);                              // the air leaving it
                data[i] = Mathf.Clamp((snap * 1.2f + thump * 0.6f + rush) * Mathf.Clamp01(t / 0.003f), -1.0f, 1.0f);
            }
            return MakeClip("ParachuteCrack", data);
        }

        private static AudioClip MakeFlutter()
        {
            int samples = SampleRate;
            var raw = new float[samples];
            var random = new System.Random(9);
            float low = 0.0f;
            // A whole number of beats in the looped length (nine tenths of the second), so the beat runs on across the seam.
            float beats = Mathf.Round(FlutterBeats * 0.9f) / 0.9f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2.0f - 1.0f;
                low += (noise - low) * 0.06f;
                float beat = 0.55f + 0.45f * Mathf.Sin(2.0f * Mathf.PI * beats * t);
                raw[i] = low * 2.0f * beat * beat;
            }
            // Looped: the last tenth faded into the first.
            int fade = samples / 10;
            var data = new float[samples - fade];
            for (int i = 0; i < data.Length; i++)
            {
                float v = raw[i];
                if (i < fade)
                {
                    float share = i / (float)fade;
                    v = raw[i] * share + raw[samples - fade + i] * (1.0f - share);
                }
                data[i] = Mathf.Clamp(v * 0.6f, -1.0f, 1.0f);
            }
            return MakeClip("ParachuteFlutter", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
