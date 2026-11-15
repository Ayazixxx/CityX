using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The one-handed cuts whose sword arm is posed in Blender (SwordStrikes.ArmMotion, SourceArt/SwordMoves/sword_arms_1h.py), read the way
    /// the skeleton plays them. Owner, 2026-11-15, with photos of his own arm: the hand straight on the forearm, the back of the hand up at the
    /// end of the second cut, the edge leading, and the trail one straight stroke from the game's camera.
    /// </summary>
    public class SwordArmClipTests
    {
        private static readonly SwordStrikeKind[] Posed = { SwordStrikeKind.Slash1H, SwordStrikeKind.Cut1H };

        private static readonly float CameraPitch = ThirdPersonCamera.DefaultPitch * Mathf.Deg2Rad;

        /// <summary>
        /// The game's camera at rest in the body's frame (ThirdPersonCamera: 0.87 m to the right of the pivot at 0.73 of the body's 2.6 m and
        /// 0.6 m over it, DefaultDistance back, DefaultPitch down), as the cuts were laid out against it (sword_arms_1h.py's CAMERA_EYE).
        /// </summary>
        private static readonly Vector3 Eye = new Vector3(0.87f,
            0.73f * BodyRig.StandingHeight + 0.6f + ThirdPersonCamera.DefaultDistance * Mathf.Sin(CameraPitch),
            -ThirdPersonCamera.DefaultDistance * Mathf.Cos(CameraPitch));

        /// <summary>The combat sword's blade, metres (CombatSword.prefab).</summary>
        private const float Blade = 1.39f;

        /// <summary>How far the tip moves across the blade in a frame (60 a second) for the blade to be cutting: 3 m/s.</summary>
        private const float CuttingMetresPerFrame = 0.05f;

        private static BakedMotion Arms() => SwordStrikes.ArmMotion;

        private static int FrameAt(SwordStrikeKind kind, float seconds) =>
            Arms().SegmentStart(SwordStrikes.NameOf(kind)) + Mathf.RoundToInt(seconds * Arms().FramesPerSecond);

        [Test]
        public void TheSlashAndTheCutCarryTheSwordArmOverTheSameBodyAsTheirClips()
        {
            BakedMotion arms = Arms(), body = SwordStrikes.Motion;
            Assert.IsNotNull(arms, $"no Resources/{SwordStrikes.ArmMotionPath}");
            foreach (string bone in new[] { "arm_r", "forearm_r", "hand_r" })
            {
                CollectionAssert.Contains(arms.Bones, bone);
            }
            Assert.IsFalse(arms.NewPose().LeavesTheArms, "the arm clip would be laid on under the arm the keys pose");
            foreach (SwordStrikeKind kind in Posed)
            {
                string name = SwordStrikes.NameOf(kind);
                Assert.AreSame(arms, SwordStrikes.MotionFor(kind), $"{kind} does not play its arm");
                Assert.AreEqual(body.SegmentFrames(name), arms.SegmentFrames(name), $"{kind}: not the same length as its body's clip");
                for (int f = 0; f < arms.SegmentFrames(name); f++)
                {
                    for (int b = 0; b < body.Bones.Count; b++)
                    {
                        int a = IndexOf(arms.Bones, body.Bones[b]);
                        Quaternion mine = arms.RotationAt(arms.SegmentStart(name) + f, a), theirs = body.RotationAt(body.SegmentStart(name) + f, b);
                        Assert.Less(Quaternion.Angle(mine, theirs), 0.5f, $"{kind} frame {f}: {body.Bones[b]} is not its body's");
                    }
                }
            }
            Assert.AreSame(body, SwordStrikes.MotionFor(SwordStrikeKind.Sweep2H), "a two-handed attack plays the one-handed arm clip");
        }

        [Test]
        public void TheHandStaysStraightOnItsForearmThroughEveryFrame()
        {
            var rest = new Dictionary<string, Vector3>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                rest[bone.Name] = bone.Position;
            }
            Vector3 forearmRest = (rest["hand_r"] - rest["forearm_r"]).normalized;
            foreach (SwordStrikeKind kind in Posed)
            {
                string name = SwordStrikes.NameOf(kind);
                for (int f = 0; f < Arms().SegmentFrames(name); f++)
                {
                    var world = BakedMotionPoses.WorldTurns(Arms(), Arms().SegmentStart(name) + f);
                    float bend = Vector3.Angle(world["forearm_r"] * forearmRest, world["hand_r"] * forearmRest);
                    Assert.Less(bend, 32.0f, $"{kind} frame {f}: the wrist is bent {bend:0} degrees");
                }
            }
        }

        [Test]
        public void TheTrailIsOneStraightStrokeFromTheCameraWithTheEdgeLeading()
        {
            Quaternion grip = SwordWeapon.SavedOneHandHoldFor(true);
            Vector3 gripAt = SwordWeapon.OneHandGripPosition;
            foreach (SwordStrikeKind kind in Posed)
            {
                SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(kind);
                float from = t.WindUp + t.Hold + t.Strike * 0.25f, to = t.WindUp + t.Hold + t.Strike + t.Follow;
                var screen = new List<Vector2>();
                Vector3 lastTip = Vector3.zero;
                bool first = true;
                for (int f = FrameAt(kind, from); f <= FrameAt(kind, to); f++)
                {
                    var world = BakedMotionPoses.WorldTurns(Arms(), f);
                    var joints = BakedMotionPoses.Joints(Arms(), f, world);
                    Quaternion sword = world["hand_r"] * grip;
                    Vector3 blade = sword * Vector3.up, root = joints["hand_r"] + world["hand_r"] * gripAt, tip = root + blade * Blade;
                    screen.Add(OnScreen(tip));
                    if (!first)
                    {
                        // Where the blade cuts (3 m/s and more across itself): as it comes to rest the body's own settling moves it as much.
                        Vector3 moved = Vector3.ProjectOnPlane(tip - lastTip, blade);
                        if (moved.magnitude > CuttingMetresPerFrame)
                        {
                            // The combat sword's edge is its +X, held toward the knuckles.
                            float lead = Vector3.Dot(moved.normalized, sword * Vector3.right);
                            Assert.Greater(lead, 0.5f, $"{kind} frame {f}: the edge does not lead ({lead:0.00})");
                        }
                    }
                    lastTip = tip;
                    first = false;
                }
                Vector2 a = screen[0], b = screen[screen.Count - 1];
                Vector2 across = new Vector2(-(b - a).y, (b - a).x).normalized;
                float worst = 0.0f;
                foreach (Vector2 p in screen)
                {
                    worst = Mathf.Max(worst, Mathf.Abs(Vector2.Dot(p - a, across)));
                }
                Assert.Less(worst / (b - a).magnitude, 0.06f, $"{kind}: the trail bows {100.0f * worst / (b - a).magnitude:0}% off a straight stroke");
                // One way: the tip never goes back along the stroke (it rose before it fell, and the trail turned back on itself).
                Vector2 way = (b - a).normalized;
                for (int i = 1; i < screen.Count; i++)
                {
                    float step = Vector2.Dot(screen[i] - screen[i - 1], way) / (b - a).magnitude;
                    Assert.Greater(step, -0.02f, $"{kind}: the tip goes back along the stroke at its frame {i} ({100.0f * step:0}% of it)");
                }
            }
        }

        [Test]
        public void TheSecondCutEndsWithTheBackOfTheHandUpAndTheKnucklesLeadingDownToTheRight()
        {
            SwordStrikes.StrikeTiming t = SwordStrikes.TimingOf(SwordStrikeKind.Cut1H);
            var world = BakedMotionPoses.WorldTurns(Arms(), FrameAt(SwordStrikeKind.Cut1H, t.WindUp + t.Hold + t.Strike + t.Follow));
            Vector3 back = world["hand_r"] * Vector3.up, knuckles = world["hand_r"] * Vector3.right;
            Assert.Greater(back.y, 0.3f, $"the back of the hand faces {back}, not up");
            Assert.Greater(knuckles.x, 0.2f, $"the knuckles face {knuckles}, not to the right");
            Assert.Less(knuckles.y, 0.0f, $"the knuckles face {knuckles}, not down");
        }

        private static Vector2 OnScreen(Vector3 point)
        {
            Vector3 d = point - Eye;
            var forward = new Vector3(0.0f, -Mathf.Sin(CameraPitch), Mathf.Cos(CameraPitch));
            var up = new Vector3(0.0f, Mathf.Cos(CameraPitch), Mathf.Sin(CameraPitch));
            float depth = Mathf.Max(Vector3.Dot(d, forward), 0.1f);
            return new Vector2(d.x / depth, Vector3.Dot(d, up) / depth);
        }

        private static int IndexOf(IReadOnlyList<string> list, string name)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == name)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
