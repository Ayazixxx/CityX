using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// THE JUMP CHARGE AND THE DASH: Space held then released (kept for the skill tree, not called now), and a double-tapped sprint.
    /// </summary>
    public sealed partial class PlayerInputReader
    {
        /// <summary>
        /// How long after tapping sprint a second tap still counts as a dash.
        ///
        /// A press-release-press inside this window is the double-tap; a single
        /// press, or sprint simply held down, never crosses it, because the
        /// timer only starts on the FIRST press and is consumed the moment a
        /// second one lands. 0.3s is generous enough to catch a deliberate
        /// double-tap without firing on two unrelated ones.
        /// </summary>
        public const float DoubleTapSeconds = 0.3f;

        private float _sprintTapWindow;

        /// <summary>
        /// How long held space takes to reach a FULL charge -- Saints Row
        /// 4's own charge jump, sized only as a first test value: hold less
        /// and the release scales smoothly down toward an ordinary jump
        /// (see PlayerLocomotion.ChargedJumpHeightMultiplier for what
        /// "full" means in real height), hold longer and it just caps here.
        /// </summary>
        public const float ChargedJumpMaxHoldSeconds = 1.5f;

        private bool _jumpHeld;

        private float _jumpHeldSeconds;

        private int _lastJumpChargeFrame = -10;

        /// <summary>
        /// Space, held then released -- the same shape TickCombat's own
        /// punch-hold uses, and for the same reason: how high this should
        /// send the body is not known until release.
        ///
        /// UNLIKE THE PUNCH, THIS NEVER FIRES ON A BARE PRESS. There is no
        /// "light jump on press, heavy jump replaces it on a long-enough
        /// hold" branch here, because a tap and a hold are the SAME gesture
        /// at opposite ends of one continuous charge: a near-instant release
        /// reports a JumpChargeFraction near 0, which PlayerLocomotion's own
        /// height math already resolves to an ordinary jump. One mechanic,
        /// not two competing ones.
        ///
        /// A CHARGE ONLY STARTS GROUNDED, matching an ordinary jump's own
        /// gate, and CANCELS -- reporting a release with whatever fraction
        /// it had reached, so the animator still has a way out of the pose
        /// -- the moment the ground disappears out from under it, rather
        /// than letting a charge that started on solid ground pay out as a
        /// mid-air launch once it releases.
        ///
        /// KEPT IN THE VAULT, NOT CALLED FROM Update() RIGHT NOW -- EXPLICIT
        /// ASK: "get rid of charged jump for now that is going to be a
        /// skill tree ability keep it in the vault." Update's own dispatch
        /// fires a plain, un-charged jump directly instead (JumpReleased
        /// with JumpChargeFraction left at 0 -- see that call site's own
        /// doc comment for why that alone is enough, with no changes needed
        /// in PlayerLocomotion). This method is untouched and still fully
        /// correct; a future skill-tree unlock can route back through it
        /// exactly as before rather than rebuilding it.
        /// </summary>
        private void TickJumpCharge(Keyboard keyboard, bool grounded, float deltaTime,
            out bool chargingJumpNow, out bool jumpReleased, out float chargeFraction)
        {
            chargingJumpNow = false;
            jumpReleased = false;
            chargeFraction = 0.0f;

            // A charge that something else interrupted (a wall grab, a vault or a swing took the frame) is dropped, never paid out later.
            if (_jumpHeld && _lastJumpChargeFrame != Time.frameCount - 1)
            {
                _jumpHeld = false;
            }
            _lastJumpChargeFrame = Time.frameCount;

            if (keyboard.spaceKey.wasPressedThisFrame && grounded)
            {
                _jumpHeld = true;
                _jumpHeldSeconds = 0.0f;
            }

            if (!_jumpHeld)
            {
                return;
            }

            if (!grounded || keyboard.spaceKey.wasReleasedThisFrame)
            {
                _jumpHeld = false;
                jumpReleased = true;
                chargeFraction = ChargedJumpMaxHoldSeconds > 0.0f
                    ? Mathf.Clamp01(_jumpHeldSeconds / ChargedJumpMaxHoldSeconds)
                    : 1.0f;
                return;
            }

            if (keyboard.spaceKey.isPressed)
            {
                _jumpHeldSeconds = Mathf.Min(_jumpHeldSeconds + deltaTime, ChargedJumpMaxHoldSeconds);
                chargingJumpNow = true;
            }
        }

        /// <summary>
        /// The shared double-tap-shift GESTURE, split out from what it DOES
        /// (start a ground dash) so a grapple swing can react to the exact
        /// same gesture a different way -- explicit ask: dashing should cut
        /// a swing's own ragdoll short, the same "land or dash" exit a
        /// released/grounded swing already has. Only one of TickDash and the
        /// swing-active branch runs on a given frame (GrappleSwing.IsActive
        /// owns the whole frame while true, same as everywhere else it
        /// early-returns), so _sprintTapWindow's own decrement below still
        /// only happens once per frame either way.
        /// </summary>
        private bool ConsumeDoubleTapShift(Keyboard keyboard, float deltaTime)
        {
            _sprintTapWindow = Mathf.Max(0.0f, _sprintTapWindow - deltaTime);

            if (!keyboard.leftShiftKey.wasPressedThisFrame)
            {
                return false;
            }

            if (_sprintTapWindow <= 0.0f)
            {
                // The first tap only opens the window. Holding sprint down
                // never gets here twice, which is what keeps hold-to-sprint and
                // double-tap-to-dash from fighting over the same key.
                _sprintTapWindow = DoubleTapSeconds;
                return false;
            }
            _sprintTapWindow = 0.0f;
            return true;
        }

        /// <summary>
        /// Double-tap sprint to dash.
        ///
        /// WHICH WAY IT GOES IS WHATEVER WAS HELD AT THAT INSTANT, and a bare
        /// double-tap with nothing held dashes BACKWARD -- that is Godot's own
        /// choice, and it is the useful one: a dash with no direction is a
        /// dodge, and a dodge goes away from whatever you are facing.
        /// </summary>
        private void TickDash(Keyboard keyboard, Vector2 move, float yawDegrees, float deltaTime)
        {
            if (!ConsumeDoubleTapShift(keyboard, deltaTime))
            {
                return;
            }

            if (_locomotion.IsDashing)
            {
                return;
            }

            // WITH A SWORD IT IS A SOULSLIKE ROLL: its own stamina cost that
            // is allowed down to the last drop, longer i-frames, and legal out
            // of the tail of a swing (never its startup or active frames).
            SoulsSwordCombat souls = SoulsSword;
            bool soulsRoll = souls != null && souls.IsEquipped;
            if (soulsRoll && !souls.CanRoll)
            {
                return;
            }

            // THE DASH IS A SKILL (owner, 2026-10-14: the Freerunner tree's Dash). The sword's roll is not: it is the sword fighter's dodge,
            // part of sword combat, and stays with the sword whatever is learnt.
            if (!soulsRoll && !_skills.Knows(SkillCatalog.Dash))
            {
                return;
            }

            // Stamina is spent through the vitals rather than checked and
            // deducted here, so the exhaustion latch stays in one place.
            if (_vitals != null)
            {
                bool paid = soulsRoll
                    ? _vitals.TrySpendCombatStamina(souls.RollStaminaCost, souls.RollRegenDelaySeconds)
                    : _vitals.TrySpendStamina(PlayerVitals.BaseMaxStamina * PlayerLocomotion.DashStaminaFraction);
                if (!paid)
                {
                    return;
                }
            }
            if (soulsRoll)
            {
                souls.BeginRoll();
            }

            Vector2 held = move.sqrMagnitude > 0.01f ? move : new Vector2(0.0f, -1.0f);
            var direction = new LocomotionInput { Move = held, YawDegrees = yawDegrees };
            _locomotion.StartDash(direction.WorldDirection());
            _vitals?.GrantInvulnerability(soulsRoll ? souls.RollInvulnerabilitySeconds : PlayerLocomotion.DashSeconds);
            _audio?.PlayDash();
        }
    }
}
