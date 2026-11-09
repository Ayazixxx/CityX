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

        /// <summary>
        /// Near-white eyes, teeth and bone glowed as dots in the sun (owner, 2026-11-07: "small dots of glowing light"): nothing in a body is
        /// paler than its skin, so no piece blooms more than a whole body does; and since 2026-11-08 none past 0.5, so no small pale bit lies in
        /// the dark as a point of light.
        /// </summary>
        [Test]
        public void NoPieceOfGoreIsPalerThanSkin()
        {
            var skin = AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/PlayerSkin.mat");
            Assert.IsNotNull(skin, "no player skin material");
            Assert.LessOrEqual(Gore.Palest, skin.GetColor("_BaseColor").maxColorComponent, "the palest gore is paler than the skin");
            foreach (GoreAnatomy.Tissue tissue in System.Enum.GetValues(typeof(GoreAnatomy.Tissue)))
            {
                for (int variant = 0; variant < 4; variant++)
                {
                    Assert.LessOrEqual(Gore.ColourOf(tissue, variant).maxColorComponent, Gore.Palest + 1e-4f, $"{tissue} is paler than skin");
                }
            }
            Assert.LessOrEqual(Gore.Collar.maxColorComponent, Gore.Palest);
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

        /// <summary>
        /// A pixel that is not a number goes into the bloom as the brightest thing there can be: a glowing dot, flickering as the view moves
        /// (owner, 2026-11-08: "they flicker as i move my mouse"). The camera that shows the game draws one black, whatever made it.
        /// </summary>
        [Test]
        public void TheGamesCameraDrawsABrokenPixelBlack()
        {
            string[] scene = System.IO.File.ReadAllLines("Assets/CityX/Scenes/CityMassing.unity");
            int graded = System.Array.FindIndex(scene, line => line.Trim() == "m_RenderPostProcessing: 1");
            Assert.GreaterOrEqual(graded, 0, "no camera in the game's scene has the grade on: rebuild the scene");
            bool stops = false;
            for (int i = graded; i < Mathf.Min(scene.Length, graded + 8); i++)
            {
                stops |= scene[i].Trim() == "m_StopNaN: 1";
            }
            Assert.IsTrue(stops, "the game's camera lets broken pixels into the bloom: rebuild the scene");
        }
    }
}
