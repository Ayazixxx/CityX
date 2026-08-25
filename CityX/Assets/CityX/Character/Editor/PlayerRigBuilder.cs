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
            walk = ExaggerateStride(walk, "walking");
            sprint = ExaggerateStride(sprint, "sprint");
            back = ExaggerateStride(back, "walk_backward");
            left = ExaggerateStride(left, "walk_left");
            right = ExaggerateStride(right, "walk_right");

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
            AddChargedJumpState(controller, root);
            AddAimingState(controller, root);
            AddPunchStates(root);
            AddCutsceneStates(root);

            EditorUtility.SetDirty(controller);
            return controller;
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

        /// <summary>How long a crouch takes to fold or unfold. character.gd's own.</summary>
        private const float CrouchBlendSeconds = 0.2f;

        /// <summary>
        /// Below this the body counts as standing still, for any Animator
        /// state that only makes sense while stationary -- the crouch
        /// fold/unfold pose (see AddCrouchStates' own doc comment) and the
        /// charged-jump pose (see AddChargedJumpState's own doc comment)
        /// both gate on it. Small rather than zero so residual float noise
        /// from a stopped body can never read as "still moving."
        /// </summary>
        private const float MovingSpeedThreshold = 0.1f;

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
        ///
        /// THE FOLD/UNFOLD POSE ONLY EVER PLAYS WHILE STANDING STILL -- ported
        /// from character.gd's own start_crouch()/end_crouch(), whose class
        /// comments say this in as many words: "Firing this while the player
        /// is ALREADY moving is fine: set_walking's own 'crouching' branch
        /// plays crouch_walk instead the very same/next frame, so this
        /// transition only actually gets seen through to completion while
        /// standing still." Godot re-evaluates which clip to play from
        /// scratch every physics frame, so that fallthrough is implicit
        /// there. Unity's graph has no equivalent per-frame re-decision, so
        /// it needs its own explicit transitions: without them, crouching (or
        /// un-crouching) while already walking sat through the WHOLE
        /// standing_to_crouch/crouch_to_idle clip -- a real reported bug
        /// ("crouch walking isn't working right") -- with the body sliding
        /// across the ground under a fold animation that was never meant to
        /// carry any actual travel. Gated on Speed rather than a separate
        /// "Moving" bool because Speed is already fed every Tick and reads
        /// zero at rest to well within MovingSpeedThreshold.
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

            // EACH OF THESE FOUR IS GATED TO STANDING STILL (Speed below the
            // threshold), made explicit rather than left to transition
            // ordering -- Mecanim does not prefer the more specific of two
            // simultaneously-satisfied transitions from the same state, it
            // takes whichever was added first, so without this the moving
            // versions added below would never be reached at all while
            // Crouching was also true. See this method's own doc comment.
            AddSpeedCondition(
                Link(locomotion, downState, CrouchBlendSeconds, "Crouching", true),
                AnimatorConditionMode.Less);
            Link(downState, crouchedState, 0.1f, null, false, exitTime: 0.9f);

            // A crouch abandoned before it finishes still has to come back up,
            // or holding and releasing quickly leaves the body folded.
            Link(downState, upState, CrouchBlendSeconds, "Crouching", false);
            AddSpeedCondition(
                Link(crouchedState, upState, CrouchBlendSeconds, "Crouching", false),
                AnimatorConditionMode.Less);
            Link(upState, locomotion, 0.1f, null, false, exitTime: 0.9f);

            // And crouching again mid-stand goes straight back down rather than
            // waiting out the rest of the stand.
            Link(upState, downState, CrouchBlendSeconds, "Crouching", true);

            // --- moving skips the fold/unfold pose entirely, see this
            // --- method's own doc comment for why.
            AddSpeedCondition(
                Link(locomotion, crouchedState, CrouchBlendSeconds, "Crouching", true),
                AnimatorConditionMode.Greater);
            AddSpeedCondition(
                Link(downState, crouchedState, CrouchBlendSeconds, "Crouching", true),
                AnimatorConditionMode.Greater);
            AddSpeedCondition(
                Link(crouchedState, locomotion, CrouchBlendSeconds, "Crouching", false),
                AnimatorConditionMode.Greater);
            AddSpeedCondition(
                Link(upState, locomotion, CrouchBlendSeconds, null, false),
                AnimatorConditionMode.Greater);
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
        /// false" exit, matching AddCrouchStates' own bidirectional Link
        /// pattern instead.
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
            into.duration = 0.12f;
            into.hasExitTime = false;
            into.canTransitionToSelf = false;

            AnimatorStateTransition outOf = aiming.AddTransition(locomotion);
            outOf.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "Aiming");
            outOf.duration = 0.15f;
            outOf.hasExitTime = false;
        }

        /// <summary>
        /// The charge-up pose for a held jump. Saints Row 4's own charge
        /// jump, with no Godot equivalent to port -- see
        /// PlayerLocomotion.ChargedJumpHeightMultiplier for the height math
        /// this feeds into.
        ///
        /// ENTRY GATED TO STANDING STILL, same reasoning and same threshold
        /// as AddCrouchStates' own fold/unfold pose -- a real reported bug
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
            dampened.name = "charged_jumpDampened";

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

            string path = $"{AnimationDirectory}/charged_jump_dampened.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(dampened, path);
            return dampened;
        }

        /// <summary>One transition, with the boilerplate that is the same every time.</summary>
        /// <summary>
        /// Returns the transition it built, so a caller that needs one more
        /// condition than this covers (see AddCrouchStates' own
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
                case "Block": return FindClip("body_block");
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

        /// <summary>How much wider than the raw retargeted swing the leg motion is stretched.</summary>
        private const float StrideExaggeration = 1.8f;

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
            exaggerated.name = modelName + "Stride";

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

            string path = $"{AnimationDirectory}/{modelName}_stride.anim";
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            AssetDatabase.CreateAsset(exaggerated, path);
            return exaggerated;
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
