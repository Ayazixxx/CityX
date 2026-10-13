using CityX.Character;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The shared posture (owner, 2026-10-13: the head always angled down, the legs too far forward under the torso): every body built carries
    /// it, the asset exists to be tuned, and it moves the head and the legs the way its names say.
    /// </summary>
    public sealed class BodyPostureTests
    {
        [Test]
        public void EveryCharacterPrefabCarriesThePostureAndTheAssetExists()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<BodyPostureSettings>(BodyPostureSettings.AssetPath), "no posture asset to tune");
            foreach (string path in new[] { "Assets/CityX/Character/Player.prefab", "Assets/CityX/Character/Civilian.prefab",
                         "Assets/CityX/Character/NetworkedPlayer.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                Assert.IsNotNull(prefab.GetComponentInChildren<BodyPosture>(true), $"{path} stands in the raw animation pose");
            }
        }

        [Test]
        public void ThePostureTipsTheHeadUpAndMovesTheLegsBack()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab);
            BodyPostureSettings before = BodyPostureSettings.Current;
            try
            {
                var settings = ScriptableObject.CreateInstance<BodyPostureSettings>();
                settings.HeadPitchUp = 20.0f;
                settings.LegsBack = 0.1f;
                settings.HeadBack = 0.08f;
                settings.HeadUp = 0.03f;
                BodyPostureSettings.Current = settings;
                Animator animator = player.GetComponentInChildren<Animator>();
                BodyPosture posture = player.GetComponentInChildren<BodyPosture>();
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform thigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Vector3 faceBefore = head.forward;
                Vector3 headBefore = head.position;
                Vector3 thighBefore = thigh.position;
                typeof(BodyPosture).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(posture, null);
                typeof(BodyPosture).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(posture, null);
                Assert.Greater(head.forward.y, faceBefore.y + 0.2f, "the face did not lift");
                Vector3 headMoved = head.position - headBefore;
                Assert.AreEqual(-0.08f, Vector3.Dot(headMoved, player.transform.forward), 0.005f, "the head did not move back");
                Assert.AreEqual(0.03f, Vector3.Dot(headMoved, player.transform.up), 0.005f, "the head did not move up");
                Vector3 moved = thigh.position - thighBefore;
                Assert.AreEqual(-0.1f, Vector3.Dot(moved, player.transform.forward), 0.005f, "the legs did not move back");
                // Applied again it does not pile up on the legs.
                typeof(BodyPosture).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(posture, null);
                Assert.AreEqual(-0.1f, Vector3.Dot(thigh.position - thighBefore, player.transform.forward), 0.005f, "the leg offset piles up frame on frame");
            }
            finally
            {
                BodyPostureSettings.Current = before;
                Object.DestroyImmediate(player);
            }
        }
    }
}
