using System;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>A Blender-made motion read and sampled: segments loop on themselves or hold their ends, frames blend the short way, bad files are refused.</summary>
    public sealed class BakedMotionTests
    {
        /// <summary>Two bones, five frames: the root turning 0, 30, 60, 90, 120 degrees about y and rising 0.1 a frame. Segments "a" (0-2) and "b" (2-4).</summary>
        private static string Json()
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            var rotations = new System.Collections.Generic.List<string>();
            var hips = new System.Collections.Generic.List<string>();
            for (int f = 0; f < 5; f++)
            {
                Quaternion q = Quaternion.Euler(0.0f, 30.0f * f, 0.0f);
                rotations.Add(string.Format(culture, "{0:R},{1:R},{2:R},{3:R},0,0,0,1", q.x, q.y, q.z, q.w));
                hips.Add(string.Format(culture, "0,{0:R},0", 0.1f * f));
            }
            return "{\"fps\":60,\"frames\":5,\"bones\":[\"hips\",\"spine\"],\"segments\":[{\"name\":\"a\",\"start\":0,\"end\":2},{\"name\":\"b\",\"start\":2,\"end\":4}]," +
                   "\"cutFrames\":[3],\"hips\":[" + string.Join(",", hips) + "],\"rotations\":[" + string.Join(",", rotations) + "]}";
        }

        private static float YawOf(Quaternion q)
        {
            Vector3 v = q * Vector3.forward;
            return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
        }

        [Test]
        public void AFrameBetweenTwoBlendsThemAndASegmentHoldsItsEnds()
        {
            BakedMotion motion = BakedMotion.FromJson(Json());
            Assert.AreEqual(5, motion.FrameCount);
            Assert.AreEqual(2, motion.SegmentFrames("a"));
            var pose = new BakedPose(motion.Bones);

            motion.Sample("b", 0.5f / 60.0f, false, pose);   // half way from frame 2 to 3
            Assert.AreEqual(75.0f, YawOf(pose.Rotations[0]), 0.1f);
            Assert.AreEqual(0.25f, pose.Hips.y, 1e-4f);

            motion.Sample("a", 10.0f, false, pose);          // long past its end: held on its last frame
            Assert.AreEqual(60.0f, YawOf(pose.Rotations[0]), 0.1f);
        }

        [Test]
        public void ALoopWrapsToItsOwnStartNotIntoTheNextSegment()
        {
            BakedMotion motion = BakedMotion.FromJson(Json());
            var pose = new BakedPose(motion.Bones);
            // "a" loops over frames 0 and 1: 1.5 frames in is half way from frame 1 back to frame 0, never toward frame 2.
            motion.Sample("a", 1.5f / 60.0f, true, pose);
            Assert.AreEqual(15.0f, YawOf(pose.Rotations[0]), 0.1f);
            motion.Sample("a", 2.0f / 60.0f, true, pose);
            Assert.AreEqual(0.0f, YawOf(pose.Rotations[0]), 0.1f);
        }

        [Test]
        public void AFileThatIsNotAWholeMotionIsRefused()
        {
            Assert.Throws<ArgumentException>(() => BakedMotion.FromJson(""));
            Assert.Throws<ArgumentException>(() => BakedMotion.FromJson(Json().Replace("\"frames\":5", "\"frames\":6")), "frames missing");
            Assert.Throws<ArgumentException>(() => BakedMotion.FromJson(Json().Replace("[\"hips\",\"spine\"]", "[\"spine\",\"hips\"]")), "the root not first");
            Assert.Throws<ArgumentException>(() => BakedMotion.FromJson(Json().Replace("\"end\":4", "\"end\":9")), "a segment past the end");
        }
    }
}
