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
    /// A walking body's planted foot slides BACKWARDS under it: walking forward the foot on the ground moves toward the back of the body, strafing left it moves toward the
    /// body's right, and so on. That is true of any real walk, so it tells which way a clip is going without reading its name. It is what caught the strafe clips baked
    /// mirrored (Godot is right-handed and Unity left-handed, and Godot's "_l" bones are the character's physical right): pressing A played the animation for D.
    /// </summary>
    public sealed class LocomotionClipDirectionPlayTests
    {
        [UnityTest]
        public IEnumerator EachLocomotionClipSlidesTheFeetOppositeToTheWayItMoves()
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            var player = Object.Instantiate(prefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
            player.GetComponent<PlayerLocomotion>().enabled = false;
            var animator = player.GetComponentInChildren<Animator>();
            animator.enabled = false;
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            // (clip, direction the body moves in its own frame): +x is the body's right, +z its front.
            (string clip, Vector3 moves)[] cases =
            {
                ("walk", Vector3.forward), ("run", Vector3.forward), ("walk_backward", Vector3.back),
                ("walk_left", Vector3.left), ("walk_right", Vector3.right),
            };
            foreach ((string name, Vector3 moves) in cases)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/CityX/Character/Animations/Godot/godot_{name}.anim");
                Assert.IsNotNull(clip, $"godot_{name}.anim has not been baked");
                float slide = 0.0f;
                Transform plantedLastFrame = null;
                Vector3 plantedLast = Vector3.zero;
                const int steps = 60;
                for (int i = 0; i <= steps; i++)
                {
                    clip.SampleAnimation(animator.gameObject, clip.length * i / steps);
                    yield return null;
                    Transform planted = leftFoot.position.y < rightFoot.position.y ? leftFoot : rightFoot;
                    Vector3 at = player.transform.InverseTransformPoint(planted.position);
                    if (plantedLastFrame == planted)
                    {
                        // Along the direction of travel: a planted foot moves the opposite way, so this sums negative for a clip that really goes that way.
                        slide += Vector3.Dot(at - plantedLast, moves);
                    }
                    plantedLastFrame = planted;
                    plantedLast = at;
                }
                Assert.Less(slide, -0.05f,
                    $"{name}: the planted foot slid {slide:F2} m along {moves} -- the clip moves the body the other way (mirrored, or the wrong clip)");
            }
            Object.Destroy(player);
#else
            yield break;
#endif
        }
    }
}
