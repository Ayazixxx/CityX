using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Everything the body needs to know about what the player wants this
    /// frame.
    ///
    /// WHY THIS IS A STRUCT AND NOT JUST KEYBOARD READS INSIDE THE CONTROLLER.
    /// Two reasons that both arrived at the same shape.
    ///
    /// Testing: a controller that reads Keyboard.current cannot be driven by a
    /// test, so the movement -- the thing that decides how the whole game feels
    /// -- would be the one part with no coverage.
    ///
    /// Networking: FishNet's prediction replicates an input struct from client
    /// to server and re-simulates it. That is exactly this. Building it now
    /// costs nothing and means the predicted player is a wrapper rather than a
    /// rewrite.
    ///
    /// Yaw is carried rather than a camera reference for the same reason: the
    /// server has no idea where a client's camera is pointing, so the intent
    /// has to travel with the input.
    /// </summary>
    public struct LocomotionInput
    {
        /// <summary>Stick or WASD, in [-1,1], relative to <see cref="YawDegrees"/>.</summary>
        public Vector2 Move;

        /// <summary>Which way the player is facing, in degrees. Usually the camera's yaw.</summary>
        public float YawDegrees;

        public bool Sprint;

        /// <summary>
        /// Held, not tapped. Sprint beats it -- player.gd's own rule, and not
        /// arbitrary: there is no crouch-sprint clip, so holding both would
        /// show a crouch walk while the body travelled at sprint speed.
        /// </summary>
        public bool Crouch;

        /// <summary>True on the frame jump was pressed, not while it is held.</summary>
        public bool Jump;

        /// <summary>
        /// True every frame a charge-jump hold is in progress -- drives the
        /// ChargedJump animator pose while it is true, nothing more. See
        /// PlayerInputReader.TickJumpCharge for how a press starts one.
        /// </summary>
        public bool ChargingJump;

        /// <summary>
        /// True on the frame a held jump was released (or the charge was
        /// cancelled by the ground disappearing out from under it). Distinct
        /// from Jump because a charge's payout is not known until release --
        /// same reasoning TickCombat's own punch-hold already uses.
        /// </summary>
        public bool JumpReleased;

        /// <summary>
        /// How much of a full charge JumpReleased is reporting, in [0,1].
        /// Only meaningful alongside JumpReleased; a near-instant tap reports
        /// near 0, which PlayerLocomotion's own height math already resolves
        /// to an ordinary jump -- there is no separate "this was just a tap"
        /// case to handle.
        /// </summary>
        public float JumpChargeFraction;

        /// <summary>
        /// True for as long as a gun's own right-click aim is held -- see
        /// PlayerInputReader.TickCombat's own gun branch. Drives the
        /// Animator's "Aiming" bool, which shows the pistol_aim_in pose;
        /// nothing here decides WHAT counts as aiming, only carries the
        /// answer to the one place (PlayerLocomotion.Tick) that already
        /// owns every other animator parameter.
        /// </summary>
        public bool Aiming;

        /// <summary>
        /// The camera's own current world position/forward while aiming --
        /// EXPLICIT BUG REPORT: "the pose he goes in when you hold right
        /// click is aimed to the left slightly he isnt even pointing the
        /// gun forward." AimPitchDegrees alone only ever pitched the arm
        /// around the BODY's own forward axis, which ignores the camera's
        /// own lateral shoulder offset while aiming (ThirdPersonCamera's
        /// own AimShoulderOffset) -- a shot fires along the CAMERA's own
        /// ray (see PlayerInputReader's own EquippedGun.Fire call), so an
        /// arm merely pitched around the body's centreline visibly diverges
        /// from where the gun is actually about to shoot, exactly the real
        /// parallax an over-the-shoulder camera creates. PlayerLocomotion.
        /// ApplyGunAimPose uses these two instead, to aim the arm AT the
        /// same point the crosshair itself converges on.
        /// </summary>
        public Vector3 AimCameraPosition;

        public Vector3 AimCameraForward;

        /// <summary>
        /// The world point the crosshair ray actually ends on (what the gun would
        /// hit, or a far point along the ray on a miss), with HasGunAimPoint.
        /// The arm aims at THIS when it is supplied, instead of a fixed 15m along
        /// the ray -- at close range the fixed point leaves the barrel visibly off
        /// the target (a 1.4m shoulder offset is 20 degrees at 4m), so the bullet
        /// tracer, which runs muzzle to hit point, left the gun at an angle that
        /// had nothing to do with where the barrel pointed.
        /// </summary>
        public Vector3 GunAimPoint;

        public bool HasGunAimPoint;

        /// <summary>
        /// True whenever the equipped weapon's own MovesetId is
        /// ComboChain.Sword -- see PlayerInputReader's own read of
        /// MeleeAttacker.EquippedWeapon. Drives the Animator's
        /// "SwordEquipped" bool, which swaps Locomotion for SwordLocomotion
        /// (see PlayerRigBuilder.AddSwordLocomotion), the same "carry the
        /// answer, decide nothing here" split Aiming already uses.
        /// </summary>
        public bool SwordEquipped;

        /// <summary>
        /// True whenever a pistol is equipped (EquippedGun != null),
        /// regardless of whether right-click is currently held -- unlike
        /// Aiming, this stays true the whole time the gun is holstered in
        /// hand. Drives the Animator's "GunEquipped" bool, which swaps
        /// Locomotion for GunLocomotion (see PlayerRigBuilder.
        /// AddGunLocomotion) the moment the character is actually moving,
        /// same "carry the answer, decide nothing here" split SwordEquipped
        /// already uses. EXPLICIT BUG REPORT: "the legs move as you hold
        /// the pistol and aim in if you are walking [in Godot] which it
        /// doesnt do in unity" -- character.gd's own set_walking() plays a
        /// full-body "gun_walk"/"gun_run" cycle (raised, ready arms +
        /// stepping legs) any time _holding_gun is true AND the character
        /// is moving, REGARDLESS of whether right-click (_is_aiming) is
        /// also held -- the static aim-in pose is reserved for standing
        /// completely still. Unity had no equivalent: the Animator's
        /// "Aiming" Any State transition (see AddAimingState) fired
        /// unconditionally on Aiming alone, freezing the WHOLE body
        /// (including legs) on the static pistol_aim_in pose the instant
        /// right-click was held, even while the CharacterController kept
        /// sliding the transform forward underneath it -- read exactly as
        /// "legs lock up." GunEquipped alone (not Aiming) is what should
        /// gate the moving pose, since Godot's own gun_walk plays during
        /// ordinary hip-fire movement too, not just while aiming.
        /// </summary>
        public bool GunEquipped;

        /// <summary>
        /// True while the camera is locked onto a target (Tab). The body then
        /// faces YawDegrees -- which the camera is steering at the target --
        /// whether it is moving or not, so WASD becomes strafing around the
        /// enemy (the directional walk clips read the body's LOCAL velocity)
        /// instead of the body turning to run wherever it is pushed. Carries
        /// the answer only; unlike Aiming it does not raise a gun pose.
        /// </summary>
        public bool LockedOn;

        /// <summary>
        /// True whenever a jetpack is equipped and the thrust key is
        /// physically held -- see PlayerInputReader's own dispatch to
        /// IJetpack.Tick. Drives the Animator's "JetpackThrusting" bool,
        /// which forces Idle over whatever Locomotion would otherwise show
        /// (see PlayerRigBuilder.AddJetpackIdleState): a walk/run/sprint
        /// cycle reads as wrong the instant the body actually leaves the
        /// ground under power, which thrusting does almost immediately.
        /// </summary>
        public bool JetpackThrusting;

        /// <summary>
        /// True whenever a jetpack is thrusting AND shift is held -- the
        /// same "boostHeld" flag IJetpack.Tick already gets for its own
        /// lean/climb amplification, reused here to also raise horizontal
        /// speed to SprintSpeed. NOT routed through Sprint/wantsSprint --
        /// that path costs stamina, and the jetpack is explicitly "infinite
        /// fuel, on purpose" (see Jetpack's own class comment), so boosting
        /// with it must not drain a resource that would otherwise gate an
        /// unrelated, on-foot mechanic. Also, unlike ground Sprint, this
        /// applies in every direction, not just forward: JetpackThrusting
        /// above already forces a fixed jetpack pose regardless of travel
        /// direction (see that field's own doc comment), so there is no
        /// missing walk/run clip for a sideways or backward burst of speed
        /// to expose the way there would be on the ground.
        /// </summary>
        public bool JetpackBoosting;

        /// <summary>
        /// True whenever a jetpack is thrusting, shift is held, AND there is
        /// a forward (W) component to the held movement -- EXPLICIT ASK:
        /// "when you hold w + shift while using jetpack your character
        /// tilts all the way horizontally and the jetpack goes twice as
        /// fast." A strict superset condition of JetpackBoosting (every
        /// dive is also boosting), checked separately in PlayerLocomotion
        /// so a dive's own even-higher speed wins over the plain boost
        /// speed rather than the two competing. IJetpack.Tick reads the
        /// same forward-component test directly off its own moveInput
        /// rather than this flag, for the pose -- see TickTilt's own doc
        /// comment.
        /// </summary>
        public bool JetpackDiving;

        /// <summary>
        /// True for as long as the /dance chat emote is playing -- see
        /// PlayerInputReader.StartDance/RequestsDanceCancel. Drives the
        /// Animator's "Dancing" bool the same "carry the answer, decide
        /// nothing here" way Aiming/SwordEquipped/JetpackThrusting already do.
        /// </summary>
        public bool Dancing;

        /// <summary>Same as Dancing, for /breakdance.</summary>
        public bool Breakdancing;

        /// <summary>The /pushups emote's phase (PushUpEmote.AnimatorPhase): 0 standing, 1 going down, 2 pushing up, 3 getting up.</summary>
        public int PushUpPhase;

        /// <summary>The world-space direction the player wants to travel, flat and normalised.</summary>
        public Vector3 WorldDirection()
        {
            Vector2 move = Move;
            if (move.sqrMagnitude > 1.0f)
            {
                move.Normalize();
            }
            if (move.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Quaternion yaw = Quaternion.Euler(0.0f, YawDegrees, 0.0f);
            return yaw * new Vector3(move.x, 0.0f, move.y);
        }
    }
}
