using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>A headless run must be silent (see BatchModeAudioMute).</summary>
    public sealed class BatchModeAudioMuteTests
    {
        [Test]
        public void ABatchModeRunIsMuted()
        {
            if (!Application.isBatchMode)
            {
                Assert.Ignore("Only meaningful in a headless run -- a hand-opened editor is never muted by this.");
            }

            Assert.IsTrue(EditorUtility.audioMasterMute,
                "a headless test run is playing audio out loud");
        }
    }
}
