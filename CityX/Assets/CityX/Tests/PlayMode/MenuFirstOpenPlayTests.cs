using System;
using System.Collections;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A page whose view sits on its own root, saved hidden (LoadoutPanelBuilder, BookPanelBuilder, PlaceholderPanelBuilder, the C-menu's Player
    /// fly-out in PlayerMenuBuilder), wakes the first time it is shown, and its Awake used to close it again at once: the first Loadout, the first
    /// book read, the first "coming soon" page and the first Player fly-out vanished as they opened (found 2026-10-17; the Stats page had the
    /// same, fixed 2026-10-14). Each is built here as its builder leaves it and shown once.
    /// </summary>
    public sealed class MenuFirstOpenPlayTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
        }

        private T HiddenPage<T>() where T : MonoBehaviour
        {
            _root = new GameObject(typeof(T).Name);
            _root.SetActive(false);
            return _root.AddComponent<T>();
        }

        private IEnumerator ShownOnce(Action show)
        {
            yield return null;
            show();
            yield return null;
            Assert.IsTrue(_root.activeSelf, "the page closed itself the first time it was shown");
        }

        [UnityTest]
        public IEnumerator TheLoadoutPageStaysOpenTheFirstTime()
        {
            var view = HiddenPage<LoadoutPanelView>();
            view.Root = _root;
            yield return ShownOnce(view.Open);
        }

        [UnityTest]
        public IEnumerator TheFirstBookReadStaysOpen()
        {
            var view = HiddenPage<BookPanelView>();
            view.Root = _root;
            yield return ShownOnce(() => view.Show("A book", "Its text"));
        }

        [UnityTest]
        public IEnumerator TheComingSoonPageStaysOpenTheFirstTime()
        {
            var view = HiddenPage<PlaceholderPanelView>();
            view.Root = _root;
            yield return ShownOnce(() => view.Show("Shop", "Coming soon"));
        }

        [UnityTest]
        public IEnumerator ThePlayerFlyOutStaysOpenTheFirstTime()
        {
            var view = HiddenPage<PlayerSubmenuView>();
            view.Root = _root;
            yield return ShownOnce(view.Show);
        }
    }
}
