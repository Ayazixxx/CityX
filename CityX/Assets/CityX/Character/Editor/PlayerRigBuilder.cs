using System.Collections.Generic;
using CityX.Character;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the animator controller and the player prefab from the imported
    /// Mixamo clips.
    ///
    /// Authored in code for the same reason the massing scene is: an
    /// AnimatorController is an opaque binary asset that cannot be reviewed in
    /// a diff and cannot be repaired without clicking through a graph editor.
    /// Twenty lines of readable setup is a better artefact, and re-running it
    /// fixes the thing rather than requiring somebody to remember what it
    /// looked like.
    /// </summary>
    public static class PlayerRigBuilder
    {
        private const string AnimationDirectory = "Assets/CityX/Character/Animations";
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";
        private const string PrefabPath = "Assets/CityX/Character/Player.prefab";

        /// <summary>
        /// Blend thresholds, in metres per second, matching PlayerLocomotion's
        /// own speeds. If those change these must too or the character will
        /// moonwalk -- feet sliding because the clip's stride does not match the
        /// distance actually travelled.
        /// </summary>
        private const float WalkSpeed = 4.0f;

        private const float SprintSpeed = 7.5f;

        [MenuItem("CityX/Rebuild Player Rig")]
        public static GameObject Build()
        {
            AnimationClip idle = FindClip("idle");
            AnimationClip walk = FindClip("walking");
            AnimationClip sprint = FindClip("sprint");
            AnimationClip jump = FindClip("jump");

            if (idle == null || walk == null || sprint == null)
            {
                Debug.LogError("CityX: missing locomotion clips -- run CityX/Import Character Clips first.");
                return null;
            }

            // Mixamo clips are single strides; without looping they play once
            // and freeze mid-step.
            MakeLooping(idle);
            MakeLooping(walk);
            MakeLooping(sprint);

            AnimatorController controller = BuildController(idle, walk, sprint, jump);
            GameObject prefab = BuildPrefab(controller);

            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: player rig written to {PrefabPath}");
            return prefab;
        }

        private static AnimatorController BuildController(
            AnimationClip idle, AnimationClip walk, AnimationClip sprint, AnimationClip jump)
        {
            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            // A 1-D blend tree on speed, with thresholds at the real movement
            // speeds. Blending by actual m/s rather than by a normalised 0..1
            // is what keeps the stride matched to the ground at every speed
            // between walking and sprinting, not just at the ends.
            controller.CreateBlendTreeInController("Locomotion", out BlendTree locomotion, 0);
            locomotion.blendParameter = "Speed";
            locomotion.blendType = BlendTreeType.Simple1D;
            locomotion.useAutomaticThresholds = false;
            locomotion.AddChild(idle, 0.0f);
            locomotion.AddChild(walk, WalkSpeed);
            locomotion.AddChild(sprint, SprintSpeed);

            AnimatorStateMachine root = controller.layers[0].stateMachine;

            if (jump != null)
            {
                AnimatorState jumpState = root.AddState("Jump");
                jumpState.motion = jump;

                AnimatorStateTransition intoJump = root.AddAnyStateTransition(jumpState);
                intoJump.AddCondition(AnimatorConditionMode.If, 0.0f, "Jump");
                intoJump.duration = 0.08f;
                intoJump.hasExitTime = false;
                // Without this a jump triggered while already jumping restarts
                // the clip from frame zero, which reads as a stutter.
                intoJump.canTransitionToSelf = false;

                AnimatorState locomotionState = FindState(root, "Locomotion");
                if (locomotionState != null)
                {
                    AnimatorStateTransition outOfJump = jumpState.AddTransition(locomotionState);
                    outOfJump.AddCondition(AnimatorConditionMode.If, 0.0f, "Grounded");
                    outOfJump.duration = 0.15f;
                    outOfJump.hasExitTime = false;
                }
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject BuildPrefab(AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{AnimationDirectory}/idle.fbx");
            if (model == null)
            {
                Debug.LogError("CityX: idle.fbx not found -- cannot build the player prefab.");
                return null;
            }

            var root = new GameObject("Player");
            try
            {
                GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(model);
                body.name = "Body";
                body.transform.SetParent(root.transform, false);

                Animator animator = body.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = body.AddComponent<Animator>();
                }
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;   // PlayerLocomotion drives position.

                CharacterController capsule = root.AddComponent<CharacterController>();
                capsule.slopeLimit = 50.0f;
                capsule.skinWidth = 0.03f;

                root.AddComponent<PlayerLocomotion>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state.name == name)
                {
                    return child.state;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the clip inside a model file, skipping Unity's own
        /// __preview__ duplicates which are not real assets.
        /// </summary>
        private static AnimationClip FindClip(string modelName)
        {
            string path = $"{AnimationDirectory}/{modelName}.fbx";
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var found = new List<AnimationClip>();
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    found.Add(clip);
                }
            }

            if (found.Count == 0)
            {
                Debug.LogWarning($"CityX: no animation clip inside {path}");
                return null;
            }
            return found[0];
        }

        private static void MakeLooping(AnimationClip clip)
        {
            string path = AssetDatabase.GetAssetPath(clip);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips.Length == 0)
            {
                return;
            }
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }
}
