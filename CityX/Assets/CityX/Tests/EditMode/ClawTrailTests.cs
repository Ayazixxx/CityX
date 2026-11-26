using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A claw's streak, as in the owner's picture (2026-11-25): pointed at both ends and fullest in the middle, along a smooth curve through
    /// where the claw went.
    /// </summary>
    public sealed class ClawTrailTests
    {
        [Test]
        public void AStreakIsPointedAtBothEndsAndFullInTheMiddle()
        {
            Assert.AreEqual(0.0f, ClawTrail.StreakTaper(0.0f), 1e-4f, "the streak's start is not pointed");
            Assert.AreEqual(0.0f, ClawTrail.StreakTaper(1.0f), 1e-4f, "the streak's end is not pointed");
            float widest = 0.0f;
            for (float u = 0.0f; u <= 1.0f; u += 0.01f)
            {
                widest = Mathf.Max(widest, ClawTrail.StreakTaper(u));
            }
            Assert.AreEqual(1.0f, widest, 0.02f);
            Assert.Greater(ClawTrail.StreakTaper(0.5f), 0.9f, "the middle is not full");
            Assert.Less(ClawTrail.StreakTaper(0.05f), ClawTrail.StreakTaper(0.3f), "it does not swell from its start");
        }

        /// <summary>However fast the swing, a streak is the last metre of the claws' path: a crescent of a size, its tail sliding after the hand.</summary>
        [Test]
        public void AStreakIsTheLastStretchOfThePathOnly()
        {
            var path = new List<Vector3>();
            for (int i = 0; i <= 6; i++)
            {
                path.Add(new Vector3(i * 0.5f, 0.0f, 0.0f));   // three metres, in half-metre frames
            }
            ClawTrail.KeepTheLast(path, 1.0f);
            float length = 0.0f;
            for (int i = 1; i < path.Count; i++)
            {
                length += Vector3.Distance(path[i - 1], path[i]);
            }
            Assert.AreEqual(1.0f, length, 1e-4f, "the streak is not a metre long");
            Assert.AreEqual(new Vector3(3.0f, 0.0f, 0.0f), path[path.Count - 1], "the streak's head is not at the claw");
            Assert.AreEqual(2.0f, path[0].x, 1e-4f, "the tail did not slide to a metre behind the head");

            var shortOne = new List<Vector3> { Vector3.zero, new Vector3(0.3f, 0.0f, 0.0f) };
            ClawTrail.KeepTheLast(shortOne, 1.0f);
            Assert.AreEqual(2, shortOne.Count, "a short streak was cut");
        }

        [Test]
        public void TheCurveGoesThroughEveryPointTheClawPassed()
        {
            var path = new List<Vector3> { new Vector3(0, 2, 0), new Vector3(0.4f, 1.6f, 0.3f), new Vector3(0.6f, 1.0f, 0.5f), new Vector3(0.5f, 0.5f, 0.6f) };
            var smooth = new List<Vector3>();
            ClawTrail.Smooth(path, smooth);
            Assert.AreEqual((path.Count - 1) * ClawTrail.StepsPerFrame + 1, smooth.Count);
            for (int i = 0; i < path.Count; i++)
            {
                Assert.Less(Vector3.Distance(path[i], smooth[i * ClawTrail.StepsPerFrame]), 1e-4f, $"the curve misses point {i}");
            }
        }
    }
}
