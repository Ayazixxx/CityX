using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The fire's noise and materials (docs/FIRE.md): the noise tiles without a seam and fills its range; the materials are the fire shader's.</summary>
    public sealed class FireSpritesTests
    {
        /// <summary>The step from the last texel to the first (across the seam) is an ordinary step, no bigger than the steps inside.</summary>
        [Test]
        public void TheNoiseTilesWithoutASeam()
        {
            Texture2D noise = FireSprites.Noise();
            int last = FireSprites.NoiseSize - 1;
            int middle = FireSprites.NoiseSize / 2;
            float seamAcross = 0.0f, insideAcross = 0.0f, seamUp = 0.0f, insideUp = 0.0f;
            for (int i = 0; i < FireSprites.NoiseSize; i++)
            {
                seamAcross += Step(noise.GetPixel(last, i), noise.GetPixel(0, i));
                insideAcross += Step(noise.GetPixel(middle - 1, i), noise.GetPixel(middle, i));
                seamUp += Step(noise.GetPixel(i, last), noise.GetPixel(i, 0));
                insideUp += Step(noise.GetPixel(i, middle - 1), noise.GetPixel(i, middle));
            }
            Assert.Less(seamAcross, insideAcross * 1.5f + 0.5f, "the noise jumps across its side seam");
            Assert.Less(seamUp, insideUp * 1.5f + 0.5f, "the noise jumps across its top seam");
        }

        private static float Step(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g);
        }

        [Test]
        public void TheNoiseFillsItsRange()
        {
            Color[] pixels = FireSprites.Noise().GetPixels();
            float fineLow = 1.0f, fineHigh = 0.0f, coarseLow = 1.0f, coarseHigh = 0.0f;
            foreach (Color pixel in pixels)
            {
                fineLow = Mathf.Min(fineLow, pixel.r);
                fineHigh = Mathf.Max(fineHigh, pixel.r);
                coarseLow = Mathf.Min(coarseLow, pixel.g);
                coarseHigh = Mathf.Max(coarseHigh, pixel.g);
            }
            Assert.Less(fineLow, 0.02f);
            Assert.Greater(fineHigh, 0.98f);
            Assert.Less(coarseLow, 0.02f);
            Assert.Greater(coarseHigh, 0.98f);
        }

        [Test]
        public void TheFlamesAndTheSmokeAreTheFireShadersWithTheNoiseOnThem()
        {
            Material flames = FireSprites.Flames();
            Material smoke = FireSprites.Smoke();
            Assert.AreEqual(FireSprites.ShaderName, flames.shader.name, "the flames are not drawn by the fire shader");
            Assert.AreEqual(FireSprites.ShaderName, smoke.shader.name, "the smoke is not drawn by the fire shader");
            Assert.AreEqual(0.0f, flames.GetFloat("_Mode"));
            Assert.AreEqual(1.0f, smoke.GetFloat("_Mode"));
            Assert.AreSame(FireSprites.Noise(), flames.GetTexture("_NoiseTex"));
            Assert.AreSame(FireSprites.Noise(), smoke.GetTexture("_NoiseTex"));
        }

        [Test]
        public void TheFireSpellsBurningGasIsTheFireShadersThirdModeWithTheNoiseOnIt()
        {
            Material gas = FireSprites.Gas();
            Assert.AreEqual(FireSprites.ShaderName, gas.shader.name, "the fire spells' gas is not drawn by the fire shader");
            Assert.AreEqual(2.0f, gas.GetFloat("_Mode"));
            Assert.AreSame(FireSprites.Noise(), gas.GetTexture("_NoiseTex"));
            Assert.IsNotNull(Resources.Load<Material>(FireSprites.GasMaterialName), "no FireGas material in Resources -- run CityX/Rebuild Fire Materials");
        }

        [Test]
        public void TheFireMaterialsAreAssetsSoABuildKeepsTheShader()
        {
            Assert.IsNotNull(Resources.Load<Material>(FireSprites.FlameMaterialName), "no FireFlames material in Resources -- run CityX/Rebuild Fire Materials");
            Assert.IsNotNull(Resources.Load<Material>(FireSprites.SmokeMaterialName), "no FireSmoke material in Resources -- run CityX/Rebuild Fire Materials");
        }
    }
}
