using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>Octaslash's timeline against Sephiroth's (SmashWiki): eight cuts on frames 36-50, two apart, the last three times the others.</summary>
    public sealed class OctaslashMoveTests
    {
        [Test]
        public void EightCutsTwoFramesApartLikeSmash()
        {
            Assert.AreEqual(8, OctaslashMove.CutCount);
            CollectionAssert.AreEqual(new[] { 36, 38, 40, 42, 44, 46, 48, 50 }, OctaslashMove.CutFrames);
            for (int i = 1; i < OctaslashMove.CutCount; i++)
            {
                Assert.AreEqual(2.0f / 60.0f, OctaslashMove.CutTime(i) - OctaslashMove.CutTime(i - 1), 1e-5f);
            }
            Assert.AreEqual(OctaslashMove.CutTime(7), OctaslashMove.DashSeconds, 1e-6f, "the dash lasts until the last cut");
        }

        [Test]
        public void TheLastCutIsAboutThreeTimesTheOthers()
        {
            // Smash: 2.3% seven times, then 7% (a ratio of about 3); the percentages themselves do not carry over.
            float ratio = OctaslashMove.CutScale(7) / OctaslashMove.CutScale(0);
            Assert.AreEqual(7.0f / 2.3f, ratio, 0.1f);
            for (int i = 0; i < 7; i++)
            {
                Assert.IsFalse(OctaslashMove.IsFinal(i));
            }
            Assert.IsTrue(OctaslashMove.IsFinal(7));
        }

        [Test]
        public void CutsComeDueOnTime()
        {
            Assert.AreEqual(0, OctaslashMove.CutsDueBy(0.0f));
            Assert.AreEqual(1, OctaslashMove.CutsDueBy(OctaslashMove.CutTime(0)));
            Assert.AreEqual(4, OctaslashMove.CutsDueBy(OctaslashMove.CutTime(3) + 0.001f));
            Assert.AreEqual(8, OctaslashMove.CutsDueBy(10.0f));
        }

        [Test]
        public void TheDashCoversItsDistanceAndTheWindUpIsTwoAndAHalfSeconds()
        {
            Assert.AreEqual(2.5f, OctaslashMove.ChargeSeconds);
            Assert.AreEqual(OctaslashMove.DashMetres, OctaslashMove.DashSpeed * OctaslashMove.DashSeconds, 1e-3f);
        }

        // ---- the animation made in Blender (CityX/SourceArt/Octaslash) ----

        private static BakedMotion Motion()
        {
            BakedMotion motion = BakedMotion.Load(OctaslashMove.MotionResource);
            Assert.IsNotNull(motion, "the Octaslash animation is missing -- export it from Blender (octaslash_export.py)");
            return motion;
        }

        [Test]
        public void TheAnimationIsOnTheGamesOwnBones()
        {
            BakedMotion motion = Motion();
            var names = new System.Collections.Generic.List<string>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                names.Add(bone.Name);
            }
            CollectionAssert.AreEquivalent(names, motion.Bones, "the animation's bones are not BodyRig's");
            Assert.AreEqual(BakedMotion.RootBone, motion.Bones[0]);
        }

        [Test]
        public void TheAnimationsDashAndFollowThroughLastAsLongAsTheMovesAndItsCutsLandOnSmashsFrames()
        {
            BakedMotion motion = Motion();
            Assert.AreEqual(60, motion.FramesPerSecond);
            foreach (string segment in new[] { OctaslashMove.CoilSegment, OctaslashMove.HoldSegment, OctaslashMove.DashSegment, OctaslashMove.RecoverSegment })
            {
                Assert.IsTrue(motion.HasSegment(segment), $"no '{segment}' in the animation");
            }
            Assert.AreEqual(OctaslashMove.DashSeconds, motion.SegmentSeconds(OctaslashMove.DashSegment), 1e-4f, "the animation's dash is not the move's");
            Assert.AreEqual(OctaslashMove.RecoverySeconds, motion.SegmentSeconds(OctaslashMove.RecoverSegment), 1e-4f);
            Assert.AreEqual(OctaslashMove.CutCount, motion.CutFrames.Count);
            int dashStart = motion.SegmentStart(OctaslashMove.DashSegment);
            for (int i = 0; i < OctaslashMove.CutCount; i++)
            {
                Assert.AreEqual(OctaslashMove.CutFrames[i] - OctaslashMove.DashStartFrame, motion.CutFrames[i] - dashStart, $"cut {i} is marked on another frame");
            }
        }

        [Test]
        public void EveryFrameIsAWholePose()
        {
            BakedMotion motion = Motion();
            for (int f = 0; f < motion.FrameCount; f++)
            {
                for (int b = 0; b < motion.Bones.Count; b++)
                {
                    Quaternion q = motion.RotationAt(f, b);
                    float norm = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                    Assert.AreEqual(1.0f, norm, 1e-3f, $"frame {f}, {motion.Bones[b]}");
                }
                Assert.Less(motion.HipsAt(f).magnitude, 3.0f, $"frame {f}: the hips are nowhere near the body");
            }
        }

        /// <summary>World turns of the body's bones on one frame, from the root down, the way the skeleton applies them.</summary>
        private static System.Collections.Generic.Dictionary<string, Quaternion> WorldTurns(BakedMotion motion, int frame)
        {
            var parent = new System.Collections.Generic.Dictionary<string, string>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                parent[bone.Name] = bone.Parent;
            }
            var world = new System.Collections.Generic.Dictionary<string, Quaternion>();
            for (int b = 0; b < motion.Bones.Count; b++)
            {
                string name = motion.Bones[b];
                Quaternion local = motion.RotationAt(frame, b);
                world[name] = parent[name] == null ? local : world[parent[name]] * local;
            }
            return world;
        }

        /// <summary>Where every joint is on one frame: the skeleton's T-pose rest offsets turned by the frame's rotations, from its hips down.</summary>
        private static System.Collections.Generic.Dictionary<string, Vector3> Joints(BakedMotion motion, int frame,
            System.Collections.Generic.Dictionary<string, Quaternion> world)
        {
            var bones = new System.Collections.Generic.Dictionary<string, BodyRig.Bone>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                bones[bone.Name] = bone;
            }
            var at = new System.Collections.Generic.Dictionary<string, Vector3>();
            foreach (string name in motion.Bones)
            {
                BodyRig.Bone bone = bones[name];
                at[name] = bone.Parent == null
                    ? motion.HipsAt(frame)
                    : at[bone.Parent] + world[bone.Parent] * (bone.Position - bones[bone.Parent].Position);
            }
            return at;
        }

        /// <summary>The blade's direction on a frame: the hand's turn applied to the sword's default hold (the animation was posed with it).</summary>
        private static Vector3 Blade(System.Collections.Generic.Dictionary<string, Quaternion> world)
        {
            return world["hand_r"] * CityX.Weapons.SwordWeapon.DefaultGripRotation * Vector3.up;
        }

        [Test]
        public void TheDashLungesAlongItFacingAheadAndEveryCutPassesStraightThrough()
        {
            // Owner, 2026-10-12: "he does like a bunch of consecutive slashes facing forward with his knee bend and other leg going back showing
            // forward motion" -- not a spin.
            BakedMotion motion = Motion();
            int dashStart = motion.SegmentStart(OctaslashMove.DashSegment);
            for (int k = 1; k <= 14; k++)
            {
                int frame = dashStart + k;
                Assert.Less(Vector3.Distance(OctaslashMove.FlightPivot, motion.HipsAt(frame)), 0.05f, $"dash frame {k}: the hips are not at the pivot");
                var world = WorldTurns(motion, frame);
                Vector3 facing = Vector3.ProjectOnPlane(world["hips"] * Vector3.forward, Vector3.up).normalized;
                Assert.Greater(facing.z, 0.85f, $"dash frame {k}: the body does not face along the dash");
                Assert.Less(Mathf.Abs((world["hips"] * Vector3.right).y), 0.3f, $"dash frame {k}: the body rolls");
                Vector3 chest = world["spine"] * Vector3.up;
                Assert.Greater(chest.z, 0.4f, $"dash frame {k}: the body does not lean into the dash");
                Assert.Greater(chest.y, 0.3f, $"dash frame {k}: the body lies flat");

                var joints = Joints(motion, frame, world);
                Vector3 hips = joints["hips"];
                Assert.Greater(joints["shin_l"].z - hips.z, 0.15f, $"dash frame {k}: the left knee is not driven forward");
                Assert.Less(joints["foot_r"].z - hips.z, -0.2f, $"dash frame {k}: the right leg does not trail behind");

                // A cut every two frames with the blade straight ahead; between them the blade is drawn back or swung across, out of the way.
                float ahead = Blade(world).z;
                if (k % 2 == 0)
                {
                    Assert.Greater(ahead, 0.8f, $"dash frame {k} is a cut, but the blade is not straight ahead");
                }
                else
                {
                    Assert.Less(ahead, 0.3f, $"dash frame {k} is between cuts, but the blade is still ahead: no slash");
                }
            }
            // The wind-up raises the blade overhead and the last cut brings it down ahead.
            var windUp = WorldTurns(motion, dashStart + 15);
            Assert.Greater(Blade(windUp).y, 0.6f, "the blade is not raised for the last cut");
            var last = WorldTurns(motion, dashStart + 16);
            Assert.Greater(Blade(last).z, 0.8f, "the last cut is not ahead");
            Assert.Less(Blade(last).y, 0.0f, "the last cut is not coming down");
        }

        /// <summary>The most any bone turns from one frame to the next across <paramref name="from"/>..<paramref name="to"/> (and from the last back to the first if it loops).</summary>
        private static float WorstStep(BakedMotion motion, int from, int to, bool loops, out string where)
        {
            float worst = 0.0f;
            where = "";
            for (int f = from; f < to + (loops ? 1 : 0); f++)
            {
                int next = f + 1 > to ? from : f + 1;
                for (int b = 0; b < motion.Bones.Count; b++)
                {
                    float step = Quaternion.Angle(motion.RotationAt(f, b), motion.RotationAt(next, b));
                    if (step > worst)
                    {
                        worst = step;
                        where = $"{motion.Bones[b]}, frame {f} -> {next}";
                    }
                }
            }
            return worst;
        }

        [Test]
        public void NothingSnapsOutsideTheDashsOwnWhirlAndTheHeldCoilLoopsOnItself()
        {
            BakedMotion motion = Motion();
            int coil = motion.SegmentStart(OctaslashMove.CoilSegment);
            int hold = motion.SegmentStart(OctaslashMove.HoldSegment);
            int dash = motion.SegmentStart(OctaslashMove.DashSegment);
            int recover = motion.SegmentStart(OctaslashMove.RecoverSegment);
            int end = recover + motion.SegmentFrames(OctaslashMove.RecoverSegment);

            float step = WorstStep(motion, coil, hold, false, out string where);
            Assert.Less(step, 12.0f, $"the coil snaps: {step:0} degrees at {where}");
            step = WorstStep(motion, hold, dash - 1, true, out where);
            Assert.Less(step, 6.0f, $"the held coil snaps (or jumps where it loops): {step:0} degrees at {where}");
            // The last cut carries on down through the follow-through's first frames; after those, the follow-through ends with a quick flourish, the
            // blade swung from behind down past the right side back into the guard (and the body is let go of over those same frames), so its wrist
            // may turn faster than the coil's.
            const int lastCutCarriesOn = 3;
            step = WorstStep(motion, recover + lastCutCarriesOn, end, false, out where);
            Assert.Less(step, 20.0f, $"the follow-through snaps: {step:0} degrees at {where}");
            // The cuts are a blade whipped across in a frame, but never so far that the frames between could be read the wrong way round (180).
            step = WorstStep(motion, dash, recover + lastCutCarriesOn, false, out where);
            Assert.Less(step, 150.0f, $"a cut turns a bone too far in one frame: {step:0} degrees at {where}");
        }
    }
}
