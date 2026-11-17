using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE HEAD IS STRAIGHT IN THE TWO-HANDED RUN (owner, 2026-11-16: "the two handed melee running animations is tilted slightly to the left make it
    /// straight"). On the real body, sprinting with the baseball bat and with nothing in hand: over the whole run the head tips to the side and turns from
    /// the way the body runs no more than in the plain run. It was tipped 10 degrees and turned 7, both to the left: the sword's head carriage stood the
    /// neck up, which turned the head with it by the clip's twist in the neck (PlayerLocomotion.ApplySwordHeadCarriage).
    /// </summary>
    public sealed class TwoHandedRunUprightPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>How much further the two-handed run's head may tip or turn, on average, than the plain run's does either way, degrees.</summary>
        private const float MostExtraDegrees = 2.0f;

        private GameObject _ground;
        private GameObject _player;
        private GameObject _weapon;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_weapon != null) Object.Destroy(_weapon);
            if (_ground != null) Object.Destroy(_ground);
        }

        private struct Head
        {
            public float Tip;
            public float Turn;
        }

#if UNITY_EDITOR
        /// <summary>The head over a run, averaged: how far it tips to the body's right, and turns to its right of the way it runs.</summary>
        private IEnumerator Sprint(bool bat, System.Action<Head> done)
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, -150.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            Animator animator = _player.GetComponentInChildren<Animator>();
            if (bat)
            {
                _weapon = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/BaseballBat.prefab"), new Vector3(10.0f, 1.0f, 10.0f), Quaternion.identity);
                _weapon.GetComponent<SwordWeapon>().OnEquip(_player, CityX.Items.ItemInstanceData.None);
            }
            var input = new LocomotionInput { Move = Vector2.up, Sprint = true, SwordEquipped = bat };
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Transform body = _player.transform;

            var sum = new Head();
            int samples = 0;
            for (float t = 0.0f; t < 3.5f; t += Dt)
            {
                locomotion.Tick(input, Dt);
                yield return null;
                if (t < 1.5f)
                {
                    continue;   // up to speed, the run settled
                }
                sum.Tip += Mathf.Atan2(Vector3.Dot(head.up, body.right), Vector3.Dot(head.up, body.up)) * Mathf.Rad2Deg;
                sum.Turn += Vector3.SignedAngle(body.forward, Vector3.ProjectOnPlane(head.forward, body.up), body.up);
                samples++;
            }
            if (bat)
            {
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName("SwordLocomotion"), "the bat is not run with the two-handed run");
            }
            done(new Head { Tip = sum.Tip / samples, Turn = sum.Turn / samples });
            Object.Destroy(_player);
            if (_weapon != null) Object.Destroy(_weapon);
            Object.Destroy(_ground);
            yield return null;
        }
#endif

        [UnityTest]
        public IEnumerator TheHeadIsAsStraightInTheTwoHandedRunAsInThePlainRun()
        {
#if UNITY_EDITOR
            Head plain = default, twoHanded = default;
            yield return Sprint(false, h => plain = h);
            yield return Sprint(true, h => twoHanded = h);
            Debug.Log($"CityX run head (+ right): plain tipped {plain.Tip:0.0} turned {plain.Turn:0.0}; two-handed tipped {twoHanded.Tip:0.0} turned {twoHanded.Turn:0.0} deg");
            Assert.LessOrEqual(Mathf.Abs(twoHanded.Tip), Mathf.Abs(plain.Tip) + MostExtraDegrees, "the head is tipped to one side in the two-handed run");
            Assert.LessOrEqual(Mathf.Abs(twoHanded.Turn), Mathf.Abs(plain.Turn) + MostExtraDegrees, "the head is turned off the way the two-handed run goes");
#else
            yield break;
#endif
        }
    }
}
