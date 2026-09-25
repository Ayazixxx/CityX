using CityX.UI;
using CityX.View;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// What a console command means. The typing and the text box are not here
    /// -- what can be quietly wrong is the parsing and the times of day.
    /// </summary>
    public sealed class DevCommandsTests
    {
        [Test]
        public void TheLeadingSlashIsOptional()
        {
            Assert.AreEqual("weather", DevCommands.Parse("/weather rain").Verb);
            Assert.AreEqual("weather", DevCommands.Parse("weather rain").Verb);
        }

        [Test]
        public void VerbsAreCaseInsensitiveAndArgumentsSurvive()
        {
            DevCommands.Line line = DevCommands.Parse("  /TIME   Sunset  ");

            Assert.AreEqual("time", line.Verb);
            Assert.AreEqual("Sunset", line.Argument(0));
        }

        [Test]
        public void AnEmptyLineIsNotACommand()
        {
            Assert.IsTrue(DevCommands.Parse(null).IsEmpty);
            Assert.IsTrue(DevCommands.Parse("").IsEmpty);
            Assert.IsTrue(DevCommands.Parse("   ").IsEmpty);
            Assert.IsTrue(DevCommands.Parse("/").IsEmpty);
        }

        [Test]
        public void AskingForAnArgumentThatIsNotThereGivesNullRatherThanThrowing()
        {
            DevCommands.Line line = DevCommands.Parse("/fly");

            Assert.IsNull(line.Argument(0));
            Assert.IsNull(line.Argument(9));
        }

        /// <summary>
        /// Sunrise and sunset are where the warm light actually is, and both
        /// are derived from the cycle's own day/night split rather than typed
        /// in -- so retuning the balance moves them with it.
        /// </summary>
        [Test]
        public void SunsetIsTheMomentTheSunReachesTheHorizon()
        {
            Assert.IsTrue(DevCommands.TryTimeOfDay(
                "sunset", SkyCycle.DaySeconds, SkyCycle.CycleSeconds, out float sunset));
            Assert.AreEqual(SkyCycle.DaySeconds, sunset, 0.001f);

            SkyCycle.State state = SkyCycle.At(sunset);
            Assert.Greater(state.SunsetAmount, 0.9f, "'sunset' does not land on the golden hour");

            Assert.IsTrue(DevCommands.TryTimeOfDay(
                "sunrise", SkyCycle.DaySeconds, SkyCycle.CycleSeconds, out float sunrise));
            Assert.Greater(SkyCycle.At(sunrise).SunsetAmount, 0.9f);
        }

        [Test]
        public void NoonIsLitAndMidnightIsNot()
        {
            DevCommands.TryTimeOfDay("noon", SkyCycle.DaySeconds, SkyCycle.CycleSeconds,
                out float noon);
            DevCommands.TryTimeOfDay("midnight", SkyCycle.DaySeconds, SkyCycle.CycleSeconds,
                out float midnight);

            Assert.Greater(SkyCycle.At(noon).Daylight, 0.99f);
            Assert.Less(SkyCycle.At(midnight).Daylight, 0.01f);
        }

        /// <summary>
        /// A bare fraction of the cycle, for anywhere the names do not reach.
        /// Parsed invariantly, because a machine set to a comma-decimal locale
        /// would otherwise read "0.5" as an unparseable string and silently
        /// print the usage line instead.
        /// </summary>
        [Test]
        public void ABareFractionWorksAndIsClamped()
        {
            Assert.IsTrue(DevCommands.TryTimeOfDay("0.5", 100.0f, 200.0f, out float half));
            Assert.AreEqual(100.0f, half, 0.001f);

            Assert.IsTrue(DevCommands.TryTimeOfDay("5", 100.0f, 200.0f, out float past));
            Assert.AreEqual(200.0f, past, 0.001f);

            Assert.IsTrue(DevCommands.TryTimeOfDay("-3", 100.0f, 200.0f, out float before));
            Assert.AreEqual(0.0f, before, 0.001f);
        }

        [Test]
        public void ATimeThatIsNeitherANameNorANumberIsRefused()
        {
            Assert.IsFalse(DevCommands.TryTimeOfDay(
                "elevenish", SkyCycle.DaySeconds, SkyCycle.CycleSeconds, out _));
        }

        /// <summary>
        /// Every verb the console answers to has a usage line. A command that
        /// exists but cannot tell you how to use it is one nobody will find.
        /// </summary>
        [Test]
        public void EveryVerbDocumentsItself()
        {
            foreach (string verb in DevCommands.Verbs)
            {
                Assert.IsNotEmpty(DevCommands.UsageFor(verb) ?? string.Empty,
                    $"'{verb}' has no usage line");
            }
        }
    }
}
