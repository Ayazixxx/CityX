using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A blow is its thrower's to land only while the thrower is still in the fight. A punch already under way used to land at its moment
    /// whatever had happened to the body since: staggered by a blow, thrown limp, even killed. And a punch pressed while busy, remembered to
    /// be thrown when the swing allowed, went off in the middle of the stagger.
    /// </summary>
    public sealed class MeleeInterruptedPlayTests
    {
        private const float Slice = 1.0f / 60.0f;

        private GameObject _attackerObject;
        private PlayerVitals _vitals;
        private MeleeAttacker _attacker;
        private GameObject _dummyObject;
        private SparringDummy _dummy;
        private GameObject _ground;
        private GameObject _player;
        private int _swings;

        [SetUp]
        public void SetUp()
        {
            _attackerObject = new GameObject("Attacker");
            _vitals = _attackerObject.AddComponent<PlayerVitals>();
            _vitals.Roll = () => 1.0f;   // never the chance block: the blows that stagger it here must land
            _attacker = _attackerObject.AddComponent<MeleeAttacker>();
            _swings = 0;
            _attacker.SwingStarted += stage => _swings++;

            _dummyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _dummyObject.transform.position = new Vector3(0.0f, 1.0f, 1.5f);   // inside a fist's reach, straight ahead
            _dummy = _dummyObject.AddComponent<SparringDummy>();
            _dummy.Reset();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_attackerObject != null) Object.DestroyImmediate(_attackerObject);
            if (_dummyObject != null) Object.DestroyImmediate(_dummyObject);
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private void Step(MeleeAttacker attacker, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Slice)
            {
                attacker.Tick(Slice);
            }
        }

        /// <summary>A blow from behind that staggers the attacker (its hitstun is not run down here: it stays staggered).</summary>
        private void Stagger()
        {
            ((IDamageable)_vitals).ApplyHit(5.0f, new Vector3(0.0f, 1.0f, -1.0f), 0.6f);
            Assert.IsTrue(_vitals.IsStunned, "setup: the blow did not stagger the attacker");
        }

        [Test]
        public void SetupAPunchLeftAloneLands()
        {
            Assert.IsTrue(_attacker.TryAttack());
            Step(_attacker, 1.0f);
            Assert.AreEqual(1, _dummy.TimesHit, "setup: the punch does not reach the dummy");
        }

        [Test]
        public void APunchUnderWayDoesNotLandOnceItsThrowerIsStaggered()
        {
            Assert.IsTrue(_attacker.TryAttack());
            _attacker.Tick(Slice);   // begun, well before its contact
            Assert.AreEqual(0, _dummy.TimesHit, "setup: the punch landed on its first frame");
            Stagger();
            Step(_attacker, 1.0f);
            Assert.AreEqual(0, _dummy.TimesHit, "staggered in the middle of its punch, the body landed it all the same");
            Assert.IsFalse(_attacker.IsSwinging, "the swing goes on under the stagger");
        }

        [Test]
        public void APunchUnderWayDoesNotLandOnceItsThrowerIsDead()
        {
            Assert.IsTrue(_attacker.TryAttack());
            _attacker.Tick(Slice);
            _vitals.TakeDamage(_vitals.Health + 10.0f);
            Assert.IsTrue(_vitals.IsDead, "setup: not dead");
            Step(_attacker, 1.0f);
            Assert.AreEqual(0, _dummy.TimesHit, "a dead body's punch landed");
        }

        [Test]
        public void APunchRememberedWhileBusyIsNotThrownInTheMiddleOfAStagger()
        {
            Assert.IsTrue(_attacker.TryAttack());
            _attacker.Tick(Slice);
            Assert.IsFalse(_attacker.TryAttack(), "setup: a second punch was thrown at once instead of remembered");
            for (int i = 0; i < 120 && _dummy.TimesHit == 0; i++)
            {
                _attacker.Tick(Slice);
            }
            Assert.AreEqual(1, _dummy.TimesHit, "setup: the first punch never landed");
            Assert.AreEqual(1, _swings, "setup: the remembered punch was thrown before the first had landed");

            Stagger();
            Step(_attacker, 1.5f);
            Assert.AreEqual(1, _swings, "the punch remembered from before went off while the body was staggered");
            Assert.AreEqual(1, _dummy.TimesHit);
        }

        /// <summary>On the real body: thrown limp in the middle of a punch (a knockdown), the punch does not land from the ground.</summary>
        [UnityTest]
        public IEnumerator APunchUnderWayDoesNotLandOnceItsThrowerIsThrownLimp()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _attackerObject.SetActive(false);   // only the real body swings here
            _dummyObject.transform.position = new Vector3(0.0f, 1.3f, 1.2f);
            Physics.SyncTransforms();
            yield return null;
            var melee = _player.GetComponent<MeleeAttacker>();
            var vitals = _player.GetComponent<PlayerVitals>();
            Assert.IsNotNull(melee, "setup: the body has no fists");

            Assert.IsTrue(melee.TryAttack());
            melee.Tick(Slice);
            vitals.KnockDown(new Vector3(0.0f, 1.0f, -0.5f));   // barely moved: the dummy is still in reach of where it stood
            Assert.IsTrue(_player.GetComponent<Ragdoll>().IsLimp, "setup: the knockdown did not throw it");
            for (int i = 0; i < 40; i++)
            {
                melee.Tick(Slice);
                yield return null;
            }
            Assert.AreEqual(0, _dummy.TimesHit, "lying limp, the body landed the punch it had begun");
#else
            yield break;
#endif
        }
    }
}
