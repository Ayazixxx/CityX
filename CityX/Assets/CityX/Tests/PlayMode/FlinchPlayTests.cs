using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Being hit shows on the real rig: a blow from the front throws the torso and head back for a moment, and another player's copy, told the
    /// same reaction by name, flinches the same way.
    /// </summary>
    public sealed class FlinchPlayTests
    {
        private GameObject _ground;
        private GameObject _player;
        private GameObject _copy;

        [TearDown]
        public void TearDown()
        {
            if (_copy != null) Object.Destroy(_copy);
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private GameObject Spawn(Vector3 at)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject body = Object.Instantiate(prefab, at, Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            return body;
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator ABlowFromTheFrontThrowsTheTorsoBackAndTheCopyFlinchesByName()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0, -0.5f, 0);
            _ground.transform.localScale = new Vector3(60, 1, 60);
            _player = Spawn(new Vector3(0, 0.05f, 0));
            _copy = Spawn(new Vector3(5, 0.05f, 0));
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            var copyLocomotion = _copy.GetComponent<PlayerLocomotion>();
            var vitals = _player.GetComponent<PlayerVitals>();
            var driver = _player.GetComponent<PlayerAnimationDriver>();
            var copyDriver = _copy.GetComponent<PlayerAnimationDriver>();
            Transform spine = _player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Spine);
            for (int i = 0; i < 30; i++)
            {
                locomotion.Tick(default, Time.deltaTime);
                copyLocomotion.Tick(default, Time.deltaTime);
                yield return null;
            }
            float restingLean = spine.up.z;

            string announced = null;
            driver.TimedStatePlayed += (name, speed, start) => announced ??= name;
            ((IDamageable)vitals).ApplyHit(10.0f, _player.transform.position + _player.transform.forward * 2.0f + Vector3.up, 0.4f);
            yield return null;

            Assert.AreEqual(HitReactions.NameFor(HitDirection.Front, true), announced, "the stagger from the front was not announced by name");
            Assert.IsTrue(driver.Flincher.IsPlaying && driver.Flincher.Direction == HitDirection.Front, "the body is not flinching from the front");
            float worst = restingLean;
            for (int i = 0; i < 8; i++)
            {
                locomotion.Tick(default, Time.deltaTime);
                vitals.TickHitstun(Time.deltaTime);
                yield return null;
                worst = Mathf.Min(worst, spine.up.z);
            }
            Assert.Less(worst, restingLean - 0.15f, $"the torso never leaned back (spine up.z went from {restingLean:0.00} to {worst:0.00} at most)");

            // The copy: no vitals of its own were hit; it is told the state by name, as the network does.
            copyDriver.PlayRemoteState(HitReactions.NameFor(HitDirection.Left, false), 1.0f, 0.0f);
            yield return null;
            Assert.IsTrue(copyDriver.Flincher.IsPlaying && copyDriver.Flincher.Direction == HitDirection.Left && !copyDriver.Flincher.IsStagger,
                "the copy did not take the flinch by name");
            Assert.IsTrue(copyLocomotion.FlinchPoseActive);

            for (int i = 0; i < 50; i++)
            {
                locomotion.Tick(default, Time.deltaTime);
                vitals.TickHitstun(Time.deltaTime);
                yield return null;
            }
            Assert.IsFalse(driver.Flincher.IsPlaying, "the recoil should be over");
            Assert.IsFalse(locomotion.FlinchPoseActive);
#else
            yield break;
#endif
        }
    }
}
