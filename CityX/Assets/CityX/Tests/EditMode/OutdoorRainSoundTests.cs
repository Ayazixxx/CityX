using System.Linq;
using CityX.View;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityX.Tests
{
    /// <summary>
    /// The outdoor rain's sound (owner, 2026-10-26: "take the audio from this and replace our outdoor rain noise with this. make it a clean
    /// loop"): the city's rain plays it; played round, there is no click and no dip where its end meets its start; and it is as loud as the
    /// sound it replaced, so the rain's volumes (Weather) still fit.
    /// </summary>
    public sealed class OutdoorRainSoundTests
    {
        private const string CityScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const string OldRainSoundPath = "Assets/CityX/UI/Audio/rain_noises.mp3";

        /// <summary>A window of loudness this long, seconds: about what an ear hears a dip over.</summary>
        private const float LoudnessWindowSeconds = 0.05f;

        private static float[] Samples(AudioClip clip)
        {
            Assert.IsTrue(clip.LoadAudioData(), $"{clip.name} would not load");
            var samples = new float[clip.samples * clip.channels];
            Assert.IsTrue(clip.GetData(samples, 0), $"{clip.name} could not be read");
            return samples;
        }

        private static AudioClip Loop()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RainEffect.OutdoorRainSoundPath);
            Assert.IsNotNull(clip, $"no outdoor rain sound at {RainEffect.OutdoorRainSoundPath}");
            return clip;
        }

        private static float Rms(float[] samples, int from, int count)
        {
            double sum = 0.0;
            for (int i = from; i < from + count; i++)
            {
                sum += samples[i] * (double)samples[i];
            }
            return (float)System.Math.Sqrt(sum / count);
        }

        [Test]
        public void TheCitysRainPlaysIt()
        {
            Scene scene = EditorSceneManager.OpenScene(CityScenePath, OpenSceneMode.Additive);
            try
            {
                RainEffect rain = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RainEffect>(true)).FirstOrDefault();
                Assert.IsNotNull(rain, "the city has no rain");
                Assert.AreEqual(Loop(), rain.RainSound, "the city's rain plays another sound: rebuild the scene (CityMassingSceneBuilder)");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void PlayedRoundThereIsNoClickAtTheSeam()
        {
            AudioClip clip = Loop();
            Assert.Greater(clip.length, 5.0f, "the loop is too short not to be heard repeating");
            float[] samples = Samples(clip);
            int channels = clip.channels;
            int frames = clip.samples;
            var steps = new float[frames - 1];
            for (int f = 0; f + 1 < frames; f++)
            {
                float step = 0.0f;
                for (int c = 0; c < channels; c++)
                {
                    step = Mathf.Max(step, Mathf.Abs(samples[(f + 1) * channels + c] - samples[f * channels + c]));
                }
                steps[f] = step;
            }
            System.Array.Sort(steps);
            float usual = steps[(int)(steps.Length * 0.99f)];
            float seam = 0.0f;
            for (int c = 0; c < channels; c++)
            {
                seam = Mathf.Max(seam, Mathf.Abs(samples[c] - samples[(frames - 1) * channels + c]));
            }
            Assert.LessOrEqual(seam, usual, "the jump from the loop's end to its start is bigger than the sound ever steps: it clicks");
        }

        [Test]
        public void PlayedRoundThereIsNoDipAtTheSeam()
        {
            AudioClip clip = Loop();
            float[] samples = Samples(clip);
            int window = Mathf.RoundToInt(clip.frequency * LoudnessWindowSeconds) * clip.channels;
            var levels = new System.Collections.Generic.List<float>();
            for (int i = 0; i + window <= samples.Length; i += window)
            {
                levels.Add(Rms(samples, i, window));
            }
            levels.Sort();
            float quiet = levels[(int)(levels.Count * 0.02f)];
            float loud = levels[(int)(levels.Count * 0.98f)];

            // The last window and the first, heard back to back.
            var across = new float[window * 2];
            System.Array.Copy(samples, samples.Length - window, across, 0, window);
            System.Array.Copy(samples, 0, across, window, window);
            float atSeam = Rms(across, window / 2, window);
            Assert.GreaterOrEqual(atSeam, quiet, "the rain dips where the loop comes round");
            Assert.LessOrEqual(atSeam, loud, "the rain swells where the loop comes round");
        }

        [Test]
        public void ItIsAsLoudAsTheSoundItReplaced()
        {
            float[] loop = Samples(Loop());
            float[] old = Samples(AssetDatabase.LoadAssetAtPath<AudioClip>(OldRainSoundPath));
            float decibels = 20.0f * Mathf.Log10(Rms(loop, 0, loop.Length) / Rms(old, 0, old.Length));
            Assert.AreEqual(0.0f, decibels, 1.5f, "the new rain is louder or quieter than the old: the rain's volumes no longer fit it");
        }
    }
}
