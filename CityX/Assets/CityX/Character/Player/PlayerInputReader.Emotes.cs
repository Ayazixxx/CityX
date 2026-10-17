using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// THE EMOTES: dancing, breakdancing and push-ups, and what cancels them.
    /// </summary>
    public sealed partial class PlayerInputReader
    {
        /// <summary>
        /// Set by PlayerChat's own /dance and /breakdance commands (see
        /// StartDance/StartBreakdance) -- ported from character.gd's own
        /// _is_dancing, "loop an emote clip until something else takes
        /// over." Godot clears it individually at the start of every other
        /// action (punch, jump, block, shoot...); this port checks once,
        /// up front in Update() (see RequestsDanceCancel), for the same
        /// end result with one check instead of one scattered through every
        /// action's own start.
        /// </summary>
        private bool _isDancing;

        private bool _isBreakdancing;

        public bool IsDancing => _isDancing;

        public bool IsBreakdancing => _isBreakdancing;

        /// <summary>The push-up clips' lengths, seconds: going down, one rep, getting up (written by PlayerRigBuilder from the clips themselves).</summary>
        [SerializeField] private float _pushUpEnterSeconds = 2.0f;

        [SerializeField] private float _pushUpRepSeconds = 1.3f;

        [SerializeField] private float _pushUpExitSeconds = 2.0f;

        /// <summary>The /pushups emote (owner, 2026-10-14). See PushUpEmote and StartPushUps.</summary>
        private PushUpEmote _pushUps;

        public PushUpEmote PushUps => _pushUps ??= new PushUpEmote(_pushUpEnterSeconds, _pushUpRepSeconds, _pushUpExitSeconds);

        /// <summary>True from /pushups until the body is standing again: it is held, and the keys only call it off.</summary>
        public bool IsDoingPushUps => _pushUps != null && _pushUps.HoldsTheBody;

        /// <summary>For PlayerRigBuilder: the three clips' lengths, so the count and the poses keep time with the animation.</summary>
        public void SetPushUpClipLengths(float enterSeconds, float repSeconds, float exitSeconds)
        {
            _pushUpEnterSeconds = enterSeconds;
            _pushUpRepSeconds = repSeconds;
            _pushUpExitSeconds = exitSeconds;
            PushUps.SetClipLengths(enterSeconds, repSeconds, exitSeconds);
        }

        /// <summary>Called by PlayerChat's /pushups: down into push-ups (PushUpEmote), unless airborne, dead or stunned. It stops a dance.</summary>
        public void StartPushUps()
        {
            if (_locomotion != null && !_locomotion.IsGrounded)
            {
                return;   // not mid-jump, as /dance
            }
            if (_vitals != null && (_vitals.IsDead || _vitals.IsStunned))
            {
                return;
            }
            _isDancing = false;
            _isBreakdancing = false;
            PushUps.Start();
        }

        /// <summary>
        /// One frame of push-ups: what calls them off (a movement key -- WASD, Space, Shift, Ctrl, as a dance -- a spell key 1 to 0, a mouse
        /// button), the count (a push-up's XP to the vitals as each one finishes), and the body ticked with no movement, so gravity and the
        /// Animator's phase carry on and other players are sent it.
        /// </summary>
        private void TickPushUps(Keyboard keyboard, float deltaTime)
        {
            Mouse mouse = Mouse.current;
            bool stop = RequestsDanceCancel(keyboard) || AnyDigitPressed(keyboard) ||
                        (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                                           mouse.middleButton.wasPressedThisFrame));
            int reps = _pushUps.Tick(deltaTime, stop);
            if (reps > 0 && _vitals != null)
            {
                _vitals.AddXp(reps * PushUpEmote.XpPerRep);
            }
            _audio?.SilenceFootsteps();
            _locomotion.Tick(new LocomotionInput { YawDegrees = transform.eulerAngles.y, PushUpPhase = _pushUps.AnimatorPhase }, deltaTime);
        }

        private static bool AnyDigitPressed(Keyboard keyboard)
        {
            return keyboard.digit1Key.wasPressedThisFrame || keyboard.digit2Key.wasPressedThisFrame || keyboard.digit3Key.wasPressedThisFrame ||
                   keyboard.digit4Key.wasPressedThisFrame || keyboard.digit5Key.wasPressedThisFrame || keyboard.digit6Key.wasPressedThisFrame ||
                   keyboard.digit7Key.wasPressedThisFrame || keyboard.digit8Key.wasPressedThisFrame || keyboard.digit9Key.wasPressedThisFrame ||
                   keyboard.digit0Key.wasPressedThisFrame;
        }

        /// <summary>
        /// Called by PlayerChat's own /dance command. No-op while airborne,
        /// matching character.gd's own dance()/_play_emote guard -- dancing
        /// mid-jump doesn't read as anything sensible, and the jump
        /// animation already owns the body at that point anyway.
        /// </summary>
        public void StartDance()
        {
            if (_locomotion != null && !_locomotion.IsGrounded)
            {
                return;
            }
            _isDancing = true;
            _isBreakdancing = false;
        }

        /// <summary>Called by PlayerChat's own /breakdance command. See StartDance's own doc comment.</summary>
        public void StartBreakdance()
        {
            if (_locomotion != null && !_locomotion.IsGrounded)
            {
                return;
            }
            _isBreakdancing = true;
            _isDancing = false;
        }

        /// <summary>
        /// Whether this frame's input should end a dance in progress --
        /// ported from character.gd's own "movement always cancels a dance"
        /// rule, plus the individual _is_dancing=false clears its own
        /// punch()/jump()/block() etc. each carry, folded into one check
        /// here instead of one per action.
        /// </summary>
        private static bool RequestsDanceCancel(Keyboard keyboard)
        {
            return keyboard.wKey.isPressed || keyboard.aKey.isPressed ||
                   keyboard.sKey.isPressed || keyboard.dKey.isPressed ||
                   keyboard.spaceKey.isPressed || keyboard.leftShiftKey.isPressed ||
                   keyboard.leftCtrlKey.isPressed;
        }
    }
}
