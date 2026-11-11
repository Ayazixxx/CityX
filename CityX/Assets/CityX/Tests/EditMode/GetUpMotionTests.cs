using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The get-up clips made for this body (GetUpMotion; owner, 2026-11-10: "the limbs bend all awkwardly clearly in an unintented way", "its
    /// like the knees bend the wrong way"), every frame of them.
    /// </summary>
    public sealed class GetUpMotionTests
    {
        private static readonly string[] Clips = { GetUpMotion.FromTheBack, GetUpMotion.FromTheFront };

        private static readonly (string Thigh, string Shin)[] Legs = { ("thigh_l", "shin_l"), ("thigh_r", "shin_r") };

        [Test]
        public void BothClipsAreThere()
        {
            BakedMotion motion = GetUpMotion.Motion;
            Assert.IsNotNull(motion, $"no get-up motion at Resources/{GetUpMotion.Resource}");
            foreach (string clip in Clips)
            {
                Assert.IsTrue(motion.HasSegment(clip), $"no {clip}");
                Assert.That(GetUpMotion.Seconds(clip), Is.InRange(2.0f, 5.0f), $"{clip} is a blink or a wait");
            }
        }

        [Test]
        public void ABodyFacingUpGetsUpFromItsBackAndOneFacingDownFromItsFront()
        {
            Assert.AreEqual(GetUpMotion.FromTheBack, GetUpMotion.For(Vector3.up));
            Assert.AreEqual(GetUpMotion.FromTheBack, GetUpMotion.For(new Vector3(1.0f, 0.1f, 0.0f)), "on its side, a little up");
            Assert.AreEqual(GetUpMotion.FromTheFront, GetUpMotion.For(Vector3.down));
            Assert.AreEqual(GetUpMotion.FromTheFront, GetUpMotion.For(new Vector3(0.0f, -0.1f, 1.0f)), "on its side, a little down");
        }

        /// <summary>
        /// The game's skeleton rests in a T-pose with every bone's turn the identity: a knee is a hinge about its thigh's own x, and bends the
        /// shin back (-z). A thigh turned more than a quarter turn about its length points the knee backward; a shin swung forward bends it the
        /// wrong way.
        /// </summary>
        [Test]
        public void NoKneeEverBendsTheWrongWay()
        {
            BakedMotion motion = GetUpMotion.Motion;
            foreach (string clip in Clips)
            {
                int first = motion.SegmentStart(clip), frames = motion.SegmentFrames(clip);
                foreach ((string thighName, string shinName) in Legs)
                {
                    int thigh = Index(motion, thighName), shin = Index(motion, shinName);
                    for (int f = first; f < first + frames; f++)
                    {
                        Vector3 hinge = motion.RotationAt(f, thigh) * Vector3.right;
                        Assert.Greater(hinge.x, 0.3f, $"{clip} {(f - first) / 60.0f:0.00} s: the {thighName} is turned round, its knee pointing back");
                        Vector3 shinAlong = motion.RotationAt(f, shin) * Vector3.down;
                        Assert.Less(shinAlong.z, 0.05f, $"{clip} {(f - first) / 60.0f:0.00} s: the {shinName} bends forward at the knee");
                    }
                }
            }
        }

        [Test]
        public void EachClipStartsLyingAndEndsStanding()
        {
            BakedMotion motion = GetUpMotion.Motion;
            foreach (string clip in Clips)
            {
                int first = motion.SegmentStart(clip), last = first + motion.SegmentFrames(clip) - 1;
                Assert.Less(motion.HipsAt(first).y, 0.4f, $"{clip} does not start lying");
                Assert.That(motion.HipsAt(last).y, Is.InRange(0.8f, 0.9f), $"{clip} does not end standing as the idle stands");
                Vector3 hips = motion.HipsAt(last);
                Assert.Less(new Vector2(hips.x, hips.z).magnitude, 0.05f, $"{clip} ends off the root it hands the body back to");
            }
        }

        private static int Index(BakedMotion motion, string bone)
        {
            for (int i = 0; i < motion.Bones.Count; i++)
            {
                if (motion.Bones[i] == bone)
                {
                    return i;
                }
            }
            Assert.Fail($"the get-up motion has no {bone}");
            return -1;
        }
    }
}
