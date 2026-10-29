using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// What the giant does, with no scene (docs/GIANT.md): the player's own body grown to the Grow spell's size, stalking the nearest player and
    /// crushing them with a jump that lands like a quake. It strides at the player; near enough, it crouches (the tell: long enough to see and
    /// run), leaps, and wherever it comes down the ground heaves (the slam: <see cref="GiantRules"/>); then it stands a moment before the next.
    /// </summary>
    public sealed class GiantMind
    {
        public enum Phase
        {
            Idle,
            Approach,
            Crouch,
            Airborne,
            Recover,
        }

        public struct Senses
        {
            public bool HasTarget;
            public float Distance;
            public bool Grounded;
            public bool Dead;
        }

        public struct Intent
        {
            /// <summary>How hard to walk at the target, 0 to 1.</summary>
            public float Move;

            public bool Crouch;
            public bool Jump;

            /// <summary>It has just come down from its leap: the slam lands this frame.</summary>
            public bool Slam;
        }

        public Phase Current { get; private set; } = Phase.Idle;

        private float _inPhase;
        private float _cooldown;
        private bool _leftGround;

        public Intent Tick(Senses senses, float deltaTime)
        {
            var intent = new Intent();
            _inPhase += deltaTime;
            _cooldown -= deltaTime;
            if (senses.Dead)
            {
                Enter(Phase.Idle);
                return intent;
            }
            switch (Current)
            {
                case Phase.Idle:
                    if (senses.HasTarget)
                    {
                        Enter(Phase.Approach);
                    }
                    break;
                case Phase.Approach:
                    if (!senses.HasTarget)
                    {
                        Enter(Phase.Idle);
                        break;
                    }
                    if (senses.Distance <= GiantRules.LeapFromMetres && _cooldown <= 0.0f && senses.Grounded)
                    {
                        Enter(Phase.Crouch);
                        intent.Crouch = true;
                        break;
                    }
                    intent.Move = senses.Distance > GiantRules.StandOffMetres ? 1.0f : 0.0f;
                    break;
                case Phase.Crouch:
                    intent.Crouch = true;
                    if (_inPhase >= GiantRules.CrouchSeconds)
                    {
                        intent.Crouch = false;
                        intent.Jump = true;
                        _leftGround = false;
                        Enter(Phase.Airborne);
                    }
                    break;
                case Phase.Airborne:
                    intent.Move = GiantRules.AirMoveShare;
                    if (!senses.Grounded)
                    {
                        _leftGround = true;
                    }
                    else if (_leftGround || _inPhase > GiantRules.MaxAirSeconds)
                    {
                        intent.Slam = true;
                        _cooldown = GiantRules.SlamCooldownSeconds;
                        Enter(Phase.Recover);
                    }
                    break;
                case Phase.Recover:
                    if (_inPhase >= GiantRules.RecoverSeconds)
                    {
                        Enter(senses.HasTarget ? Phase.Approach : Phase.Idle);
                    }
                    break;
            }
            return intent;
        }

        private void Enter(Phase phase)
        {
            Current = phase;
            _inPhase = 0.0f;
        }
    }
}
