using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// RECOVERY (the Freerunner skill; owner, 2026-11-08: "add that as a skill in the movement tree called Recovery", then "make the recover skill
    /// work like the recovery in saints row 4"). Saints Row IV's Ninja Reflexes: knocked down, press Jump and the Boss is back up at once with a
    /// flip. Here a body that is down -- thrown, flung, falling, lying on the ground -- flips onto its feet (<see cref="Recover"/>): the bones stop
    /// flopping and are posed again (standing still, curled into a tuck, TuckPose, the whole body turning head over heels about its middle,
    /// RecoveryFlip), the standing capsule carries it on the way it was going, and once the flip is round the walker has it, in the air with its
    /// momentum, to land on its feet. A body lying still pops up into the flip first.
    ///
    /// The flip is part of being down (IsLimp stays true through it, as through a get-up), so its pose goes to the other players with the rest of
    /// the ragdoll (NetworkedRagdollSync) and their copies flip as it does.
    /// </summary>
    public sealed partial class Ragdoll
    {
        /// <summary>The flip turns the body about this far above its hips, metres: about where a curled body's middle is.</summary>
        public const float FlipPivotAboveHips = 0.2f;

        /// <summary>The pose the body was flung in is blended into the flip's over this long, seconds.</summary>
        public const float RecoveryBlendSeconds = 0.12f;

        private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int MoveXParameter = Animator.StringToHash("MoveX");
        private static readonly int MoveZParameter = Animator.StringToHash("MoveZ");

        private RecoveryFlip _flip;
        private float _flipElapsed = -1.0f;
        private Vector3 _flipVelocity;
        private Vector3 _flipPivot;
        private float _snapshotBlendSeconds = StandUpBlendSeconds;

        /// <summary>Flipping back onto its feet (<see cref="Recover"/>), until the walker has it again.</summary>
        public bool IsRecovering => _flipElapsed >= 0.0f;

        /// <summary>The flip under way (its shape; tests and the captures read it).</summary>
        public RecoveryFlip Flip => _flip;

        /// <summary>Down, and able to flip back up now: not dead (the caller's to say), not held in a fist, not already getting up, run here.</summary>
        public bool CanRecover => _limp && !_held && !IsStandingUp && !IsRecovering && !IsMirrored && Torso != null && Animator != null &&
                                  Animator.isHuman && _controller != null && _locomotion != null;

        /// <summary>
        /// Flips the body back onto its feet, facing the way its body lies (or <paramref name="fallbackFacing"/> when it gives no answer). False
        /// when it cannot (<see cref="CanRecover"/>).
        /// </summary>
        public bool Recover(Vector3 fallbackFacing)
        {
            if (!CanRecover)
            {
                return false;
            }
            Transform hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform neck = Animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform leftLeg = Animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Transform rightLeg = Animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            if (hips == null || neck == null || leftLeg == null || rightLeg == null)
            {
                return false;
            }
            Vector3 hipsAt = hips.position;
            Vector3 bodyUp = neck.position - hipsAt;
            Vector3 bodyRight = rightLeg.position - leftLeg.position;
            Vector3 spin = Torso.isKinematic ? Vector3.zero : Torso.angularVelocity;
            Vector3 velocity = Torso.isKinematic ? Vector3.zero : Torso.linearVelocity;
            float groundY = GroundBelow(hipsAt);

            CapturePose();
            SetKinematic(true);

            // Standing still, posed now (upright, at the root) so the hips' height in it can be measured. Not the jump clip: it carries the body
            // a metre and a quarter below where it stands (the walker's ground clamp lifts it back in a jump) and holds the arms out in front.
            Transform visual = Animator.transform;
            visual.localRotation = Quaternion.identity;
            visual.localPosition = Vector3.zero;
            Animator.enabled = true;
            Animator.speed = 1.0f;
            Animator.SetFloat(SpeedParameter, 0.0f);
            Animator.SetFloat(MoveXParameter, 0.0f);
            Animator.SetFloat(MoveZParameter, 0.0f);
            Animator.SetBool(GroundedParameter, false);
            Animator.Play("Locomotion", 0, 0.0f);
            Animator.Update(0.0f);
            Vector3 hipsInParent = visual.parent != null ? visual.parent.InverseTransformPoint(hips.position) : hips.position;
            _flipPivot = hipsInParent + Vector3.up * FlipPivotAboveHips;

            float feetUpright = hipsAt.y - hipsInParent.y;   // where its feet would be, stood up with its hips where they are
            _flip = RecoveryFlip.Plan(bodyUp, bodyRight, spin, velocity, Mathf.Max(0.0f, feetUpright - groundY),
                _locomotion.Gravity * _locomotion.GravityScale, fallbackFacing);
            _flipVelocity = _flip.Velocity;

            // The root turned to face along the flip and put where the flip's first pose has its hips on the ragdoll's (never under the ground).
            transform.rotation = Quaternion.LookRotation(_flip.Facing, Vector3.up);
            Quaternion firstTurn = _flip.TurnAt(0.0f);
            Vector3 hipsFromRoot = _flipPivot + firstTurn * (hipsInParent - _flipPivot);
            Vector3 root = hipsAt - transform.rotation * hipsFromRoot;
            root.y = Mathf.Max(root.y, groundY);
            transform.position = root;
            Physics.SyncTransforms();

            _flipElapsed = 0.0f;
            _restSeconds = 0.0f;
            _snapshotBlendSeconds = RecoveryBlendSeconds;
            _blendElapsed = _snapshotBones != null ? 0.0f : -1.0f;
            _controller.enabled = true;
            PoseFlip();

            // The fall is watched from the feet from here, not the torso: its highest point moves with it, so the landing counts the same drop.
            _vitals?.ShiftFallPeak(transform.position.y - hipsAt.y);
            return true;
        }

        /// <summary>The flip's step: the capsule carries the body on under gravity; the walker takes it over once the flip is round or it lands.</summary>
        private void TickRecovery(float deltaTime)
        {
            _flipElapsed += deltaTime;
            _flipVelocity.y -= _locomotion.Gravity * _locomotion.GravityScale * deltaTime;
            CollisionFlags hit = _controller.Move(_flipVelocity * deltaTime);
            if ((hit & CollisionFlags.Above) != 0 && _flipVelocity.y > 0.0f)
            {
                _flipVelocity.y = 0.0f;
            }
            if ((hit & CollisionFlags.Sides) != 0)
            {
                Vector3 moved = _controller.velocity;
                _flipVelocity.x = moved.x;
                _flipVelocity.z = moved.z;
            }
            bool landed = (hit & CollisionFlags.Below) != 0 && _flipVelocity.y <= 0.0f;
            if (_flipElapsed >= _flip.Seconds || landed)
            {
                FinishRecovery();
            }
        }

        /// <summary>The body turned and curled as the flip has it now (LateUpdate, after the Animator, before the blend from the flung pose).</summary>
        private void PoseFlip()
        {
            if (!IsRecovering || Animator == null)
            {
                return;
            }
            Transform visual = Animator.transform;
            Quaternion turn = _flip.TurnAt(_flipElapsed);
            visual.localRotation = turn;
            visual.localPosition = _flipPivot - turn * _flipPivot;
            TuckPose.Apply(Animator, visual, _flip.TuckAt(_flipElapsed));
        }

        /// <summary>Round and on its feet: the walker has the body again, in the air, flying on as it was.</summary>
        private void FinishRecovery()
        {
            ClearFlip();
            _limp = false;
            _restSeconds = 0.0f;
            _allowImmediateStandUp = false;
            _locomotion.enabled = true;
            _locomotion.TakeOverFlight(_flipVelocity);
        }

        /// <summary>Lets go of a flip under way (knocked down again, killed, revived): the body upright and as it was before it went limp.</summary>
        private void AbandonRecovery()
        {
            ClearFlip();
            _limp = false;
        }

        private void ClearFlip()
        {
            _flipElapsed = -1.0f;
            _blendElapsed = -1.0f;
            _snapshotBones = null;
            _snapshotBlendSeconds = StandUpBlendSeconds;
            if (Animator != null)
            {
                Animator.transform.localRotation = Quaternion.identity;
                Animator.transform.localPosition = Vector3.zero;
            }
        }

    }
}
