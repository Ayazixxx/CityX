using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The RPG's rocket's sounds, made in code as the explosion's are (ExplosionSounds): the motor's hiss, a roar of low noise with a rasp in it,
    /// one second that loops seamlessly (its two ends faded into each other), and the back-blast's whoosh, a burst of noise that sweeps down.
    /// </summary>
    public static class RocketSounds
    {
        private const int SampleRate = 44100;
        private static AudioClip _hiss;
        private static AudioClip _whoosh;

        public static AudioClip MotorHiss => _hiss != null ? _hiss : (_hiss = MakeHiss());

        public static AudioClip Whoosh => _whoosh != null ? _whoosh : (_whoosh = MakeWhoosh());

        private static AudioClip MakeHiss()
        {
            int samples = SampleRate;
            var raw = new float[samples];
            var random = new System.Random(7);
            float low = 0.0f, band = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float noise = (float)random.NextDouble() * 2.0f - 1.0f;
                low += (noise - low) * 0.08f;          // the roar
                band += (noise - band) * 0.45f;        // the rasp
                raw[i] = low * 2.2f + (band - low) * 0.35f;
            }
            // Looped: the last tenth faded into the first, so the seam is never heard.
            int fade = samples / 10;
            var data = new float[samples - fade];
            for (int i = 0; i < data.Length; i++)
            {
                float v = raw[i];
                if (i < fade)
                {
                    float t = i / (float)fade;
                    v = raw[i] * t + raw[samples - fade + i] * (1.0f - t);
                }
                data[i] = Mathf.Clamp(v * 0.5f, -1.0f, 1.0f);
            }
            return MakeClip("RocketMotorHiss", data);
        }

        private static AudioClip MakeWhoosh()
        {
            int samples = (int)(0.7f * SampleRate);
            var data = new float[samples];
            var random = new System.Random(11);
            float low = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2.0f - 1.0f;
                low += (noise - low) * Mathf.Lerp(0.6f, 0.05f, t / 0.7f);   // bright to dull as it sweeps away
                data[i] = Mathf.Clamp(low * 1.6f * Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 4.5f), -1.0f, 1.0f);
            }
            return MakeClip("RocketWhoosh", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
