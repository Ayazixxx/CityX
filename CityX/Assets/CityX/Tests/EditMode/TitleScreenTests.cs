using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// TitleScreen.Show()'s own logo-hiding rule, confirmed live as a real
    /// bug against the rendered Settings panel: the logo sits dead centre,
    /// exactly where Settings' own heading and resolution value land, and
    /// with nothing hiding it the X drew right over both.
    /// </summary>
    public sealed class TitleScreenTests
    {
        private GameObject _object;
        private TitleScreen _title;
        private GameObject _logoGroup;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Title");
            _title = _object.AddComponent<TitleScreen>();
            _logoGroup = new GameObject("LogoGroup");
            _title.LogoGroup = _logoGroup;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_logoGroup);
        }

        [TestCase(TitleScreen.Panel.Menu, true)]
        [TestCase(TitleScreen.Panel.Multiplayer, true)]
        [TestCase(TitleScreen.Panel.CharacterSelect, false)]
        [TestCase(TitleScreen.Panel.Creator, false)]
        [TestCase(TitleScreen.Panel.Settings, false)]
        public void TheLogoIsHiddenOnlyBehindAFullScreenOfControls(TitleScreen.Panel panel, bool logoVisible)
        {
            _title.Show(panel);

            Assert.AreEqual(logoVisible, _logoGroup.activeSelf,
                $"the logo group's active state for {panel} should be {logoVisible}");
        }
    }
}
