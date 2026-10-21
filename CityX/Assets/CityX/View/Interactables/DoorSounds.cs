using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The sounds of a door, made in code rather than shipped as files: a latch and a creak as it opens, a
    /// heavy thud as it shuts. A few hundred milliseconds of mono audio each, built once on first use --
    /// the game has no door recordings, and this keeps it from growing by any.
    ///
    /// Sound for a door needs a beat at the START and at the END of the swing (the level-design guidance for
    /// doors is to give sound a hook at open-start and close-finish), which is why there are two clips and
    /// not one.
    /// </summary>
    public static class DoorSounds
    {
        private const int SampleRate = 22050;

        private static AudioClip _open;
        private static AudioClip _shut;

        /// <summary>The latch letting go and a short creak of the hinge.</summary>
        public static AudioClip Open => _open != null ? _open : (_open = BuildOpen());

        /// <summary>The leaf meeting its frame: a low thud with a little wood knock on top.</summary>
        public static AudioClip Shut => _shut != null ? _shut : (_shut = BuildShut());

        private static AudioClip BuildOpen()
        {
            int samples = (int)(SampleRate * 0.55f);
            var data = new float[samples];
            var noise = new System.Random(7);
            float creakPhase = 0.0f;
            float lowpass = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;

                // The latch: a tiny click of noise in the first 25 ms.
                float click = t < 0.025f ? ((float)noise.NextDouble() * 2.0f - 1.0f) * (1.0f - t / 0.025f) * 0.55f : 0.0f;

                // The creak: a sawtooth whose pitch wanders around 190 Hz, softened, swelling and fading.
                float pitch = 190.0f + 45.0f * Mathf.Sin(t * 9.0f) + 30.0f * Mathf.Sin(t * 23.0f);
                creakPhase += pitch / SampleRate;
                float saw = (creakPhase - Mathf.Floor(creakPhase)) * 2.0f - 1.0f;
                float envelope = Mathf.Sin(Mathf.Clamp01((t - 0.02f) / 0.5f) * Mathf.PI);
                lowpass += (saw * envelope * 0.22f - lowpass) * 0.25f;

                data[i] = Mathf.Clamp(click + lowpass, -1.0f, 1.0f);
            }
            return MakeClip("DoorOpen", data);
        }

        private static AudioClip BuildShut()
        {
            int samples = (int)(SampleRate * 0.42f);
            var data = new float[samples];
            var noise = new System.Random(11);
            float knock = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;

                // The thud: a low tone that drops as it dies.
                float thud = Mathf.Sin(2.0f * Mathf.PI * (62.0f - 22.0f * t) * t) * Mathf.Exp(-t * 11.0f) * 0.85f;

                // The knock of wood on wood: a burst of low-passed noise at the very start.
                float raw = ((float)noise.NextDouble() * 2.0f - 1.0f) * Mathf.Exp(-t * 60.0f);
                knock += (raw - knock) * 0.35f;

                data[i] = Mathf.Clamp(thud + knock * 0.7f, -1.0f, 1.0f);
            }
            return MakeClip("DoorShut", data);
        }

        private static AudioClip MakeClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
