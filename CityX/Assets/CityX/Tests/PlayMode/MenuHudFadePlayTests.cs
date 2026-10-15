using System.Collections;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The portrait frame (top left) and the hotbar (bottom left) fade out while the C menu or one of its pages is open, and back when it shuts
    /// (owner, 2026-10-14: "make the hotbar and portrait in the top left fade away when you open the c menu").
    /// </summary>
    public sealed class MenuHudFadePlayTests
    {
        private GameObject _hud;
        private GameObject _menuObject;

        [TearDown]
        public void TearDown()
        {
            if (_hud != null) Object.Destroy(_hud);
            if (_menuObject != null) Object.Destroy(_menuObject);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheFrameAndTheBarFadeOutWhileTheMenuIsOpenAndBackWhenItCloses()
        {
            // A HUD canvas with the builder's portrait frame and the bar, and a C menu.
            _hud = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            var frame = new GameObject(MenuHudFade.StatusFrameName, typeof(RectTransform));
            frame.transform.SetParent(_hud.transform, false);
            var bar = _hud.AddComponent<HotbarView>();
            var fade = _hud.AddComponent<MenuHudFade>();
            _menuObject = new GameObject("PlayerMenu");
            var root = new GameObject("Root");
            root.transform.SetParent(_menuObject.transform, false);
            var menu = _menuObject.AddComponent<PlayerMenuView>();
            menu.Root = root;
            menu.Close();
            yield return Wait(0.1f);

            var frameGroup = frame.GetComponent<CanvasGroup>();
            Assert.IsNotNull(frameGroup, "the portrait frame was not given a CanvasGroup to fade");
            var barGroup = bar.Bar.GetComponent<CanvasGroup>();
            Assert.AreEqual(1.0f, frameGroup.alpha, 0.001f, "the frame is not fully shown with the menu shut");
            Assert.AreEqual(1.0f, barGroup.alpha, 0.001f, "the bar is not fully shown with the menu shut");

            root.SetActive(true);   // the C menu opens
            yield return Wait(MenuHudFade.FadeSeconds * 0.5f);
            Assert.Greater(frameGroup.alpha, 0.05f, "the frame vanished at once instead of fading");
            Assert.Less(frameGroup.alpha, 0.95f, "the frame did not start fading");
            yield return Wait(MenuHudFade.FadeSeconds);
            Assert.AreEqual(0.0f, frameGroup.alpha, 0.001f, "the frame is still showing over the open menu");
            Assert.AreEqual(0.0f, barGroup.alpha, 0.001f, "the bar is still showing over the open menu");

            root.SetActive(false);   // and shuts
            yield return Wait(MenuHudFade.FadeSeconds * 1.5f);
            Assert.AreEqual(1.0f, frameGroup.alpha, 0.001f, "the frame did not come back");
            Assert.AreEqual(1.0f, barGroup.alpha, 0.001f, "the bar did not come back");
        }
    }
}
