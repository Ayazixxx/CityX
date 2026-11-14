using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// CROUCHING (docs/CROUCH.md): Source's rules (CrouchHull) on this body. The controller is the crouched one while ducked, so the body
    /// really is shorter (shots over it miss, it fits under things); ducking in the air lifts the body by the difference, so the feet come up
    /// and the head stays (the crouch jump); the body cannot stand where the standing controller would not fit. The crouch pose is the
    /// Animator's "Crouch" layer (the baked crouch legs and lean, CrouchBaker) at the duck's own eased weight, over whatever the arms are
    /// doing; the hips follow it there (the walk keeps them upright, see LateUpdate). The camera comes down with the eyes
    /// (<see cref="CrouchDrop"/>).
    ///
    /// Another player's copy is never Ticked: it follows the synced Crouching and Grounded parameters with the same rules, so its controller
    /// (what a shot at it hits) and its pose duck with the owner's.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>
        /// The crouched controller's height, as a share of standing: the crouched pose's crown (CrouchBaker, measured by CrouchPoseTests) with a
        /// little room. Source halves its hull (72 to 36 units); this body's legs are a third of its height and its head is big, so its crouch
        /// comes mostly from the lean and stops higher.
        /// </summary>
        public const float CrouchHeightShare = 0.78f;

        /// <summary>The Animator layer that plays the crouch (PlayerRigBuilder.AddCrouchLayer).</summary>
        public const string CrouchLayerName = "Crouch";

        /// <summary>The crouch layer's one state, a directional blend of the crouch clips (its strides are GaitStrides' under this name).</summary>
        public const string CrouchStateName = "Crouched";

        private readonly CrouchHull _crouch = new CrouchHull();
        private static readonly Collider[] RoomHits = new Collider[16];
        private int _crouchLayer = -2;
        private int _crouchTickedFrame = -1;

        /// <summary>The crouched controller's height, in the body's own units.</summary>
        public float CrouchedHeight => StandingHeight * CrouchHeightShare;

        /// <summary>The hull is the crouched one (Source's FL_DUCKING).</summary>
        public bool IsDucked => _crouch.Ducked;

        /// <summary>How far down the crouch shows, eased, 0 standing .. 1 crouched.</summary>
        public float CrouchShown => _crouch.Shown;

        /// <summary>
        /// How far the eyes are below a standing body's, world metres: they come down with the duck on the ground; in the air the body was
        /// lifted by the same amount, so the head stays where it was (the camera does not jump).
        /// </summary>
        public float CrouchDrop => (StandingHeight - CrouchedHeight) * _crouch.Shown * BodyScale;

        /// <summary>Source's duck speed crop for this step (1 standing, a third ducked on the ground).</summary>
        private float CrouchSpeedFactor(bool grounded) => _crouch.SpeedFactor(grounded);

        /// <summary>
        /// One step of the crouch on the owner's body, from the key: in the air the body is lifted (or lowered) by the hull difference, and the
        /// controller takes the height the crouch now has.
        /// </summary>
        private void TickCrouch(bool wanted, bool grounded, float deltaTime)
        {
            _crouchTickedFrame = Time.frameCount;
            bool held = wanted && !IsFloating && !ClimbLimbsActive && !HasForcedVelocity;
            bool room = grounded ? StandingFits(transform.position) : StandingFits(transform.position + Vector3.down * HullDifference);
            CrouchHull.Shift shift = _crouch.Step(held, grounded, room, deltaTime);
            switch (shift)
            {
                case CrouchHull.Shift.TuckFeet:
                    ApplyHull();
                    transform.position += Vector3.up * HullDifference;
                    Physics.SyncTransforms();
                    break;
                case CrouchHull.Shift.DropFeet:
                    transform.position += Vector3.down * HullDifference;
                    ApplyHull();
                    Physics.SyncTransforms();
                    break;
                default:
                    ApplyHull();
                    break;
            }
            IsCrouching = held || _crouch.Ducked;
        }

        /// <summary>A copy of another player's body: the same rules from the synced parameters, no room checks and no lifting (the owner's position comes over the network).</summary>
        private void FollowRemoteCrouch(float deltaTime)
        {
            if (_crouchTickedFrame == Time.frameCount || _animator == null || !_animator.enabled || _animator.runtimeAnimatorController == null)
            {
                return;
            }
            _crouch.Step(_animator.GetBool(CrouchingParameter), _animator.GetBool(GroundedParameter), true, deltaTime);
            ApplyHull();
        }

        /// <summary>The crouch pose at the duck's eased weight (both kinds of body), after the Animator has been told everything else.</summary>
        private void ShowCrouch()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                return;
            }
            if (_crouchLayer == -2)
            {
                _crouchLayer = _animator.GetLayerIndex(CrouchLayerName);
            }
            if (_crouchLayer >= 0)
            {
                _animator.SetLayerWeight(_crouchLayer, _crouch.Shown);
            }
        }

        /// <summary>The difference between the two controllers' heights, world metres.</summary>
        private float HullDifference => (StandingHeight - CrouchedHeight) * BodyScale;

        /// <summary>The controller at the height the crouch has now (the crouched one while ducked), its feet where they were.</summary>
        private void ApplyHull()
        {
            if (_controller == null)
            {
                return;
            }
            float height = _crouch.Ducked ? CrouchedHeight : StandingHeight;
            if (Mathf.Approximately(_controller.height, height))
            {
                return;
            }
            _controller.height = height;
            _controller.center = new Vector3(0.0f, height * 0.5f, 0.0f);
        }

        /// <summary>Whether the standing controller fits with its feet at <paramref name="feet"/> (Source's CanUnduck): nothing solid but this body inside it.</summary>
        private bool StandingFits(Vector3 feet)
        {
            if (_controller == null)
            {
                return true;
            }
            float scale = BodyScale;
            float radius = _controller.radius * scale * 0.95f;
            float height = StandingHeight * scale;
            Vector3 bottom = feet + Vector3.up * (radius + _controller.skinWidth * scale + 0.02f);
            Vector3 top = feet + Vector3.up * (height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, RoomHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!RoomHits[i].transform.IsChildOf(transform))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
