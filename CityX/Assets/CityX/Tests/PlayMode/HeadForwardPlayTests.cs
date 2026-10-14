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
    /// Owner, 2026-10-13: "I don't like how the head looks side to side when running, it should just look forward." Walking and sprinting on the real rig,
    /// the face points the way the body faces (no side-to-side swing) and the head stays level (no roll), every frame.
    /// </summary>
    public sealed class HeadForwardPlayTests
    {
        private GameObject _ground;
        private GameObject _player;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        [UnityTest]
        public IEnumerator WalkingAndSprintingTheHeadFacesForwardAndStaysLevel()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0, -0.5f, 0);
            _ground.transform.localScale = new Vector3(300, 1, 300);
            Physics.SyncTransforms();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
            _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            Transform head = _player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            Time.captureFramerate = 60;
            yield return null;
            yield return null;

            float worstSwing = 0.0f, worstRoll = 0.0f;
            for (int frame = 0; frame < 240; frame++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = frame >= 120 }, 1.0f / 60.0f);
                yield return null;
                if (frame % 120 < 30) continue;   // let each gait settle in
                Vector3 face = head.forward;
                face.y = 0.0f;
                worstSwing = Mathf.Max(worstSwing, Vector3.Angle(face, _player.transform.forward));
                // Roll: the head's right-hand side tipped up or down.
                worstRoll = Mathf.Max(worstRoll, Mathf.Abs(Mathf.Asin(Mathf.Clamp(head.right.y, -1.0f, 1.0f)) * Mathf.Rad2Deg));
            }
            Assert.Less(worstSwing, 3.0f, $"the head swung {worstSwing:0.0} degrees off the way the body faces");
            Assert.Less(worstRoll, 3.0f, $"the head rolled {worstRoll:0.0} degrees");
#else
            yield break;
#endif
        }
    }
}
