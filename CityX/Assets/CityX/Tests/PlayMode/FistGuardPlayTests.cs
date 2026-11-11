using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The bare-handed guard (FistGuard; owner, 2026-11-10: "improve upon the blocking no weapon in hand animation right now the legs dont
    /// even move and it isnt timed right at all. redo the hole thing like a souls like game"): held for as long as the button is, its first
    /// instant a parry, the fists up before the face while the legs walk on under it, knocked back by a blow it soaks, swatting one it parries.
    /// </summary>
    public sealed class FistGuardPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private MeleeAttacker _melee;
        private PlayerVitals _vitals;
        private FistGuardDriver _guard;
        private Animator _animator;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private IEnumerator Spawn()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;   // the Animator's clock the same as the steps': in a batch run a frame is well under a millisecond
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _melee = _player.GetComponent<MeleeAttacker>();
            _vitals = _player.GetComponent<PlayerVitals>();
            _vitals.Roll = () => 1.0f;   // everyone's chance to stop a blow outright would make these random
            _animator = _player.GetComponentInChildren<Animator>();
            yield return Steps(20, false, default);
            _guard = _player.GetComponent<FistGuardDriver>();
            Assert.IsNotNull(_guard, "the body has no guard to play");
#else
            yield break;
#endif
        }

        /// <summary>Frames as the input reader runs them: the guard's button, the fists' clock, the body's step.</summary>
        private IEnumerator Steps(int count, bool guardHeld, LocomotionInput input)
        {
            for (int i = 0; i < count; i++)
            {
                _melee.SetGuardHeld(guardHeld);
                _melee.Tick(Dt);
                _locomotion.Tick(input, Dt);
                yield return null;
            }
        }

        private float HandHeight(HumanBodyBones hand) => _animator.GetBoneTransform(hand).position.y - _player.transform.position.y;

        [UnityTest]
        public IEnumerator HeldTheGuardLastsAsLongAsTheButtonAndItsFirstInstantParries()
        {
            yield return Spawn();
            var target = (IDamageable)_vitals;
            yield return Steps(2, true, default);
            Assert.IsTrue(_melee.IsGuarding);
            Assert.AreEqual(HitResponse.Blocked, target.ApplyHit(30.0f, Vector3.forward, 0.5f), "a blow in the guard's first instant was not parried");

            yield return Steps(Mathf.CeilToInt(2.0f / Dt), true, default);
            Assert.IsTrue(_vitals.IsBlocking, "the guard came down by itself while the button was still held (it was a 0.7 s block)");
            float health = _vitals.Health, stamina = _vitals.Stamina;
            Assert.AreEqual(HitResponse.Guarded, target.ApplyHit(20.0f, Vector3.forward, 0.5f), "a held guard did not soak the blow");
            Assert.Less(health - _vitals.Health, 20.0f * 0.5f, "the guard let most of the blow through");
            Assert.Less(_vitals.Stamina, stamina, "soaking the blow cost no stamina");

            yield return Steps(2, false, default);
            Assert.IsFalse(_melee.IsGuarding);
            Assert.IsFalse(_vitals.IsBlocking, "the guard stayed up after the button was let go");
        }

        [UnityTest]
        public IEnumerator TheFistsComeUpBeforeTheFaceAndTheLegsWalkOnUnderThem()
        {
            yield return Spawn();
            float idleHand = HandHeight(HumanBodyBones.RightHand);
            yield return Steps(Mathf.CeilToInt(0.4f / Dt), true, default);
            Assert.AreEqual(1.0f, _guard.Weight, 1e-3f, "the guard is not up a moment after the button");
            Assert.Greater(HandHeight(HumanBodyBones.RightHand), idleHand + 0.8f, "the right fist is not up before the face");
            Assert.Greater(HandHeight(HumanBodyBones.LeftHand), idleHand + 0.8f, "the left fist is not up before the face");

            // Walking with the guard up, the thighs swing as far as they do walking plain: the guard holds the arms, not the legs.
            var walk = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 0.0f };
            float guarded = 0.0f;
            yield return ThighSwing(true, walk, swing => guarded = swing);
            Assert.Greater(HandHeight(HumanBodyBones.RightHand), idleHand + 0.8f, "walking let the guard down");
            float plain = 0.0f;
            yield return ThighSwing(false, walk, swing => plain = swing);
            Assert.Greater(plain, 15.0f, "setup: the plain walk swings the thighs too little to compare with");
            Assert.Greater(guarded, 0.7f * plain, $"the legs barely walked under the guard ({guarded:0} degrees of thigh swing against {plain:0} plain)");
        }

        /// <summary>How far the left thigh swings forward and back over two seconds of walking, degrees, with the guard up or not.</summary>
        private IEnumerator ThighSwing(bool guardHeld, LocomotionInput walk, System.Action<float> swing)
        {
            Transform hip = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), knee = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            yield return Steps(30, guardHeld, walk);
            float least = float.MaxValue, most = float.MinValue;
            for (int i = 0; i < 120; i++)
            {
                yield return Steps(1, guardHeld, walk);
                Vector3 thigh = _player.transform.InverseTransformDirection(knee.position - hip.position);
                float forward = Mathf.Atan2(thigh.z, -thigh.y) * Mathf.Rad2Deg;
                least = Mathf.Min(least, forward);
                most = Mathf.Max(most, forward);
            }
            swing(most - least);
        }

        [UnityTest]
        public IEnumerator ABlowSoakedKnocksTheGuardBackAndOneParriedIsSwattedAside()
        {
            yield return Spawn();
            var target = (IDamageable)_vitals;
            yield return Steps(2, true, default);
            target.ApplyHit(20.0f, Vector3.forward, 0.5f);
            yield return Steps(1, true, default);
            Assert.AreEqual(FistGuard.ParryClip, _guard.Clip, "a parried blow was not swatted aside");

            yield return Steps(Mathf.CeilToInt(1.0f / Dt), true, default);
            Assert.AreEqual(FistGuard.HoldClip, _guard.Clip, "the swat did not settle back into the held guard");
            target.ApplyHit(20.0f, Vector3.forward, 0.5f);
            yield return Steps(1, true, default);
            Assert.AreEqual(FistGuard.HitClip, _guard.Clip, "a soaked blow did not knock the guard back");
        }

        [UnityTest]
        public IEnumerator APunchDropsTheGuardAndHeldItComesBackAfter()
        {
            yield return Spawn();
            yield return Steps(10, true, default);
            _melee.TryAttack();
            yield return Steps(3, true, default);
            Assert.IsFalse(_melee.IsGuarding, "the guard stayed up through a punch");
            Assert.IsFalse(_guard.IsUp);
            float waited = 0.0f;
            while (_melee.IsSwinging && waited < 3.0f)
            {
                yield return Steps(1, true, default);
                waited += Dt;
            }
            yield return Steps(3, true, default);
            Assert.IsTrue(_melee.IsGuarding, "the guard did not come back up after the punch with the button still held");
        }
    }
}
