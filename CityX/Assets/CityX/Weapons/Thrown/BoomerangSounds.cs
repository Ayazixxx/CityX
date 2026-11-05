using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>A boomerang's whirr, made in code (no audio files to ship): air cut twice a turn, four turns a second. Built once, on first use.</summary>
    public static class BoomerangSounds
    {
        private const int SampleRate = 44100;
        private const float SecondsPerCut = 0.13f;
        private const int CutsInTheLoop = 8;

        private static AudioClip _whirr;

        public static AudioClip Whirr => _whirr != null ? _whirr : (_whirr = MakeWhirr());

        /// <summary>Soft noise that swells and dies with each cut: silent at every cut's end, so the loop has no seam.</summary>
        private static AudioClip MakeWhirr()
        {
            int samples = (int)(SecondsPerCut * CutsInTheLoop * SampleRate);
            var data = new float[samples];
            var noise = new System.Random(77);
            float smoothed = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float raw = (float)noise.NextDouble() * 2.0f - 1.0f;
                smoothed += (raw - smoothed) * 0.18f;
                float cut = Mathf.Sin(Mathf.PI * Mathf.Repeat(t, SecondsPerCut) / SecondsPerCut);
                data[i] = smoothed * cut * cut * 0.9f;
            }
            AudioClip clip = AudioClip.Create("BoomerangWhirr", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
