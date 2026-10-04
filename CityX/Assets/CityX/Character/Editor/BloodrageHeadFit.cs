using UnityEngine.Playables;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Brings the head of the Bloodrage run back to where the ordinary sprint carries it.
    ///
    /// Reported: "the bloodrage run animation has the head looking upward ... the head positioning shouldn't be too different from the standard sprint."
    /// The Mutant Run is a hunched run whose neck is cranked far up (its Neck Nod averages 1.1 against the sprint's 0.07); on this body that
    /// reads as the head staring at the sky. So the clip's neck nod is SHIFTED (not flattened: it keeps its own bob) by whatever makes the head's
    /// average pitch, as the body actually poses it, equal to the sprint's. Found by posing the real player body: sample both clips across their
    /// length, measure how far the head's facing axis points above or below the body's, and search for the shift that closes the gap.
    /// </summary>
    public static class BloodrageHeadFit
    {
        private const string NeckNod = "Neck Nod Down-Up";

        public static void Apply(GameObject playerPrefab, AnimationClip run, AnimationClip reference)
        {
            if (playerPrefab == null || run == null || reference == null)
            {
                return;
            }
            GameObject body = (GameObject)Object.Instantiate(playerPrefab);
            try
            {
                var animator = body.GetComponentInChildren<Animator>();
                if (animator == null || !animator.isHuman)
                {
                    Debug.LogWarning("CityX: Bloodrage head fit skipped -- the player body has no humanoid animator.");
                    return;
                }
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Transform root = animator.transform;
                if (head == null)
                {
                    return;
                }
                Vector3 axis = FacingAxis(head, root);

                float target = MeanPitch(animator.gameObject, head, root, axis, reference);

                EditorCurveBinding binding = default;
                bool found = false;
                foreach (EditorCurveBinding candidate in AnimationUtility.GetCurveBindings(run))
                {
                    if (candidate.propertyName == NeckNod)
                    {
                        binding = candidate;
                        found = true;
                    }
                }
                if (!found)
                {
                    Debug.LogWarning("CityX: Bloodrage run has no neck nod curve to fit.");
                    return;
                }
                AnimationCurve original = AnimationUtility.GetEditorCurve(run, binding);

                float bestShift = 0.0f, bestError = float.MaxValue;
                for (float shift = -1.5f; shift <= 0.3f; shift += 0.05f)
                {
                    SetShifted(run, binding, original, shift);
                    float measured = MeanPitch(animator.gameObject, head, root, axis, run);
                    float error = Mathf.Abs(measured - target);
                    if (error < bestError)
                    {
                        bestError = error;
                        bestShift = shift;
                    }
                }
                SetShifted(run, binding, original, bestShift);
                EditorUtility.SetDirty(run);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: Bloodrage run neck nod fitted (shift {bestShift:0.00}; the clip's own values past the muscle limit of 1 are clamped): head pitch {target - bestError:0.0}..{target + bestError:0.0} degrees against the sprint's {target:0.0}");
            }
            finally
            {
                Object.DestroyImmediate(body);
            }
        }

        private static void SetShifted(AnimationClip clip, EditorCurveBinding binding, AnimationCurve original, float shift)
        {
            Keyframe[] keys = original.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].value = Mathf.Clamp(keys[i].value + shift, -1.0f, 1.0f);
            }
            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
        }

        /// <summary>Poses a humanoid body in a clip at a time through a playable graph (AnimationClip.SampleAnimation leaves humanoid bodies in their rest pose).</summary>
        public static void Pose(Animator animator, AnimationClip clip, float time)
        {
            var graph = UnityEngine.Playables.PlayableGraph.Create("BloodrageHeadFit");
            try
            {
                graph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.Manual);
                var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "pose", animator);
                var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetTime(time);
                output.SetSourcePlayable(playable);
                graph.Evaluate(0.0f);
            }
            finally
            {
                graph.Destroy();
            }
        }

        /// <summary>The head bone's own axis that points most nearly the way the body faces, in the pose the body is in now.</summary>
        private static Vector3 FacingAxis(Transform head, Transform root)
        {
            Vector3[] axes = { Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left };
            Vector3 best = Vector3.forward;
            float bestDot = -2.0f;
            foreach (Vector3 axis in axes)
            {
                float dot = Vector3.Dot(root.InverseTransformDirection(head.rotation * axis), Vector3.forward);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = axis;
                }
            }
            return best;
        }

        /// <summary>
        /// The average, over the clip's length, of how far the head is tipped forward RELATIVE TO THE TORSO, in degrees (positive: the top of the head
        /// is ahead of where the spine points; negative: the head is cocked back on it). Measured against the torso rather than the world because the
        /// Mutant Run is a far more hunched run than the sprint (the torso leans 46 degrees against 28): matching the head's world pitch left it level
        /// with the horizon but cranked back on a body bent double, which is what looked wrong.
        /// </summary>
        private static float MeanPitch(GameObject body, Transform head, Transform root, Vector3 axis, AnimationClip clip)
        {
            const int samples = 16;
            var animator = body.GetComponentInChildren<Animator>();
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            float sum = 0.0f;
            for (int i = 0; i < samples; i++)
            {
                Pose(animator, clip, clip.length * i / samples);
                Vector3 spine = root.InverseTransformDirection(neck.position - hips.position);
                float torsoLean = 90.0f - Mathf.Atan2(spine.y, spine.z) * Mathf.Rad2Deg;      // 0 upright, bigger = bent further forward
                Vector3 top = root.InverseTransformDirection(head.rotation * Vector3.up);
                float headTipped = Mathf.Atan2(top.z, top.y) * Mathf.Rad2Deg;                  // 0 upright head, bigger = top tipped forward
                sum += headTipped - torsoLean;
            }
            return sum / samples;
        }
    }
}
