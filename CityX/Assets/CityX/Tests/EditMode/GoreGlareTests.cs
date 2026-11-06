using CityX.Character;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CityX.Tests
{
    /// <summary>
    /// No flashlight glare off the gore (owner, 2026-11-06, with screenshots: "there is still these bright flashing lights from the
    /// dismemberment"): glossy pieces in the sun or under a lamp caught it as pinpoints thousands of times brighter than white, and the bloom,
    /// uncapped, spread each into a blob of glare. Every piece is matte now, and the bloom is capped.
    /// </summary>
    public sealed class GoreGlareTests
    {
        [Test]
        public void EveryPieceOfGoreIsMatte()
        {
            foreach (GoreAnatomy.Tissue tissue in System.Enum.GetValues(typeof(GoreAnatomy.Tissue)))
            {
                Material material = Gore.MaterialOf(tissue);
                Assert.AreEqual(0.0f, material.GetFloat("_Smoothness"), $"{tissue} is glossy");
                Assert.IsTrue(material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"), $"{tissue} still catches highlights");
            }
        }

        [Test]
        public void TheCitysBloomIsCapped()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/CityX/View/Materials/CityGrade.asset");
            Assert.IsNotNull(profile, "no city grade profile: rebuild the scene");
            Assert.IsTrue(profile.TryGet(out Bloom bloom), "the city grade has no bloom");
            Assert.IsTrue(bloom.clamp.overrideState, "the bloom is not capped, so one bright pinpoint blooms into a glare");
            Assert.LessOrEqual(bloom.clamp.value, ViewEditor.CityMassingSceneBuilder.BloomClamp);
            Assert.Greater(bloom.clamp.value, View.DayNightCycle.WindowGlow, "the cap would dim the lit windows' own glow");
        }
    }
}
