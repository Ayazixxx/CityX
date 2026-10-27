using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EVERY PLAYMODE TEST RUNS AT A STEADY 60 FRAMES PER SIMULATED SECOND.
    ///
    /// A batch-mode frame takes a fraction of a millisecond, and how long exactly depends on what the machine is doing. Many tests step their
    /// subject at a fixed 1/60 s per frame (a swing, a jetpack) while the Animator and every rate check (the pose watchdog's degrees per second,
    /// the arm-snap guard) used the real, tiny Time.deltaTime -- so a perfectly ordinary 22-degree step of the walk/run blend between two frames
    /// read as 5,000 degrees a second and was "a snap" (the watchdog test failed every run for weeks), and anything physical happened or did not
    /// by luck. With Time.captureFramerate at 60, every frame advances the clock by exactly 1/60 s however fast the machine renders it.
    ///
    /// Set once for the whole run (an assembly-level test action is not honoured by Unity's runner for coroutine tests). The tests that set
    /// their own rate put it back to this one, not to 0, when they finish.
    /// </summary>
    [SetUpFixture]
    public sealed class SteadyFrameTime
    {
        public const int FramesPerSecond = 60;

        [OneTimeSetUp]
        public void Steady()
        {
            Time.captureFramerate = FramesPerSecond;
        }

        [OneTimeTearDown]
        public void Release()
        {
            Time.captureFramerate = 0;
        }
    }

    /// <summary>Proves the steady frame time is in force for a coroutine test, without the test asking for it.</summary>
    public sealed class SteadyFrameTimeTests
    {
        [UnityTest]
        public IEnumerator ACoroutineTestRunsAtSixtyFramesASecond()
        {
            yield return null;
            yield return null;
            Assert.AreEqual(SteadyFrameTime.FramesPerSecond, Time.captureFramerate, "the steady frame time is not set for coroutine tests");
            Assert.AreEqual(1.0f / SteadyFrameTime.FramesPerSecond, Time.deltaTime, 1e-4f, "a frame is not 1/60 s");
        }

        /// <summary>
        /// No test leaves the clock running in real time behind it: twelve did (their tear-downs set 0), and every test after one ran on a
        /// clock of a fraction of a millisecond a frame (WindowSprayFramePlayTests, run after WhiteSneakerPlayTests, fired 7 to 16 shots
        /// instead of 29 and failed every full run).
        /// </summary>
        [Test]
        public void NoTestLeavesTheClockRunningInRealTime()
        {
            string folder = System.IO.Path.Combine(Application.dataPath, "CityX", "Tests", "PlayMode");
            var offenders = new System.Collections.Generic.List<string>();
            foreach (string file in System.IO.Directory.GetFiles(folder, "*.cs"))
            {
                string name = System.IO.Path.GetFileName(file);
                if (name != "SteadyFrameTime.cs" && System.IO.File.ReadAllText(file).Contains("captureFramerate = 0"))
                {
                    offenders.Add(name);
                }
            }
            Assert.IsEmpty(offenders, "these set the clock back to real time instead of SteadyFrameTime.FramesPerSecond");
        }
    }
}
