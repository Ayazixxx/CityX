using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A dead body stays down. A knockdown ends by its own timer with the body getting up: one killed while it lay knocked down got up
    /// all the same when the timer ran out, a corpse on its feet; one killed while getting up finished the clip standing; and a respawn
    /// after either kept the get-up going on the new life (every animation at the get-up's pace, the next knockdown popping straight up).
    /// </summary>
    public sealed class CorpseStaysDownPlayTests
    {
        private const float Step = 1.0f / 60.0f;

        private GameObject _ground;
        private GameObject _player;
        private PlayerVitals _vitals;
        private Ragdoll _ragdoll;
        private Animator _animator;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
#if UNITY_EDITOR
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.3f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;   // the frames are run by hand, as it runs them (Frames)
            _vitals = _player.GetComponent<PlayerVitals>();
            _ragdoll = _player.GetComponent<Ragdoll>();
            _animator = _player.GetComponentInChildren<Animator>();
#endif
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        /// <summary>
        /// Frames as PlayerInputReader runs them for this body: its vitals' clocks always (the knockdown's timer among them), the limp body's
        /// own tick only while it lives.
        /// </summary>
        private IEnumerator Frames(float seconds, System.Func<bool> until = null)
        {
            for (float t = 0.0f; t < seconds && (until == null || !until()); t += Step)
            {
                _vitals.TickHitstun(Step);
                if (_ragdoll.IsLimp && !_vitals.IsDead)
                {
                    _ragdoll.Tick(Step);
                }
                yield return null;
            }
        }

        private bool Lying => _ragdoll.IsLimp && !_ragdoll.IsStandingUp && !_animator.enabled && !_ragdoll.Torso.isKinematic;

        [UnityTest]
        public IEnumerator ABodyKilledWhileKnockedDownStaysDownWhenTheKnockdownRunsOut()
        {
            yield return Frames(0.3f);
            _vitals.KnockDown(new Vector3(0.0f, 2.0f, -3.0f));
            Assert.IsTrue(_ragdoll.IsLimp, "setup: the knockdown did not throw it");
            yield return Frames(PlayerVitals.FinisherRagdollSeconds * 0.4f);
            _vitals.TakeDamage(_vitals.Health + 50.0f);
            Assert.IsTrue(_vitals.IsDead, "setup: the blow did not kill it");

            yield return Frames(PlayerVitals.FinisherRagdollSeconds + 1.0f);
            Assert.IsFalse(_ragdoll.IsStandingUp, "the corpse began to get up when its knockdown ran out");
            Assert.IsTrue(Lying, "the corpse is not lying limp");
        }

        [UnityTest]
        public IEnumerator ABodyKilledWhileGettingUpFallsAgain()
        {
            yield return Frames(0.3f);
            _vitals.KnockDown(new Vector3(0.0f, 2.0f, -3.0f));
            yield return Frames(PlayerVitals.FinisherRagdollSeconds + 1.0f, () => _ragdoll.IsStandingUp);
            Assert.IsTrue(_ragdoll.IsStandingUp, "setup: the knockdown never ended in a get-up");
            yield return Frames(0.15f);
            Assert.IsTrue(_ragdoll.IsStandingUp, "setup: the get-up was over too soon to die in");

            _vitals.TakeDamage(_vitals.Health + 50.0f);
            Assert.IsTrue(_vitals.IsDead, "setup: the blow did not kill it");
            yield return Frames(0.1f);
            Assert.IsTrue(Lying, "killed while getting up, the body went on with the get-up");
            Assert.AreEqual(1.0f, _animator.speed, 1e-4f, "the get-up's pace was left on the animator");

            // And it stays down.
            yield return Frames(Ragdoll.StandUpRecoverySeconds + 1.0f);
            Assert.IsTrue(Lying, "the corpse got up");
        }

        [UnityTest]
        public IEnumerator ARespawnInTheMiddleOfAGetUpLeavesNothingOfItBehind()
        {
            yield return Frames(0.3f);
            _ragdoll.Activate(Vector3.zero, allowImmediateStandUp: true);
            yield return Frames(0.5f);
            _ragdoll.BeginStandingUp(force: true);
            Assert.IsTrue(_ragdoll.IsStandingUp, "setup: no get-up");
            yield return Frames(0.1f);

            _ragdoll.Deactivate();   // what a respawn does (PlayerVitals.Revived)
            Assert.IsFalse(_ragdoll.IsLimp);
            Assert.IsFalse(_ragdoll.IsStandingUp, "stood back up for a respawn, the body is still getting up");
            Assert.AreEqual(1.0f, _animator.speed, 1e-4f, "the get-up's pace was left on the animator: every animation of the new life plays at it");
            Assert.IsTrue(_player.GetComponent<PlayerLocomotion>().enabled, "the body cannot move");
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }
            Assert.IsFalse(_animator.GetCurrentAnimatorStateInfo(0).IsName("GettingUp"), "the new life goes on playing the get-up");

            // The next knockdown is a knockdown: the body lies until its timer, it does not pop straight up.
            _vitals.KnockDown(new Vector3(0.0f, 2.0f, -3.0f));
            yield return Frames(PlayerVitals.FinisherRagdollSeconds * 0.5f);
            Assert.IsTrue(Lying, "the next knockdown did not leave it lying");
        }
    }
}
