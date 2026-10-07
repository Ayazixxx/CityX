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
    /// The body must never stretch: every bone stays the rest distance from its parent and keeps scale one, whatever is driving the pose (a sprint, a climb, a punch).
    /// A limb drawn twice as wide as it should be, or an arm longer than its own length, is a bone that moved away from its parent or was scaled.
    /// </summary>
    public sealed class BodyCannotStretchPlayTests
    {
        private GameObject _ground, _wall, _player;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
            if (_wall != null) Object.Destroy(_wall);
        }

#if UNITY_EDITOR
        private sealed class Rest
        {
            public Transform Bone, Parent;
            public float Distance;
        }

        private List<Rest> Measure(Animator animator)
        {
            // Rest lengths come from the skeleton as built (a T-pose with every bone straight to its parent).
            var list = new List<Rest>();
            foreach (HumanBodyBones id in System.Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (id == HumanBodyBones.LastBone) continue;
                Transform bone = animator.GetBoneTransform(id);
                if (bone == null || bone.parent == null || bone.parent == animator.transform) continue;
                list.Add(new Rest { Bone = bone, Parent = bone.parent, Distance = bone.localPosition.magnitude * bone.parent.lossyScale.x });
            }
            return list;
        }

        private static string Worst(List<Rest> rest, out float worstRatio)
        {
            worstRatio = 1.0f;
            string name = "";
            foreach (Rest r in rest)
            {
                float d = Vector3.Distance(r.Bone.position, r.Parent.position);
                float ratio = r.Distance > 1e-4f ? d / r.Distance : 1.0f;
                float scale = r.Bone.lossyScale.x;
                float worst = Mathf.Max(Mathf.Abs(ratio - 1.0f), Mathf.Abs(scale - 1.0f));
                if (worst > Mathf.Abs(worstRatio - 1.0f))
                {
                    worstRatio = Mathf.Abs(ratio - 1.0f) >= Mathf.Abs(scale - 1.0f) ? ratio : scale;
                    name = $"{r.Bone.name} (distance to parent {d:F3} m, rest {r.Distance:F3} m, scale {scale:F2})";
                }
            }
            return name;
        }
#endif

        [UnityTest]
        public IEnumerator NoBoneStretchesWhileClimbingSprintingOrPunching()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0, -0.5f, 0); _ground.transform.localScale = new Vector3(200, 1, 200);
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0, 20.0f, 52.0f); _wall.transform.localScale = new Vector3(20, 40, 2);
            Physics.SyncTransforms();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 50.3f), Quaternion.identity);
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            var climbing = _player.GetComponent<PlayerClimbing>();
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.InfiniteStamina = true;
            var melee = _player.GetComponent<MeleeAttacker>();
            var animator = _player.GetComponentInChildren<Animator>();
            yield return null; yield return null;
            List<Rest> rest = Measure(animator);
            Assert.Greater(rest.Count, 10, "could not measure the skeleton");
            const float dt = 1.0f / 60.0f;
            float worst = 0.0f; string where = "", phase = "";

            void Check(string label)
            {
                string name = Worst(rest, out float ratio);
                if (Mathf.Abs(ratio - 1.0f) > Mathf.Abs(worst - 1.0f) || worst == 0.0f) { worst = ratio; where = name; phase = label; }
            }
            worst = 1.0f;

            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));
            for (int i = 0; i < 300; i++)
            {
                climbing.Tick(false, i < 200, i >= 200, vitals, dt);
                yield return null;
                Check("climbing");
            }
            yield return null;

            var controller = _player.GetComponent<CharacterController>();
            controller.enabled = false; _player.transform.position = new Vector3(0, 0.05f, 0); _player.transform.rotation = Quaternion.identity; controller.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 150; i++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = true }, dt);
                yield return null;
                Check("sprinting");
            }
            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(default, dt);
                if (i % 25 == 0) melee.TryAttack();
                melee.Tick(dt);
                yield return null;
                Check("punching");
            }
            Assert.That(worst, Is.InRange(0.97f, 1.03f), $"a bone stretched to {worst:F2} of its rest length/scale while {phase}: {where}");
#else
            yield break;
#endif
        }
    }
}
