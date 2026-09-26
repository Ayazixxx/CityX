using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The two-bone reach that puts the free hand on a weapon. Pure geometry: a hanging arm (0.5m upper,
    /// 0.4m fore) with a slight elbow bend, moved to targets near and far.
    /// </summary>
    public sealed class TwoBoneIKTests
    {
        private const float UpperLength = 0.5f;
        private const float LowerLength = 0.4f;

        private GameObject _root;
        private Transform _upper;
        private Transform _lower;
        private Transform _end;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Arm");
            _upper = new GameObject("Upper").transform;
            _lower = new GameObject("Lower").transform;
            _end = new GameObject("End").transform;
            _upper.SetParent(_root.transform, false);
            _lower.SetParent(_upper, false);
            _end.SetParent(_lower, false);

            _lower.localPosition = new Vector3(0.0f, -UpperLength, 0.0f);
            _end.localPosition = new Vector3(0.0f, -LowerLength, 0.0f);
            // A slight bend, so the elbow has a side to be on.
            _lower.localRotation = Quaternion.Euler(-25.0f, 0.0f, 0.0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        private float Upper => Vector3.Distance(_upper.position, _lower.position);

        private float Lower => Vector3.Distance(_lower.position, _end.position);

        [Test]
        public void AReachableTargetIsReachedAndTheBonesKeepTheirLength()
        {
            Vector3 target = new Vector3(0.25f, -0.35f, 0.4f);

            float shortfall = TwoBoneIK.Solve(_upper, _lower, _end, target, Vector3.down);

            Assert.AreEqual(0.0f, shortfall, 0.002f);
            Assert.AreEqual(0.0f, Vector3.Distance(_end.position, target), 0.002f);
            Assert.AreEqual(UpperLength, Upper, 0.0005f, "the upper arm changed length");
            Assert.AreEqual(LowerLength, Lower, 0.0005f, "the forearm changed length");
        }

        [Test]
        public void ATargetBeyondReachStraightensTheArmTowardIt()
        {
            Vector3 target = new Vector3(0.0f, 0.0f, 2.0f);

            float shortfall = TwoBoneIK.Solve(_upper, _lower, _end, target, Vector3.down);

            Assert.AreEqual(2.0f - (UpperLength + LowerLength), shortfall, 0.02f, "it should fall about a metre short");
            Vector3 toEnd = (_end.position - _upper.position).normalized;
            Assert.Greater(Vector3.Dot(toEnd, Vector3.forward), 0.999f, "the arm is not pointing at the target");
            Assert.AreEqual(UpperLength + LowerLength, Vector3.Distance(_upper.position, _end.position), 0.01f);
        }

        [Test]
        public void TheElbowBowsTowardTheHint()
        {
            Vector3 target = new Vector3(0.0f, 0.0f, 0.6f);

            TwoBoneIK.Solve(_upper, _lower, _end, target, Vector3.right);
            float elbowRight = _lower.position.x;
            Assert.Greater(elbowRight, 0.05f, "the elbow should be out to the right");

            // Back to the start pose, then the opposite hint.
            _lower.localRotation = Quaternion.Euler(-25.0f, 0.0f, 0.0f);
            _upper.localRotation = Quaternion.identity;
            TwoBoneIK.Solve(_upper, _lower, _end, target, Vector3.left);
            Assert.Less(_lower.position.x, -0.05f, "the elbow should be out to the left");
        }

        [Test]
        public void ZeroWeightChangesNothingAndHalfWeightIsHalfway()
        {
            Vector3 target = new Vector3(0.3f, -0.2f, 0.4f);
            Vector3 start = _end.position;

            TwoBoneIK.Solve(_upper, _lower, _end, target, Vector3.down, 0.0f);
            Assert.AreEqual(0.0f, Vector3.Distance(start, _end.position), 1e-5f, "zero weight moved the hand");

            TwoBoneIK.Solve(_upper, _lower, _end, target, Vector3.down, 0.5f);
            float remaining = Vector3.Distance(_end.position, target);
            float full = Vector3.Distance(start, target);
            Assert.Less(remaining, full * 0.95f, "half weight did not move toward the target");
            Assert.Greater(remaining, 0.02f, "half weight arrived as if it were full");
        }

        [Test]
        public void ATargetOnTheShoulderNeverProducesNaN()
        {
            TwoBoneIK.Solve(_upper, _lower, _end, _upper.position, Vector3.down);
            TwoBoneIK.Solve(_upper, _lower, _end, _upper.position + Vector3.up * 0.001f, Vector3.down);

            Assert.IsFalse(float.IsNaN(_end.position.x) || float.IsNaN(_end.position.y) || float.IsNaN(_end.position.z));
            Assert.IsFalse(float.IsNaN(_lower.rotation.x));
        }

        [Test]
        public void AHintAlongTheLimbFallsBackToTheAnimatedElbow()
        {
            // The target is straight ahead of the shoulder and the hint points the same way: no bow direction
            // can come from it, so the elbow stays on the side the pose already had (-Z bend => behind).
            Vector3 target = new Vector3(0.0f, 0.0f, 0.6f);

            TwoBoneIK.Solve(_upper, _lower, _end, target, Vector3.forward);

            Assert.AreEqual(0.0f, Vector3.Distance(_end.position, target), 0.005f);
            Assert.IsFalse(float.IsNaN(_lower.position.y));
        }
    }
}
