using CityX.View;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The radio. What is worth pinning here is not "does a clip play" --
    /// that needs a live audio device, which a headless test run does not
    /// have -- but the bookkeeping around it: that switching off and back on
    /// resumes rather than restarts, and that the playlist wraps rather than
    /// running off the end.
    /// </summary>
    public sealed class RadioTests
    {
        private GameObject _object;
        private Radio _radio;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Radio");
            _radio = _object.AddComponent<Radio>();
            _radio.Bind();
            _radio.Playlist = new[]
            {
                AudioClip.Create("A", 1, 1, 44100, false),
                AudioClip.Create("B", 1, 1, 44100, false),
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void ItStartsOff()
        {
            Assert.IsFalse(_radio.IsPoweredOn);
        }

        [Test]
        public void TogglingOnStartsTheFirstTrack()
        {
            _radio.Toggle();

            Assert.IsTrue(_radio.IsPoweredOn);
            Assert.AreEqual(0, _radio.Track);
        }

        /// <summary>
        /// Power off and back on must not be indistinguishable from stop and
        /// restart -- it has to leave the same track selected rather than
        /// silently reaching for the next one.
        /// </summary>
        [Test]
        public void TogglingOffAndBackOnKeepsTheSameTrack()
        {
            _radio.Toggle();
            _radio.Toggle();
            Assert.IsFalse(_radio.IsPoweredOn);

            _radio.Toggle();

            Assert.IsTrue(_radio.IsPoweredOn);
            Assert.AreEqual(0, _radio.Track, "powering back on jumped to a different track");
        }

        [Test]
        public void WithNoPlaylistTogglingDoesNotThrow()
        {
            var empty = new GameObject("EmptyRadio").AddComponent<Radio>();
            empty.Bind();
            try
            {
                Assert.DoesNotThrow(() => empty.Toggle());
            }
            finally
            {
                Object.DestroyImmediate(empty.gameObject);
            }
        }

        [Test]
        public void ThePlaylistWrapsRatherThanRunningOffTheEnd()
        {
            Assert.AreEqual(1, Radio.NextTrack(0, 2));
            Assert.AreEqual(0, Radio.NextTrack(1, 2), "the last track did not wrap back to the first");
        }

        [Test]
        public void AnEmptyPlaylistNeverProducesAnOutOfRangeIndex()
        {
            Assert.AreEqual(0, Radio.NextTrack(0, 0));
        }
    }
}
