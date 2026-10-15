using System.Collections.Generic;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// The recorded thunder (owner, 2026-10-14: "add these 3 as thunder sounds during the rain ... randomly pick between the 3 sounds, we will
    /// introduce more later"): every recording gets played, never the same one twice running, a far strike is muffled, and the scene's rain
    /// carries all three.
    /// </summary>
    public sealed class ThunderRecordingTests
    {
        [Test]
        public void EachStrikePicksARecordingAtRandomNeverTheSameTwiceRunning()
        {
            var host = new GameObject("Rain");
            try
            {
                var rain = host.AddComponent<RainEffect>();
                rain.ThunderRecordings = new[]
                {
                    AudioClip.Create("a", 100, 1, 44100, false),
                    AudioClip.Create("b", 100, 1, 44100, false),
                    AudioClip.Create("c", 100, 1, 44100, false),
                };
                var seen = new HashSet<AudioClip>();
                AudioClip last = null;
                for (int i = 0; i < 60; i++)
                {
                    AudioClip clip = rain.ThunderClipFor(500.0f);
                    Assert.IsNotNull(clip);
                    Assert.AreNotSame(last, clip, "the same recording played twice running");
                    CollectionAssert.Contains(rain.ThunderRecordings, clip, "a strike played something other than a recording");
                    seen.Add(clip);
                    last = clip;
                }
                Assert.AreEqual(3, seen.Count, "not every recording was ever picked");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AFarStrikeIsMuffledAndANearOneIsNot()
        {
            Assert.AreEqual(22000.0f, RainEffect.RecordedThunderCutoffHz(100.0f), 1.0f, "a near strike keeps its crack");
            Assert.AreEqual(1200.0f, RainEffect.RecordedThunderCutoffHz(3000.0f), 1.0f, "a far strike is a rumble");
            Assert.Greater(RainEffect.RecordedThunderCutoffHz(600.0f), RainEffect.RecordedThunderCutoffHz(1000.0f), "farther is not more muffled");
        }

#if UNITY_EDITOR
        [Test]
        public void TheCitysRainCarriesTheThreeRecordings()
        {
            foreach (string name in new[] { "thunder_loud", "thunder_strike", "thunder_clap" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/CityX/UI/Audio/{name}.ogg");
                Assert.IsNotNull(clip, name + " is missing");
                Assert.Greater(clip.length, 3.0f, name + " was cut too short");
            }
        }
#endif
    }
}
