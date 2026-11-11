using System.Collections;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The title menu's drawn buttons (DrawnFace; owner, 2026-11-11: "a red aura starts emanating from them as well as the slight grow", "make
    /// the red aura animated kind of like the bloodrage spell"): pointed at, embers rise off the drawing over a flickering haze and the button
    /// grows; let go, the haze and the growth go and the risen embers die away.
    /// </summary>
    public sealed class DrawnButtonPlayTests
    {
        private GameObject _canvas;
        private Texture2D _texture;

        [TearDown]
        public void TearDown()
        {
            if (_canvas != null) Object.Destroy(_canvas);
            if (_texture != null) Object.Destroy(_texture);
        }

        private Button Make()
        {
            _canvas = new GameObject("Canvas", typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _texture = new Texture2D(64, 16);
            Sprite sprite = Sprite.Create(_texture, new Rect(0, 0, 64, 16), new Vector2(0.5f, 0.5f));
            var sources = new[] { new Vector2(0.2f, 0.5f), new Vector2(0.5f, 0.4f), new Vector2(0.8f, 0.6f) };
            return DrawnFace.Make(_canvas.transform, "Singleplayer", sprite, sprite, null, sources, 80.0f);
        }

        private static IEnumerator For(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PointedAtEmbersRiseOffItOverAFlickeringHazeAndItGrowsThenItAllDiesAway()
        {
            Button button = Make();
            var face = button.GetComponent<DrawnFace>();
            var look = button.GetComponent<ButtonLook>();
            Assert.AreEqual(320.0f, ((RectTransform)button.transform).sizeDelta.x, 0.01f, "not as wide as the drawing is");
            yield return null;
            Assert.AreEqual(0.0f, face.AuraShown, "the haze shows before anything points at it");
            Assert.AreEqual(0, face.Embers.Count, "embers rise before anything points at it");

            look.OnPointerEnter(null);
            yield return For(ButtonLook.EaseSeconds + 0.1f);
            Assert.AreEqual(ButtonLook.HoverScale, button.transform.localScale.x, 1e-3f, "it did not grow");
            float dimmest = 1.0f, brightest = 0.0f;
            int most = 0;
            for (float t = 0.0f; t < 1.5f; t += Time.unscaledDeltaTime)
            {
                yield return null;
                dimmest = Mathf.Min(dimmest, face.AuraShown);
                brightest = Mathf.Max(brightest, face.AuraShown);
                most = Mathf.Max(most, face.Embers.Count);
            }
            Assert.Greater(brightest, 0.5f, "the haze did not come in");
            Assert.Greater(brightest - dimmest, 0.1f, "the haze does not flicker");
            Assert.Greater(most, 20, "no embers rose off it");

            look.OnPointerExit(null);
            yield return For(ButtonLook.EaseSeconds + 0.1f);
            Assert.AreEqual(0.0f, face.AuraShown, 1e-3f, "the haze stayed");
            Assert.AreEqual(1.0f, button.transform.localScale.x, 1e-3f);
            yield return For(Embers.LifeMax + 0.1f);
            Assert.AreEqual(0, face.Embers.Count, "embers went on rising after it was let go");
        }
    }
}
