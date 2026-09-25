using CityX.UI;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// OutputOptions is asserted as an exact, permanent single-item list --
    /// see its own doc comment on why there is genuinely nothing else Unity
    /// can offer there. InputOptions only asserts "Default" is first;
    /// asserting anything about real device COUNT would assume this test
    /// runner's own hardware, which a headless CI machine may not have any
    /// of at all.
    /// </summary>
    public sealed class VoiceDeviceOptionsTests
    {
        [Test]
        public void OutputOptionsIsAlwaysExactlyDefault()
        {
            string[] options = VoiceDeviceOptions.OutputOptions();
            Assert.AreEqual(new[] { "Default" }, options);
        }

        [Test]
        public void InputOptionsAlwaysStartsWithDefault()
        {
            string[] options = VoiceDeviceOptions.InputOptions();
            Assert.GreaterOrEqual(options.Length, 1);
            Assert.AreEqual("Default", options[0]);
        }

        [Test]
        public void IndexOfFindsARealName()
        {
            var options = new[] { "Default", "Headset Mic" };
            Assert.AreEqual(1, VoiceDeviceOptions.IndexOf(options, "Headset Mic"));
        }

        [Test]
        public void IndexOfFallsBackToDefaultForAVanishedDevice()
        {
            var options = new[] { "Default", "Headset Mic" };
            Assert.AreEqual(0, VoiceDeviceOptions.IndexOf(options, "A USB Mic That Got Unplugged"));
        }
    }
}
