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
        public const float WalkSpeed = 4.0f;

        public const float SprintSpeed = 7.5f;

        [MenuItem("CityX/Rebuild Player Rig")]
        public static GameObject Build()
        {
            // Always first. Dropping a new FBX into the folder and rebuilding
            // should just work; without this the clip imports as Generic and
            // then silently animates nothing.
            CharacterImportSetup.Import();

            // THE GODOT CHARACTER'S OWN MOTION where it has been baked (GodotMotionBaker), the retargeted Mixamo files only if not. Both come from the same
            // source clips, but the Godot ones are what the owner means by "looks better": see GodotMotionBaker for the measured difference.
            GodotMotionBaker.BakeAll();
            // The one-handed sword's carry, baked from those same clips with a sword arm (AddSword1HLocomotion).
            SwordCarryBaker.BakeAll();
            // The crouch, baked from those same clips crouched (AddCrouchLayer).
            CrouchBaker.BakeAll();
            AnimationClip idle = GodotOrMixamo("idle", "idle");
            AnimationClip walk = GodotOrMixamo("walk", "walking");
            AnimationClip sprint = GodotOrMixamo("run", "sprint");
            AnimationClip jump = FindClip("jump");
            AnimationClip back = GodotOrMixamo("walk_backward", "walk_backward");
            AnimationClip left = GodotOrMixamo("walk_left", "walk_left");
            AnimationClip right = GodotOrMixamo("walk_right", "walk_right");

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
            // The Mixamo originals of those six are only the fallback now, but they stay set to loop: left alone, the importer settings flip back and forth on every
            // rebuild (and the fallback would play once and freeze mid-step).
            foreach (string mixamo in new[] { "idle", "walking", "sprint", "walk_backward", "walk_left", "walk_right" })
            {
                MakeLooping(FindClip(mixamo));
            }

            // Mecanim's Humanoid retargeting is proportion-aware: it resolves
            // the source stride against THIS avatar's own bone lengths, and on
            // this rig's short legs (see BodyRig) that lands on a smaller
            // knee/hip swing than the same clip reads as on Mixamo's own
            // longer-legged reference body -- confirmed by direct measurement,
            // the muscle values never approach their own +-1 limit, so nothing
            // is being clamped, the retargeted motion is just genuinely
            // smaller. Widening it back out around each curve's own mean
            // fixes that proportion mismatch without touching anything the
            // limit-clamping fix would have addressed instead.
            // Baked Godot clips are already the right size (see StrideExaggeration), so only the Mixamo fallbacks pass through this.
            walk = ExaggerateStrideIfMixamo(walk, "walking");
            sprint = ExaggerateStrideIfMixamo(sprint, "sprint");
            back = ExaggerateStrideIfMixamo(back, "walk_backward");
            left = ExaggerateStrideIfMixamo(left, "walk_left");
            right = ExaggerateStrideIfMixamo(right, "walk_right");
            // The Godot walks with steps long enough for this body's speeds, so the foot locks can hold their feet (PacedGait).
            if (IsBakedGodotClip(walk))
            {
                walk = PacedGait.Build(walk, "walk", PacedGait.Walk);
                back = PacedGait.Build(back, "walk_backward", PacedGait.Backward);
                left = PacedGait.Build(left, "walk_left", PacedGait.Strafe);
                right = PacedGait.Build(right, "walk_right", PacedGait.Strafe);
            }

            // BLOODRAGE (key 8) swaps the walking, running and jumping clips above for the user's own. Looped and stride-widened exactly as the
            // ordinary ones are, and paired with the clips the controller actually uses so BloodrageAbility can override them.
            AnimationClip bloodWalk = FindClip("bloodrage_walking");
            AnimationClip bloodRun = FindClip("bloodrage_run");
            AnimationClip bloodJump = FindClip("bloodrage_jump");
            MakeLooping(bloodWalk);
            MakeLooping(bloodRun);
            bloodWalk = ExaggerateStride(bloodWalk, "bloodrage_walking");
            bloodRun = ExaggerateStride(bloodRun, "bloodrage_run");
            _bloodrageOriginals = new[] { walk, sprint, jump };
            _bloodrageReplacements = new[] { bloodWalk, bloodRun, bloodJump };

            AnimatorController controller = BuildController(idle, walk, sprint, jump, back, left, right);
            GameObject prefab = BuildPrefab(controller);

            // The Bloodrage run's head is brought to where the ordinary sprint carries it (see BloodrageHeadFit).
            BloodrageHeadFit.Apply(prefab, bloodRun, sprint);

            // THE GAITS (ClipCadence): at each speed the clip whose own pace is nearest plays (4 m/s is a jog on this body), re-paced by at most 20%,
            // with up to 20% more stride from the foot placement (GaitStrides) and the foot locks absorbing the rest -- the research's answer to
            // feet that slid 2.6 m/s under a Mixamo walk, without the mince of speeding the walk up 2.8x.
            int matched = ClipCadence.MatchController(controller, prefab);
            Debug.Log($"CityX: {matched} locomotion clips given their gait and pace.");

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

            root.AddComponent<CityX.Character.PlayerVoiceCapture>();
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
            // A shield held up (PlayerLocomotion.Shield.cs): the left arm's pose, not a state of its own; here so other players' copies are told it.
            controller.AddParameter("ShieldRaised", AnimatorControllerParameterType.Bool);

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
            // FREEFORM DIRECTIONAL, not Cartesian (owner, 2026-10-11: W+A / W+D walked "in like slow motion"). Cartesian weighted the clips by
            // plain distance, so a diagonal walk at walking speed sat as near the idle at the centre as the walk and strafe on its sides and
            // took 23% idle -- whose long clip stretched one step cycle from 1.03 s to 3.05 s at the same ground speed. Directional blends by
            // ANGLE between the clips round the centre and only uses the centre for low speeds, so a diagonal is half walk, half strafe, no idle,
            // and steps at their own 1.03 s (DiagonalWalkCadencePlayTests).
            locomotion.blendType = BlendTreeType.FreeformDirectional2D;
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
            AddDiagonals(locomotion, walk, back, left, right);

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

            AddDashState(controller, root);
            AddChargedJumpState(controller, root);
            AddAimingState(controller, root);
            // AFTER AddAimingState: AddGunLocomotion looks up the "Aiming"
            // state by name to add its own moving-exit transitions out of it.
            // The plain side-steps for the gun and the two-handed sword: their own (Mixamo pistol and great-sword strafes) squat bow-legged (docs/MOVEMENT.md).
            AddGunLocomotion(controller, root, left, right);
            AddJetpackIdleState(controller, root, idle);
            AddDanceStates(controller, root);
            AddPushUpStates(controller, root);
            AddSelfSmiteState(root);
            AddClimbingStates(controller, root);
            AddCarSeatStates(controller, root);
            AddPunchStates(root);
            // AFTER AddPunchStates: AddSwordLocomotion has to exist before
            // AddSwordStates looks its state up by name for the sword
            // swings' own exit transition.
            AddSwordLocomotion(controller, root, left, right);
            AddSword1HLocomotion(controller, root);
            AddSwordStates(root);
            // AFTER AddSwordStates and every base state: the upper-body layer
            // plays the SAME sword clips as those base states, over the top of
            // whatever the legs are doing.
            // The scream's torso+head layer and its arms layer go BELOW the sword's upper-body layer (added after them), so a sword swing or guard
            // wins the arms (and torso) while one is playing, and the scream carries on underneath.
            // The crouch (legs, hips, head) over the base, under the upper-body layers that take the torso for a swing or a scream.
            AddCrouchLayer(controller);
            AddScreamLayers(controller);
            AddSwordUpperBodyLayer(controller);
            AddCutsceneStates(root);
            // AFTER every walking state exists.
            AddStrideRate(controller, root);

            // FOOT IK ON THE BLENDED LOCOMOTION STATES was tried here as a
            // mitigation for a reported "the running animation feels
            // choppy" -- and reverted. Turning on iKOnFeet + the layer's own
            // IK pass measurably broke something already relied on:
            // PlayerLocomotionGroundClampPlayTests.HoldingTheChargeNeverSinks
            // TheFeetBelowGround went from passing to a foot bone sinking to
            // -0.057 against ground contact at 0.030, confirmed by the full
            // suite, not a guess -- Unity's own foot-IK pass fights
            // PlayerLocomotion's own hand-rolled ClampFeetAboveGround for
            // control of the same bones. The choppy-running report itself
            // was never verified either way (a motion-quality effect,
            // invisible in the single-frame stills this project's own
            // capture tooling produces, unlike every other render-backed
            // fix in this codebase's history) -- a confirmed regression
            // against an unconfirmed fix is not a trade worth making, so
            // this reverts clean rather than shipping half of it.

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>The walking states whose cycles Grow and Shrink slow down or speed up (BodySizeAbility.StrideRateName).</summary>
        public static readonly string[] StridingStates = { "Locomotion", "SwordLocomotion", "Sword1HLocomotion", "GunLocomotion" };

        /// <summary>
        /// GROW AND SHRINK'S STRIDE (BodySizeAbility): a float, 1 at the body's own size, that every walking state plays at. A giant's legs are
        /// ten times as long and it walks 3.2 times as fast, so each step takes 3.2 times as long (the rate is 1/sqrt(size)); without it the
        /// feet would skate. Defaults to 1, so a body without the spell (a civilian on this controller) walks as it always did.
        /// </summary>
        private static void AddStrideRate(AnimatorController controller, AnimatorStateMachine root)
        {
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = BodySizeAbility.StrideRateName,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1.0f,
            });
            foreach (string name in StridingStates)
            {
                AnimatorState state = FindState(root, name);
                if (state == null)
                {
                    Debug.LogWarning($"CityX: no {name} state: Grow and Shrink will not pace its steps.");
                    continue;
                }
                state.speedParameterActive = true;
                state.speedParameter = BodySizeAbility.StrideRateName;
            }
        }

        /// <summary>
        /// Falling/impact/getting-up/looking-around, for IntroCutscene's own
        /// one-time fall on a brand new character. Ported from
        /// player/intro_cutscene.gd's four anim_player.play() calls.
        ///
        /// NO TRANSITIONS AT ALL, in or out -- same "told by name, has no
        /// opinion" reasoning PlayerAnimationDriver's own class comment
        /// already gives for the punch states, just taken further: even the
        /// punch states get an exit-time transition back to Locomotion,
        /// because a swing's own duration is fixed and predictable. This
        /// cutscene's beats are not (impact holds for a tuned constant,
        /// getting_up and looking_around run to their own real clip
        /// lengths, which IntroCutscene reads off the Animator itself) --
        /// a graph transition would have to guess at a duration it cannot
        /// know, where a raw CrossFadeInFixedTime call from the one place
        /// that DOES know is exact. IntroCutscene drives every step of this
        /// sequence directly, including the final CrossFadeInFixedTime back
        /// to "Locomotion" at handoff.
        /// </summary>
        private static void AddCutsceneStates(AnimatorStateMachine root)
        {
            AnimationClip falling = FindClip("falling");
            if (falling != null)
            {
                // The only looped clip of the four -- see intro_cutscene.gd's
                // own class comment: "falling is the only LOOPED clip in
                // this game." The fall's own real duration (15s) almost
                // never matches the clip's short dive-loop length exactly.
                MakeLooping(falling);
            }

            AddCutsceneState(root, "Falling", falling);
            AddCutsceneState(root, "FallingFlatImpact", FindClip("falling_flat_impact"));
            AddCutsceneState(root, "GettingUp", FindClip("getting_up"));
            AddCutsceneState(root, "LookingAround", FindClip("looking_around"));
        }

        private static void AddCutsceneState(AnimatorStateMachine root, string name, AnimationClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning($"CityX: no clip for cutscene state '{name}' -- the intro " +
                                  "cutscene will hold on whatever animation was already playing.");
                return;
            }
            if (FindState(root, name) != null)
            {
                return;
            }
            AnimatorState state = root.AddState(name);
            state.motion = clip;
        }

        /// <summary>
        /// Below this the body counts as standing still, for any Animator
        /// state that only makes sense while stationary -- the
        /// charged-jump pose (see AddChargedJumpState's own doc comment)
        /// gates on it. Small rather than zero so residual float noise
        /// from a stopped body can never read as "still moving."
        /// </summary>
        private const float MovingSpeedThreshold = 0.1f;

        /// <summary>The crouch layer's mask: the hips and spine (its lean), the head (turned back up) and the legs. The arms stay whatever plays below.</summary>
        private const string CrouchMaskPath = "Assets/CityX/Character/CrouchMask.mask";

        /// <summary>
        /// THE CROUCH (docs/CROUCH.md): its own layer over everything below it, not states beside the walk. Its one state is a directional
        /// blend of the baked crouch clips (CrouchBaker: the idle and the walk four ways, crouched) at Source's crouching speed, a third of the
        /// walk's; the layer's weight is the duck's own eased share, set every frame by PlayerLocomotion.ShowCrouch, so going down and coming up
        /// are the 0.4 s and 0.2 s of Source's duck with nothing to transition between. Masked to the hips, spine, head and legs: the arms keep
        /// what the base and the upper layers give them, so a crouch works with fists, a sword, a gun, a spell. Added before the upper-body
        /// layers, so a swing still takes the torso over it. The Crouching parameter is what other players' copies of the body follow.
        /// </summary>
        private static void AddCrouchLayer(AnimatorController controller)
        {
            controller.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
            AnimationClip idle = CrouchBaker.Load("idle");
            AnimationClip walk = CrouchBaker.Load("walk");
            if (idle == null || walk == null)
            {
                Debug.LogWarning("CityX: no crouch clips (CrouchBaker) -- crouching will not be shown.");
                return;
            }

            AvatarMask mask = Mask(CrouchMaskPath, AvatarMaskBodyPart.Root, AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftLeg,
                AvatarMaskBodyPart.RightLeg, AvatarMaskBodyPart.LeftFootIK, AvatarMaskBodyPart.RightFootIK);
            controller.AddLayer(PlayerLocomotion.CrouchLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            int index = layers.Length - 1;
            AnimatorControllerLayer layer = layers[index];
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 0.0f;
            layer.iKPass = false;
            controller.layers = layers;

            controller.CreateBlendTreeInController(PlayerLocomotion.CrouchStateName, out BlendTree crouched, index);
            crouched.blendType = BlendTreeType.FreeformDirectional2D;
            crouched.blendParameter = "MoveX";
            crouched.blendParameterY = "MoveZ";
            AnimationClip back = CrouchBaker.Load(CrouchBaker.Backward);
            AnimationClip left = CrouchBaker.Load("walk_left");
            AnimationClip right = CrouchBaker.Load("walk_right");
            crouched.AddChild(idle, Vector2.zero);
            crouched.AddChild(walk, new Vector2(0.0f, CrouchSpeed));
            if (back != null) crouched.AddChild(back, new Vector2(0.0f, -CrouchSpeed));
            if (left != null) crouched.AddChild(left, new Vector2(-CrouchSpeed, 0.0f));
            if (right != null) crouched.AddChild(right, new Vector2(CrouchSpeed, 0.0f));
            AddDiagonals(crouched, walk, back, left, right, CrouchSpeed);

            // Grow and Shrink pace its steps as they pace every walk (AddStrideRate).
            AnimatorState state = FindState(controller.layers[index].stateMachine, PlayerLocomotion.CrouchStateName);
            if (state != null)
            {
                state.speedParameterActive = true;
                state.speedParameter = BodySizeAbility.StrideRateName;
            }
        }

        /// <summary>Source's crouched speed: a third of the walk (CrouchHull.SpeedShare).</summary>
        public const float CrouchSpeed = WalkSpeed * CrouchHull.SpeedShare;

        /// <summary>A humanoid avatar mask at <paramref name="maskPath"/> with exactly <paramref name="parts"/> on.</summary>
        private static AvatarMask Mask(string maskPath, params AvatarMaskBodyPart[] parts)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, maskPath);
            }
            foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart)
                {
                    continue;
                }
                mask.SetHumanoidBodyPartActive(part, System.Array.IndexOf(parts, part) >= 0);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static AnimatorStateTransition AddSpeedCondition(AnimatorStateTransition transition, AnimatorConditionMode mode)
        {
            transition.AddCondition(mode, MovingSpeedThreshold, "Speed");
            return transition;
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

        /// <summary>
        /// Raises the pistol to aim while right-click is held. Ported from
        /// nowhere -- pistol_aim_in.fbx was already sitting retargeted and
        /// unused in the Animations folder (this project's own doc comment
        /// on UpdateHeldPose's "no dedicated reload mocap clip exists
        /// (unlike shoot/aim-in/locomotion, all real retargeted Mixamo
        /// clips)" already named it) until this wired it up.
        ///
        /// A FULL-BODY STANDING POSE, not an upper-body-only overlay --
        /// confirmed by sampling its own curve bindings directly: it
        /// animates the legs too (Right Upper Leg Front-Back and friends),
        /// the same shape as ChargedJump's own held pose, so it replaces
        /// Locomotion outright the same way rather than needing a second
        /// additive Animator layer over the walk cycle.
        ///
        /// BOOL-GATED BOTH WAYS, unlike ChargedJump's one-way trigger reuse
        /// -- there is no other any-state transition this can piggyback on
        /// to get back out, so it needs its own explicit "Aiming went
        /// false" exit: a bidirectional pair of Links.
        ///
        /// ENTRY IS ALSO GATED ON STANDING STILL (Speed below
        /// MovingSpeedThreshold), added after this was found to be the
        /// actual cause of an Bug report: "the legs move as you
        /// hold the pistol and aim in if you are walking [in Godot] which
        /// it doesnt do in unity." Before this, the Any State transition
        /// fired on Aiming alone, so holding right-click froze the WHOLE
        /// body -- legs included -- on this single static standing pose
        /// while the CharacterController kept sliding the transform forward
        /// underneath it, reading exactly as "legs lock up." character.gd's
        /// own set_walking() only ever shows the equivalent gun_aim_in pose
        /// while genuinely standing still; moving switches to a full-body
        /// gun_walk/gun_run cycle instead regardless of aim (see
        /// AddGunLocomotion, which also adds the matching moving-exit
        /// transitions out of this same "Aiming" state once it exists).
        /// </summary>
        private static void AddAimingState(AnimatorController controller, AnimatorStateMachine root)
        {
            AnimationClip clip = FindClip("pistol_aim_in");
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (clip == null || locomotion == null)
            {
                Debug.LogWarning("CityX: no pistol_aim_in clip -- aiming will not raise the gun.");
                return;
            }

            controller.AddParameter("Aiming", AnimatorControllerParameterType.Bool);

            AnimatorState aiming = root.AddState("Aiming");
            aiming.motion = clip;

            AnimatorStateTransition into = root.AddAnyStateTransition(aiming);
            into.AddCondition(AnimatorConditionMode.If, 0.0f, "Aiming");
            AddSpeedCondition(into, AnimatorConditionMode.Less);
            into.duration = 0.12f;
            into.hasExitTime = false;
            into.canTransitionToSelf = false;

            AnimatorStateTransition outOf = aiming.AddTransition(locomotion);
            outOf.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "Aiming");
            outOf.duration = 0.15f;
            outOf.hasExitTime = false;
        }

        /// <summary>
        /// Forces Idle over whatever Locomotion would otherwise show while
        /// the jetpack is actually thrusting -- an explicit new-mechanic ask
        /// ("get rid of the walking and running animation... just when the
        /// jetpack is activated or when space is being held"). A walk/run
        /// cycle reads as wrong the moment the body is actually airborne
        /// under thrust, which happens almost immediately once thrust
        /// starts (ThrustSpeed comfortably beats gravity), so this is gated
        /// on the thrust key itself rather than on IsGrounded.
        ///
        /// BOOL-GATED BOTH WAYS, the same shape AddAimingState's own doc
        /// comment already explains: nothing else exits back to Locomotion
        /// for this bool to piggyback on.
        /// </summary>
        private static void AddJetpackIdleState(AnimatorController controller, AnimatorStateMachine root, AnimationClip idle)
        {
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (idle == null || locomotion == null)
            {
                return;
            }

            controller.AddParameter("JetpackThrusting", AnimatorControllerParameterType.Bool);

            AnimatorState jetpackIdle = root.AddState("JetpackIdle");
            jetpackIdle.motion = idle;

            AnimatorStateTransition into = root.AddAnyStateTransition(jetpackIdle);
            into.AddCondition(AnimatorConditionMode.If, 0.0f, "JetpackThrusting");
            into.duration = 0.12f;
            into.hasExitTime = false;
            into.canTransitionToSelf = false;

            AnimatorStateTransition outOf = jetpackIdle.AddTransition(locomotion);
            outOf.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "JetpackThrusting");
            outOf.duration = 0.15f;
            outOf.hasExitTime = false;
        }

        /// <summary>
        /// The /dance and /breakdance chat emotes -- ported from
        /// character.gd's own dance()/breakdance() ("loop an emote clip
        /// until something else takes over"), dance.fbx/breakdance.fbx
        /// already sitting retargeted and unused in the Animations folder,
        /// the same "already imported, never wired up" situation
        /// AddAimingState's own doc comment found for pistol_aim_in.fbx.
        ///
        /// BOTH DIRECTIONS ARE ANY-STATE ENTRIES, INCLUDING FROM EACH OTHER,
        /// the same shape AddClimbingStates already uses for its own two
        /// mutually-exclusive states -- typing /breakdance mid-dance needs
        /// no direct Dance-to-Breakdance transition, only its own
        /// independent entry condition, since Unity re-evaluates every Any
        /// State transition every frame regardless of the current state.
        /// </summary>
        private static void AddDanceStates(AnimatorController controller, AnimatorStateMachine root)
        {
            AnimationClip dance = FindClip("dance");
            AnimationClip breakdance = FindClip("breakdance");
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (dance == null || breakdance == null || locomotion == null)
            {
                Debug.LogWarning("CityX: missing a dance clip -- the /dance emotes will show no animation.");
                return;
            }
            MakeLooping(dance);
            MakeLooping(breakdance);

            controller.AddParameter("Dancing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Breakdancing", AnimatorControllerParameterType.Bool);

            AnimatorState danceState = root.AddState("Dance");
            danceState.motion = dance;
            AnimatorStateTransition intoDance = root.AddAnyStateTransition(danceState);
            intoDance.AddCondition(AnimatorConditionMode.If, 0.0f, "Dancing");
            intoDance.duration = 0.15f;
            intoDance.hasExitTime = false;
            intoDance.canTransitionToSelf = false;
            AnimatorStateTransition outOfDance = danceState.AddTransition(locomotion);
            outOfDance.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "Dancing");
            outOfDance.duration = 0.15f;
            outOfDance.hasExitTime = false;

            AnimatorState breakdanceState = root.AddState("Breakdance");
            breakdanceState.motion = breakdance;
            AnimatorStateTransition intoBreakdance = root.AddAnyStateTransition(breakdanceState);
            intoBreakdance.AddCondition(AnimatorConditionMode.If, 0.0f, "Breakdancing");
            intoBreakdance.duration = 0.15f;
            intoBreakdance.hasExitTime = false;
            intoBreakdance.canTransitionToSelf = false;
            AnimatorStateTransition outOfBreakdance = breakdanceState.AddTransition(locomotion);
            outOfBreakdance.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "Breakdancing");
            outOfBreakdance.duration = 0.15f;
            outOfBreakdance.hasExitTime = false;
        }

        /// <summary>
        /// /pushups (owner, 2026-10-14): going down (pushup_enter), push-ups on repeat (pushup) and getting up (pushup_exit), driven by the
        /// PushUpPhase number PlayerInputReader's PushUpEmote sets (0 standing, 1 going down, 2 pushing up, 3 getting up). A number, not a bool,
        /// so Any State can start it without restarting it while it holds: only phase 1 enters PushUpEnter, and each later phase moves it on. The
        /// emote keeps the phases to the clips' own lengths (written into the reader), so each state plays whole.
        /// </summary>
        private static void AddPushUpStates(AnimatorController controller, AnimatorStateMachine root)
        {
            AnimationClip enter = FindClip("pushup_enter");
            AnimationClip rep = FindClip("pushup");
            AnimationClip exit = FindClip("pushup_exit");
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (enter == null || rep == null || exit == null || locomotion == null)
            {
                Debug.LogWarning("CityX: missing a push-up clip -- /pushups will show no animation.");
                return;
            }
            MakeLooping(rep);
            controller.AddParameter("PushUpPhase", AnimatorControllerParameterType.Int);

            AnimatorState down = root.AddState("PushUpEnter");
            down.motion = enter;
            AnimatorState pushing = root.AddState("PushUpLoop");
            pushing.motion = rep;
            AnimatorState up = root.AddState("PushUpExit");
            up.motion = exit;

            AnimatorStateTransition intoDown = root.AddAnyStateTransition(down);
            intoDown.AddCondition(AnimatorConditionMode.Equals, 1, "PushUpPhase");
            intoDown.duration = 0.2f;
            intoDown.hasExitTime = false;
            intoDown.canTransitionToSelf = false;

            AddPhaseTransition(down, pushing, 2, 0.05f);
            AddPhaseTransition(down, up, 3, 0.1f);          // called off going down: it finishes going down, then gets straight up
            AddPhaseTransition(pushing, up, 3, 0.15f);
            AddPhaseTransition(up, locomotion, 0, 0.15f);
            AddPhaseTransition(down, locomotion, 0, 0.15f);  // ended at once (a blow, a fall): straight back to standing
            AddPhaseTransition(pushing, locomotion, 0, 0.15f);
        }

        /// <summary>
        /// Self Smite (owner, 2026-10-15: "use this animation for the spell speed it up or slow it down as needed"): the owner's selfsmite.fbx,
        /// played by the spell (SelfSmiteAbility cross-fades into it by name, on this body and on other players' copies) at
        /// SelfSmiteMove.ClipSpeed, so the hands come down just as the bolt lands. Back to Locomotion when it ends.
        /// </summary>
        private static void AddSelfSmiteState(AnimatorStateMachine root)
        {
            AnimationClip clip = FindClip("selfsmite");
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (clip == null || locomotion == null)
            {
                Debug.LogWarning("CityX: selfsmite.fbx is missing -- Self Smite will show no animation.");
                return;
            }
            AnimatorState state = root.AddState(SelfSmiteMove.StateName);
            state.motion = clip;
            state.speed = SelfSmiteMove.ClipSpeed;
            AnimatorStateTransition back = state.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = Mathf.Clamp01(SelfSmiteMove.ClipExitSeconds / SelfSmiteMove.ClipSeconds);   // as the caster is freed
            back.hasFixedDuration = true;
            back.duration = SelfSmiteMove.ClipExitBlendSeconds;
        }

        private static void AddPhaseTransition(AnimatorState from, AnimatorState to, int phase, float duration)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.AddCondition(AnimatorConditionMode.Equals, phase, "PushUpPhase");
            transition.duration = duration;
            transition.hasExitTime = false;
        }

        /// <summary>
        /// Wall climbing -- ported from player.gd/character.gd's own
        /// start_climb_up/start_climb_down/hold_climb/play_climb_top_out.
        /// See PlayerClimbing.cs for the movement/state-machine side; this
        /// only builds what the Animator needs to show it.
        ///
        /// climbing_up_wall.fbx/climbing_down_wall.fbx/braced_hang_to_crouch.fbx
        /// were already sitting retargeted and unused in the Animations
        /// folder -- the same "already imported, never wired up" situation
        /// AddAimingState's own doc comment found for pistol_aim_in.fbx.
        ///
        /// TWO LOOPING STATES, NOT THREE -- character.gd's own hold_climb()
        /// has no dedicated held pose; it freezes whichever of climb_up/
        /// climb_down was already showing by setting speed_scale to 0.
        /// Unity's equivalent is a state's own Speed Parameter: both
        /// ClimbUp and ClimbDown multiply their playback speed by
        /// ClimbSpeedScale, which PlayerClimbing.Tick sets to 0 whenever
        /// neither W nor S is held and 1 otherwise -- so "holding position"
        /// is a frozen frame of whichever direction was last active, exactly
        /// matching character.gd's own behaviour, with no third state
        /// needed.
        ///
        /// BOTH DIRECTIONS ARE ANY-STATE ENTRIES, including from EACH OTHER
        /// -- Unity re-evaluates Any State transitions every frame
        /// regardless of the current state, so switching from ClimbUp to
        /// ClimbDown mid-climb (or back) needs no direct transition between
        /// the two, only their own independent entry conditions.
        /// </summary>
        private static void AddClimbingStates(AnimatorController controller, AnimatorStateMachine root)
        {
            AnimationClip up = FindClip("climbing_up_wall");
            AnimationClip down = FindClip("climbing_down_wall");
            AnimationClip topOut = FindClip("braced_hang_to_crouch");
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (up == null || down == null || locomotion == null)
            {
                Debug.LogWarning("CityX: missing a climbing clip -- wall climbing will show no animation.");
                return;
            }
            MakeLooping(up);
            MakeLooping(down);

            controller.AddParameter("Climbing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("ClimbDirection", AnimatorControllerParameterType.Int);
            controller.AddParameter("ClimbTopOut", AnimatorControllerParameterType.Trigger);
            // Defaults to 1 (playing normally), NOT Unity's usual 0-for-a-
            // fresh-float-parameter -- PlayerClimbing.Tick only sets this
            // every frame WHILE already climbing, so the very first frame of
            // a fresh grab (before Tick has run once) would otherwise show
            // ClimbUp frozen on its opening pose for a frame.
            controller.AddParameter("ClimbSpeedScale", AnimatorControllerParameterType.Float);
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == "ClimbSpeedScale")
                {
                    parameters[i].defaultFloat = 1.0f;
                }
            }
            controller.parameters = parameters;

            AnimatorState climbUp = root.AddState("ClimbUp");
            climbUp.motion = up;
            climbUp.speedParameterActive = true;
            climbUp.speedParameter = "ClimbSpeedScale";

            AnimatorState climbDown = root.AddState("ClimbDown");
            climbDown.motion = down;
            climbDown.speedParameterActive = true;
            climbDown.speedParameter = "ClimbSpeedScale";

            AnimatorStateTransition intoUp = root.AddAnyStateTransition(climbUp);
            intoUp.AddCondition(AnimatorConditionMode.If, 0.0f, "Climbing");
            intoUp.AddCondition(AnimatorConditionMode.Greater, 0.0f, "ClimbDirection");
            intoUp.duration = 0.1f;
            intoUp.hasExitTime = false;
            intoUp.canTransitionToSelf = false;

            AnimatorStateTransition intoDown = root.AddAnyStateTransition(climbDown);
            intoDown.AddCondition(AnimatorConditionMode.If, 0.0f, "Climbing");
            intoDown.AddCondition(AnimatorConditionMode.Less, 0.0f, "ClimbDirection");
            intoDown.duration = 0.1f;
            intoDown.hasExitTime = false;
            intoDown.canTransitionToSelf = false;

            AnimatorStateTransition outOfUp = climbUp.AddTransition(locomotion);
            outOfUp.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "Climbing");
            outOfUp.duration = 0.15f;
            outOfUp.hasExitTime = false;

            AnimatorStateTransition outOfDown = climbDown.AddTransition(locomotion);
            outOfDown.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "Climbing");
            outOfDown.duration = 0.15f;
            outOfDown.hasExitTime = false;

            if (topOut == null)
            {
                Debug.LogWarning("CityX: no braced_hang_to_crouch clip -- topping out a climb will show no mantle.");
                return;
            }

            AnimatorState climbTopOut = root.AddState("ClimbTopOut");
            climbTopOut.motion = topOut;

            AnimatorStateTransition intoTopOut = root.AddAnyStateTransition(climbTopOut);
            intoTopOut.AddCondition(AnimatorConditionMode.If, 0.0f, "ClimbTopOut");
            intoTopOut.duration = 0.1f;
            intoTopOut.hasExitTime = false;
            intoTopOut.canTransitionToSelf = false;

            // OUT ON THE FULL CLIP, not a partial exit time like Dash's own
            // 0.6 -- character.gd's own play_climb_top_out is a deliberate
            // one-shot mantle player.gd waits out completely (real
            // animation_finished, not a fixed fraction) before relocating
            // the body; PlayerClimbing.TickTopOut polls this same clip
            // finishing independently to know when to do that relocation,
            // so the visual state exit and the actual repositioning land on
            // the same frame.
            AnimatorStateTransition outOfTopOut = climbTopOut.AddTransition(locomotion);
            outOfTopOut.duration = 0.1f;
            outOfTopOut.hasExitTime = true;
            outOfTopOut.exitTime = 1.0f;
        }

        /// <summary>
        /// Entering/exiting a car -- ported from character.gd's own
        /// play_enter_car()/play_exit_car(). entering_car.fbx/exiting_car.fbx
        /// were already sitting retargeted and unused in the Animations
        /// folder, the same "already imported, never wired up" situation
        /// AddAimingState's own doc comment found for pistol_aim_in.fbx.
        ///
        /// NOT MakeLooping -- these are LOOP_NONE in Godot too (character.gd's
        /// own comment: "once it finishes, anim_player just holds its own
        /// last frame indefinitely"). EnteringCar's OWN held last frame IS
        /// the seated driving pose for the whole rest of the drive -- see
        /// its own missing outgoing transition below.
        ///
        /// TRIGGERS, NOT BOOLS, unlike Dance/Breakdance's own toggleable
        /// pair -- a Trigger is auto-consumed by whichever transition
        /// actually reads it, which is what keeps ExitingCar's own exit-time
        /// return to Locomotion (below) safe: a stale Bool left sitting
        /// true would immediately re-fire the Any State entry into
        /// ExitingCar the instant Locomotion became current again (Any
        /// State conditions are evaluated every frame regardless of the
        /// current state), replaying the clip forever. A Trigger cannot do
        /// that -- it is gone the moment this same transition consumes it.
        /// </summary>
        private static void AddCarSeatStates(AnimatorController controller, AnimatorStateMachine root)
        {
            AnimationClip entering = FindClip("entering_car");
            AnimationClip exiting = FindClip("exiting_car");
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (entering == null || exiting == null || locomotion == null)
            {
                Debug.LogWarning("CityX: missing a car seat clip -- entering/exiting a car will show no animation.");
                return;
            }

            controller.AddParameter("EnteringCar", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("ExitingCar", AnimatorControllerParameterType.Trigger);

            AnimatorState enteringState = root.AddState("EnteringCar");
            enteringState.motion = entering;
            AnimatorStateTransition intoEntering = root.AddAnyStateTransition(enteringState);
            intoEntering.AddCondition(AnimatorConditionMode.If, 0.0f, "EnteringCar");
            intoEntering.duration = 0.1f;
            intoEntering.hasExitTime = false;
            intoEntering.canTransitionToSelf = false;
            // NO OUTGOING TRANSITION -- EnteringCar holds its own last frame
            // for the whole drive (see Vehicles/Car.cs's own seat-occupancy
            // gate), matching character.gd's own comment exactly. The only
            // way out is the Any State transition into ExitingCar below.

            AnimatorState exitingState = root.AddState("ExitingCar");
            exitingState.motion = exiting;
            AnimatorStateTransition intoExiting = root.AddAnyStateTransition(exitingState);
            intoExiting.AddCondition(AnimatorConditionMode.If, 0.0f, "ExitingCar");
            intoExiting.duration = 0.1f;
            intoExiting.hasExitTime = false;
            intoExiting.canTransitionToSelf = false;

            // OUT ON THE FULL CLIP, unconditional -- matches ClimbTopOut's
            // own precedent above: a real, deliberate one-shot player.gd
            // waits out completely (PlayerLocomotion.IsCarAnimatorStateFinished
            // polls this same clip finishing independently) before actually
            // completing the exit.
            AnimatorStateTransition outOfExiting = exitingState.AddTransition(locomotion);
            outOfExiting.duration = 0.1f;
            outOfExiting.hasExitTime = true;
            outOfExiting.exitTime = 1.0f;
        }

        // Sitting on a BenchSeat (View/Interactables/BenchSeat.cs) does NOT get an
        // Animator state here -- a first version reused entering_car.fbx's
        // own held pose (a real Bool state, matching Dance/Breakdance's
        // toggle shape rather than AddCarSeatStates' one-shot Triggers),
        // but a scratch capture showed it looking broken no matter where
        // the root was placed: that clip's own hip/leg shape is built for
        // reaching a car's low wheel with legs stretched flat, not for
        // sitting upright with bent knees -- Feedback confirmed
        // it ("make him sit on his but[t]"). PlayerLocomotion.
        // ApplySittingPose now bends the real leg bones directly instead,
        // the same procedural-pose idiom ApplyCastingPose/
        // ApplyDrivingWheelPose already use for "no real clip fits" --
        // nothing for this editor script to build.

        /// <summary>
        /// The charge-up pose for a held jump. Saints Row 4's own charge
        /// jump, with no Godot equivalent to port -- see
        /// PlayerLocomotion.ChargedJumpHeightMultiplier for the height math
        /// this feeds into.
        ///
        /// ENTRY GATED TO STANDING STILL, same reasoning and same threshold
        /// as the crouch's old fold/unfold pose had -- a real reported bug
        /// ("legs still move while holding space if you are moving while
        /// charging your jump"): this is a single fixed held pose, not a
        /// blend over Speed/MoveX/MoveZ like Locomotion, so entering it
        /// while already moving froze the legs mid-stride for as long as
        /// space stayed held. Already-moving-then-charging now stays in
        /// Locomotion for exactly this reason.
        ///
        /// ONLY THE ENTRY IS GATED -- a charge that starts standing still
        /// and THEN moves stays frozen in the held pose until release,
        /// deliberately, not merely unhandled. A matching Speed-gated EXIT
        /// was tried and measurably broke jump releases: with it wired,
        /// releasing a charge on the same frame movement starts lands Speed
        /// above the threshold and fires the Jump trigger simultaneously,
        /// and Unity checks a state's own transitions before any-state ones
        /// (see below), so the Speed exit fired instead of the any-state
        /// Jump transition -- and the pending trigger was gone by the next
        /// frame rather than still waiting to be consumed, so the jump was
        /// silently swallowed. AnimatorConditionMode.IfNot cannot gate on a
        /// Trigger parameter to defer to it either (confirmed by Unity's own
        /// runtime validation error, not assumed) -- avoiding the whole
        /// class of failure by not adding the exit is worth more than
        /// covering the rarer reverse-order case.
        ///
        /// NO WAY OUT IS WIRED HERE, on purpose. The only exit this state
        /// needs is the same "Jump" any-state transition every other state
        /// already has (added just above, if a jump clip exists) -- release
        /// fires the very same Jump trigger, whether the charge paid out or
        /// was cancelled by the ground disappearing out from under it (see
        /// PlayerInputReader.TickJumpCharge's own comment on why a
        /// cancelled charge still reports a release). Giving this state its
        /// own separate "ChargingJump went false" transition back to
        /// Locomotion would race that any-state transition on the exact
        /// frame both are true -- Unity checks a state's OWN transitions
        /// before any-state ones, so an explicit second exit here would win
        /// on every real release and silently swallow the jump.
        /// </summary>
        private static void AddChargedJumpState(AnimatorController controller, AnimatorStateMachine root)
        {
            AnimationClip clip = DampenChargedJumpPose(FindClip("charged_jump"));
            if (clip == null)
            {
                Debug.LogWarning("CityX: no charged_jump clip -- holding space will not show a charging pose.");
                return;
            }

            controller.AddParameter("ChargingJump", AnimatorControllerParameterType.Bool);

            AnimatorState charging = root.AddState("ChargedJump");
            charging.motion = clip;

            AnimatorStateTransition into = root.AddAnyStateTransition(charging);
            into.AddCondition(AnimatorConditionMode.If, 0.0f, "ChargingJump");
            AddSpeedCondition(into, AnimatorConditionMode.Less);
            into.duration = 0.08f;
            into.hasExitTime = false;
            into.canTransitionToSelf = false;
        }

        /// <summary>
        /// How much of the raw retargeted pose survives, GLOBALLY across
        /// every muscle channel, scaled toward TRUE REST (zero) rather than
        /// the clip's own mean -- the opposite knob from ExaggerateStride's
        /// own StrideExaggeration, and a real dead end before it got here.
        ///
        /// MEASURED, NOT GUESSED, IN FOUR WRONG TURNS BEFORE THIS ONE.
        /// CharacterCapture's own bright-light diagnostic (floods the crease
        /// with light far past anything believable, so a true gap --
        /// background showing straight through where there is no surface at
        /// all -- cannot be mistaken for deep shadow) confirmed a real hole
        /// at the armpit in this clip and nowhere else in the existing
        /// sweep. In order: widening FusedBodyShape.WeightFalloffRadii did
        /// nothing (rules out the skin weight blend width);
        /// DiagnoseFusedBodyMeshTopology found ZERO boundary edges in the
        /// base mesh (rules out an actual hole in the geometry -- it is
        /// watertight); forcing double-sided rendering on the skin material
        /// did nothing either (rules out a patch of triangles flipping
        /// inside-out and getting back-face culled); damping just the two
        /// biggest single-joint deltas, the shoulder muscles, from partial
        /// down to a full zero-swing lock, did nothing (rules out one joint
        /// bent too far -- the legs, neck and chest turned out to be moving
        /// by comparable amounts, so one joint was never the whole story);
        /// and scaling the WHOLE pose toward its OWN MEAN, at 50% and then
        /// 10%, ALSO did nothing -- which is what finally exposed the real
        /// bug. This clip is 0.267 seconds, mostly one held pose from very
        /// early on, so its own mean across its own keyframes IS
        /// approximately the extreme pose again, not a stand-in for rest.
        /// Scaling toward zero -- the actual neutral value every Humanoid
        /// muscle curve is defined against -- is what a real reduction in
        /// intensity looks like, and at 0.1 it was confirmed against a
        /// direct baseline: the ORDINARY, already-shipped Rest pose shows a
        /// comparable thin sliver under this same unrealistically bright
        /// diagnostic light, so a pose that reads no worse than Rest here
        /// is not a defect, it is what this camera angle always shows under
        /// light no real scene produces.
        /// </summary>
        private const float ChargedJumpPoseDamping = 0.1f;

        /// <summary>
        /// A duplicate of <paramref name="source"/> with every muscle curve
        /// scaled toward TRUE REST (zero) -- see ChargedJumpPoseDamping's
        /// own comment for why zero and not the clip's own mean, and
        /// ExaggerateStride's own comment for why a duplicate rather than an
        /// in-place edit (the source is an FBX sub-asset, regenerated -- and
        /// this edit silently discarded -- on every reimport).
        /// </summary>
        private static AnimationClip DampenChargedJumpPose(AnimationClip source)
        {
            if (source == null)
            {
                return null;
            }

            var dampened = Object.Instantiate(source);

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                Keyframe[] keys = curve.keys;

                // TOWARD TRUE ZERO, NOT THE CLIP'S OWN MEAN. ExaggerateStride
                // scales around the curve's own mean because a walk cycle
                // oscillates roughly symmetrically around a genuinely
                // neutral stance, so that mean already IS close to rest.
                // This clip is a single short (0.267s) held pose that barely
                // moves once it gets there -- its own mean across its own
                // keyframes is just the extreme pose again, not a stand-in
                // for rest. Scaling toward it changed almost nothing at any
                // factor tried (0.5, then 0.1), which is what exposed this:
                // zero is the actual neutral value every Humanoid muscle
                // curve is defined against, and it is what a duplicate of
                // this rig standing in Rest reads on every one of these
                // channels.
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i].value = Mathf.Clamp(keys[i].value * ChargedJumpPoseDamping, -1.0f, 1.0f);
                    keys[i].inTangent *= ChargedJumpPoseDamping;
                    keys[i].outTangent *= ChargedJumpPoseDamping;
                }
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(dampened, binding, curve);
            }

            return ClipAsset.Store(dampened, $"{AnimationDirectory}/charged_jump_dampened.anim");
        }

        /// <summary>
        /// One transition, with the boilerplate that is the same every time.
        /// Returns the transition it built, so a caller that needs one more
        /// condition than this covers (see AddGunLocomotion's own
        /// AddSpeedCondition calls) can add it without a second, near-
        /// identical helper.
        /// </summary>
        private static AnimatorStateTransition Link(AnimatorState from, AnimatorState to, float duration,
            string parameter, bool wanted, float exitTime = -1.0f)
        {
            if (from == null || to == null)
            {
                return null;
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
            return transition;
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

            // The chain, plus the haymaker, which belongs to no chain but needs
            // a state exactly like theirs -- and the block, which is neither an
            // attack nor part of a chain but reaches the animator the same way.
            var swings = new List<ComboStage>(ComboChain.For(ComboChain.Unarmed))
            {
                CombatTuning.HeavyPunch,
                new ComboStage("Block", 0.0f, 0.0f, 0.0f, false),
            };

            foreach (ComboStage stage in swings)
            {
                if (FindState(root, stage.Animation) != null)
                {
                    continue;
                }
                if (FistStrikes.Has(stage.Animation))
                {
                    continue;   // the unarmed strikes are procedural (FistStrikes / FistPoseDriver): no clip, no state, and the legs keep walking
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
                case "Block": return FindClip("body_block");
                default: return null;
            }
        }

        /// <summary>
        /// A whole-body alternate locomotion set for while a sword is
        /// equipped -- 2hsw_idle/walk/sprint/backwardwalk/left/right were
        /// already sitting retargeted and unused in the Animations folder,
        /// the same "already imported, never wired up" situation
        /// AddAimingState's own doc comment found for pistol_aim_in.fbx and
        /// AddClimbingStates found for its own three clips.
        ///
        /// SAME SHAPE AS THE MAIN LOCOMOTION TREE, not a smaller one --
        /// FreeformCartesian2D over MoveX/MoveZ, the same blend parameters
        /// PlayerLocomotion already feeds every frame regardless of what is
        /// equipped, so nothing about how movement itself works has to
        /// change for this to slot in.
        ///
        /// BOOL-GATED BOTH WAYS, matching AddAimingState's own reasoning
        /// verbatim: there is no other transition this can piggyback on to
        /// get back out, so it needs its own explicit "SwordEquipped went
        /// false" exit.
        ///
        /// DOES NOT TOUCH CROUCH OR JUMP -- a sword-specific crouch/jump
        /// set exists too (2hsw_crouch/uncrouch/jump) but composing three
        /// independent bool/speed gates (Crouching x SwordEquipped x
        /// moving) is a real increase in the transition graph's own size
        /// for a detail nobody asked for; left as a known, deliberate gap
        /// rather than attempted half-way.
        /// </summary>
        private static void AddSwordLocomotion(AnimatorController controller, AnimatorStateMachine root, AnimationClip left, AnimationClip right)
        {
            AnimationClip idle = FindClip("2hsw_idle");
            AnimationClip walk = FindClip("2hsw_walk");
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (idle == null || walk == null || locomotion == null)
            {
                Debug.LogWarning("CityX: sword locomotion clips missing -- " +
                                  "equipping a sword will not change the walk cycle.");
                return;
            }

            AnimationClip sprint = FindClip("2hsw_sprint");
            AnimationClip back = FindClip("2hsw_backwardwalk");

            // Bug report, with a screenshot: "this is the current
            // idle pose ... it should be moving slightly but its frozen."
            // The file was right (2hsw_idle.fbx is byte-identical to the one
            // the user sent) -- the CLIP was imported with loopTime OFF, so
            // it played its 2-second breathing idle exactly once and then held
            // the last frame forever. The plain Locomotion tree loops all of
            // its clips (see Build()); this sword set never did (the
            // "pre-existing gap" noted for 2hsw_walk further up this file's
            // history), which also froze the sword walk/strafe cycles after a
            // single stride.
            MakeLooping(idle);
            MakeLooping(walk);
            MakeLooping(sprint);
            MakeLooping(back);

            controller.AddParameter("SwordEquipped", AnimatorControllerParameterType.Bool);

            controller.CreateBlendTreeInController("SwordLocomotion", out BlendTree swordLocomotion, 0);
            swordLocomotion.blendType = BlendTreeType.FreeformDirectional2D;   // directional for the same reason as Locomotion: no idle in a diagonal
            swordLocomotion.blendParameter = "MoveX";
            swordLocomotion.blendParameterY = "MoveZ";
            swordLocomotion.AddChild(idle, Vector2.zero);
            swordLocomotion.AddChild(walk, new Vector2(0.0f, WalkSpeed));
            if (sprint != null) swordLocomotion.AddChild(sprint, new Vector2(0.0f, SprintSpeed));
            if (back != null) swordLocomotion.AddChild(back, new Vector2(0.0f, -WalkSpeed));
            if (left != null) swordLocomotion.AddChild(left, new Vector2(-WalkSpeed, 0.0f));
            if (right != null) swordLocomotion.AddChild(right, new Vector2(WalkSpeed, 0.0f));
            AddDiagonals(swordLocomotion, walk, back, left, right);

            AnimatorState swordLocomotionState = FindState(root, "SwordLocomotion");

            Link(locomotion, swordLocomotionState, 0.15f, "SwordEquipped", true);
            Link(swordLocomotionState, locomotion, 0.15f, "SwordEquipped", false);
        }

        /// <summary>
        /// THE ONE-HANDED SWORD'S OWN MOVES (owner, 2026-10-17: "make you own sword running and walking animations and make them look good"):
        /// the sword1h_* clips SwordCarryBaker makes from the game's own idle, walk, run and strafes with the right arm carrying the sword
        /// (SwordCarryArm: hanging a little back and out, the blade forward and down clear of the legs, swinging a fraction of the free arm's
        /// swing, in step). The same shape as the plain Locomotion tree at the same places (directional over MoveX/MoveZ, real metres per
        /// second, diagonals as mixes), so the legs, the cadence and the foot placement are exactly the plain walk's and only the arm differs.
        /// BOOL-GATED both ways like the two-handed set, and cross-faded over 0.25 s so drawing or putting away the sword never snaps the
        /// legs; the attacks are posed on top of whatever this plays and their legs hand over to the footwork stepper and back over 0.15 s
        /// (SwordFootwork). (The owner's six 1hsw_* clips of 2026-10-17 were played here before; their arm swung the sword about, and they are
        /// no longer used.)
        /// </summary>
        private static void AddSword1HLocomotion(AnimatorController controller, AnimatorStateMachine root)
        {
            AnimatorState locomotion = FindState(root, "Locomotion");
            AnimationClip idle = SwordCarryBaker.Load("idle");
            AnimationClip walk = SwordCarryBaker.Load("walk");
            AnimationClip run = SwordCarryBaker.Load("run");
            if (idle == null || walk == null || run == null || locomotion == null)
            {
                Debug.LogWarning("CityX: the one-handed sword's carry clips are missing -- a one-handed sword will walk with the plain cycle.");
                return;
            }
            AnimationClip back = SwordCarryBaker.Load("walk_backward");
            AnimationClip left = SwordCarryBaker.Load("walk_left");
            AnimationClip right = SwordCarryBaker.Load("walk_right");   // (all baked looping)

            controller.AddParameter("Sword1HEquipped", AnimatorControllerParameterType.Bool);
            controller.CreateBlendTreeInController("Sword1HLocomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";
            tree.AddChild(idle, Vector2.zero);
            tree.AddChild(walk, new Vector2(0.0f, WalkSpeed));
            tree.AddChild(run, new Vector2(0.0f, SprintSpeed));
            if (back != null) tree.AddChild(back, new Vector2(0.0f, -WalkSpeed));
            if (left != null) tree.AddChild(left, new Vector2(-WalkSpeed, 0.0f));
            if (right != null) tree.AddChild(right, new Vector2(WalkSpeed, 0.0f));
            AddDiagonals(tree, walk, back, left, right);

            AnimatorState state = FindState(root, "Sword1HLocomotion");
            Link(locomotion, state, 0.25f, "Sword1HEquipped", true);
            Link(state, locomotion, 0.25f, "Sword1HEquipped", false);
        }

        /// <summary>
        /// A whole-body alternate locomotion set for while a pistol is
        /// equipped -- pistol_walk/run/walk_backward/walk_left/walk_right
        /// were already sitting retargeted and unused in the Animations
        /// folder, the same "already imported, never wired up" situation
        /// AddSwordLocomotion's own doc comment found for the 2hsw pack.
        ///
        /// Bug report (with the Godot original held up as the
        /// reference to match): "the legs move as you hold the pistol and
        /// aim in if you are walking which it doesnt do in unity."
        /// character.gd's own set_walking() plays this exact clip set any
        /// time _holding_gun is true AND the character is moving -- REGARDLESS
        /// of whether right-click (aiming) is also held, since ordinary
        /// hip-fire movement with a drawn pistol looks the same as aiming
        /// movement does in the source mocap. GATED ON GunEquipped ALONE,
        /// not Aiming, for exactly that reason -- see LocomotionInput.
        /// GunEquipped's own doc comment for the fuller story of why the
        /// "Aiming" Any State transition (see AddAimingState) was the actual
        /// root cause: it fired regardless of movement, freezing the whole
        /// body -- legs included -- on the static standing aim-in pose while
        /// the CharacterController kept sliding the transform forward
        /// underneath it.
        ///
        /// NO ZERO-VELOCITY CHILD, unlike AddSwordLocomotion's own idle --
        /// there is no dedicated "pistol idle" clip in either engine (see
        /// GunEquipped's own comment), and this state is only ever entered
        /// once Speed is already above MovingSpeedThreshold (see the Link
        /// calls below), so the blend tree never actually needs to cover
        /// true zero.
        ///
        /// SPEED-GATED, NOT JUST GunEquipped-GATED, unlike
        /// AddSwordLocomotion's own bool-only Link calls -- there is no
        /// dedicated pistol idle pose to fall back on while standing still
        /// with the gun drawn (ordinary Locomotion idle already covers
        /// that, matching character.gd's own final "else: idle" fallback),
        /// so GunLocomotion has to hand back to Locomotion the moment Speed
        /// drops, not just when the gun itself comes out of the holster.
        /// Hence AddSpeedCondition: a bool alone is not the whole story.
        /// </summary>
        private static void AddGunLocomotion(AnimatorController controller, AnimatorStateMachine root, AnimationClip left, AnimationClip right)
        {
            AnimationClip walk = FindClip("pistol_walk");
            AnimatorState locomotion = FindState(root, "Locomotion");
            if (walk == null || locomotion == null)
            {
                Debug.LogWarning("CityX: pistol_walk clip missing -- holding a pistol will not change the walk cycle.");
                return;
            }

            AnimationClip run = FindClip("pistol_run");
            AnimationClip back = FindClip("pistol_walk_backward");

            // MIXAMO CLIPS ARE SINGLE STRIDES -- see Build()'s own comment on
            // why idle/walk/sprint/etc all get this same treatment. Confirmed
            // directly that AddSwordLocomotion's 2hsw_walk never gets this
            // call either (a pre-existing, unrelated gap -- out of scope
            // here), which would freeze the legs on a single held frame
            // after one stride, the exact OPPOSITE of the explicit ask this
            // method exists to satisfy ("the legs move as you hold the
            // pistol and aim in if you are walking").
            MakeLooping(walk);
            MakeLooping(run);
            MakeLooping(back);

            controller.AddParameter("GunEquipped", AnimatorControllerParameterType.Bool);

            controller.CreateBlendTreeInController("GunLocomotion", out BlendTree gunLocomotion, 0);
            gunLocomotion.blendType = BlendTreeType.FreeformCartesian2D;
            gunLocomotion.blendParameter = "MoveX";
            gunLocomotion.blendParameterY = "MoveZ";
            gunLocomotion.AddChild(walk, new Vector2(0.0f, WalkSpeed));
            if (run != null) gunLocomotion.AddChild(run, new Vector2(0.0f, SprintSpeed));
            if (back != null) gunLocomotion.AddChild(back, new Vector2(0.0f, -WalkSpeed));
            if (left != null) gunLocomotion.AddChild(left, new Vector2(-WalkSpeed, 0.0f));
            if (right != null) gunLocomotion.AddChild(right, new Vector2(WalkSpeed, 0.0f));

            AnimatorState gunLocomotionState = FindState(root, "GunLocomotion");

            AddSpeedCondition(
                Link(locomotion, gunLocomotionState, 0.15f, "GunEquipped", true),
                AnimatorConditionMode.Greater);
            AddSpeedCondition(
                Link(gunLocomotionState, locomotion, 0.15f, "GunEquipped", true),
                AnimatorConditionMode.Less);
            Link(gunLocomotionState, locomotion, 0.15f, "GunEquipped", false);

            // THE SAME MOVING-EXIT "Aiming" ITSELF NEEDS -- see
            // AddAimingState's own doc comment on why its entry is now
            // gated to standing still. Without this, starting to walk while
            // right-click is still held would leave the body stuck on the
            // static aim-in pose (Aiming stays true, so nothing else about
            // this state would otherwise change) until right-click was
            // released, instead of moving straight into this gun-walk cycle
            // the instant Speed crosses the same threshold Locomotion's own
            // entry above already uses.
            //
            // KNOWN MINOR GAP: releasing aim (Aiming -> false) WHILE ALREADY
            // MOVING with the gun still drawn takes AddAimingState's own
            // plain "Aiming false" exit (added first, so Mecanim's
            // first-satisfied-transition-wins rule picks it ahead of these
            // two) straight to Locomotion for one blend, which Locomotion's
            // own GunEquipped-and-moving entry above then immediately
            // re-fires into GunLocomotion -- a
            // double-hop, smoothed over by the short
            // crossfade durations rather than eliminated outright.
            AnimatorState aiming = FindState(root, "Aiming");
            if (aiming != null)
            {
                AnimatorStateTransition outWhileGunMoving = aiming.AddTransition(gunLocomotionState);
                outWhileGunMoving.AddCondition(AnimatorConditionMode.If, 0.0f, "GunEquipped");
                AddSpeedCondition(outWhileGunMoving, AnimatorConditionMode.Greater);
                outWhileGunMoving.duration = 0.12f;
                outWhileGunMoving.hasExitTime = false;

                AnimatorStateTransition outWhileMoving = aiming.AddTransition(locomotion);
                outWhileMoving.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "GunEquipped");
                AddSpeedCondition(outWhileMoving, AnimatorConditionMode.Greater);
                outWhileMoving.duration = 0.12f;
                outWhileMoving.hasExitTime = false;
            }
        }

        /// <summary>
        /// One state per sword-chain animation, named to match
        /// ComboStage.Animation -- same "no triggers, no conditions in"
        /// shape AddPunchStates already uses, and the same reason: the
        /// chain (here ComboChain.Sword, driven by MeleeAttacker exactly
        /// like unarmed is) already decides which swing plays and when.
        ///
        /// EXITS TO SwordLocomotion, NOT Locomotion -- unlike a bare-fisted
        /// swing, the sword is still in hand once the animation ends, so
        /// the body should settle back into the sword's own idle/walk
        /// rather than popping the weapon out of frame into the bare-handed
        /// cycle for one frame before SwordEquipped's own transition (which
        /// only fires on a CHANGE, not every frame) catches up.
        /// </summary>
        private static void AddSwordStates(AnimatorStateMachine root)
        {
            AnimatorState swordLocomotion = FindState(root, "SwordLocomotion");
            if (swordLocomotion == null)
            {
                return;   // AddSwordLocomotion already warned why.
            }

            var swings = new List<ComboStage>(ComboChain.For(ComboChain.Sword))
            {
                CombatTuning.SwordHeavyAttack,
                new ComboStage("SwordBlock", 0.0f, 0.0f, 0.0f, false),
            };

            foreach (ComboStage stage in swings)
            {
                if (FindState(root, stage.Animation) != null)
                {
                    continue;
                }

                AnimationClip clip = ClipForSword(stage.Animation);
                if (clip == null)
                {
                    Debug.LogWarning($"CityX: no clip for '{stage.Animation}' -- that stage of the " +
                                     "sword chain will still hit, but the character will not swing.");
                    continue;
                }

                AnimatorState state = root.AddState(stage.Animation);
                state.motion = clip;

                AnimatorStateTransition back = state.AddTransition(swordLocomotion);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.12f;
            }
        }

        // Shared with PlayerAnimationDriver, which routes sword swings and the
        // guard to this layer by asking the Animator which layer HAS the state.
        public const string UpperBodyLayerName = PlayerAnimationDriver.UpperBodyLayerName;
        public const string UpperEmptyStateName = PlayerAnimationDriver.UpperEmptyStateName;
        public const string UpperSpeedParameterName = PlayerAnimationDriver.UpperSpeedParameterName;
        private const string UpperBodyMaskPath = "Assets/CityX/Character/UpperBodyMask.mask";

        /// <summary>
        /// THE SWORD'S SWINGS AND GUARD PLAY ON THE UPPER BODY ONLY, over the
        /// top of the walk cycle -- Requested: "make the legs walk and not
        /// freeze up during sword use too." The Mixamo swing clips are
        /// full-body: played on the base layer they overwrite the legs with a
        /// planted lunge, so the body froze mid-stride for the whole swing (or,
        /// once movement was allowed, slid along in that frozen pose). Same
        /// answer the pistol got (GunLocomotion): don't fight the legs. This
        /// layer masks to spine/head/arms/hands, so the base layer keeps
        /// driving hips-down.
        ///
        /// TWO THINGS ABOUT IT ARE LOAD-BEARING:
        ///  * The default state is EMPTY. An empty state in an Override layer
        ///    lets the base layer through, so blending a swing back to
        ///    "UpperEmpty" is a clean hand-back to the walk cycle's own arms.
        ///  * SPEED IS A PER-STATE PARAMETER (UpperSpeed), not Animator.speed.
        ///    SoulsSwordCombat stretches each swing to fit its own timing, and
        ///    Animator.speed would ALSO speed the legs up 1.5x while swinging.
        /// </summary>
        private static void EnsureUpperSpeedParameter(AnimatorController controller)
        {
            foreach (AnimatorControllerParameter existing in controller.parameters)
            {
                if (existing.name == UpperSpeedParameterName)
                {
                    return;
                }
            }
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = UpperSpeedParameterName,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1.0f,
            });
        }

        private const string TorsoLayerName = PlayerAnimationDriver.TorsoLayerName;
        private const string ArmsLayerName = PlayerAnimationDriver.ScreamArmsLayerName;

        /// <summary>
        /// THE POISON BREATH'S SCREAM (key 4), on two masked layers so the legs and hips keep walking under it: one for the TORSO AND HEAD (the bow and
        /// the head thrown back and forward), one for the ARMS. The arms are a layer of their own, below the sword's upper-body layer, because the
        /// arms are often busy with something else: a sword swing or guard on the upper-body layer simply wins them, and the gun-aim and cast poses
        /// are applied after the Animator anyway. So the arms do the scream's own arm motion only when nothing else is using them.
        /// Both play at the caster's speed through the shared UpperSpeed parameter and leave to an empty state by an exit-time transition, like the swings.
        /// </summary>
        private static void AddScreamLayers(AnimatorController controller)
        {
            AnimationClip clip = FindClip("poison_breath_scream");
            if (clip == null)
            {
                Debug.LogWarning("CityX: no scream clip -- the poison breath will not play its animation.");
                return;
            }
            EnsureUpperSpeedParameter(controller);
            AddScreamLayer(controller, TorsoLayerName, "PoisonBreath", clip, "Assets/CityX/Character/TorsoHeadMask.mask",
                AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head);
            AddScreamLayer(controller, ArmsLayerName, "PoisonBreathArms", clip, "Assets/CityX/Character/ScreamArmsMask.mask",
                AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers);
        }

        private static void AddScreamLayer(AnimatorController controller, string layerName, string stateName, AnimationClip clip, string maskPath,
            params AvatarMaskBodyPart[] parts)
        {
            AvatarMask mask = Mask(maskPath, parts);

            controller.AddLayer(layerName);
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer layer = layers[layers.Length - 1];
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 1.0f;
            layer.iKPass = false;

            AnimatorStateMachine machine = layer.stateMachine;
            AnimatorState empty = machine.AddState(layerName + "Empty");
            machine.defaultState = empty;
            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;
            state.speedParameterActive = true;
            state.speedParameter = UpperSpeedParameterName;
            AnimatorStateTransition back = state.AddTransition(empty);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.15f;
            controller.layers = layers;
        }

        private static void AddSwordUpperBodyLayer(AnimatorController controller)
        {
            EnsureUpperSpeedParameter(controller);

            AvatarMask mask = BuildUpperBodyMask();

            controller.AddLayer(UpperBodyLayerName);
            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer layer = layers[layers.Length - 1];
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.defaultWeight = 1.0f;
            layer.iKPass = false;

            AnimatorStateMachine machine = layer.stateMachine;
            AnimatorState empty = machine.AddState(UpperEmptyStateName);
            machine.defaultState = empty;

            foreach (string swing in new[] { "sword_attack", "sword_attack2", "sword_charged_attack", "SwordBlock" })
            {
                AnimationClip clip = ClipForSword(swing);
                if (clip == null)
                {
                    continue;
                }

                AnimatorState state = machine.AddState(swing);
                state.motion = clip;
                state.speedParameterActive = true;
                state.speedParameter = UpperSpeedParameterName;

                // Same exit as the base-layer copy: back to nothing, and the
                // base layer's arms show through again.
                AnimatorStateTransition back = state.AddTransition(empty);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.12f;
            }

            controller.layers = layers;
        }

        private static AvatarMask BuildUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            foreach (AvatarMaskBodyPart part in System.Enum.GetValues(typeof(AvatarMaskBodyPart)))
            {
                if (part == AvatarMaskBodyPart.LastBodyPart)
                {
                    continue;
                }
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                    || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm
                    || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers
                    || part == AvatarMaskBodyPart.LeftHandIK || part == AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        /// <summary>
        /// Maps a sword-chain animation name to the Mixamo file that
        /// provides it -- same explicit-map reasoning ClipForPunch's own
        /// doc comment gives.
        /// </summary>
        private static AnimationClip ClipForSword(string animation)
        {
            switch (animation)
            {
                case "sword_attack": return FindClip("2hsw_attack");
                case "sword_attack2": return FindClip("2hsw_attack2");
                case "sword_charged_attack": return FindClip("2hsw_chargedattack");
                case "SwordBlock": return FindClip("2hsw_blocking");
                case "PoisonBreath": return FindClip("poison_breath_scream");   // the Scream animation: the poison breath ability (key 4)
                default: return null;
            }
        }

        private static AnimationClip[] _bloodrageOriginals = new AnimationClip[0];
        private static AnimationClip[] _bloodrageReplacements = new AnimationClip[0];

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
                root.AddComponent<PlayerClimbing>();
                root.AddComponent<PlayerVaulting>();
                root.AddComponent<WallStick>();
                root.AddComponent<PlayerVitals>();
                MeleeAttacker melee = root.AddComponent<MeleeAttacker>();
                // player.gd's three punch impact samples, the same files. A
                // serialized array rather than a Resources load, so a missing
                // clip shows up as an empty slot on the prefab instead of a
                // silent null at runtime.
                melee.ImpactSounds = LoadImpactSounds();
                AddAudio(root);
                root.AddComponent<PlayerAnimationDriver>();
                BloodrageAbility bloodrage = root.AddComponent<BloodrageAbility>();
                bloodrage.OriginalClips = _bloodrageOriginals;
                bloodrage.BloodrageClips = _bloodrageReplacements;
                bloodrage.AuraPrefab = BloodrageAuraBuilder.Build();
                melee.SpinSlashMaterial = SwordSlashMaterialBuilder.Ensure();
                // Makes the body be whoever was chosen in the menu, and does
                // nothing at all when the world is opened directly -- which is
                // how it is opened most of the time.
                root.AddComponent<CharacterIdentity>();

                // Built BEFORE the input reader and after the capsule, because
                // it needs the CharacterController to exclude from its own
                // collisions and it hangs bodies off the instantiated skeleton.
                RagdollBuilder.Build(root, animator);

                // After the ragdoll, so a death throws the body down before it is taken apart (docs/DISMEMBERMENT.md).
                root.AddComponent<Dismemberer>();
                // How cold the body is: a frost blow slows it and, full, freezes it (docs/FROST.md).
                root.AddComponent<Chill>();

                // Added last: it looks for the others in Awake.
                PlayerInputReader reader = root.AddComponent<PlayerInputReader>();
                // The push-ups keep time with their own clips (PushUpEmote).
                AnimationClip pushUpEnter = FindClip("pushup_enter"), pushUpRep = FindClip("pushup"), pushUpExit = FindClip("pushup_exit");
                if (pushUpEnter != null && pushUpRep != null && pushUpExit != null)
                {
                    reader.SetPushUpClipLengths(pushUpEnter.length, pushUpRep.length, pushUpExit.length);
                }

                // DELIBERATELY NOT NetworkObject/NetworkTransform/
                // NetworkedPlayerIdentity/NetworkedAnimatorSync here. Tried
                // that first; it broke ~80 pre-existing PlayMode tests that
                // Object.Instantiate this exact prefab directly with no
                // FishNet spawn pipeline or NetworkManager involved at all
                // (the vast majority of this project's own test suite, all
                // written before networking existed) -- FishNet's own
                // NetworkObject.Awake() unconditionally logs an error
                // ("is expected to be initialized but was not") for ANY
                // instance that is neither a real scene object (a baked,
                // SceneId-bearing placement) nor spawned through FishNet's
                // own object pool (NetworkManager.GetPooledInstantiated),
                // and Unity's Test Framework fails a test on any unexpected
                // error log. A plain Object.Instantiate always hits that
                // exact gap, no matter how the prefab was reserialized.
                // See NetworkedPlayerSpawner's own doc comment for where the
                // networking components actually get added instead: the ONE
                // baked scene instance directly (a genuine scene object,
                // which does get a valid SceneId), and a SEPARATE derived
                // prefab used only for spawned clones (never this shared
                // one, so every existing single-player test keeps
                // Instantiating a completely plain Player prefab, exactly
                // as before this feature ever existed).
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// THE FOUR DIAGONALS AS SAMPLES OF THEIR OWN (owner, 2026-10-11: W+A and W+D walked "in like slow motion"). With only the four straight
        /// walks round the idle, a diagonal at walking speed fell between them and took a share of the idle -- 23% in the Cartesian blend, 12%
        /// still in the directional one -- and the idle's long clip stretched one step cycle from 1.03 s to 3 s and 2 s at the same ground speed.
        /// Each diagonal is now its own sample at walking speed: a small 1-D blend of the straight walk and the side-step by MoveX, so a diagonal
        /// lands on a walking mix with no idle in it. The walk and strafe clips both cycle in 1.03 s, so the diagonal steps at that pace too
        /// (DiagonalWalkCadencePlayTests).
        /// </summary>
        private static void AddDiagonals(BlendTree tree, Motion forward, Motion back, Motion left, Motion right, float speed = WalkSpeed)
        {
            float d = speed * Mathf.Sqrt(0.5f);
            AddDiagonal(tree, "ForwardLeft", forward, left, -1.0f, new Vector2(-d, d), speed);
            AddDiagonal(tree, "ForwardRight", forward, right, 1.0f, new Vector2(d, d), speed);
            AddDiagonal(tree, "BackLeft", back, left, -1.0f, new Vector2(-d, -d), speed);
            AddDiagonal(tree, "BackRight", back, right, 1.0f, new Vector2(d, -d), speed);
        }

        private static void AddDiagonal(BlendTree tree, string name, Motion straight, Motion side, float sideSign, Vector2 position, float speed)
        {
            if (straight == null || side == null)
            {
                return;
            }
            BlendTree mix = tree.CreateBlendTreeChild(position);
            mix.name = name;
            mix.blendType = BlendTreeType.Simple1D;
            mix.blendParameter = "MoveX";
            mix.useAutomaticThresholds = false;
            // A 1-D blend's thresholds go up: the side-step first when it is to the left.
            if (sideSign < 0.0f)
            {
                mix.AddChild(side, -speed);
                mix.AddChild(straight, 0.0f);
            }
            else
            {
                mix.AddChild(straight, 0.0f);
                mix.AddChild(side, speed);
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

        /// <summary>
        /// The baked Godot clip of that name if there is one, otherwise the Mixamo clip retargeted through the humanoid avatar.
        /// </summary>
        private static AnimationClip GodotOrMixamo(string godotName, string mixamoName)
        {
            AnimationClip baked = GodotMotionBaker.Load(godotName);
            return baked != null ? baked : FindClip(mixamoName);
        }

        private static bool IsBakedGodotClip(AnimationClip clip)
        {
            return clip != null && AssetDatabase.GetAssetPath(clip).StartsWith(GodotMotionBaker.OutputDirectory, System.StringComparison.Ordinal);
        }

        private static AnimationClip ExaggerateStrideIfMixamo(AnimationClip clip, string modelName)
        {
            return IsBakedGodotClip(clip) ? clip : ExaggerateStride(clip, modelName);
        }

        /// <summary>
        /// How much wider than the raw retargeted swing the leg motion is stretched, for the Mixamo clips only. 1.0 (no stretch): an earlier 1.8 was added to cure a
        /// stride that looked short and measured, on the sprint, as thighs swinging 130 degrees each way where the Godot character's own clip swings 66 forward and 40 back.
        /// </summary>
        private const float StrideExaggeration = 1.0f;

        /// <summary>
        /// The muscle channels that actually carry a walking stride's knee
        /// and hip swing. Deliberately narrow -- this is a fix for the leg
        /// motion the retargeting proportion mismatch visibly compresses,
        /// not a general "make the animation bigger" knob.
        /// </summary>
        private static readonly string[] StrideMuscles =
        {
            "Left Upper Leg Front-Back", "Right Upper Leg Front-Back",
            "Left Lower Leg Stretch", "Right Lower Leg Stretch",
        };

        /// <summary>
        /// A duplicate of <paramref name="source"/> with its leg-swing muscle
        /// curves widened around their own mean, so the average pose (and
        /// therefore ground contact and the blend tree's speed-to-stride
        /// matching) is unchanged -- only how far the leg swings each way.
        ///
        /// A DUPLICATE, NOT AN IN-PLACE EDIT: the source clip is an FBX
        /// sub-asset, and its curves are regenerated from the model file on
        /// every reimport, so any edit made directly on it would be silently
        /// discarded the next time the clip gets reimported.
        ///
        /// <paramref name="modelName"/> names the destination asset rather
        /// than reusing <c>source.name</c> -- every Mixamo clip in this set
        /// shares the same internal clip name ("mixamo.com"), so keying off
        /// it would make every call collide on one path.
        /// </summary>
        private static AnimationClip ExaggerateStride(AnimationClip source, string modelName)
        {
            if (source == null)
            {
                return null;
            }

            var exaggerated = Object.Instantiate(source);

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                if (System.Array.IndexOf(StrideMuscles, binding.propertyName) < 0)
                {
                    continue;
                }

                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                Keyframe[] keys = curve.keys;
                float mean = 0.0f;
                foreach (Keyframe key in keys)
                {
                    mean += key.value;
                }
                mean /= keys.Length;

                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i].value = Mathf.Clamp(
                        mean + (keys[i].value - mean) * StrideExaggeration, -1.0f, 1.0f);
                    keys[i].inTangent *= StrideExaggeration;
                    keys[i].outTangent *= StrideExaggeration;
                }
                curve.keys = keys;
                AnimationUtility.SetEditorCurve(exaggerated, binding, curve);
            }

            return ClipAsset.Store(exaggerated, $"{AnimationDirectory}/{modelName}_stride.anim");
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
