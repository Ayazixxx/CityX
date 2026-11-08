using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// ITS ONE-HANDED GRAB AND THROW (owner, 2026-11-08: "can you make an attack where the crimson claw grabs you and throws you", then "i
    /// wanted it to be a 1 handed grab... you can use the swing attack animation but he just grabs his and the raises his arm and throws him").
    /// Every fourth attack (three swipes at you on your feet first; owner, 2026-11-08: "it should be mostly swing attacks"), when what it
    /// attacks can be thrown, its swipe is a grab: the same wind-up, tell and glint, the same swing, but what
    /// the swing reaches is caught instead of cut: held stiff and upright in that fist like a doll (owner's drawing: the chest in the hollow
    /// under the thumb, the talons round the front, the head out of the top and the legs hanging out of the bottom; Ragdoll.Seize) while it
    /// raises its arm and rears back (<see cref="ClawGrabMotion"/>), then let go limp, flung forward and up, hurt, and up again a moment after
    /// it lands ("you are ragdolled when thrown"). A swing that
    /// reaches nothing (or someone dashing) is a swipe that missed. Killed while holding, it drops what it held.
    /// Only the body's own machine seizes it; everyone sees it held and thrown through the ragdoll's mirror (docs/MULTIPLAYER_STATE.md).
    /// </summary>
    public sealed partial class CrimsonClaw
    {
        /// <summary>Swipes at a standing player between two grabs: every fourth attack at most is a grab, never two in a row.</summary>
        public const int SwipesBetweenGrabs = 3;

        /// <summary>Thrown this fast ahead of it and up, metres per second.</summary>
        public const float ThrowSpeed = 13.0f;
        public const float ThrowUpSpeed = 6.0f;

        public const float ThrowDamage = 20.0f;

        /// <summary>Thrown, the body lies limp this long before it gets up, seconds: about its flight and a moment on the ground.</summary>
        public const float ThrownLimpSeconds = 2.5f;

        /// <summary>A whole grab that catches, wind-up to settled, seconds.</summary>
        public const float GrabSeconds = WindupSeconds + TellSeconds + SwingSeconds + ClawGrabMotion.Seconds;

        /// <summary>
        /// Where the -X claw's fist holds a body by the middle of its torso, from its wrist, in the model's units; the +X claw's is its mirror.
        /// Measured (owner, 2026-11-08: "try to find a way to angle it best so he is seen as within the hand with minimal clipping through"):
        /// a stand-in of the player's body at its real sizes was tried in the claw at every angle, height and facing, at sizes 2.2 to 2.6, for
        /// the most of the chest the talons wrap round closely with the least of the body and claw inside each other, the head and legs
        /// clear. At 2.4, the claw turned as <see cref="ClawGrabMotion"/> holds it (tipped 15 degrees back, turned 20 out) round a body
        /// facing where the Claw faces: the talons round 15/16 of the chest, 6% of the body inside the claw.
        /// </summary>
        public static readonly Vector3 FistHollow = new Vector3(0.11f, 0.06f, 0.48f);

        /// <summary>Caught, the body is eased from where it stood into the fist over this long, seconds, as the arm rises with it.</summary>
        public const float TakeInSeconds = 0.3f;

        private bool _grabbing;
        private ClawGrabMotion _grabMotion;
        private PlayerVitals _held;
        private Vector3 _caughtAt;
        private Vector3 _caughtFacing;
        private int _swipesSinceGrab;

        /// <summary>It holds someone it caught.</summary>
        public bool IsHolding => _held != null;

        /// <summary>Its next attack is a grab, if what it attacks can be thrown (tests and captures, not to wait out two swipes).</summary>
        public void GrabNext() => _swipesSinceGrab = SwipesBetweenGrabs;

        /// <summary>A grab is a player's to be caught in: a summon is only ever swiped at.</summary>
        private bool WantsToGrab() => _swipesSinceGrab >= SwipesBetweenGrabs && Opponents.Alive(_target) && _target is PlayerVitals player && player.CanBeSeized;

        /// <summary>Its swing closes on what it reaches, as a swipe would cut it, and not on someone dashing.</summary>
        private void TryCatch()
        {
            if (!Opponents.Alive(_target) || !(_target is PlayerVitals player))
            {
                return;
            }
            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0.0f;
            if (!InFront(toTarget, ClawHitRadius, ClawHitHalfAngleDegrees) || !player.Seize())
            {
                return;
            }
            _held = player;
            if (MainCollision != null)
            {
                MainCollision.enabled = false;   // the held body hangs inside its box; the ragdoll's depenetration would fight the grip
            }
            _caughtAt = _held.HeldMiddle;
            _caughtFacing = _held.transform.forward;
            _grabMotion = new ClawGrabMotion(ClawStrikeMotion.At(WindupSeconds + TellSeconds + _attackPhaseTimer));
            _attackState = AttackState.Holding;
            _attackPhaseTimer = 0.0f;
            Hold();
        }

        /// <summary>Raising its arm with what it caught, rearing back, throwing it and settling.</summary>
        private void TickHolding()
        {
            Place(_grabMotion.At(_attackPhaseTimer));
            if (_held != null && !_held.IsAlive)
            {
                LetGoOfHeld(Vector3.zero);
            }
            if (_held != null && _attackPhaseTimer >= ClawGrabMotion.LetGoSeconds)
            {
                Fling();
            }
            else if (_held != null)
            {
                Hold();
            }
            if (_attackPhaseTimer >= ClawGrabMotion.Seconds)
            {
                LetGoOfHeld(Vector3.zero);
                _grabMotion = null;
                EndAttack();
                if (MainCollision != null)
                {
                    // Only now: switched on as it let go, the box round it stopped the thrown body dead inside it.
                    MainCollision.enabled = true;
                }
            }
        }

        /// <summary>Hurts what it holds and throws it ahead of it and up.</summary>
        private void Fling()
        {
            PlayerVitals thrown = _held;
            LetGoOfHeld(transform.forward * ThrowSpeed + Vector3.up * ThrowUpSpeed);
            ((IDamageable)thrown).ApplyHit(ThrowDamage, transform.position, 0.0f);
        }

        /// <summary>Lets go of what it holds, if anything, at <paramref name="velocity"/> (zero: dropped).</summary>
        private void LetGoOfHeld(Vector3 velocity)
        {
            if (_held == null)
            {
                return;
            }
            _held.Throw(velocity, ThrownLimpSeconds);
            _held = null;
        }

        /// <summary>Upright in the fist, facing where the Claw faces (the measured fit); eased in from where it was caught.</summary>
        private void Hold()
        {
            float taken = Mathf.SmoothStep(0.0f, 1.0f, _attackPhaseTimer / TakeInSeconds);
            Vector3 facing = Vector3.Slerp(_caughtFacing, transform.forward, taken);
            _held.HoldAt(Vector3.Lerp(_caughtAt, FistPoint(), taken), facing);
        }

        /// <summary>The hollow of the fist that caught it, in the world.</summary>
        public Vector3 FistPoint()
        {
            if (_activeClawPivot == null)
            {
                return transform.position;
            }
            Vector3 hollow = FistHollow;
            if (_activeClawPivot.localPosition.x > 0.0f)
            {
                hollow.x = -hollow.x;
            }
            return _activeClawPivot.TransformPoint(hollow);
        }
    }
}
