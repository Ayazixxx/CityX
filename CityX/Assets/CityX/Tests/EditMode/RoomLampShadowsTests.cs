using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Which room lamps hold the shadow maps (RoomLampShadows): a lamp holding one lays its light out through the windows, so handing a map on
    /// has to fade that light (owner, 2026-11-10, of the window-shaped light on the pavement: "id like that effect to still be in the game"),
    /// and the maps are never more than the budget, or the cost of the fade would be an extra render of the scene.
    /// </summary>
    public sealed class RoomLampShadowsTests
    {
        private const int Slots = 2;
        private const float Frame = 1.0f / 60.0f;

        private readonly List<GameObject> _made = new List<GameObject>();

        private Light Lamp()
        {
            var go = new GameObject("Lamp");
            _made.Add(go);
            return go.AddComponent<Light>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        private static void Play(RoomLampShadows shadows, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Frame)
            {
                shadows.Step(Frame);
                Assert.LessOrEqual(shadows.Casting, Slots, "more lamps holding a shadow map than there are maps");
            }
        }

        [Test]
        public void AWantedLampComesUpToItsFullStrengthOverItsFade()
        {
            Light lamp = Lamp();
            var shadows = new RoomLampShadows(Slots);
            shadows.Want(lamp);
            shadows.Step(Frame);
            Assert.AreEqual(LightShadows.Soft, lamp.shadows);
            Assert.Greater(lamp.shadowStrength, 0.0f, "a strength of nothing is no shadow map at all to URP");
            Assert.Less(lamp.shadowStrength, RoomLampShadows.FullStrength * 0.2f, "it came on in a frame");
            Play(shadows, RoomLampShadows.FadeSeconds + 0.05f);
            Assert.AreEqual(RoomLampShadows.FullStrength, lamp.shadowStrength, 1e-4f);
        }

        [Test]
        public void ALampNoLongerWantedKeepsItsMapUntilItHasFadedAndOnlyThenTheNextLampHasIt()
        {
            Light first = Lamp(), second = Lamp(), third = Lamp();
            var shadows = new RoomLampShadows(Slots);
            shadows.Want(first);
            shadows.Want(second);
            Play(shadows, RoomLampShadows.FadeSeconds + 0.05f);

            shadows.ClearWanted();
            shadows.Want(second);
            shadows.Want(third);
            Play(shadows, RoomLampShadows.FadeSeconds * 0.5f);
            Assert.AreEqual(LightShadows.Soft, first.shadows, "the lamp lost its map before it had faded: its light left the pavement in one frame");
            Assert.Less(first.shadowStrength, RoomLampShadows.FullStrength, "it is not fading");
            Assert.AreEqual(LightShadows.None, third.shadows, "the next lamp had a map while the last still held it");

            Play(shadows, RoomLampShadows.FadeSeconds * 0.5f + 0.05f);
            Assert.AreEqual(LightShadows.None, first.shadows);
            Assert.AreEqual(LightShadows.Soft, third.shadows, "the freed map went to nobody");
            Assert.AreEqual(RoomLampShadows.FullStrength, second.shadowStrength, 1e-4f, "the lamp that stayed wanted changed");
        }

        [Test]
        public void SnappingPutsTheChoiceInForceAtOnce()
        {
            Light first = Lamp(), second = Lamp();
            var shadows = new RoomLampShadows(Slots);
            shadows.Want(first);
            shadows.Snap();
            Assert.AreEqual(RoomLampShadows.FullStrength, first.shadowStrength);
            shadows.ClearWanted();
            shadows.Want(second);
            shadows.Snap();
            Assert.AreEqual(LightShadows.None, first.shadows);
            Assert.AreEqual(LightShadows.Soft, second.shadows);
            Assert.AreEqual(1, shadows.Casting);
        }

        [Test]
        public void ALampBackInThePoolHoldsNoMap()
        {
            Light lamp = Lamp();
            var shadows = new RoomLampShadows(Slots);
            shadows.Want(lamp);
            shadows.Snap();
            shadows.Forget(lamp);
            Assert.AreEqual(0, shadows.Casting);
            shadows.Step(Frame);
            Assert.AreEqual(0, shadows.Casting, "a forgotten lamp was given a map again");
        }

        [Test]
        public void TheShaderKnowsALampsFullStrength()
        {
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "CityX/View/Shaders/CityXToonLighting.hlsl"));
            Match define = Regex.Match(text, @"#define CITYX_ROOM_LAMP_SHADOW_STRENGTH ([0-9.]+)");
            Assert.IsTrue(define.Success, "CityXToonLighting.hlsl no longer says a lamp's full shadow strength");
            Assert.AreEqual(RoomLampShadows.FullStrength, float.Parse(define.Groups[1].Value, CultureInfo.InvariantCulture), 1e-4f);
        }
    }
}
