using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The living body against a wall, its own shape (owner, 2026-11-10, two screenshots of an arm and a shoulder in the bricks: "make it so
    /// body parts cant clip through walls and other objects, make the hitbox the exact size of the body and stop at contact with the wall").
    /// The moving capsule is 0.5 m round; the hanging arms reach some 0.62 m.
    /// </summary>
    public sealed class BodyWallClearancePlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>How far an arm may still be in the wall: the contact itself.</summary>
        private const float InTheWall = 0.01f;

        private GameObject _ground;
        private GameObject _left;
        private GameObject _right;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private Animator _animator;

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject made in new[] { _player, _ground, _left, _right })
            {
                if (made != null) Object.Destroy(made);
            }
        }

        /// <summary>A wall whose face toward the body is <paramref name="face"/> metres to its right (negative: to its left).</summary>
        private static GameObject Wall(float face)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(1.0f, 6.0f, 40.0f);
            wall.transform.position = new Vector3(face + Mathf.Sign(face) * 0.5f, 3.0f, 0.0f);
            return wall;
        }

        private IEnumerator Spawn()
        {
#if UNITY_EDITOR
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            yield return Steps(30, default);
#else
            yield break;
#endif
        }

        private IEnumerator Steps(int count, LocomotionInput input)
        {
            for (int i = 0; i < count; i++)
            {
                _locomotion.Tick(input, Dt);
                yield return null;
            }
        }

        /// <summary>How far the deepest of the arms and head is into <paramref name="wall"/>.</summary>
        private float DeepestIn(GameObject wall)
        {
            Collider solid = wall.GetComponent<Collider>();
            float deepest = 0.0f;
            foreach (HumanBodyBones part in new[]
                     {
                         HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                         HumanBodyBones.Head,
                     })
            {
                Collider limb = _animator.GetBoneTransform(part).GetComponent<Collider>();
                if (Physics.ComputePenetration(limb, limb.transform.position, limb.transform.rotation, solid, solid.transform.position,
                        solid.transform.rotation, out _, out float distance))
                {
                    deepest = Mathf.Max(deepest, distance);
                }
            }
            return deepest;
        }

        [UnityTest]
        public IEnumerator StandingAgainstAWallTheArmStopsAtItsFace()
        {
            yield return Spawn();
            // Its face just outside the capsule: the capsule is clear of it, the hanging right arm is not.
            const float Face = 0.51f;
            _right = Wall(Face);
            Physics.SyncTransforms();
            Assert.Greater(ArmReach(), Face + 0.01f, "setup: the arm does not reach into the wall, so this shows nothing");

            yield return Steps(20, new LocomotionInput { YawDegrees = 0.0f });
            Assert.Less(DeepestIn(_right), InTheWall, "the arm is still in the wall");
            Assert.Less(_player.transform.position.x, -0.01f, "the body was not moved off the wall its arm was in");
        }

        [UnityTest]
        public IEnumerator WalkingAlongAWallTheArmsStayOutOfItWithoutShaking()
        {
            yield return Spawn();
            _right = Wall(0.52f);
            Physics.SyncTransforms();
            // Walking forward and a little toward the wall, as the screenshots were.
            var input = new LocomotionInput { Move = new Vector2(0.25f, 1.0f).normalized, YawDegrees = 0.0f };
            yield return Steps(30, input);
            float deepest = 0.0f, nearest = float.MaxValue, furthest = float.MinValue;
            for (int i = 0; i < 90; i++)
            {
                _locomotion.Tick(input, Dt);
                yield return null;
                deepest = Mathf.Max(deepest, DeepestIn(_right));
                nearest = Mathf.Min(nearest, _player.transform.position.x);
                furthest = Mathf.Max(furthest, _player.transform.position.x);
            }
            Assert.Less(deepest, 0.03f, "walking along the wall, an arm went into it");
            Assert.Less(furthest - nearest, 0.04f, "the body shook toward and away from the wall as it walked");
        }

        [UnityTest]
        public IEnumerator SqueezedBetweenTwoWallsTheBodyIsNotPushedEitherWay()
        {
            yield return Spawn();
            // Narrower than the arms, wider than the capsule.
            _left = Wall(-0.54f);
            _right = Wall(0.54f);
            Physics.SyncTransforms();
            yield return Steps(30, new LocomotionInput { YawDegrees = 0.0f });
            Assert.AreEqual(0.0f, _player.transform.position.x, 0.01f, "a gap narrower than the arms pushed the body to one side");
        }

        /// <summary>How far from the body's middle line the right forearm reaches, standing.</summary>
        private float ArmReach()
        {
            var forearm = (CapsuleCollider)_animator.GetBoneTransform(HumanBodyBones.RightLowerArm).GetComponent<Collider>();
            return forearm.bounds.max.x - _player.transform.position.x;
        }
    }
}
