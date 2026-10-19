using System.Collections.Generic;
using CityX.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// THE GAITS: which clip plays at which speed, and how fast (owner, 2026-10-13: feet "gliding", then "the walk and running animations feel too rapid
    /// and like short steps compared to the speed", then "do what you think is best, research other games with dynamic movement like Last of Us").
    ///
    /// Measured on the rig (<see cref="GroundSpeed"/>, CityX/Measure Clip Ground Speeds): the walk clip moves its feet over the ground at 1.4 m/s and
    /// the run at 4.5, under a body that walks at 4 m/s and sprints at 7.5. Both games have played the walk under the 4 m/s body since the start, so the
    /// feet always slid 2.6 m/s. Speeding the walk up 2.8x to keep up (tried) is a mince. What the research says (Epic's Lyra animation guide, Naughty
    /// Dog's motion matching, a mocap studio's locomotion guide): choose the clip whose own pace is NEAREST the speed (walk, jog, run tiers), change its
    /// play rate by at most 15-20% and its stride by at most as much again (beyond that the motion visibly degrades), and let the feet absorb the rest.
    /// So, built here when the animator is built (<see cref="MatchController"/>):
    ///  * at every straight-ahead sample the clip that needs the least re-pacing plays: 4 m/s is a JOG on this body (the run clip slowed 11%), the
    ///    sprint is the run sped up 20%; the walk keeps a sample at its own 1.4 m/s for a slow push on a stick;
    ///  * the play rate takes up to 20% of what is left (50% for a strafe or backward walk, which have no faster clip to hand over to), the STRIDE takes up
    ///    to 20% more (<see cref="GaitStrides"/>: the foot placement puts the feet that much further along the body's travel, the hips dipping to reach),
    ///    and the foot locks hold the feet through the stance for the remainder when it is small (FootPlanting.LocksViable), so nothing slides;
    ///  * the walks are not the plain Godot clips but PacedGait's: the same with longer steps and a quicker cycle, so at 4 m/s their feet keep up
    ///    within these limits and the locks hold them (a strafe was 1.4 m/s of clip under 2.4x of re-pacing and slid).
    /// </summary>
    public static class ClipCadence
    {
        /// <summary>A foot within this of the clip's lowest foot height is on the ground.</summary>
        public const float PlantedWithinMetres = 0.03f;

        public const int Samples = 90;

        /// <summary>A straight-ahead clip's play rate changes by at most this factor either way (the industry's 15-20%).</summary>
        public const float MaxRateChange = 1.2f;

        /// <summary>A strafe's or backward walk's play rate may go further: there is no faster clip to hand the speed to.</summary>
        public const float MaxSideRateChange = 1.5f;

        /// <summary>The feet are placed at most this much further (or nearer) along the stride than the clip has them (more and the hips dip at every heel strike).</summary>
        public const float MaxStrideChange = 1.12f;

        /// <summary>A blend child slower than this (the idle at the centre, a held pose) is left alone.</summary>
        public const float MinBodySpeed = 0.2f;

        /// <summary>A clip whose feet hardly move (an idle, a turn on the spot) is left alone.</summary>
        public const float MinClipSpeed = 0.2f;

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

        /// <summary>Logs the ground speed of every animation clip in the character's folders, measured on the player rig: for choosing gaits.</summary>
        [MenuItem("CityX/Measure Clip Ground Speeds")]
        public static void MeasureAllClips()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            if (prefab == null)
            {
                Debug.LogError("CityX: no Player.prefab to measure clips on.");
                return;
            }
            GameObject rig = Object.Instantiate(prefab);
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/CityX/Character" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        {
                            Debug.Log($"CityX clip speed: {System.IO.Path.GetFileName(path)} [{clip.name}] {clip.length:0.00} s, ground {GroundSpeed(clip, rig):0.00} m/s");
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        /// <summary>
        /// How a required speed-up <paramref name="required"/> (body speed over clip speed) is shared out: the play rate takes up to <paramref name="maxRate"/>
        /// either way, the stride up to <see cref="MaxStrideChange"/>; whatever is left is for the foot locks (or, when large, a slide).
        /// </summary>
        public static void Split(float required, float maxRate, out float rate, out float stride)
        {
            rate = Mathf.Clamp(required, 1.0f / maxRate, maxRate);
            stride = Mathf.Clamp(required / rate, 1.0f / MaxStrideChange, MaxStrideChange);
        }

        /// <summary>
        /// Chooses and paces the clips in every blend tree of <paramref name="controller"/> (measuring on a throwaway instance of <paramref name="prefab"/>),
        /// and writes the strides to <see cref="GaitStrides.AssetPath"/>. Returns how many clips were paced.
        /// </summary>
        public static int MatchController(AnimatorController controller, GameObject prefab)
        {
            GameObject rig = Object.Instantiate(prefab);
            rig.name = "ClipCadenceRig";
            try
            {
                var cache = new Dictionary<AnimationClip, float>();
                var gaits = ScriptableObject.CreateInstance<GaitStrides>();
                int count = 0;
                foreach (AnimatorControllerLayer layer in controller.layers)
                {
                    foreach (ChildAnimatorState child in layer.stateMachine.states)
                    {
                        if (child.state.motion is BlendTree tree)
                        {
                            count += MatchTree(tree, child.state.name, cache, rig, gaits);
                        }
                    }
                }
                var existing = AssetDatabase.LoadAssetAtPath<GaitStrides>(GaitStrides.AssetPath);
                if (existing != null)
                {
                    existing.Entries = gaits.Entries;
                    EditorUtility.SetDirty(existing);
                    Object.DestroyImmediate(gaits);
                }
                else
                {
                    AssetDatabase.CreateAsset(gaits, GaitStrides.AssetPath);
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

        private static float Speed(AnimationClip clip, Dictionary<AnimationClip, float> cache, GameObject rig)
        {
            if (!cache.TryGetValue(clip, out float speed))
            {
                speed = GroundSpeed(clip, rig);
                cache[clip] = speed;
                Debug.Log($"CityX cadence: {clip.name} moves the ground at {speed:0.00} m/s");
            }
            return speed;
        }

        /// <summary>The clip whose own pace is nearest <paramref name="bodySpeed"/> (by ratio).</summary>
        private static AnimationClip Nearest(List<AnimationClip> clips, float bodySpeed, Dictionary<AnimationClip, float> cache, GameObject rig, AnimationClip fallback)
        {
            AnimationClip best = fallback;
            float bestScore = float.MaxValue;
            foreach (AnimationClip clip in clips)
            {
                float speed = Speed(clip, cache, rig);
                if (speed < MinClipSpeed) continue;
                float score = Mathf.Abs(Mathf.Log(bodySpeed / speed));
                if (score < bestScore)
                {
                    bestScore = score;
                    best = clip;
                }
            }
            return best;
        }

        private static bool StraightAhead(in ChildMotion child) => Mathf.Abs(child.position.x) < 0.01f && child.position.y > MinBodySpeed;

        /// <summary>
        /// Only the plain locomotion tree swaps gaits: its run clip is a jog. A sword or gun tree's "sprint" is a sprint with the weapon held for
        /// sprinting, and playing it for an ordinary move changed how the weapon is carried; those keep their own clips, re-paced within the limits.
        ///
        /// SWITCHED OFF (owner, 2026-10-13, after trying the jog: "the walking animation is the same as the running animation now just slower. he
        /// shouldn't be pumping his arms like that when he is just walking"). Every tree keeps its own clips, re-paced within the limits; the walk at
        /// 4 m/s is PacedGait's longer-stepping walk, whose feet keep up closely enough to be held (FootPlanting.LocksViable).
        /// Set this to "Locomotion" to bring the jog back.
        /// </summary>
        public const string GaitSwapTree = "";

        private static int MatchTree(BlendTree tree, string treeName, Dictionary<AnimationClip, float> cache, GameObject rig, GaitStrides gaits)
        {
            bool twoD = tree.blendType != BlendTreeType.Simple1D;
            ChildMotion[] children = tree.children;

            // The straight-ahead clips on offer (the walk and the run): any speed ahead may be played by whichever of them suits it best.
            var forward = new List<AnimationClip>();
            if (twoD && treeName == GaitSwapTree)
            {
                foreach (ChildMotion child in children)
                {
                    if (child.motion is AnimationClip clip && StraightAhead(child) && !forward.Contains(clip))
                    {
                        forward.Add(clip);
                    }
                }
            }

            int count = 0;
            var used = new HashSet<AnimationClip>();
            for (int i = 0; i < children.Length; i++)
            {
                float bodySpeed = twoD ? children[i].position.magnitude : Mathf.Abs(children[i].threshold);
                if (children[i].motion is BlendTree mix)
                {
                    count += MatchMix(mix, treeName, children[i].position, cache, rig, gaits, forward, used);
                    continue;
                }
                if (!(children[i].motion is AnimationClip original) || bodySpeed < MinBodySpeed)
                {
                    continue;
                }
                bool straight = twoD && StraightAhead(children[i]);
                AnimationClip chosen = straight && forward.Count > 1 ? Nearest(forward, bodySpeed, cache, rig, original) : original;
                float clipSpeed = Speed(chosen, cache, rig);
                if (clipSpeed < MinClipSpeed)
                {
                    continue;
                }
                Split(bodySpeed / clipSpeed, straight ? MaxRateChange : MaxSideRateChange, out float rate, out float stride);
                children[i].motion = chosen;
                children[i].timeScale = rate;
                used.Add(chosen);
                gaits.Add(treeName, twoD ? children[i].position : new Vector2(0.0f, children[i].threshold), stride);
                count++;
            }
            tree.children = children;

            // A forward clip nothing plays any more (the walk, once the jog takes its speed) keeps a sample at its own pace: a slow push on a stick.
            foreach (AnimationClip clip in forward)
            {
                float speed = Speed(clip, cache, rig);
                if (!used.Contains(clip) && speed >= MinClipSpeed)
                {
                    var at = new Vector2(0.0f, speed);
                    tree.AddChild(clip, at);
                    gaits.Add(treeName, at, 1.0f);
                }
            }
            return count;
        }

        /// <summary>A diagonal: a 1-D mix of a straight clip (threshold 0) and a side clip, played at the parent sample's speed.</summary>
        private static int MatchMix(BlendTree mix, string treeName, Vector2 parentPosition, Dictionary<AnimationClip, float> cache, GameObject rig,
            GaitStrides gaits, List<AnimationClip> forward, HashSet<AnimationClip> used)
        {
            float bodySpeed = parentPosition.magnitude;
            ChildMotion[] children = mix.children;
            int count = 0;
            float strideSum = 0.0f;
            int strides = 0;
            for (int i = 0; i < children.Length; i++)
            {
                if (!(children[i].motion is AnimationClip original))
                {
                    continue;
                }
                bool straight = Mathf.Abs(children[i].threshold) < 0.01f;
                AnimationClip chosen = straight && forward.Count > 1 ? Nearest(forward, bodySpeed, cache, rig, original) : original;
                float clipSpeed = Speed(chosen, cache, rig);
                if (clipSpeed < MinClipSpeed)
                {
                    continue;
                }
                Split(bodySpeed / clipSpeed, straight ? MaxRateChange : MaxSideRateChange, out float rate, out float stride);
                children[i].motion = chosen;
                children[i].timeScale = rate;
                used.Add(chosen);
                strideSum += stride;
                strides++;
                count++;
            }
            mix.children = children;
            if (strides > 0)
            {
                gaits.Add(treeName, parentPosition, strideSum / strides);
            }
            return count;
        }
    }
}
