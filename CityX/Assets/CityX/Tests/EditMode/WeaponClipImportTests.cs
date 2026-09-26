using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CityX.Tests
{
    /// <summary>
    /// How the pistol and two-handed-sword Mixamo clips are IMPORTED: whether the ones that loop close
    /// their loop. Evaluated on the real player rig through a PlayableGraph (the only way to run a
    /// Humanoid clip outside Play Mode -- AnimationClip.SampleAnimation silently does nothing to one),
    /// not read out of the .meta, because a setting that reads right can still leave a seam.
    /// </summary>
    public sealed class WeaponClipImportTests
    {
        private const string Folder = "Assets/CityX/Character/Animations/";

        /// <summary>A loop closes when its last frame is the pose its first frame starts from: at most this far apart, in metres, on any tracked bone.</summary>
        private const float MaxSeamMetres = 0.05f;

        private static readonly string[] LoopingClips =
        {
            "2hsw_idle", "2hsw_walk", "2hsw_sprint", "2hsw_backwardwalk", "2hsw_left", "2hsw_right",
            "pistol_walk", "pistol_run", "pistol_walk_backward", "pistol_walk_left", "pistol_walk_right",
        };

        private static readonly HumanBodyBones[] Tracked =
        {
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
            HumanBodyBones.Head,
        };

        private static AnimationClip LoadClip(string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(Folder + name + ".fbx"))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    return clip;
                }
            }
            return null;
        }

        /// <summary>Each tracked bone's position relative to the HIPS (a walk travels; the seam that shows is the limbs against the body).</summary>
        private static Dictionary<HumanBodyBones, Vector3> PoseAt(PlayableGraph graph, AnimationClipPlayable playable, Animator animator, float time)
        {
            playable.SetTime(time);
            graph.Evaluate(0.0f);
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var pose = new Dictionary<HumanBodyBones, Vector3>();
            foreach (HumanBodyBones bone in Tracked)
            {
                pose[bone] = hips.InverseTransformPoint(animator.GetBoneTransform(bone).position);
            }
            return pose;
        }

        private static float Gap(Dictionary<HumanBodyBones, Vector3> a, Dictionary<HumanBodyBones, Vector3> b)
        {
            float worst = 0.0f;
            foreach (HumanBodyBones bone in Tracked)
            {
                worst = Mathf.Max(worst, Vector3.Distance(a[bone], b[bone]));
            }
            return worst;
        }

        /// <summary>The distance between the first frame and a hair before the end, and the distance the pose travels half way through (to prove it is moving at all).</summary>
        private static void Measure(Animator animator, AnimationClip clip, out float seam, out float travelled)
        {
            PlayableGraph graph = PlayableGraph.Create("seam-" + clip.name);
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "out", animator);
                AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
                output.SetSourcePlayable(playable);
                graph.Play();

                Dictionary<HumanBodyBones, Vector3> first = PoseAt(graph, playable, animator, 0.0f);
                travelled = Gap(first, PoseAt(graph, playable, animator, clip.length * 0.5f));
                // A hair before the end is the LAST frame's pose (at exactly the length a looping clip
                // wraps to its first frame, which would make every seam read zero).
                seam = Gap(first, PoseAt(graph, playable, animator, Mathf.Max(0.0f, clip.length - 0.001f)));
            }
            finally
            {
                graph.Destroy();
            }
        }

        [Test]
        public void EveryLoopingWeaponClipClosesItsLoop()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            GameObject player = Object.Instantiate(prefab);
            try
            {
                Animator animator = player.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = null;   // the graph drives it, not the state machine
                var offenders = new List<string>();
                var report = new List<string>();
                foreach (string name in LoopingClips)
                {
                    AnimationClip clip = LoadClip(name);
                    Assert.IsNotNull(clip, $"{name} has no clip");
                    Measure(animator, clip, out float seam, out float travelled);
                    Assert.Greater(travelled, 0.02f, $"{name}: evaluating it never moved the pose, so a zero seam would mean nothing");
                    report.Add($"{name}: seam {seam * 100:0.0}cm (moves {travelled * 100:0}cm, loop {clip.isLooping}, {clip.length:0.00}s)");
                    if (seam > MaxSeamMetres)
                    {
                        offenders.Add($"{name} {seam * 100:0.0}cm");
                    }
                }
                Debug.Log("CityX loop seams: " + string.Join("; ", report));
                Assert.IsEmpty(offenders, "these loops jump at the seam: " + string.Join(", ", offenders));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }
    }
}
