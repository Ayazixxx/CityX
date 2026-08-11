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
            // Always first. Dropping a new FBX into the folder and rebuilding
            // should just work; without this the clip imports as Generic and
            // then silently animates nothing.
            CharacterImportSetup.Import();

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

            AddPunchStates(root);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// One state per attack-chain animation, named to match
        /// <see cref="ComboStage.Animation"/>.
        ///
        /// NO TRIGGERS AND NO CONDITIONS IN. The chain already decides which
        /// swing plays and when; duplicating that as a web of animator
        /// parameters means two state machines that have to agree, and they
        /// will not. PlayerAnimationDriver cross-fades to the state by name
        /// instead, so the chain stays the single source of truth.
        ///
        /// The way OUT is a plain exit-time transition, because a swing ending
        /// is a property of the clip, not of anything the game knows.
        /// </summary>
        private static void AddPunchStates(AnimatorStateMachine root)
        {
            AnimatorState locomotion = FindState(root, "Locomotion");

            foreach (ComboStage stage in ComboChain.For(ComboChain.Unarmed))
            {
                if (FindState(root, stage.Animation) != null)
                {
                    continue;   // the chain reuses punch_right and punch_left
                }

                AnimationClip clip = ClipForPunch(stage.Animation);
                if (clip == null)
                {
                    Debug.LogWarning($"CityX: no clip for '{stage.Animation}' -- that stage of the " +
                                     "chain will still hit, but the character will not swing.");
                    continue;
                }

                AnimatorState state = root.AddState(stage.Animation);
                state.motion = clip;

                if (locomotion != null)
                {
                    AnimatorStateTransition back = state.AddTransition(locomotion);
                    back.hasExitTime = true;
                    back.exitTime = 0.85f;
                    back.duration = 0.12f;
                }
            }
        }

        /// <summary>
        /// Maps a chain animation name to the Mixamo file that provides it.
        /// Explicit rather than by filename convention, because the chain's
        /// names describe the MOVE and the files are named after whatever
        /// Mixamo called the clip.
        /// </summary>
        private static AnimationClip ClipForPunch(string animation)
        {
            switch (animation)
            {
                case "punch_right": return FindClip("boxing_right_jab");
                case "punch_left": return FindClip("boxing_left_jab");
                case "punch_heavy": return FindClip("heavy_punch");
                default: return null;
            }
        }

        private static GameObject BuildPrefab(AnimatorController controller)
        {
            var root = new GameObject("Player");
            try
            {
                // THE GAME'S OWN SKELETON, not the imported one. The Mixamo FBX
                // is now only a source of animation clips; nothing of its rig
                // survives into the character. See BodyRig for why.
                Material skin = CharacterSkin.Ensure("PlayerSkin", CharacterSkin.PlayerColour);
                GameObject body = FusedBodyBuilder.BuildBody(skin, controller);
                body.transform.SetParent(root.transform, false);

                Animator animator = body.GetComponent<Animator>();

                CharacterController capsule = root.AddComponent<CharacterController>();
                capsule.slopeLimit = 50.0f;
                capsule.skinWidth = 0.03f;
                // PlayerLocomotion sizes this in Awake from StandingHeight,
                // which is now the rig's own measured crown height rather than
                // a number typed in beside it.
                capsule.height = BodyRig.StandingHeight;
                capsule.center = new Vector3(0.0f, BodyRig.StandingHeight * 0.5f, 0.0f);

                root.AddComponent<PlayerLocomotion>();
                root.AddComponent<PlayerVitals>();
                root.AddComponent<MeleeAttacker>();
                root.AddComponent<PlayerAnimationDriver>();

                // Built BEFORE the input reader and after the capsule, because
                // it needs the CharacterController to exclude from its own
                // collisions and it hangs bodies off the instantiated skeleton.
                RagdollBuilder.Build(root, animator);

                // Added last: it looks for the others in Awake.
                root.AddComponent<PlayerInputReader>();

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
