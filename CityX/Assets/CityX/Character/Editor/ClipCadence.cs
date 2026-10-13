using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// CADENCE MATCHING: every locomotion clip is played at the speed that makes its planted feet move back through the body as fast as the body
    /// really goes (owner, 2026-10-13: "without sliding feet"). The walk and run clips are Mixamo-paced (the Godot game played them the same way):
    /// a planted foot moves back at about 1.5 m/s in the walk and 4 m/s in the run, under a body that walks at 4 m/s and sprints at 7.5. Every
    /// step therefore slid two to three metres a second on flat ground, and no foot lock can hold a foot the clip is dragging that fast (it reads as a
    /// swing within two frames). Research's answer is stride or cadence warping; the stride cannot stretch on legs this short, so the cadence does:
    /// each clip's ground speed is MEASURED on the rig when the animator is built (<see cref="GroundSpeed"/>), and the blend tree child that plays it
    /// at a body speed gets <c>timeScale = body speed / clip speed</c> (<see cref="MatchController"/>). After that the feet keep up, the locks hold
    /// through a whole stance, and the stride is the ground the body covers.
    /// </summary>
    public static class ClipCadence
    {
        /// <summary>A foot within this of the clip's lowest foot height is on the ground.</summary>
        public const float PlantedWithinMetres = 0.03f;

        /// <summary>A clip is never slowed below half or sped past four times its own pace.</summary>
        public const float MinTimeScale = 0.5f;

        public const float MaxTimeScale = 4.0f;

        /// <summary>A blend child slower than this (the idle at the centre, a held pose) is left alone.</summary>
        public const float MinBodySpeed = 0.2f;

        /// <summary>A clip whose feet hardly move (an idle, a turn on the spot) is left alone.</summary>
        public const float MinClipSpeed = 0.2f;

        public const int Samples = 90;

        /// <summary>
        /// How fast the ground moves back under the planted feet of an in-place clip on <paramref name="rig"/> (an instance with a humanoid Animator),
        /// metres per second: the median over every pair of consecutive samples in which the foot is on the ground. 0 when the feet never plant.
        /// </summary>
        public static float GroundSpeed(AnimationClip clip, GameObject rig)
        {
            Animator animator = rig.GetComponentInChildren<Animator>();
            if (clip == null || animator == null || !animator.isHuman || clip.length <= 0.0f)
            {
                return 0.0f;
            }
            Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
            if (feet[0] == null || feet[1] == null)
            {
                return 0.0f;
            }
            Transform root = animator.transform;
            var positions = new Vector3[2][];
            positions[0] = new Vector3[Samples];
            positions[1] = new Vector3[Samples];
            float floor = float.MaxValue;
            for (int i = 0; i < Samples; i++)
            {
                clip.SampleAnimation(animator.gameObject, clip.length * i / Samples);
                for (int f = 0; f < 2; f++)
                {
                    positions[f][i] = root.InverseTransformPoint(feet[f].position);
                    floor = Mathf.Min(floor, positions[f][i].y);
                }
            }
            float dt = clip.length / Samples;
            var speeds = new List<float>();
            for (int f = 0; f < 2; f++)
            {
                for (int i = 0; i < Samples; i++)
                {
                    int next = (i + 1) % Samples;   // the clips loop
                    if (positions[f][i].y - floor > PlantedWithinMetres || positions[f][next].y - floor > PlantedWithinMetres)
                    {
                        continue;
                    }
                    Vector3 moved = positions[f][next] - positions[f][i];
                    moved.y = 0.0f;
                    speeds.Add(moved.magnitude / dt);
                }
            }
            if (speeds.Count < 6)
            {
                return 0.0f;
            }
            speeds.Sort();
            return speeds[speeds.Count / 2];
        }

        /// <summary>
        /// Sets the time scale of every clip in every blend tree of <paramref name="controller"/> so its ground speed matches the body speed the
        /// tree plays it at (a 2-D child's position, a 1-D child's threshold, or the parent's speed for a nested mix). Returns how many were scaled.
        /// Measures on a throwaway instance of <paramref name="prefab"/>.
        /// </summary>
        public static int MatchController(AnimatorController controller, GameObject prefab)
        {
            GameObject rig = Object.Instantiate(prefab);
            rig.name = "ClipCadenceRig";
            try
            {
                var cache = new Dictionary<AnimationClip, float>();
                int count = 0;
                foreach (AnimatorControllerLayer layer in controller.layers)
                {
                    foreach (ChildAnimatorState child in layer.stateMachine.states)
                    {
                        if (child.state.motion is BlendTree tree)
                        {
                            count += MatchTree(tree, cache, rig, 0.0f);
                        }
                    }
                }
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                return count;
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        /// <summary>The time scale a clip of <paramref name="clipSpeed"/> needs to keep up with <paramref name="bodySpeed"/>.</summary>
        public static float TimeScaleFor(float bodySpeed, float clipSpeed)
        {
            return Mathf.Clamp(bodySpeed / clipSpeed, MinTimeScale, MaxTimeScale);
        }

        private static int MatchTree(BlendTree tree, Dictionary<AnimationClip, float> cache, GameObject rig, float inheritedBodySpeed)
        {
            ChildMotion[] children = tree.children;
            int count = 0;
            for (int i = 0; i < children.Length; i++)
            {
                float bodySpeed = inheritedBodySpeed > 0.0f ? inheritedBodySpeed
                    : tree.blendType == BlendTreeType.Simple1D ? Mathf.Abs(children[i].threshold) : children[i].position.magnitude;
                if (children[i].motion is BlendTree sub)
                {
                    count += MatchTree(sub, cache, rig, bodySpeed);
                    continue;
                }
                if (!(children[i].motion is AnimationClip clip) || bodySpeed < MinBodySpeed)
                {
                    continue;
                }
                if (!cache.TryGetValue(clip, out float clipSpeed))
                {
                    clipSpeed = GroundSpeed(clip, rig);
                    cache[clip] = clipSpeed;
                    Debug.Log($"CityX cadence: {clip.name} moves the ground at {clipSpeed:0.00} m/s");
                }
                if (clipSpeed < MinClipSpeed)
                {
                    continue;
                }
                children[i].timeScale = TimeScaleFor(bodySpeed, clipSpeed);
                count++;
            }
            tree.children = children;
            return count;
        }
    }
}
