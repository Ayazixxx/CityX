using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>A limp body's pose on the wire (docs/MULTIPLAYER_STATE.md): small, near enough exact, and nothing but a pose gets through.</summary>
    public sealed class RagdollPoseCodecTests
    {
        private static RagdollPose Pose(int bones, int seed)
        {
            var random = new System.Random(seed);
            var pose = new RagdollPose
            {
                Time = 12.5f,
                HipsPosition = new Vector3(3.0f, 1.2f, -7.5f),
                HipsRotation = Quaternion.Euler(10.0f, 200.0f, -35.0f),
                Count = bones,
            };
            for (int i = 0; i < bones; i++)
            {
                pose.Local[i] = Quaternion.Euler((float)random.NextDouble() * 360.0f, (float)random.NextDouble() * 360.0f, (float)random.NextDouble() * 360.0f);
                pose.LocalPositions[i] = new Vector3((float)random.NextDouble() - 0.5f, (float)random.NextDouble() * 0.8f, (float)random.NextDouble() * 0.2f);
            }
            return pose;
        }

        [Test]
        public void ATurnPackedIn32BitsComesBackWithinATenthOfADegree()
        {
            var random = new System.Random(7);
            float worst = 0.0f;
            for (int i = 0; i < 2000; i++)
            {
                Quaternion q = Quaternion.Euler((float)random.NextDouble() * 360.0f, (float)random.NextDouble() * 360.0f, (float)random.NextDouble() * 360.0f);
                worst = Mathf.Max(worst, Quaternion.Angle(q, RagdollPoseCodec.UnpackRotation(RagdollPoseCodec.PackRotation(q))));
            }
            Assert.Less(worst, 0.25f, "a packed turn came back too far off");
        }

        [Test]
        public void APoseComesBackAsItWentAndIsSmall()
        {
            RagdollPose sent = Pose(17, 3);
            byte[] data = RagdollPoseCodec.Pack(sent);
            Assert.LessOrEqual(data.Length, 200, "a seventeen-bone pose is too big for twenty a second");
            var got = new RagdollPose();
            Assert.IsTrue(RagdollPoseCodec.TryUnpack(data, 17, Vector3.zero, got));
            Assert.AreEqual(sent.Time, got.Time);
            Assert.AreEqual(sent.HipsPosition, got.HipsPosition);
            Assert.Less(Quaternion.Angle(sent.HipsRotation, got.HipsRotation), 0.25f);
            for (int i = 0; i < 17; i++)
            {
                Assert.Less(Quaternion.Angle(sent.Local[i], got.Local[i]), 0.25f, $"bone {i}'s turn");
                Assert.Less(Vector3.Distance(sent.LocalPositions[i], got.LocalPositions[i]), 0.0005f, $"bone {i}'s place");
            }
        }

        [Test]
        public void NothingButAPoseGetsThrough()
        {
            var got = new RagdollPose();
            byte[] good = RagdollPoseCodec.Pack(Pose(17, 4));
            Assert.IsFalse(RagdollPoseCodec.TryUnpack(null, 17, Vector3.zero, got), "nothing");
            Assert.IsFalse(RagdollPoseCodec.TryUnpack(new byte[5], 17, Vector3.zero, got), "too short");
            Assert.IsFalse(RagdollPoseCodec.TryUnpack(good, 16, Vector3.zero, got), "another body's bone count");
            var longer = new byte[good.Length + 4];
            System.Array.Copy(good, longer, good.Length);
            Assert.IsFalse(RagdollPoseCodec.TryUnpack(longer, 17, Vector3.zero, got), "trailing bytes");
            Assert.IsFalse(RagdollPoseCodec.TryUnpack(good, 17, new Vector3(1000.0f, 0.0f, 0.0f), got), "hips a kilometre from the body");

            RagdollPose bad = Pose(17, 5);
            bad.HipsPosition = new Vector3(float.NaN, 0.0f, 0.0f);
            Assert.IsFalse(RagdollPoseCodec.TryUnpack(RagdollPoseCodec.Pack(bad), 17, Vector3.zero, got), "not a number");
        }

        [Test]
        public void HalfwayBetweenTwoPosesIsHalfwayInPlaceAndTurn()
        {
            RagdollPose a = Pose(3, 6);
            RagdollPose b = Pose(3, 6);
            b.Time = a.Time + 0.2f;
            b.HipsPosition = a.HipsPosition + new Vector3(2.0f, 0.0f, 0.0f);
            b.Local[1] = a.Local[1] * Quaternion.Euler(0.0f, 0.0f, 90.0f);
            var half = new RagdollPose();
            RagdollPoseCodec.Lerp(a, b, 0.5f, half);
            Assert.AreEqual(a.Time + 0.1f, half.Time, 1e-4f);
            Assert.Less(Vector3.Distance(half.HipsPosition, a.HipsPosition + new Vector3(1.0f, 0.0f, 0.0f)), 1e-4f);
            Assert.AreEqual(45.0f, Quaternion.Angle(a.Local[1], half.Local[1]), 0.1f);
        }
    }
}
