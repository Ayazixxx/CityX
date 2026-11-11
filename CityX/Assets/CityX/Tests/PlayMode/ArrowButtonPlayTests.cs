using System.Collections;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The arrow buttons (ArrowButtons; owner, 2026-11-10: "make yellow outline appear in between the black outline and the arrow when you
    /// hover over the buttons"): pointed at, the band comes in and the arrow grows; only the arrow takes a click, not its box's empty corners.
    /// </summary>
    public sealed class ArrowButtonPlayTests
    {
        private GameObject _canvas;

        [TearDown]
        public void TearDown()
        {
            if (_canvas != null) Object.Destroy(_canvas);
        }

        private Button Make(bool curled)
        {
            _canvas = new GameObject("Canvas", typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return curled
                ? ArrowButtons.Curled(_canvas.transform, "Back", "BACK", new Vector2(150.0f, 70.0f), ArrowButtons.RedTop, ArrowButtons.RedBottom)
                : ArrowButtons.Straight(_canvas.transform, "Proceed", "PROCEED", new Vector2(214.0f, 92.0f), ArrowButtons.GreenTop, ArrowButtons.GreenBottom);
        }

        [UnityTest]
        public IEnumerator PointedAtTheYellowBandComesInAndTheArrowGrowsThenBothGo()
        {
            Button button = Make(false);
            var look = button.GetComponent<ArrowButtonLook>();
            var arrow = button.GetComponent<ArrowGraphic>();
            yield return null;
            Assert.AreEqual(0.0f, arrow.Highlight, "the band shows before anything points at it");

            look.OnPointerEnter(null);
            for (float t = 0.0f; t < ArrowButtonLook.EaseSeconds + 0.1f; t += Time.unscaledDeltaTime)
            {
                yield return null;
            }
            Assert.AreEqual(1.0f, arrow.Highlight, 1e-3f, "the band did not come in");
            Assert.AreEqual(ArrowButtonLook.HoverScale, button.transform.localScale.x, 1e-3f, "the arrow did not grow");

            look.OnPointerExit(null);
            for (float t = 0.0f; t < ArrowButtonLook.EaseSeconds + 0.1f; t += Time.unscaledDeltaTime)
            {
                yield return null;
            }
            Assert.AreEqual(0.0f, arrow.Highlight, 1e-3f, "the band stayed");
            Assert.AreEqual(1.0f, button.transform.localScale.x, 1e-3f);
        }

        [UnityTest]
        public IEnumerator OnlyTheArrowTakesAClickNotTheCornersOfItsBox([Values(false, true)] bool curled)
        {
            Button button = Make(curled);
            yield return null;
            var arrow = button.GetComponent<ArrowGraphic>();
            var rect = (RectTransform)button.transform;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            // Overlay canvas: world is screen.
            Vector2 middleOfTheBand = curled
                ? (Vector2)rect.TransformPoint(arrow.ToLocal(ArrowShape.Polar(ArrowShape.CurveMiddle, 80.0f * Mathf.Deg2Rad)))
                : (Vector2)rect.TransformPoint(arrow.ToLocal(new Vector2(3.0f, 0.0f)));
            Assert.IsTrue(arrow.IsRaycastLocationValid(middleOfTheBand, null), "a click on the arrow is not taken");
            Vector2 emptyCorner = curled ? (Vector2)corners[0] + new Vector2(4.0f, 4.0f) : (Vector2)corners[1] + new Vector2(4.0f, -4.0f);
            Assert.IsFalse(arrow.IsRaycastLocationValid(emptyCorner, null), "a click beside the arrow, in its box, is taken");
        }
    }
}
