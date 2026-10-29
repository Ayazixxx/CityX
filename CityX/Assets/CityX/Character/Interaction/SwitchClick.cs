using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A switch's click, made in code (no sound asset): a short tick of noise over a high ping, gone in 40 ms. The room lights' switches click
    /// with it and so does the flashlight, each a little lower going off.
    /// </summary>
    public static class SwitchClick
    {
        /// <summary>The pitch it is played at switching off: a little lower than on.</summary>
        public const float OffPitch = 0.85f;

        private static AudioClip _clip;

        public static AudioClip Clip => _clip != null ? _clip : (_clip = Build());

        private static AudioClip Build()
        {
            const int rate = 44100;
            var samples = new float[rate / 25];
            var random = new System.Random(7);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)samples.Length;
                float envelope = Mathf.Exp(-t * 18.0f);
                samples[i] = ((float)random.NextDouble() * 2.0f - 1.0f) * envelope * 0.6f
                    + Mathf.Sin(i * 2.0f * Mathf.PI * 2400.0f / rate) * envelope * 0.25f;
            }
            AudioClip clip = AudioClip.Create("SwitchClick", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
