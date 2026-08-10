using CityX.Core;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// The Phase 0 spike launches one headless server and thirty clients from
    /// a script. If argv parsing is wrong, every one of those processes comes
    /// up misconfigured and the spike measures nothing. Cheap to cover, so
    /// covered properly.
    /// </summary>
    public class LaunchArgsTests
    {
        [Test]
        public void NoArgumentsMeansSingleplayer()
        {
            LaunchArgs args = LaunchArgs.Parse(new string[0]);
            Assert.IsTrue(args.IsValid, args.Error);
            Assert.AreEqual(GameMode.Singleplayer, args.Mode);
            Assert.IsFalse(GameModeInfo.UsesTransport(args.Mode));
        }

        [Test]
        public void NullArgvIsTreatedAsNoArguments()
        {
            LaunchArgs args = LaunchArgs.Parse(null);
            Assert.IsTrue(args.IsValid, args.Error);
            Assert.AreEqual(GameMode.Singleplayer, args.Mode);
        }

        [Test]
        public void ParsesADedicatedShardLine()
        {
            LaunchArgs args = LaunchArgs.Parse(new[] { "-mode", "dedicated", "-port", "7770", "-seed", "12345" });
            Assert.IsTrue(args.IsValid, args.Error);
            Assert.AreEqual(GameMode.DedicatedShard, args.Mode);
            Assert.AreEqual(7770, args.Port);
            Assert.AreEqual(12345, args.WorldSeed);
            Assert.IsFalse(GameModeInfo.RunsClient(args.Mode));
        }

        [Test]
        public void ParsesAJoiningClientLine()
        {
            LaunchArgs args = LaunchArgs.Parse(new[] { "-mode", "client", "-address", "203.0.113.7", "-port", "7000" });
            Assert.IsTrue(args.IsValid, args.Error);
            Assert.AreEqual(GameMode.RemoteClient, args.Mode);
            Assert.AreEqual("203.0.113.7", args.Address);
            Assert.AreEqual(7000, args.Port);
        }

        /// <summary>
        /// Unity hands player builds a pile of engine flags. Choking on those
        /// would mean the spike's own server refuses to start under
        /// -batchmode, which is exactly how it has to run.
        /// </summary>
        [Test]
        public void EngineFlagsAreIgnoredRatherThanRejected()
        {
            LaunchArgs args = LaunchArgs.Parse(new[]
            {
                "C:/builds/CityX.exe", "-batchmode", "-nographics",
                "-logFile", "server.log",
                "-mode", "dedicated",
            });
            Assert.IsTrue(args.IsValid, args.Error);
            Assert.AreEqual(GameMode.DedicatedShard, args.Mode);
        }

        /// <summary>
        /// A shard that quietly comes up on the wrong port is worse than one
        /// that refuses to start, so bad values for OUR flags are hard errors.
        /// </summary>
        [Test]
        public void MalformedValuesForOurOwnFlagsAreHardErrors()
        {
            Assert.IsFalse(LaunchArgs.Parse(new[] { "-port", "notanumber" }).IsValid);
            Assert.IsFalse(LaunchArgs.Parse(new[] { "-port", "0" }).IsValid, "port 0 is not bindable as a listen port");
            Assert.IsFalse(LaunchArgs.Parse(new[] { "-port", "70000" }).IsValid, "above ushort range");
            Assert.IsFalse(LaunchArgs.Parse(new[] { "-seed", "12.5" }).IsValid);
            Assert.IsFalse(LaunchArgs.Parse(new[] { "-mode", "coop" }).IsValid);
        }

        /// <summary>
        /// `-port -batchmode` must report a missing value, not swallow the
        /// next flag as if it were the port.
        /// </summary>
        [Test]
        public void AFlagFollowedByAnotherFlagIsAMissingValue()
        {
            LaunchArgs args = LaunchArgs.Parse(new[] { "-port", "-batchmode" });
            Assert.IsFalse(args.IsValid);
            StringAssert.Contains("-port", args.Error);
        }

        [Test]
        public void ModeAliasesResolveToTheSameTopology()
        {
            Assert.AreEqual(GameMode.DedicatedShard, LaunchArgs.Parse(new[] { "-mode", "server" }).Mode);
            Assert.AreEqual(GameMode.DedicatedShard, LaunchArgs.Parse(new[] { "-mode", "SHARD" }).Mode);
            Assert.AreEqual(GameMode.HostedWorld, LaunchArgs.Parse(new[] { "-mode", "listen" }).Mode);
            Assert.AreEqual(GameMode.RemoteClient, LaunchArgs.Parse(new[] { "-mode", "join" }).Mode);
            Assert.AreEqual(GameMode.Singleplayer, LaunchArgs.Parse(new[] { "-mode", "offline" }).Mode);
        }

        /// <summary>
        /// The seed is the world. Two processes given the same seed must
        /// generate the same city, so a negative or large seed has to survive
        /// the round trip intact rather than being clamped.
        /// </summary>
        [Test]
        public void SeedSurvivesNegativeAndExtremeValues()
        {
            Assert.AreEqual(-99, LaunchArgs.Parse(new[] { "-seed", "-99" }).WorldSeed);
            Assert.AreEqual(int.MaxValue, LaunchArgs.Parse(new[] { "-seed", int.MaxValue.ToString() }).WorldSeed);
            Assert.AreEqual(int.MinValue, LaunchArgs.Parse(new[] { "-seed", int.MinValue.ToString() }).WorldSeed);
        }
    }
}
