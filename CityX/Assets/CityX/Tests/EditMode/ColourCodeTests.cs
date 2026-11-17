using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The creator's colour codes (owner, 2026-11-17: "make the color code visible at the bottom and it lets you enter color codes to get an
    /// exact hue") and the circle on the colour square where the colour is ("i want it in the box where you select your color").
    /// </summary>
    public sealed class ColourCodeTests
    {
        [Test]
        public void AColourIsWrittenAsItsSixDigitCode()
        {
            Assert.AreEqual("#3A6BD9", ColourCode.Of(new Color32(0x3A, 0x6B, 0xD9, 255)));
            Assert.AreEqual("#000000", ColourCode.Of(Color.black));
            Assert.AreEqual("#FFFFFF", ColourCode.Of(Color.white));
        }

        [TestCase("#3A6BD9")]
        [TestCase("#3a6bd9")]
        [TestCase("3A6BD9")]
        [TestCase(" #3A 6B D9 ")]
        public void ASixDigitCodeIsReadWithOrWithoutTheHashInAnyCase(string typed)
        {
            Assert.IsTrue(ColourCode.TryRead(typed, out Color colour), typed);
            Assert.AreEqual((Color)new Color32(0x3A, 0x6B, 0xD9, 255), colour);
        }

        [Test]
        public void AThreeDigitCodeDoublesEachDigit()
        {
            Assert.IsTrue(ColourCode.TryRead("#f80", out Color colour));
            Assert.AreEqual("#FF8800", ColourCode.Of(colour));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("#")]
        [TestCase("#12345")]
        [TestCase("#1234567")]
        [TestCase("#12Z456")]
        [TestCase("red")]
        public void AnythingElseIsNotACode(string typed)
        {
            Assert.IsFalse(ColourCode.TryRead(typed, out _), typed ?? "null");
        }

        [Test]
        public void EveryColourReadsBackAsTheCodeItWasWrittenAs()
        {
            var random = new System.Random(17);
            for (int i = 0; i < 200; i++)
            {
                var colour = new Color32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255);
                string code = ColourCode.Of(colour);
                Assert.IsTrue(ColourCode.TryRead(code, out Color read), code);
                Assert.AreEqual(code, ColourCode.Of(read));
            }
        }

        [Test]
        public void TheColourMarkerIsARingWithAClearMiddleAndADarkOutsideLine()
        {
            const int size = ColourMarker.PictureSize;
            Color32[] ring = ColourMarker.RingPixels(size);
            Color32 At(Color32[] pixels, float share) => pixels[(size / 2) * size + Mathf.Clamp(Mathf.RoundToInt(size * 0.5f * (1.0f + share)), 0, size - 1)];
            Assert.AreEqual(0, At(ring, 0.0f).a, "the ring's middle is not see-through (the hue shows there)");
            Assert.AreEqual(0, ring[0].a, "the ring's corner is not see-through");
            Color32 band = At(ring, (ColourMarker.RingInner + ColourMarker.RingOuter) * 0.5f);
            Assert.AreEqual(255, band.a, "the ring's band is not solid");
            Assert.Greater(band.r, 200, "the ring's band is not white");
            Color32 rim = At(ring, ColourMarker.RingOuter - ColourMarker.Outline * 0.5f);
            Assert.Less(rim.r, 100, "the ring has no dark line round its outside (it would vanish on the pale corner)");

            Color32[] disc = ColourMarker.DiscPixels(size);
            Assert.AreEqual(255, At(disc, 0.0f).a, "the colour disc is not solid in the middle");
            Assert.AreEqual(0, At(disc, ColourMarker.RingOuter).a, "the colour disc spills past the ring");
        }

        [Test]
        public void TheCodesInkIsDarkOnALightColourAndLightOnADarkOne()
        {
            Assert.GreaterOrEqual(ColourCodeField.Luma(Color.yellow), ColourCodeField.LightFrom);
            Assert.GreaterOrEqual(ColourCodeField.Luma(Color.white), ColourCodeField.LightFrom);
            Assert.Less(ColourCodeField.Luma(Color.blue), ColourCodeField.LightFrom);
            Assert.Less(ColourCodeField.Luma(Color.black), ColourCodeField.LightFrom);
        }
    }
}
