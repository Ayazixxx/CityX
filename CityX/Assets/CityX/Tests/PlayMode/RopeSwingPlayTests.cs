using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The ragdoll rope swing (docs/GRAPPLE_RAGDOLL.md, attempt 7): the hook lands and the body goes limp on a rope tied to its hand; through
    /// three seconds of swinging with the player pushing, the hand stays at the rope's end, no body of the ragdoll flies off (speed and the
    /// joints' stretch bounded), the rope reels and pays out; let go, the rope is gone and the body flies limp, lands and stands up by itself;
    /// a body swung while dying drops the rope; and the classic swing is still there behind the switch.
    /// </summary>
    public sealed class RopeSwingPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();
        private bool _modeBefore;

        private sealed class NoRope : IGrappleLauncher
        {
            public bool FindAnchor(Vector3 origin, Vector3 direction, float maxRange, out Vector3 anchor)
            {
                anchor = origin + direction * 10.0f;
                return true;
            }

            public void ShowRope(Vector3 anchor) { }
            public void UpdateRope(Vector3 anchor) { }
            public void HideRope() { }
        }

        [SetUp]
        public void SetUp()
        {
            _modeBefore = GrappleSwing.RagdollMode;
            GrappleSwing.RagdollMode = true;
        }

        [TearDown]
        public void TearDown()
        {
            GrappleSwing.RagdollMode = _modeBefore;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private GameObject Body(Vector3 at)
        {
#if UNITY_EDITOR
            if (_made.Count == 0)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
                Physics.SyncTransforms();
                _made.Add(ground);
            }
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            return player;
#else
            return null;
#endif
        }

        private static GrappleSwing Swing(GameObject player)
        {
            var swing = player.GetComponent<GrappleSwing>();
            if (swing == null)
            {
                swing = player.AddComponent<GrappleSwing>();
            }
            swing.Bind();
            swing.EquippedHook = new NoRope();
            return swing;
        }

        /// <summary>Every joint of the ragdoll with the distance between its body and the body it hangs from, as built.</summary>
        private static List<(CharacterJoint joint, float rest)> JointsOf(Ragdoll ragdoll)
        {
            var joints = new List<(CharacterJoint, float)>();
            foreach (CharacterJoint joint in ragdoll.GetComponentsInChildren<CharacterJoint>())
            {
                if (joint.connectedBody != null)
                {
                    joints.Add((joint, Vector3.Distance(joint.transform.position, joint.connectedBody.position)));
                }
            }
            return joints;
        }

        private static float WorstStretch(List<(CharacterJoint joint, float rest)> joints)
        {
            float worst = 0.0f;
            foreach ((CharacterJoint joint, float rest) in joints)
            {
                if (joint == null || joint.connectedBody == null) continue;
                worst = Mathf.Max(worst, Vector3.Distance(joint.transform.position, joint.connectedBody.position) - rest);
            }
            return worst;
        }

        private static float FastestBody(Ragdoll ragdoll)
        {
            float fastest = 0.0f;
            foreach (Rigidbody body in ragdoll.Bodies)
            {
                if (body != null) fastest = Mathf.Max(fastest, body.linearVelocity.magnitude);
            }
            return fastest;
        }

        /// <summary>A body in the air, a few frames into a fall, as one that has jumped off a roof is.</summary>
        private IEnumerator AirborneBody(float height, System.Action<GameObject> got)
        {
            GameObject player = Body(new Vector3(0.0f, height, 0.0f));
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 3; i++)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
            got(player);
        }

        [UnityTest]
        public IEnumerator TheBodyHangsLimpFromItsHandAndSwingsWithoutTearing()
        {
            GameObject player = null;
            yield return AirborneBody(8.0f, p => player = p);
            var ragdoll = player.GetComponent<Ragdoll>();
            Assert.IsNotNull(ragdoll, "setup: the body has no ragdoll");
            GrappleSwing swing = Swing(player);
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            List<(CharacterJoint, float)> joints = JointsOf(ragdoll);
            Assert.Greater(joints.Count, 8, "setup: the ragdoll has too few joints to watch");

            // The hook lands 6 m up and 6 m ahead: a rope of about 8.5 m, the body hanging well above the ground and swinging forward.
            Vector3 anchor = player.transform.position + new Vector3(0.0f, 6.0f, 6.0f);
            swing.Attach(anchor, Vector3.forward * 6.0f);
            Assert.IsTrue(swing.IsActive && swing.IsRoped, "the swing did not take the rope");
            Assert.IsTrue(ragdoll.IsLimp, "the body did not go limp on the rope");
            Assert.IsNotNull(swing.Rope, "no rope joint");
            Assert.AreSame(ragdoll.Torso, swing.Rope.GetComponent<Rigidbody>(), "the rope's load is not on the torso");
            Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Assert.Less(Vector3.Distance(swing.Rope.transform.TransformPoint(swing.Rope.anchor), shoulder.position), 0.1f, "the rope does not pull at the right shoulder");
            Assert.IsFalse(animator.enabled, "the Animator fights the ragdoll");

            float fastest = 0.0f, stretched = 0.0f, farthest = 0.0f, lowest = float.MaxValue, handUp = 0.0f;
            for (float t = 0.0f; t < 3.0f; t += Step)
            {
                swing.Tick(new Vector2(0.0f, 1.0f), 0.0f, false, false, false, false, Step);
                ragdoll.Tick(Step);
                yield return null;
                fastest = Mathf.Max(fastest, FastestBody(ragdoll));
                stretched = Mathf.Max(stretched, WorstStretch(joints));
                farthest = Mathf.Max(farthest, Vector3.Distance(swing.Rope.transform.TransformPoint(swing.Rope.anchor), anchor) - swing.RopeLength);
                lowest = Mathf.Min(lowest, ragdoll.Torso.position.y);
                if (t > 1.5f) handUp += (hand.position.y - ragdoll.Torso.position.y) * Step;
            }
            handUp /= 1.5f;
            Debug.Log($"CityX rope swing: 3 s on the rope, fastest body {fastest:F1} m/s, worst joint stretch {stretched * 100.0f:F0} cm, " +
                $"the rope's pull {farthest * 100.0f:F0} cm past its length at most, torso down to {lowest:F2} m, the hand {handUp:F2} m above the torso on average");
            Assert.Less(fastest, GrappleSwing.RopedMaxSpeed + 5.0f, "a body of the ragdoll flew off");
            Assert.Less(stretched, 0.25f, "a joint of the ragdoll stretched apart");
            Assert.Less(farthest, 0.6f, "the rope stretched past its length (half a metre at the bottom of a fast swing is its give)");
            Assert.Greater(handUp, 0.2f, "the arm does not hold the rope line (the hand hangs below the torso)");
            Assert.IsTrue(ragdoll.IsLimp && swing.IsRoped, "the swing ended by itself");
            Assert.Greater(lowest, 1.0f, "the hanging body touched the ground");
            Assert.Greater(ragdoll.Torso.linearVelocity.magnitude + swing.Velocity.magnitude, 0.5f, "the body is not swinging at all");

            // Reeling in shortens the rope and the hand follows it up; paying out lengthens it.
            float before = swing.RopeLength;
            for (float t = 0.0f; t < 1.0f; t += Step)
            {
                swing.Tick(Vector2.zero, 0.0f, false, false, true, false, Step);
                ragdoll.Tick(Step);
                yield return null;
            }
            Assert.Less(swing.RopeLength, before - 3.0f, "reeling did not shorten the rope");
            Assert.AreEqual(swing.RopeLength, swing.Rope.linearLimit.limit, 1e-3f, "the rope joint did not follow the rope's length");
            Assert.Less(Vector3.Distance(hand.position, anchor), before + 0.5f, "the hand did not come up with the reeled rope");
            for (float t = 0.0f; t < 0.5f; t += Step)
            {
                swing.Tick(Vector2.zero, 0.0f, false, false, false, true, Step);
                ragdoll.Tick(Step);
                yield return null;
            }
            Assert.Greater(swing.RopeLength, before - 3.0f, "paying out did not lengthen the rope");
        }

        [UnityTest]
        public IEnumerator LetGoTheBodyFliesLimpLandsAndStandsUpByItself()
        {
            GameObject player = null;
            yield return AirborneBody(6.0f, p => player = p);
            var ragdoll = player.GetComponent<Ragdoll>();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            GrappleSwing swing = Swing(player);
            swing.Attach(player.transform.position + new Vector3(0.0f, 5.0f, 5.0f), Vector3.forward * 6.0f);
            Assert.IsTrue(swing.IsRoped, "setup: the airborne body did not take the rope");
            for (float t = 0.0f; t < 1.0f; t += Step)
            {
                swing.Tick(new Vector2(0.0f, 1.0f), 0.0f, false, false, false, false, Step);
                ragdoll.Tick(Step);
                yield return null;
            }
            Vector3 torsoVelocity = ragdoll.Torso.linearVelocity;
            swing.Release(true);
            Assert.IsFalse(swing.IsRoped);
            Assert.IsFalse(swing.IsActive);
            Assert.IsNull(swing.Rope, "the rope joint stayed");
            Assert.IsTrue(swing.IsFlyingLimp, "the body is not flying limp after the release");
            Assert.IsTrue(ragdoll.IsLimp);
            Assert.Greater(swing.LastReleaseVelocity.y, torsoVelocity.y + GrappleSwing.ReleaseBoost - 0.5f, "a boosted release added no height");

            // A boosted release off a fast swing flies far and tumbles long on landing: give it time to lie still and get up.
            float waited = 0.0f;
            while (ragdoll.IsLimp && waited < 14.0f)
            {
                ragdoll.Tick(Step);
                waited += Step;
                yield return null;
            }
            Assert.IsFalse(ragdoll.IsLimp, "the body never got up after landing");
            Assert.IsFalse(swing.IsFlyingLimp);
            Assert.IsTrue(player.GetComponentInChildren<Animator>().enabled, "the Animator is still off after standing up");
            Assert.IsTrue(locomotion.enabled, "the locomotion is still off after standing up");
        }

        [UnityTest]
        public IEnumerator DyingOnTheRopeDropsIt()
        {
            GameObject player = null;
            yield return AirborneBody(6.0f, p => player = p);
            var ragdoll = player.GetComponent<Ragdoll>();
            var vitals = player.GetComponent<PlayerVitals>();
            GrappleSwing swing = Swing(player);
            swing.Attach(player.transform.position + new Vector3(0.0f, 5.0f, 5.0f), Vector3.forward * 4.0f);
            Assert.IsTrue(swing.IsRoped, "setup: the airborne body did not take the rope");
            for (float t = 0.0f; t < 0.5f; t += Step)
            {
                swing.Tick(Vector2.zero, 0.0f, false, false, false, false, Step);
                ragdoll.Tick(Step);
                yield return null;
            }
            DamageContext.Strike(vitals, vitals.Health + 100.0f, player.transform.position + Vector3.forward * 3.0f, 0.0f, DamageKind.Bullet, 1.0f);
            swing.Tick(Vector2.zero, 0.0f, false, false, false, false, Step);
            Assert.IsTrue(vitals.IsDead, "setup: the blow did not kill");
            Assert.IsFalse(swing.IsRoped, "a dead body is still on the rope");
            Assert.IsNull(swing.Rope);
        }

        [UnityTest]
        public IEnumerator TheClassicSwingIsStillThereBehindTheSwitchAndOnTheGround()
        {
            // The switch off: an airborne body swings upright, never limp.
            GrappleSwing.RagdollMode = false;
            GameObject player = null;
            yield return AirborneBody(6.0f, p => player = p);
            var ragdoll = player.GetComponent<Ragdoll>();
            GrappleSwing swing = Swing(player);
            swing.Attach(player.transform.position + new Vector3(0.0f, 5.0f, 5.0f), Vector3.forward * 6.0f);
            Assert.IsTrue(swing.IsActive);
            Assert.IsFalse(swing.IsRoped, "the classic swing took the rope");
            Assert.IsFalse(ragdoll.IsLimp, "the classic swing went limp");
            Assert.IsTrue(player.GetComponentInChildren<Animator>().enabled);
            swing.Release(false);
            Object.Destroy(player);
            yield return null;

            // The switch on but hooked from the street: the body runs with the rope on its feet until it swings off the ground.
            GrappleSwing.RagdollMode = true;
            GameObject walker = Body(new Vector3(30.0f, 0.05f, 0.0f));
            var walkerLocomotion = walker.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 20; i++)
            {
                walkerLocomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }
            Assert.IsTrue(walkerLocomotion.IsGrounded, "setup: the walker is not on the ground");
            GrappleSwing walkerSwing = Swing(walker);
            walkerSwing.Attach(walker.transform.position + new Vector3(0.0f, 12.0f, 6.0f), Vector3.forward * 2.0f);
            Assert.IsTrue(walkerSwing.IsActive);
            Assert.IsFalse(walkerSwing.IsRoped, "hooked from the ground, the body hung at once");
            Assert.IsFalse(walker.GetComponent<Ragdoll>().IsLimp, "hooked from the ground, the body went limp on the street");
            walkerSwing.Release(false);
        }
    }
}
