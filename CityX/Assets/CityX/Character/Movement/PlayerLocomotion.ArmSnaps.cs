using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE ARM-SNAP GUARD: a head or arm bone that turns faster than MaxPlausibleArmDegreesPerSecond from one frame to the next is
    /// held at its last real pose instead (see SuppressImplausibleArmSnaps for the bug it answers).
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>
        /// How fast a watched bone may rotate, in degrees per second, before
        /// SuppressImplausibleArmSnaps holds it at last frame's own real
        /// pose instead of accepting the new one -- the SAME generous bound
        /// PoseDistortionWatchdog.MaxBoneDegreesPerSecond already uses to
        /// flag this exact class of glitch (a real swing's own reach eases
        /// at 240 deg/s; this is meant to catch an instantaneous jump, not
        /// flag ordinary fast movement).
        /// </summary>
        // 1500 until 2026-10-13. The locomotion clips are now paced to the body (ClipCadence: the run plays 1.7x, the walk 2.8x, a backward walk
        // 4x), and a sprint's arm pump at 1.7x passed 1500 on its own; the hold-then-accept then doubled the next frame's step and the watchdog
        // (same bound) cried wolf on an ordinary run. The glitch this catches was 10627 deg/s, so there is room.
        private const float MaxPlausibleArmDegreesPerSecond = 4500.0f;

        private Quaternion _previousHeadRotation = Quaternion.identity;

        private Quaternion _previousRightUpperArmRotation = Quaternion.identity;

        private Quaternion _previousRightLowerArmRotation = Quaternion.identity;

        private Quaternion _previousLeftUpperArmRotation = Quaternion.identity;

        private Quaternion _previousLeftLowerArmRotation = Quaternion.identity;

        /// <summary>
        /// Belongs ONLY to the ordinary per-frame LateUpdate path -- the
        /// separate, on-demand capture path (SuppressImplausibleArmSnapsNow,
        /// used by GrappleSwing.Attach/DevConsoleView.Open) never reads or
        /// writes these, by design; see SuppressImplausibleArmSnapsNowCore's
        /// own doc comment for why sharing this exact state was a real,
        /// shipped bug. If a bone was held last frame, THIS frame accepts
        /// whatever the Animator is doing now unconditionally instead of
        /// re-checking it against the same stale reference, so a real
        /// glitch (self-corrects within a frame or two) still gets hidden,
        /// but a legitimate fast transition can never lock up permanently.
        /// </summary>
        private bool _headSnapHeldLastFrame;

        private bool _rightUpperArmSnapHeldLastFrame;

        private bool _rightLowerArmSnapHeldLastFrame;

        private bool _leftUpperArmSnapHeldLastFrame;

        private bool _leftLowerArmSnapHeldLastFrame;

        /// <summary>True once _previousXxx has been populated for real at least once -- the only case with nothing yet to plausibly compare against.</summary>
        private bool _haveEverCapturedArmSnapBaseline;

        /// <summary>
        /// Bug report, with real screenshots: "the console and
        /// grapple still wildly stretches the body" -- arms twisted into an
        /// X, arms flung up and out. ROOT-CAUSED DIRECTLY: a new PlayMode
        /// test (LocomotionAnimatorStabilityPlayTests) drove realistic,
        /// changing movement input with the Animator completely live and
        /// unmolested -- no console, no grapple, nothing ever disabling
        /// anything -- and caught the locomotion Animator's OWN blend tree
        /// snapping RightLowerArm 10627 degrees/s in a single frame on its
        /// own, specifically while airborne (jump/fall) with changing
        /// input, i.e. mid-transition between locomotion states. Unlike
        /// hips/spine, NOTHING anywhere in this codebase ever corrects an
        /// arm or head bone after the Animator writes it, so a bad
        /// transitional frame like this is exactly what would render --
        /// invisible for the one single frame it lasts during ordinary
        /// unfrozen play, but exactly what a console/grapple freeze
        /// disabling the Animator on that same frame would catch and hold
        /// for as long as the freeze lasts. This is what actually explains
        /// "random" (depends on exact freeze timing vs. the transition's
        /// own timing), "movement-influenced" (only a moving, possibly-
        /// airborne body is ever mid-transition), and "snaps back
        /// instantly" (the very next live Animator.Update recomputes a
        /// clean pose from the current, correct state).
        ///
        /// FIXED HERE RATHER THAN IN THE ANIMATOR CONTROLLER ITSELF: the
        /// bad frame is a Mecanim blend-tree/transition artifact, not a
        /// wrong C# value, so there is no single clip or transition setting
        /// to correct -- the same class of fix hips/spine already uses
        /// (LateUpdate holds a bone to a known-good value every frame,
        /// unconditionally) is reused here instead: if a watched bone
        /// rotates further in one frame than any real intentional motion
        /// plausibly would, HOLD it at its own last real pose rather than
        /// accepting the implausible jump. A real player is never meant to
        /// see the difference either way -- a legitimate fast motion never
        /// approaches this threshold (see MaxPlausibleArmDegreesPerSecond's
        /// own doc comment), so this only ever intervenes on a frame that
        /// was already a glitch, holding the PREVIOUS good frame instead of
        /// however many further frames the same glitch might otherwise
        /// have compounded across.
        ///
        /// SKIPPED WHILE A REAL MELEE SWING IS IN PROGRESS -- a sword
        /// swing's own crossfade genuinely can move an arm fast over a
        /// short blend, and this project has no measured, trustworthy
        /// upper bound on that the way PoseDistortionWatchdog's own 1500
        /// deg/s figure is measured against ordinary locomotion/swing-pose
        /// motion specifically -- suppressing a real swing's own arm speed
        /// would be a new, self-inflicted regression, not a fix.
        ///
        /// SKIPPED ENTIRELY WHILE MeleeAttacker/GrappleSwing OWN THESE SAME
        /// BONES -- Bug, found live (broke two established
        /// GrappleSwing tests the first time this shipped): Unity does not
        /// guarantee this component's own LateUpdate runs before or after
        /// GrappleSwing's, and if this one ran FIRST, WRITING a bone back
        /// to "last frame's value" here is exactly what GrappleSwing's own
        /// release blend then read back as ITS OWN blend target, gluing
        /// the blend in place instead of letting it ease toward the live
        /// animator pose.
        ///
        /// TRACKING (updating _previousXxx without correcting, rather than
        /// skipping outright) WAS TRIED AND REVERTED -- Bug,
        /// found live via a new randomised PlayMode test: while
        /// GrappleSwing/MeleeAttacker actively drive these bones, the
        /// value sitting on them is THEIR OWN pose, not the Animator's --
        /// a fundamentally different reference frame. Tracking it as if it
        /// were "the Animator's last plausible value" broke
        /// RepeatedlyFiringAndReleasingNeverDriftsTheCapturedArmBase: a
        /// rapid re-fire's freshly re-evaluated Animator pose looked
        /// "implausible" only because it was being compared against the
        /// FIRST swing's own leftover reach pose. Skipping outright avoids
        /// this entirely -- there is nothing useful to compare a future
        /// Animator transition against here anyway, since nothing legitimately
        /// moves these bones frame-to-frame while the Animator is disabled
        /// and nothing else owns them (a plain console freeze) either.
        /// GetComponent, not a cached field bound once in Bind() --
        /// GrappleSwing is added/removed dynamically as the hook is
        /// equipped/unequipped, well after Bind() ever runs.
        /// </summary>
        private void SuppressImplausibleArmSnaps()
        {
            if (_melee != null && _melee.IsSwinging)
            {
                return;
            }

            // THE SOULSLIKE SWORD DRIVES THE SAME ARM BONES THROUGH ITS OWN
            // (time-stretched) swing clips, for the same reason a
            // MeleeAttacker swing is exempt above. Looked up lazily because
            // SwordWeapon.OnEquip adds it at runtime, long after Bind().
            if (_soulsSword == null)
            {
                _soulsSword = GetComponent<SoulsSwordCombat>();
            }
            if (_soulsSword != null && _soulsSword.IsCommitted)
            {
                return;
            }

            // A baked move (ApplyBakedPose) swings the arms faster than any clip on purpose -- Octaslash's whirl is 90 degrees a frame -- and sets
            // every bone itself afterwards anyway.
            if (_bakedPoseWeight > 0.0f)
            {
                return;
            }

            // GrappleSwing used to need an exclusion here too -- it owned
            // these same bones directly while a rope was attached. Rebuilt
            // from square one to never touch a bone at all (see its own
            // class doc comment), so a swing is just ordinary Animator
            // output now, exactly like a fall, and this correction applies
            // to it unconditionally like everything else.

            // NOTE: gun-aiming does NOT get a blanket exclusion here the
            // way swing/melee do -- see SuppressImplausibleArmSnapsCore's
            // own doc comment for why a blanket skip was tried and
            // reverted, and why only the ONE bone ApplyGunAimPose actually
            // touches (_rightUpperArm) is excluded, inside Core, instead.
            SuppressImplausibleArmSnapsCore();
        }

        /// <summary>
        /// Gap, found live after the user reported the distortion
        /// still happening post-fix: SuppressImplausibleArmSnaps only ever
        /// runs from THIS component's own LateUpdate, but GrappleSwing.
        /// Attach and DevConsoleView.Open both force an EXTRA, synchronous
        /// zero-delta `Animator.Update(0f)` evaluation of their own --
        /// called from Update(), well before this frame's own LateUpdate
        /// would otherwise run -- specifically to warm up an
        /// never-evaluated Animator out of its raw bind pose (see either
        /// method's own doc comment). That forced evaluation can land on
        /// the exact same kind of implausible mid-transition blend-tree
        /// snap SuppressImplausibleArmSnaps exists to catch, and both
        /// GrappleSwing's CaptureBase and the console's freeze read the
        /// bone straight off it with no correction ever applied -- meaning
        /// a swing's ENTIRE held pose (or the console's whole frozen
        /// pose) could be built on top of one bad snap for as long as it
        /// stays engaged, not just a single invisible frame. Called
        /// manually, right after that forced Update(0f) and before
        /// anything reads the bones it just wrote.
        ///
        /// COMPARES AGAINST THE PERSISTENT _previousXxx STATE, NEVER A
        /// FRESH READ TAKEN RIGHT BEFORE THE FORCED UPDATE -- a
        /// `[UnityTest]` coroutine (and, by the same reasoning,
        /// PlayerInputReader's own real Update dispatch) resumes AFTER
        /// that frame's own automatic Mecanim evaluation but BEFORE that
        /// frame's own LateUpdate (see LocomotionAnimatorStabilityPlayTests's
        /// own doc comment for the full, separately-confirmed story) --
        /// meaning a bone read at the exact moment Attach/Open gets called
        /// can ALREADY be sitting on this frame's own raw, not-yet-corrected
        /// transitional value. _previousXxx is safe from this: it is only
        /// ever written from inside SuppressImplausibleArmSnaps's own
        /// LateUpdate call, i.e. AFTER a frame's Animation phase has
        /// already been corrected, so it always holds a genuinely safe,
        /// already-rendered reference from the last ordinary frame
        /// (SuppressImplausibleArmSnaps skips entirely while ownership is
        /// elsewhere, so this can occasionally be a few frames old across
        /// a swing -- acceptable here, since Attach/Open only calls this
        /// once, right at the ownership boundary, not every frame).
        ///
        /// SKIPS THE MeleeAttacker/GrappleSwing OWNERSHIP CHECKS ON
        /// PURPOSE, unlike the LateUpdate version -- this runs at the
        /// exact moment GrappleSwing is ABOUT to take ownership (IsActive
        /// is already true by the time Attach calls this) and needs a
        /// clean bone to capture as its own base, not during its own
        /// ongoing ownership.
        /// </summary>
        public void SuppressImplausibleArmSnapsNow()
        {
            SuppressImplausibleArmSnapsNowCore();
        }

        private void RememberCurrentArmRotationsWithoutCorrecting()
        {
            _previousHeadRotation = _head != null ? _head.localRotation : Quaternion.identity;
            _previousRightUpperArmRotation = _rightUpperArm != null ? _rightUpperArm.localRotation : Quaternion.identity;
            _previousRightLowerArmRotation = _rightLowerArm != null ? _rightLowerArm.localRotation : Quaternion.identity;
            _previousLeftUpperArmRotation = _leftUpperArm != null ? _leftUpperArm.localRotation : Quaternion.identity;
            _previousLeftLowerArmRotation = _leftLowerArm != null ? _leftLowerArm.localRotation : Quaternion.identity;
            _headSnapHeldLastFrame = false;
            _rightUpperArmSnapHeldLastFrame = false;
            _rightLowerArmSnapHeldLastFrame = false;
            _leftUpperArmSnapHeldLastFrame = false;
            _leftLowerArmSnapHeldLastFrame = false;
            _haveEverCapturedArmSnapBaseline = true;
        }

        /// <summary>
        /// Shared by both the ordinary per-frame LateUpdate path and the
        /// manual "Now" path. Guarded by _haveEverCapturedArmSnapBaseline
        /// for the one true edge case: the literal first call this
        /// component ever makes, with nothing yet to plausibly compare
        /// against.
        ///
        /// _rightUpperArm AND _head ARE SKIPPED SPECIFICALLY, NOT THE WHOLE
        /// METHOD -- Regression, found by the full suite: a first
        /// version of this gun-aim exclusion skipped this entire method
        /// whenever ApplyGunAimPose's own blend was nonzero, the same
        /// "something else legitimately owns these bones" shape the
        /// swing/melee exclusions in SuppressImplausibleArmSnaps use. That
        /// is too broad here -- ApplyGunAimPose only ever rotates
        /// _rightUpperArm and _head (head added later, see its own doc
        /// comment on the "angled left" head-cant bug), never
        /// leftArm/rightLowerArm, and RealisticChangingMovementNever
        /// SnapsAnArmOrHeadBoneOnItsOwn's own randomised input flips Aiming
        /// on a 20%-per-frame coin flip that keeps the gun-aim blend
        /// hovering above zero for long stretches (unrealistic for a real
        /// player holding right-click, but exactly what that stress test
        /// does) -- disabling the WHOLE check for that long let the
        /// pre-existing, already-documented rare Mecanim blend-tree glitch
        /// this test exists to catch (on the LEFT arm, or even
        /// RightLowerArm, neither of which gun-aiming ever touches) sneak
        /// through uncorrected. Excluding only the bones actually being
        /// driven keeps every other bone's own protection fully intact.
        ///
        /// THE HEAD'S OWN EXCLUSION IS BROADER THAN THE ARM'S, DELIBERATELY
        /// -- ApplyGunWalkHeadPose also drives the head (never the arm)
        /// whenever _gunWalkHeadBlend is positive, the hip-fire case
        /// _gunAimBlend alone does not cover. The right upper arm's own
        /// exclusion stays scoped to _gunAimBlend only, since hip-fire
        /// never touches it.
        /// </summary>
        private void SuppressImplausibleArmSnapsCore()
        {
            if (!_haveEverCapturedArmSnapBaseline)
            {
                RememberCurrentArmRotationsWithoutCorrecting();
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 1.0f / 240.0f);
            if (_gunAimBlend > 0.0f || _gunWalkHeadBlend > 0.0f)
            {
                // Keep the tracked baseline in sync with whatever
                // ApplyGunAimPose/ApplyGunWalkHeadPose just set, rather than
                // comparing against it (or leaving it stale) -- the same
                // "nothing useful to compare against while something else
                // legitimately drives this bone" reasoning
                // RememberCurrentArmRotationsWithoutCorrecting already uses
                // for the bootstrap case, so the FIRST frame after the
                // correction ends has a genuinely fresh, just-rendered
                // reference instead of a stale one.
                if (_head != null)
                {
                    _previousHeadRotation = _head.localRotation;
                }
                _headSnapHeldLastFrame = false;
            }
            else
            {
                SuppressBoneSnap(_head, ref _previousHeadRotation, ref _headSnapHeldLastFrame, deltaTime);
            }
            if (_gunAimBlend > 0.0f)
            {
                if (_rightUpperArm != null)
                {
                    _previousRightUpperArmRotation = _rightUpperArm.localRotation;
                }
                _rightUpperArmSnapHeldLastFrame = false;
            }
            else
            {
                SuppressBoneSnap(_rightUpperArm, ref _previousRightUpperArmRotation, ref _rightUpperArmSnapHeldLastFrame, deltaTime);
            }
            SuppressBoneSnap(_rightLowerArm, ref _previousRightLowerArmRotation, ref _rightLowerArmSnapHeldLastFrame, deltaTime);
            SuppressBoneSnap(_leftUpperArm, ref _previousLeftUpperArmRotation, ref _leftUpperArmSnapHeldLastFrame, deltaTime);
            SuppressBoneSnap(_leftLowerArm, ref _previousLeftLowerArmRotation, ref _leftLowerArmSnapHeldLastFrame, deltaTime);
        }

        /// <summary>
        /// The on-demand capture path (GrappleSwing.Attach/DevConsoleView.Open,
        /// via SuppressImplausibleArmSnapsNow) used to share this exact
        /// correction with the ordinary per-frame path above --
        /// Bug, found live: that sharing meant an UNRELATED hold from the
        /// ordinary path's own most recent LateUpdate (some earlier,
        /// completely different frame-to-frame glitch, nothing to do with
        /// this Attach/Open call at all) could leave its own
        /// "accept-unconditionally-next-call" flag set, and an on-demand
        /// call arriving right then would accept literally ANYTHING with
        /// zero validation -- including a genuinely bad mid-transition
        /// snap, at the EXACT moment (a capture or freeze) this whole
        /// mechanism exists to protect. A bounded RotateTowards catch-up
        /// was tried next to remove that cliff-edge, but that traded away
        /// something just as important: a single bounded correction only
        /// closes PART of a large jump, so a genuinely big glitch (the
        /// 4000-12000+ deg/s snaps this file's own history has repeatedly
        /// measured) still shows through substantially once frozen -- CONFIRMED
        /// live, the user reported zero improvement (and, if anything,
        /// more frequent visible distortion) after that version shipped.
        ///
        /// THE ACTUAL FIX: give this path its OWN independent hold state,
        /// touched by nothing else. A full, unconditional hold here can
        /// never be "wrong" the way the shared-state version was, because
        /// this method only ever runs ONCE per Attach/Open call -- there is
        /// no repeating per-frame process here to get permanently stuck,
        /// so the ordinary path's whole reason for ever accepting a
        /// still-implausible value (round 7's fix) does not apply. A
        /// held-at-last-known-good value is exactly the right, final
        /// answer for a one-shot capture: it can never read raw, unfiltered
        /// live data, and it can never contaminate (or be contaminated by)
        /// the ordinary path's own, separately-tracked recovery flags.
        /// </summary>
        private void SuppressImplausibleArmSnapsNowCore()
        {
            if (!_haveEverCapturedArmSnapBaseline)
            {
                RememberCurrentArmRotationsWithoutCorrecting();
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 1.0f / 240.0f);
            // See SuppressImplausibleArmSnapsCore's own comment on why the
            // head's exclusion is broader than the right upper arm's.
            if (_gunAimBlend > 0.0f || _gunWalkHeadBlend > 0.0f)
            {
                if (_head != null)
                {
                    _previousHeadRotation = _head.localRotation;
                }
            }
            else
            {
                SuppressBoneSnapForCapture(_head, ref _previousHeadRotation, deltaTime);
            }
            if (_gunAimBlend > 0.0f)
            {
                if (_rightUpperArm != null)
                {
                    _previousRightUpperArmRotation = _rightUpperArm.localRotation;
                }
            }
            else
            {
                SuppressBoneSnapForCapture(_rightUpperArm, ref _previousRightUpperArmRotation, deltaTime);
            }
            SuppressBoneSnapForCapture(_rightLowerArm, ref _previousRightLowerArmRotation, deltaTime);
            SuppressBoneSnapForCapture(_leftUpperArm, ref _previousLeftUpperArmRotation, deltaTime);
            SuppressBoneSnapForCapture(_leftLowerArm, ref _previousLeftLowerArmRotation, deltaTime);
        }

        /// <summary>
        /// Full, unconditional hold at `previous` -- no bounding, no
        /// advancing, no flag. See SuppressImplausibleArmSnapsNowCore's own
        /// doc comment for why a one-shot capture call needs none of the
        /// ordinary per-frame path's own recovery machinery.
        /// </summary>
        private static void SuppressBoneSnapForCapture(Transform bone, ref Quaternion previous, float deltaTime)
        {
            if (bone == null)
            {
                return;
            }
            Quaternion current = bone.localRotation;
            float degreesPerSecond = Quaternion.Angle(previous, current) / deltaTime;
            if (degreesPerSecond > MaxPlausibleArmDegreesPerSecond)
            {
                bone.localRotation = previous;
            }
            else
            {
                previous = current;
            }
        }

        /// <summary>
        /// HOLD-THEN-ACCEPT-NEXT-FRAME. Holding a bone at `previous`
        /// outright (leaving `previous` itself untouched) self-corrects a
        /// genuine one-frame Mecanim glitch fine, but a LEGITIMATE fast
        /// transition (landing, finishing a swing, a sharp direction
        /// change -- not a glitch, a real new pose that just arrived in
        /// one frame) trips the exact same check -- without this method's
        /// own `heldLastFrame` flag, it would never recover: every later
        /// frame's own ordinary continuation of that new pose would look
        /// "implausible" against the same, increasingly stale reference
        /// forever. `heldLastFrame` bounds the hold to exactly one frame:
        /// the very next call accepts whatever the Animator is doing now
        /// unconditionally instead of re-checking it, so a real glitch
        /// (self-corrects within a frame or two) still gets hidden, but a
        /// legitimate fast transition can never lock up permanently.
        ///
        /// SAFE TO DO THIS HERE, UNLIKE THE ON-DEMAND CAPTURE PATH --
        /// Bug, found live: an earlier version shared this exact
        /// flag/logic with SuppressImplausibleArmSnapsNow (GrappleSwing.
        /// Attach/DevConsoleView.Open's own one-shot capture call), and an
        /// UNRELATED hold from this method's own most recent, completely
        /// separate LateUpdate call could leave the flag set such that the
        /// next on-demand call accepted literally anything with zero
        /// validation -- at the exact moment (a capture or freeze) that
        /// validation matters most. `heldLastFrame` is now private to THIS
        /// per-frame path alone (see its own field doc comment); the
        /// on-demand path has its own, completely separate, always-strict
        /// SuppressBoneSnapForCapture instead. This method's own
        /// one-frame grace period is safe specifically because nothing
        /// else ever observes or acts on a single frame of this path's own
        /// output the way a capture moment does -- ordinary unfrozen play
        /// has no "instant" for a one-frame recovery lag to be visible in.
        /// </summary>
        private static void SuppressBoneSnap(Transform bone, ref Quaternion previous, ref bool heldLastFrame, float deltaTime)
        {
            if (bone == null)
            {
                return;
            }
            Quaternion current = bone.localRotation;

            if (heldLastFrame)
            {
                previous = current;
                heldLastFrame = false;
                return;
            }

            float degreesPerSecond = Quaternion.Angle(previous, current) / deltaTime;
            if (degreesPerSecond > MaxPlausibleArmDegreesPerSecond)
            {
                bone.localRotation = previous;
                heldLastFrame = true;
            }
            else
            {
                previous = current;
            }
        }
    }
}
