using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHAT A BLOW DOES TO THE WHOLE BODY (owner, 2026-10-17: "the hits dont feel impactful at all ... fluidity and reactivity"): the impact
    /// freeze and the shove. A landed hit holds BOTH bodies dead still for a few frames -- the attacker's blade in the wound, the victim's pose
    /// at the moment it struck -- and then throws the victim back. Before this the hit-stop froze only the attacker's sword arm while the legs
    /// walked on under it, and nothing moved the victim at all. Fighting games hold both fighters (Street Fighter V pauses 8-15 frames, God of
    /// War on every hit); the displacement is what sells the force of a blow (see docs/SOULSLIKE_COMBAT.md, "hits that land").
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>The longest any freeze may hold the body: a freeze a copy was told to start and never told to end lets go on its own.</summary>
        public const float MaxImpactFreezeSeconds = 0.25f;

        private bool _impactFrozen;
        private float _impactFreezeRemaining;
        private int _freezeTickedFrame = -1;
        private float _animatorSpeedBeforeFreeze = 1.0f;

        private Vector3 _shoveDirection;
        private float _shoveMetres;
        private float _shoveSeconds;
        private float _shoveElapsed;
        private float _shoveDone;

        /// <summary>True while an impact holds the body still: nothing moves, the Animator is stopped, the clocks wait.</summary>
        public bool IsImpactFrozen => _impactFrozen;

        /// <summary>True while a blow is still throwing the body back.</summary>
        public bool IsShoved => _shoveSeconds > 0.0f && _shoveElapsed < _shoveSeconds;

        /// <summary>
        /// Holds the body dead still for <paramref name="seconds"/> (at most <see cref="MaxImpactFreezeSeconds"/>): the Animator stopped, no
        /// movement, no gravity, the sword's footwork waiting. A longer freeze already running keeps its time. The attacker's hit-stop and the
        /// victim's own instant of impact both come through here.
        /// </summary>
        public void ImpactFreeze(float seconds)
        {
            seconds = Mathf.Clamp(float.IsNaN(seconds) ? 0.0f : seconds, 0.0f, MaxImpactFreezeSeconds);
            if (seconds <= 0.0f)
            {
                return;
            }
            if (!_impactFrozen)
            {
                _impactFrozen = true;
                if (_animator != null)
                {
                    _animatorSpeedBeforeFreeze = _animator.speed > 0.0f ? _animator.speed : 1.0f;
                    _animator.speed = 0.0f;
                }
            }
            _impactFreezeRemaining = Mathf.Max(_impactFreezeRemaining, seconds);
        }

        /// <summary>
        /// Frozen solid (Chill, docs/FROST.md): its Animator stays stopped whatever blow's own instant of impact comes and goes (the frost's
        /// ticks are blows too, and letting go of their freeze would set it running again).
        /// </summary>
        public bool FrozenSolid { get; set; }

        /// <summary>Lets the body go before its freeze ran out (the hit-stop ending, a swing dropped).</summary>
        public void ReleaseImpactFreeze()
        {
            if (!_impactFrozen)
            {
                return;
            }
            _impactFrozen = false;
            _impactFreezeRemaining = 0.0f;
            if (_animator != null && _animator.speed <= 0.0f && !FrozenSolid)
            {
                _animator.speed = _animatorSpeedBeforeFreeze > 0.0f ? _animatorSpeedBeforeFreeze : 1.0f;
            }
        }

        /// <summary>
        /// A playback speed something wants on the Animator while it is frozen (a reaction played into the freeze): kept for when the freeze
        /// lets go, so the body comes back at that speed instead of the freeze being quietly undone.
        /// </summary>
        public void NoteAnimatorSpeedWhileFrozen(float speed)
        {
            if (_impactFrozen)
            {
                _animatorSpeedBeforeFreeze = speed > 0.0f ? speed : 1.0f;
            }
        }

        /// <summary>
        /// Throws the body <paramref name="metres"/> along <paramref name="direction"/> (flattened) over <paramref name="seconds"/>, fast at
        /// first and easing out, through the controller so walls still stop it. A new shove replaces one still running. Waits out a freeze.
        /// </summary>
        public void Shove(Vector3 direction, float metres, float seconds)
        {
            direction.y = 0.0f;
            if (direction.sqrMagnitude < 1e-6f || metres <= 0.0f || seconds <= 0.0f || float.IsNaN(metres))
            {
                return;
            }
            _shoveDirection = direction.normalized;
            _shoveMetres = metres * SizeSpeedFactor;
            _shoveSeconds = seconds;
            _shoveElapsed = 0.0f;
            _shoveDone = 0.0f;
        }

        /// <summary>Counts a freeze down; true while the body must stay still this frame.</summary>
        private bool TickImpactFreeze(float deltaTime)
        {
            if (!_impactFrozen)
            {
                return false;
            }
            _impactFreezeRemaining -= deltaTime;
            if (_impactFreezeRemaining <= 0.0f)
            {
                ReleaseImpactFreeze();
                return false;
            }
            if (_animator != null && _animator.speed > 0.0f)
            {
                // Something set a speed on the Animator since the freeze began (a state played into it): held still all the same.
                _animatorSpeedBeforeFreeze = _animator.speed;
                _animator.speed = 0.0f;
            }
            Velocity = Vector3.zero;
            CurrentSpeed = 0.0f;
            return true;
        }

        /// <summary>This frame's share of the shove, moved by what is still owed so a long frame cannot overshoot.</summary>
        private void ApplyShove(float deltaTime)
        {
            if (!IsShoved)
            {
                return;
            }
            _shoveElapsed += deltaTime;
            float p = Mathf.Clamp01(_shoveElapsed / _shoveSeconds);
            float eased = 1.0f - (1.0f - p) * (1.0f - p);
            float wanted = eased * _shoveMetres;
            float step = wanted - _shoveDone;
            _shoveDone = wanted;
            if (step > 0.0f && _controller != null && _controller.enabled)
            {
                _controller.Move(_shoveDirection * step);
            }
        }
    }
}
