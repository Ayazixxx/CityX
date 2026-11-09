using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// RECOVERY ON THE REAL BODY (Ragdoll.Recovery.cs, docs/RECOVERY.md; owner, 2026-11-08: "make the recover skill work like the recovery in
    /// saints row 4"). Thrown, the body flips head over heels back onto its feet and flies on as it was going; lying on the ground it pops up
    /// into the flip; the flip starts from the pose it was flung in; it cannot be done held in the Crimson Claw's fist; a blow or death mid-flip
    /// throws it limp again; it ends a knockdown's timer. And the knockdown's own timed get-up waits for a body thrown high to come down.
    /// The test drives the body (there is no keyboard here): the ragdoll's tick, then the walker's, as the input reader would.
    /// </summary>
    public sealed class RecoveryPlayTests
    {
        private GameObject _ground;
        private GameObject _player;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

#if UNITY_EDITOR
        private Ragdoll Spawn(Vector3 at)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, at, Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;   // driven here
            return _player.GetComponent<Ragdoll>();
        }
#endif

        private Animator Animator => _player.GetComponentInChildren<Animator>();

        private Vector3 BodyUp => Animator.GetBoneTransform(HumanBodyBones.Neck).position - Animator.GetBoneTransform(HumanBodyBones.Hips).position;

        /// <summary>One frame of the body as the input reader runs it: the ragdoll's tick while it is down, the walker's once it is up.</summary>
        private IEnumerator Step(Ragdoll ragdoll)
        {
            if (ragdoll.IsLimp)
            {
                ragdoll.Tick(Time.deltaTime);
            }
            else
            {
                _player.GetComponent<PlayerLocomotion>().Tick(default, Time.deltaTime);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThrownItFlipsHeadOverHeelsOntoItsFeetAndFliesOnAsItWasGoing()
        {
#if UNITY_EDITOR
            Ragdoll ragdoll = Spawn(new Vector3(0.0f, 0.1f, 0.0f));
            yield return null;
            var thrown = new Vector3(8.0f, 9.0f, 0.0f);
            _player.GetComponent<PlayerVitals>().KnockDown(thrown);
            for (int i = 0; i < 12; i++)
            {
                yield return Step(ragdoll);
            }
            Assert.IsTrue(ragdoll.IsLimp, "setup: not thrown");
            Vector3 flying = ragdoll.Torso.linearVelocity;

            Assert.IsTrue(ragdoll.Recover(Vector3.forward), "a thrown body should be able to flip back up");
            Assert.IsTrue(ragdoll.IsRecovering && ragdoll.IsLimp, "the flip is part of being down");
            Vector3 start = _player.transform.position;
            float turned = 0.0f;
            Vector3 lastUp = BodyUp;
            float flipSeconds = 0.0f;
            while (ragdoll.IsRecovering && flipSeconds < 2.0f)
            {
                yield return Step(ragdoll);
                flipSeconds += Time.deltaTime;
                turned += Vector3.Angle(lastUp, BodyUp);
                lastUp = BodyUp;
            }
            Debug.Log($"CityX recovery: thrown at {flying}, flipped {turned:0} deg in {flipSeconds:0.00} s (planned {ragdoll.Flip.SweepDegrees:0} deg, " +
                      $"{ragdoll.Flip.Seconds:0.00} s), travelled {Vector3.Distance(start, _player.transform.position):0.00} m");
            Assert.IsFalse(ragdoll.IsLimp, "the flip never handed the body to the walker");
            Assert.Greater(turned, RecoveryFlip.MinSweepDegrees - 20.0f, "the body should turn a real flip, head over heels");
            Assert.AreEqual(ragdoll.Flip.Seconds, flipSeconds, 0.05f, "the flip should take its planned time");
            Assert.Less(Quaternion.Angle(Animator.transform.localRotation, Quaternion.identity), 0.5f, "the body is left turned");

            var flat = new Vector3(flying.x, 0.0f, flying.z);
            Vector3 moved = _player.transform.position - start;
            moved.y = 0.0f;
            Assert.Greater(Vector3.Dot(moved, flat.normalized), flat.magnitude * flipSeconds * 0.8f, "it should fly on the way it was thrown through the flip");
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            Assert.IsFalse(locomotion.IsGrounded, "thrown high, it should come out of the flip still in the air");

            float before = _player.transform.position.x;
            for (float t = 0.0f; t < 4.0f && !locomotion.IsGrounded; t += Time.deltaTime)
            {
                yield return Step(ragdoll);
            }
            Assert.IsTrue(locomotion.IsGrounded, "it never landed");
            Assert.Greater(_player.transform.position.x - before, 1.0f, "its momentum carried it on after the flip, through the air");
            yield return Step(ragdoll);
            float headOverFeet = Animator.GetBoneTransform(HumanBodyBones.Head).position.y - _player.transform.position.y;
            Assert.Greater(headOverFeet, 1.6f, "it should land on its feet, upright");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator LyingOnTheGroundItPopsUpIntoTheFlipAndLandsOnItsFeetWithoutWaiting()
        {
#if UNITY_EDITOR
            Ragdoll ragdoll = Spawn(new Vector3(0.0f, 0.1f, 0.0f));
            yield return null;
            _player.GetComponent<PlayerVitals>().KnockDown(new Vector3(0.0f, 1.0f, 2.0f));
            for (float t = 0.0f; t < 1.2f; t += Time.deltaTime)
            {
                yield return Step(ragdoll);
            }
            Assert.IsTrue(ragdoll.IsLimp, "setup: not down");
            Assert.IsFalse(ragdoll.CanStandUp, "setup: it has not lain long enough to get up the ordinary way");
            Assert.Less(ragdoll.Torso.position.y, 0.6f, "setup: not lying on the ground");

            Assert.IsTrue(ragdoll.Recover(Vector3.forward), "lying down, it should flip up at once, without waiting");
            float highest = 0.0f;
            bool uprightInTheAir = false;
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < 3.0f && (ragdoll.IsLimp || !locomotion.IsGrounded); t += Time.deltaTime)
            {
                yield return Step(ragdoll);
                highest = Mathf.Max(highest, _player.transform.position.y);
                uprightInTheAir |= !ragdoll.IsLimp && !locomotion.IsGrounded;
            }
            Debug.Log($"CityX recovery from lying down: feet rose {highest:0.00} m, the flip {ragdoll.Flip.SweepDegrees:0} deg");
            Assert.Greater(highest, 0.3f, "it should spring up off the ground into the flip");
            Assert.IsTrue(uprightInTheAir, "the flip should be round, the body on its feet, before it lands");
            Assert.IsFalse(ragdoll.IsLimp);
            Assert.IsTrue(locomotion.IsGrounded, "it never landed");
            Assert.Less(_player.transform.position.y, 0.2f, "it should land on the ground, not on top of something it imagined");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheFlipStartsFromThePoseTheBodyWasFlungIn()
        {
#if UNITY_EDITOR
            Ragdoll ragdoll = Spawn(new Vector3(0.0f, 6.0f, 0.0f));
            yield return null;
            _player.GetComponent<PlayerVitals>().KnockDown(new Vector3(3.0f, 2.0f, 0.0f));
            ragdoll.Torso.angularVelocity = new Vector3(0.0f, 0.0f, 6.0f);
            for (int i = 0; i < 15; i++)
            {
                yield return Step(ragdoll);
            }
            Transform hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform head = Animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 hipsBefore = hips.position;
            Vector3 headBefore = head.position;

            Assert.IsTrue(ragdoll.Recover(Vector3.forward));
            yield return null;   // one frame in (not ticked, so not carried on): the blend from the flung pose has had its LateUpdate
            Assert.Less(Vector3.Distance(hips.position, hipsBefore), 0.3f, "the hips jumped when the flip began");
            Assert.Less(Vector3.Distance(head.position, headBefore), 0.6f, "the head jumped when the flip began");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator HeldInTheClawsFistItCannotFlipFree()
        {
#if UNITY_EDITOR
            Ragdoll ragdoll = Spawn(new Vector3(0.0f, 0.1f, 0.0f));
            yield return null;
            var vitals = _player.GetComponent<PlayerVitals>();
            Assert.IsTrue(vitals.Seize(), "setup: not seized");
            Assert.IsFalse(ragdoll.CanRecover);
            Assert.IsFalse(ragdoll.Recover(Vector3.forward), "held in a fist, it flipped free");
            vitals.Throw(new Vector3(0.0f, 6.0f, 10.0f), 2.5f);
            yield return null;
            Assert.IsTrue(ragdoll.CanRecover, "thrown, it can flip back up");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ABlowOrDeathMidFlipThrowsItLimpAgain()
        {
#if UNITY_EDITOR
            Ragdoll ragdoll = Spawn(new Vector3(0.0f, 6.0f, 0.0f));
            yield return null;
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.KnockDown(new Vector3(0.0f, 2.0f, 4.0f));
            yield return Step(ragdoll);
            Assert.IsTrue(ragdoll.Recover(Vector3.forward));
            yield return Step(ragdoll);
            yield return Step(ragdoll);

            vitals.KnockDown(new Vector3(0.0f, 2.0f, -6.0f));
            Assert.IsTrue(ragdoll.IsLimp, "knocked down mid-flip, it should go limp again");
            Assert.IsFalse(ragdoll.IsRecovering, "...and not carry on flipping");
            Assert.IsFalse(ragdoll.Torso.isKinematic, "...limp for real");
            Assert.Less(Quaternion.Angle(Animator.transform.localRotation, Quaternion.identity), 0.5f, "the flip's turn was left on the body");

            for (int i = 0; i < 3; i++)
            {
                yield return Step(ragdoll);
            }
            Assert.IsTrue(ragdoll.Recover(Vector3.forward), "and it can flip up from that too");
            yield return Step(ragdoll);
            vitals.TakeDamage(vitals.MaxHealth * 10.0f);
            Assert.IsTrue(vitals.IsDead);
            Assert.IsTrue(ragdoll.IsLimp && !ragdoll.IsRecovering && !ragdoll.Torso.isKinematic, "killed mid-flip, the body should fall limp");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator FlippingUpEndsTheKnockdownsTimer()
        {
#if UNITY_EDITOR
            Ragdoll ragdoll = Spawn(new Vector3(0.0f, 0.1f, 0.0f));
            yield return null;
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.TriggerFinisherKnockdown(Vector3.forward);
            yield return Step(ragdoll);
            Assert.IsTrue(ragdoll.Recover(Vector3.forward));
            bool stoodUp = false;
            for (float t = 0.0f; t < PlayerVitals.FinisherRagdollSeconds + 1.0f; t += Time.deltaTime)
            {
                vitals.TickHitstun(Time.deltaTime);
                yield return Step(ragdoll);
                stoodUp |= ragdoll.IsStandingUp;
            }
            Assert.IsFalse(stoodUp, "the knockdown's timer stood the body up again after it had flipped up");
            Assert.IsFalse(ragdoll.IsLimp);
            Assert.IsFalse(vitals.IsInvulnerable, "flipping up is not the timed get-up: it earns no moment of safety");
#else
            yield break;
#endif
        }

        /// <summary>A knockdown's time ran out while the body was still high in the air: it was stood up there. It waits to come down now.</summary>
        [UnityTest]
        public IEnumerator AKnockdownThrownHighGetsUpOnlyOnceItHasComeDown()
        {
#if UNITY_EDITOR
            Ragdoll ragdoll = Spawn(new Vector3(0.0f, 0.1f, 0.0f));
            yield return null;
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.KnockDown(new Vector3(0.0f, 16.0f, 2.0f));
            bool up = false;
            for (float t = 0.0f; t < 8.0f && !up; t += Time.deltaTime)
            {
                float torsoHeight = ragdoll.Torso.position.y;
                vitals.TickHitstun(Time.deltaTime);
                if (ragdoll.IsStandingUp)
                {
                    up = true;
                    Assert.Greater(t, PlayerVitals.FinisherRagdollSeconds, "setup: the throw should outlast the knockdown's time");
                    Assert.Less(torsoHeight, Ragdoll.OnGroundTorsoHeight + 0.05f, $"it began to get up {torsoHeight:0.0} m up, in the air");
                }
                yield return Step(ragdoll);
            }
            Assert.IsTrue(up, "it never got up once it had come down");
#else
            yield break;
#endif
        }
    }
}
