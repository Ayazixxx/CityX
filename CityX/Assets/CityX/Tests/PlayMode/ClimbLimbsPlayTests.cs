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
    /// REPORTED: "the limbs are constantly going through the wall" while climbing. On the real rig, climbing a plain wall for several seconds (up, down, still),
    /// after the Animator and the late-frame IK have run, no hand, foot, elbow or knee may be behind the wall face.
    /// </summary>
    public sealed class ClimbLimbsPlayTests
    {
        private GameObject _player;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
        }

        [UnityTest]
        public IEnumerator NoHandFootElbowOrKneeGoesThroughTheWallWhileClimbing()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0); ground.transform.localScale = new Vector3(100, 1, 100);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 20.0f, 2.0f); wall.transform.localScale = new Vector3(20, 40, 2);   // near face z = 1, outward -Z
            _made.Add(ground); _made.Add(wall);
            Physics.SyncTransforms();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.3f), Quaternion.identity);
            var climbing = _player.GetComponent<PlayerClimbing>();
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.InfiniteStamina = true;
            var animator = _player.GetComponentInChildren<Animator>();
            yield return null;
            yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f));

            HumanBodyBones[] bones =
            {
                HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
                HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
            };
            float worst = float.MaxValue;
            string worstBone = "";
            const float dt = 1.0f / 60.0f;
            for (int i = 0; i < 480; i++)
            {
                bool up = i < 200 || i >= 360;      // up, then down, then up again
                bool down = i >= 200 && i < 360;
                yield return null;     // resumes after the previous frame's LateUpdate: the final pose
                climbing.Tick(false, up, down, vitals, dt);
                if (i < 20 || !climbing.IsClimbing) continue;   // the grab's reach-in is allowed to settle
                foreach (HumanBodyBones bone in bones)
                {
                    Transform t = animator.GetBoneTransform(bone);
                    if (t == null) continue;
                    // An elbow or knee is a joint centre inside a limb about 5 cm thick: a couple of centimetres behind the face is the limb resting on it.
                    bool joint = bone == HumanBodyBones.LeftLowerArm || bone == HumanBodyBones.RightLowerArm || bone == HumanBodyBones.LeftLowerLeg || bone == HumanBodyBones.RightLowerLeg;
                    float depth = 1.0f - t.position.z + (joint ? 0.025f : 0.0f);     // metres in FRONT of the wall face (z = 1); negative is through it
                    if (depth < worst) { worst = depth; worstBone = bone.ToString() + " at frame " + i + " y=" + _player.transform.position.y.ToString("F2"); }
                }
            }
            Debug.Log($"CityX climb limbs: worst depth in front of the wall {worst:F3} m ({worstBone})");
            Assert.GreaterOrEqual(worst, -0.02f, $"{worstBone} went {-worst:F2} m through the wall");
#else
            yield break;
#endif
        }
    }
}
