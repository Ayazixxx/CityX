using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// How far past a wall on z = 0 (the street on the -z side) any body or sword reaches, read after every body's own
    /// posing in the frame (the last LateUpdate): a coroutine wakes before LateUpdate, and there it sees the Animator's pose, not the drawn one.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class WallReachProbe : MonoBehaviour
    {
        public readonly List<GameObject> Bodies = new List<GameObject>();

        public float DeepestBody = float.MinValue;
        public float DeepestBlade = float.MinValue;
        public string BodyNote = "";
        public string BladeNote = "";

        /// <summary>The bodies whose root has gone past the wall's middle (z = 0).</summary>
        public readonly HashSet<int> Through = new HashSet<int>();

        private void LateUpdate()
        {
            for (int i = 0; i < Bodies.Count; i++)
            {
                GameObject body = Bodies[i];
                if (body == null)
                {
                    continue;
                }
                if (body.transform.position.z > 0.0f)
                {
                    Through.Add(i);
                    continue;
                }
                var locomotion = body.GetComponent<PlayerLocomotion>();
                ISwordBlade blade = locomotion != null ? locomotion.HeldSword : null;
                if (blade != null)
                {
                    float z = Mathf.Max(blade.BladeTipWorld.z, blade.BladeBaseWorld.z);
                    if (z > DeepestBlade)
                    {
                        DeepestBlade = z;
                        bool hit = Physics.Linecast(blade.BladeBaseWorld, blade.BladeTipWorld, out RaycastHit what, ~0, QueryTriggerInteraction.Ignore);
                        BladeNote = $"blade of {i} at {Time.time:F1} s, root {body.transform.position}, base {blade.BladeBaseWorld} tip {blade.BladeTipWorld}, "
                            + $"phase {locomotion.SwordCombat?.Phase}, line hits {(hit ? what.collider.name : "nothing")}";
                    }
                }
                Animator animator = body.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    continue;
                }
                for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
                {
                    Transform t = animator.GetBoneTransform(bone);
                    if (t != null && t.position.z > DeepestBody)
                    {
                        DeepestBody = t.position.z;
                        BodyNote = $"{bone} of {i} at {Time.time:F1} s, root z {body.transform.position.z:F2}";
                    }
                }
            }
        }
    }
}
