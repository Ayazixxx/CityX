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
            AnimationClip back = FindClip("walk_backward");
            AnimationClip left = FindClip("walk_left");
            AnimationClip right = FindClip("walk_right");

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
            MakeLooping(back);
            MakeLooping(left);
            MakeLooping(right);

            AnimatorController controller = BuildController(idle, walk, sprint, jump, back, left, right);
            GameObject prefab = BuildPrefab(controller);

            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: player rig written to {PrefabPath}");
            return prefab;
        }

        /// <summary>
        /// The punch impact clips, carried across from player/assets/audio.
        /// Missing ones are dropped rather than left as null slots, so the
        /// random pick can never land on nothing.
        /// </summary>
        private static AudioClip[] LoadImpactSounds()
        {
            AudioClip[] clips = LoadNumbered("punch_impact", 3);
            if (clips.Length == 0)
            {
                Debug.LogWarning("CityX: no punch impact clips -- landing a hit will be silent.");
            }
            return clips;
        }

        /// <summary>
        /// A numbered bank of clips. Missing ones are dropped rather than left
        /// as null slots, so a random pick can never land on nothing.
        /// </summary>
        private static AudioClip[] LoadNumbered(string stem, int count)
        {
            var clips = new List<AudioClip>();
            for (int i = 1; i <= count; i++)
            {
                AudioClip clip = LoadClip($"{stem}_{i}");
                if (clip != null)
                {
                    clips.Add(clip);
                }
            }
            return clips.ToArray();
        }

        private static AudioClip LoadClip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(
                $"Assets/CityX/Character/Audio/{name}.mp3");
        }

        /// <summary>
        /// The noises the player makes, carried across from the pile of
        /// AudioStreamPlayers player.tscn hangs off its own root.
        ///
        /// The clips are attached HERE rather than in the scene builder because
        /// they belong to the player prefab, and a prefab that only makes noise
        /// in one particular scene is a trap for whoever drops it into the next
        /// one. Same reason the punch impacts already live here.
        /// </summary>
        private static void AddAudio(GameObject root)
        {
            var audio = root.AddComponent<CityX.Character.PlayerAudio>();
            audio.WalkLoop = LoadClip("walk_sound");
            audio.RunLoop = LoadClip("run_sound");
            audio.FallImpact = LoadClip("fall_impact");
            audio.DashSounds = LoadNumbered("dash", 3);
            audio.CityAmbience = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/CityX/View/Audio/city_ambience.mp3");

            if (audio.WalkLoop == null || audio.RunLoop == null)
            {
                Debug.LogWarning("CityX: footstep clips missing -- walking will be silent.");
            }
        }

        private static AnimatorController BuildController(
            AnimationClip idle, AnimationClip walk, AnimationClip sprint, AnimationClip jump,
            AnimationClip back, AnimationClip left, AnimationClip right)
        {
            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            // A 2-D blend over the body's own velocity IN ITS OWN FRAME, in
            // metres per second. It was a 1-D blend on speed alone, which could
            // only ever say how fast, never which way -- so walking backwards
            // was shown by a forward stride.
            //
            // STILL IN REAL m/s, NOT A NORMALISED 0..1. That is what keeps the
            // stride matched to the ground between walking and sprinting rather
            // than only at the ends, and it is why the forward axis carries two
            // clips while the lateral ones carry one: sprint only ever applies
            // going forward (see PlayerLocomotion), so no sideways sample can
            // ever be asked for above walking pace.
            controller.CreateBlendTreeInController("Locomotion", out BlendTree locomotion, 0);
            locomotion.blendType = BlendTreeType.FreeformCartesian2D;
            locomotion.blendParameter = "MoveX";
            locomotion.blendParameterY = "MoveZ";
            locomotion.AddChild(idle, Vector2.zero);
            locomotion.AddChild(walk, new Vector2(0.0f, WalkSpeed));
            locomotion.AddChild(sprint, new Vector2(0.0f, SprintSpeed));
            if (back != null)
            {
                locomotion.AddChild(back, new Vector2(0.0f, -WalkSpeed));
            }
            if (left != null)
            {
                locomotion.AddChild(left, new Vector2(-WalkSpeed, 0.0f));
            }
            if (right != null)
            {
                locomotion.AddChild(right, new Vector2(WalkSpeed, 0.0f));
            }

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

            AddCrouchStates(controller, root);
            AddDashState(controller, root);
            AddPunchStates(root);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>How long a crouch takes to fold or unfold. character.gd's own.</summary>
        private const float CrouchBlendSeconds = 0.2f;

        /// <summary>
        /// Going down, being down, and coming back up.
        ///
        /// THREE STATES RATHER THAN A BLEND, because Godot has three clips and
        /// two of them are transitions -- standing_to_crouch and crouch_to_idle
        /// exist precisely so the fold reads as a movement rather than as the
        /// body sliding between two poses.
        ///
        /// THE HELD CROUCH IS crouch_walk FROZEN AT ITS FIRST FRAME. There is
        /// no crouch-idle clip in the set; Godot holds the last pose of
        /// standing_to_crouch instead, which cannot be expressed as a blend
        /// tree child without replaying the fold every loop. Freezing the walk
        /// gives the same thing -- a crouched body, not moving -- and comes
        /// from the clip the body will actually blend INTO the moment it walks,
        /// so there is nothing to pop between.
        /// </summary>
        private static void AddCrouchStates(AnimatorController controller,
            AnimatorStateMachine root)
        {
            AnimationClip down = FindClip("standing_to_crouch");
            AnimationClip walk = FindClip("crouch_walk");
            AnimationClip up = FindClip("crouch_to_idle");
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (down == null || walk == null || up == null || locomotion == null)
            {
                Debug.LogWarning("CityX: crouch clips missing -- crouching will not be shown.");
                return;
            }

            controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);

            controller.CreateBlendTreeInController("Crouched", out BlendTree crouched, 0);
            crouched.blendType = BlendTreeType.Simple1D;
            crouched.blendParameter = "Speed";
            crouched.AddChild(walk, 0.0f);
            crouched.AddChild(walk, WalkSpeed);
            ChildMotion[] children = crouched.children;
            children[0].timeScale = 0.0f;   // held, not walking on the spot
            crouched.children = children;
            AnimatorState crouchedState = FindState(root, "Crouched");

            AnimatorState downState = root.AddState("StandingToCrouch");
            downState.motion = down;

            AnimatorState upState = root.AddState("CrouchToIdle");
            upState.motion = up;

            Link(locomotion, downState, CrouchBlendSeconds, "Crouching", true);
            Link(downState, crouchedState, 0.1f, null, false, exitTime: 0.9f);

            // A crouch abandoned before it finishes still has to come back up,
            // or holding and releasing quickly leaves the body folded.
            Link(downState, upState, CrouchBlendSeconds, "Crouching", false);
            Link(crouchedState, upState, CrouchBlendSeconds, "Crouching", false);
            Link(upState, locomotion, 0.1f, null, false, exitTime: 0.9f);

            // And crouching again mid-stand goes straight back down rather than
            // waiting out the rest of the stand.
            Link(upState, downState, CrouchBlendSeconds, "Crouching", true);
        }

        /// <summary>
        /// The dash.
        ///
        /// ONE CLIP FOR EVERY DIRECTION, which is Godot's own arrangement: only
        /// dash_backward was ever authored, and a bare double-tap with nothing
        /// held dashes backward anyway. A sideways dash borrows it rather than
        /// showing a walk cycle at 24 m/s.
        /// </summary>
        private static void AddDashState(AnimatorController controller, AnimatorStateMachine root)
        {
            AnimationClip clip = FindClip("dash_backward");
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (clip == null || locomotion == null)
            {
                return;
            }

            controller.AddParameter("Dash", AnimatorControllerParameterType.Trigger);

            AnimatorState dash = root.AddState("Dash");
            dash.motion = clip;

            AnimatorStateTransition into = root.AddAnyStateTransition(dash);
            into.AddCondition(AnimatorConditionMode.If, 0.0f, "Dash");
            into.duration = 0.05f;
            into.hasExitTime = false;
            into.canTransitionToSelf = false;

            // OUT ON EXIT TIME, not on the burst ending. The clip runs well
            // past the 0.2s of real movement into its own recovery, and Godot
            // cuts that tail short deliberately -- but cutting it at 0.2s here
            // would snap mid-lunge. 0.6 of the clip is the compromise: the
            // recovery reads, and the body is not still lunging a second after
            // it stopped moving.
            AnimatorStateTransition outOf = dash.AddTransition(locomotion);
            outOf.duration = 0.15f;
            outOf.hasExitTime = true;
            outOf.exitTime = 0.6f;
        }

        /// <summary>One transition, with the boilerplate that is the same every time.</summary>
        private static void Link(AnimatorState from, AnimatorState to, float duration,
            string parameter, bool wanted, float exitTime = -1.0f)
        {
            if (from == null || to == null)
            {
                return;
            }
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.duration = duration;
            if (parameter != null)
            {
                transition.AddCondition(
                    wanted ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0.0f, parameter);
            }
            if (exitTime >= 0.0f)
            {
                transition.hasExitTime = true;
                transition.exitTime = exitTime;
            }
            else
            {
                transition.hasExitTime = false;
            }
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
                MeleeAttacker melee = root.AddComponent<MeleeAttacker>();
                // player.gd's three punch impact samples, the same files. A
                // serialized array rather than a Resources load, so a missing
                // clip shows up as an empty slot on the prefab instead of a
                // silent null at runtime.
                melee.ImpactSounds = LoadImpactSounds();
                melee.BloodPrefab = BloodSplatterBuilder.Build();
                AddAudio(root);
                root.AddComponent<PlayerAnimationDriver>();
                // Makes the body be whoever was chosen in the menu, and does
                // nothing at all when the world is opened directly -- which is
                // how it is opened most of the time.
                root.AddComponent<CharacterIdentity>();

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
