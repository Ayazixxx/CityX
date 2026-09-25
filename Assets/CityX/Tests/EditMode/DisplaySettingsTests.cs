using CityX.UI;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The resolution list's own bookkeeping -- not the resize itself, which
    /// needs a real window, but whether an index from a saved file or a
    /// stepper button always lands on a real entry.
    /// </summary>
    public sealed class DisplaySettingsTests
    {
        [Test]
        public void TheDefaultIndexIsARealEntry()
        {
            Assert.GreaterOrEqual(DisplaySettings.DefaultIndex, 0);
            Assert.Less(DisplaySettings.DefaultIndex, DisplaySettings.Options.Length);
        }

        [Test]
        public void TheDefaultIs1920x1080()
        {
            DisplaySettings.Resolution resolution = DisplaySettings.Options[DisplaySettings.DefaultIndex];
            Assert.AreEqual(1920, resolution.Width);
            Assert.AreEqual(1080, resolution.Height);
        }

        [Test]
        public void AnInRangeIndexIsLeftAlone()
        {
            Assert.AreEqual(2, DisplaySettings.Clamp(2));
        }

        [Test]
        public void ANegativeIndexClampsToTheFirstEntry()
        {
            Assert.AreEqual(0, DisplaySettings.Clamp(-1));
        }

        /// <summary>
        /// Guards against a settings file saved by a build with a longer
        /// list -- the extra entries are gone, but the saved index should
        /// not be able to read past the end of today's list.
        /// </summary>
        [Test]
        public void AnIndexPastTheEndClampsToTheLastEntry()
        {
            Assert.AreEqual(DisplaySettings.Options.Length - 1, DisplaySettings.Clamp(999));
        }

        [Test]
        public void EveryOptionFormatsAsWidthByHeight()
        {
            var resolution = new DisplaySettings.Resolution(1280, 720);
            Assert.AreEqual("1280x720", resolution.ToString());
        }
    }
}
