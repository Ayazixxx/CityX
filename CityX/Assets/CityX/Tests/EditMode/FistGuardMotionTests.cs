using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The bare-handed guard's clips (SourceArt/FistGuard; owner, 2026-11-10: "redo the hole thing like a souls like game").</summary>
    public sealed class FistGuardMotionTests
    {
        private static readonly string[] Clips = { FistGuard.RaiseClip, FistGuard.HoldClip, FistGuard.HitClip, FistGuard.ParryClip };

        [Test]
        public void EveryClipIsThereAndQuickEnoughForAFight()
        {
            BakedMotion motion = FistGuard.Motion;
            Assert.IsNotNull(motion, $"no guard motion at Resources/{FistGuard.MotionPath}");
            foreach (string clip in Clips)
            {
                Assert.IsTrue(motion.HasSegment(clip), $"no {clip}");
            }
            // A Souls guard is up in a few frames: the raise is the tell that the guard is coming, never a wind-up.
            Assert.LessOrEqual(motion.SegmentSeconds(FistGuard.RaiseClip), 0.2f, "the guard is slow to come up");
            Assert.LessOrEqual(motion.SegmentSeconds(FistGuard.HitClip), 0.4f, "a soaked blow holds the guard back too long");
        }

        /// <summary>The raise, the hit and the parry each end on the held guard's first frame, and the hold loops onto itself: no pop as one hands over.</summary>
        [Test]
        public void EachClipHandsOverToTheHeldGuardWithoutAPop()
        {
            BakedMotion motion = FistGuard.Motion;
            int holdFirst = motion.SegmentStart(FistGuard.HoldClip);
            foreach (string clip in new[] { FistGuard.RaiseClip, FistGuard.HoldClip, FistGuard.HitClip, FistGuard.ParryClip })
            {
                int last = motion.SegmentStart(clip) + motion.SegmentFrames(clip) - 1;
                for (int bone = 0; bone < motion.Bones.Count; bone++)
                {
                    float apart = Quaternion.Angle(motion.RotationAt(last, bone), motion.RotationAt(holdFirst, bone));
                    Assert.Less(apart, 1.0f, $"{clip} ends {apart:0.0} degrees off the held guard at {motion.Bones[bone]}");
                }
            }
        }
    }
}
