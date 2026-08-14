using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The claim banner's fade timeline. Ported from ui/claim_banner.gd.
    /// </summary>
    public sealed class ClaimBannerTests
    {
        private GameObject _object;
        private ClaimBanner _banner;
        private Text _label;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Banner");
            _banner = _object.AddComponent<ClaimBanner>();
            _label = _object.AddComponent<Text>();
            _banner.Label = _label;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void AlphaIsZeroAtTheVeryStart()
        {
            float alpha = ClaimBanner.AlphaFor(0.0f, ClaimBanner.FadeInSeconds, ClaimBanner.HoldSeconds, ClaimBanner.FadeOutSeconds);
            Assert.AreEqual(0.0f, alpha);
        }

        [Test]
        public void AlphaReachesFullPartwayThroughFadeIn()
        {
            float half = ClaimBanner.AlphaFor(ClaimBanner.FadeInSeconds * 0.5f, ClaimBanner.FadeInSeconds, ClaimBanner.HoldSeconds, ClaimBanner.FadeOutSeconds);
            Assert.AreEqual(0.5f, half, 0.001f);
        }

        [Test]
        public void AlphaStaysFullThroughTheHold()
        {
            float startOfHold = ClaimBanner.AlphaFor(ClaimBanner.FadeInSeconds, ClaimBanner.FadeInSeconds, ClaimBanner.HoldSeconds, ClaimBanner.FadeOutSeconds);
            float endOfHold = ClaimBanner.AlphaFor(ClaimBanner.FadeInSeconds + ClaimBanner.HoldSeconds - 0.01f, ClaimBanner.FadeInSeconds, ClaimBanner.HoldSeconds, ClaimBanner.FadeOutSeconds);

            Assert.AreEqual(1.0f, startOfHold, 0.001f);
            Assert.AreEqual(1.0f, endOfHold, 0.001f);
        }

        [Test]
        public void AlphaFadesBackToZero()
        {
            float total = ClaimBanner.TotalDuration();
            float atEnd = ClaimBanner.AlphaFor(total, ClaimBanner.FadeInSeconds, ClaimBanner.HoldSeconds, ClaimBanner.FadeOutSeconds);
            float beyond = ClaimBanner.AlphaFor(total + 5.0f, ClaimBanner.FadeInSeconds, ClaimBanner.HoldSeconds, ClaimBanner.FadeOutSeconds);

            Assert.AreEqual(0.0f, atEnd, 0.001f);
            Assert.AreEqual(0.0f, beyond, "elapsed time past the whole fade should stay clamped at zero, not go negative");
        }

        [Test]
        public void ShowingAMessageSetsTheTextAndStartsInvisible()
        {
            _banner.ShowMessage("BUILDING ACQUIRED");

            Assert.AreEqual("BUILDING ACQUIRED", _label.text);
            Assert.AreEqual(0.0f, _label.color.a, 0.001f, "a freshly shown message should start faded out, not snap straight to visible");
            Assert.IsTrue(_banner.IsShowing);
        }

        [Test]
        public void TickingThroughTheWholeDurationHidesItAndFiresFinished()
        {
            _banner.ShowMessage("BUILDING ACQUIRED");
            bool finished = false;
            _banner.Finished += () => finished = true;

            _banner.Tick(ClaimBanner.TotalDuration() + 1.0f);

            Assert.IsTrue(finished);
            Assert.IsFalse(_banner.IsShowing);
            Assert.AreEqual(0.0f, _label.color.a, 0.001f);
        }

        [Test]
        public void ReTriggeringWhileVisibleRestartsFromTheBeginning()
        {
            _banner.ShowMessage("first");
            _banner.Tick(ClaimBanner.FadeInSeconds + ClaimBanner.HoldSeconds * 0.5f);
            Assert.AreEqual(1.0f, _label.color.a, 0.001f, "should be fully visible mid-hold");

            _banner.ShowMessage("second");

            Assert.AreEqual("second", _label.text);
            Assert.AreEqual(0.0f, _label.color.a, 0.001f, "re-triggering should restart the fade, not jump in mid-way");
        }
    }
}
