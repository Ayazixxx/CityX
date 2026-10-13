using System.Collections;
using System.Collections.Generic;
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
    /// Sideways climbing on the real rig (owner, 2026-10-13: "allow sideways climbing with A &amp; D"): holding D shuffles the body right along the wall
    /// (still climbing, still on the wall), A left, the shuffle stops at the wall's end rather than hanging in the air beside it, the limbs keep stepping and
    /// stay on the wall, and the hang point never runs off ahead of a blocked body.
    /// </summary>
    public sealed class SidewaysClimbPlayTests
    {
        private GameObject _player;
        private readonly List<GameObject> _made = new List<GameObject>();
        private const float Dt = 1.0f / 60.0f;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_player != null) Object.Destroy(_player);
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        private GameObject Box(Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = centre;
            go.transform.localScale = size;
            _made.Add(go);
            return go;
        }

#if UNITY_EDITOR
        private PlayerClimbing Grab(float wallWidth)
        {
            Box(new Vector3(0, -0.5f, 0), new Vector3(100, 1, 100));
            Box(new Vector3(0, 20.0f, 2.0f), new Vector3(wallWidth, 40, 2));   // near face z = 1, facing -Z, from x = -w/2 to +w/2
            Physics.SyncTransforms();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.3f), Quaternion.identity);
            _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            Time.captureFramerate = 60;
            return _player.GetComponent<PlayerClimbing>();
        }
#endif

        [UnityTest]
        public IEnumerator HoldingDShufflesRightAlongTheWallAndAShufflesBack()
        {
#if UNITY_EDITOR
            PlayerClimbing climbing = Grab(30.0f);
            var vitals = _player.GetComponent<PlayerVitals>();
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            yield return null; yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));
            // Up a little first, so the feet are off the ground.
            for (int i = 0; i < 40; i++) { climbing.Tick(false, true, false, vitals, Dt); yield return null; }
            float startX = _player.transform.position.x;
            float startY = _player.transform.position.y;
            float startZ = _player.transform.position.z;   // the hang distance from the face, once drawn onto the wall
            Assert.Greater(startY, 0.8f, "did not climb up first");

            for (int i = 0; i < 60; i++)
            {
                climbing.Tick(false, false, false, vitals, Dt, rightHeld: true);
                yield return null;
                Assert.IsTrue(climbing.IsClimbing, "let go of the wall while shuffling right");
                Assert.AreEqual(1.0f, locomotion.ClimbTravel.x, 0.01f, "the body was not told it is travelling right");
            }
            float moved = _player.transform.position.x - startX;
            Assert.Greater(moved, 1.2f, $"holding D for a second moved the body only {moved:F2} m along the wall");
            Assert.AreEqual(startY, _player.transform.position.y, 0.05f, "shuffling sideways changed the height");
            Assert.Less(Mathf.Abs(_player.transform.position.z - startZ), 0.12f, "the body left the wall face");

            for (int i = 0; i < 60; i++)
            {
                climbing.Tick(false, false, false, vitals, Dt, leftHeld: true);
                yield return null;
            }
            Assert.Less(_player.transform.position.x - startX, 0.3f, "holding A did not bring the body back left");
            Assert.IsTrue(climbing.IsClimbing);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheShuffleStopsAtTheEndOfTheWall()
        {
#if UNITY_EDITOR
            PlayerClimbing climbing = Grab(4.0f);   // the wall ends at x = 2
            var vitals = _player.GetComponent<PlayerVitals>();
            yield return null; yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));
            for (int i = 0; i < 40; i++) { climbing.Tick(false, true, false, vitals, Dt); yield return null; }
            for (int i = 0; i < 180; i++)
            {
                climbing.Tick(false, false, false, vitals, Dt, rightHeld: true);
                yield return null;
                Assert.IsTrue(climbing.IsClimbing, "the climb ended at the wall's edge instead of holding");
            }
            float x = _player.transform.position.x;
            Assert.Greater(x, 0.5f, "never shuffled toward the edge");
            Assert.Less(x, 2.0f - _player.GetComponent<CharacterController>().radius, $"the body shuffled past the end of the wall to x = {x:F2}");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ShufflingTheLimbsKeepSteppingAndStayOnTheWall()
        {
#if UNITY_EDITOR
            PlayerClimbing climbing = Grab(30.0f);
            var vitals = _player.GetComponent<PlayerVitals>();
            var animator = _player.GetComponentInChildren<Animator>();
            yield return null; yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));
            for (int i = 0; i < 40; i++) { climbing.Tick(false, true, false, vitals, Dt); yield return null; }

            Transform[] ends =
            {
                animator.GetBoneTransform(HumanBodyBones.LeftHand), animator.GetBoneTransform(HumanBodyBones.RightHand),
                animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot),
            };
            var startX = new float[4];
            for (int i = 0; i < 4; i++) startX[i] = ends[i].position.x;
            float deepest = float.MaxValue, farthest = 0.0f;
            for (int i = 0; i < 90; i++)
            {
                climbing.Tick(false, false, false, vitals, Dt, rightHeld: true);
                yield return null;
                foreach (Transform end in ends)
                {
                    float depth = 1.0f - end.position.z;   // distance in front of the face at z = 1
                    deepest = Mathf.Min(deepest, depth);
                    farthest = Mathf.Max(farthest, depth);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                Assert.Greater(ends[i].position.x - startX[i], 0.8f, $"{ends[i].name} was left behind: it did not step along the wall");
            }
            Assert.Greater(deepest, -0.03f, $"a hand or foot went {-deepest:F2} m into the wall while shuffling");
            Assert.Less(farthest, 0.45f, "a hand or foot waved in the air away from the wall while shuffling");
#else
            yield break;
#endif
        }
    }
}
