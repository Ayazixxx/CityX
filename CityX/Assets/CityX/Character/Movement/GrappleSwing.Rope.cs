using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE ROPE SWING AS A RAGDOLL (attempt 7, 2026-10-28; owner: "try making it so the body goes ragdoll while swinging but the rope is
    /// connected to the hand ... please try again"; docs/GRAPPLE_RAGDOLL.md). The six earlier attempts dragged a ragdoll (or one arm) along the
    /// swing the controller was already computing, and the limbs tore off under the yank. Here nothing is dragged: the hook lands, the body goes
    /// limp exactly as a knockdown does (Ragdoll.Activate: Animator, PlayerLocomotion and CharacterController off, every body dynamic with the
    /// swing's velocity), and a rope -- a ConfigurableJoint on the right forearm's rigidbody at the hand's point on it (the ragdoll's arm is two
    /// bodies; the hand bone only ends the forearm), its linear limit the rope's length, soft so a slack rope goes taut without a snap -- ties
    /// the hand to the hook point. PhysX does the swinging: gravity on the hanging body, the rope's pull at the hand, WASD a force on the torso,
    /// reel and payout the limit's length. Nothing kinematic is moved and the root is never moved while the body is limp (moving a dynamic
    /// body's parent teleports it). The body hangs only once it is off the ground with the rope taut: hooked from the street it runs with the
    /// rope as before (the classic swing) and goes limp the moment it swings off the ground, with the speed it had. Let go, the rope goes and
    /// the body flies limp; on the ground it stands up by itself. The classic swing (GrappleSwing's own pendulum on the controller) stays as it
    /// was, for bodies without a ragdoll and behind <see cref="RagdollMode"/> (/grapple classic).
    /// </summary>
    public sealed partial class GrappleSwing
    {
        /// <summary>The ragdoll swing on (the default) or the classic controller swing (/grapple classic).</summary>
        public static bool RagdollMode = true;

        /// <summary>
        /// The rope's limit is soft: this stiff and this damped (a hard limit snaps a slack rope taut in one step). At 2500 the rope stretched
        /// nearly 2 m at the bottom of a swing (a bungee); at this, with the projection, it gives half a metre at the bottom of a fast swing.
        /// </summary>
        public const float RopeSpring = 15000.0f;

        public const float RopeDamper = 600.0f;

        /// <summary>Past this much beyond its length the rope is projected back (the solver alone let a heavy, fast body drift metres past a soft limit).</summary>
        public const float RopeProjectionMetres = 0.15f;

        /// <summary>The hanging body's bodies are held under this speed, m/s (the upright swing's cap is for a body that cannot tear).</summary>
        public const float RopedMaxSpeed = 18.0f;

        /// <summary>How hard the forearm is pulled toward the hook, m/s², so the arm holds the rope line it is drawn from.</summary>
        public const float ArmReachAccel = 14.0f;

        /// <summary>On the ground after a release, the body lies this long before it gets up by itself, seconds.</summary>
        public const float LandSettleSeconds = 0.4f;

        private Ragdoll _ragdoll;
        private PlayerVitals _vitals;
        private ConfigurableJoint _rope;
        private Rigidbody _hand;
        private Transform _handBone;
        private float _landed;

        /// <summary>Swinging as a ragdoll on the rope (the body limp, the rope joint on the hand).</summary>
        public bool IsRoped { get; private set; }

        /// <summary>Let go of the rope and still limp in the air (or on the ground, about to get up).</summary>
        public bool IsFlyingLimp { get; private set; }

        /// <summary>The rope joint while roped (tests look at it), or null.</summary>
        public ConfigurableJoint Rope => _rope;

        private void BindRope()
        {
            _ragdoll = GetComponent<Ragdoll>();
            _vitals = GetComponent<PlayerVitals>();
        }

        /// <summary>How close to taut the rope must be, metres short of its length, before a body off the ground hangs from it.</summary>
        public const float TautWithin = 0.3f;

        /// <summary>
        /// Hanging is due: the ragdoll swing is on, the body is off the ground and the rope is taut (hooked from the street the body runs with the
        /// rope until it swings off the ground, exactly as the classic swing has it).
        /// </summary>
        private bool RopeDue(Vector3 anchor)
        {
            return RagdollMode && _ragdoll != null && !_ragdoll.IsLimp && _ragdoll.Torso != null
                && _controller != null && !_controller.isGrounded
                && Vector3.Distance(transform.position, anchor) >= _ropeLength - TautWithin;
        }

        /// <summary>The body goes limp and the rope ties its hand to <paramref name="anchor"/>. False when there is no ragdoll to hang.</summary>
        private bool TryAttachRope(Vector3 anchor, Vector3 velocity)
        {
            if (!RagdollMode || _ragdoll == null || _ragdoll.IsLimp || _ragdoll.Torso == null)
            {
                return false;
            }
            // The rope's load goes into the torso at the right shoulder: hung from the forearm's own body, the body's whole weight came down the
            // arm's two joints and the solver let them part by 30-45 cm (measured). The arm itself holds the rope line: the forearm is pulled
            // toward the hook every tick (ArmReachAccel), so the hand reaches up the rope the hook is drawn from.
            Animator animator = GetComponentInChildren<Animator>();
            Transform handBone = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            Transform shoulderBone = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
            Transform forearmBone = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
            Rigidbody forearm = forearmBone != null ? forearmBone.GetComponent<Rigidbody>() : null;
            Rigidbody torso = _ragdoll.Torso;
            if (forearm == null || handBone == null || shoulderBone == null)
            {
                return false;
            }
            _ragdoll.Activate(velocity);
            if (!_ragdoll.IsLimp)
            {
                return false;
            }
            _hand = forearm;
            _handBone = handBone;
            _ropeLength = Mathf.Clamp(Vector3.Distance(handBone.position, anchor), MinRopeLength, MaxRopeLength);
            _rope = torso.gameObject.AddComponent<ConfigurableJoint>();
            _rope.autoConfigureConnectedAnchor = false;
            _rope.connectedBody = null;
            _rope.anchor = torso.transform.InverseTransformPoint(shoulderBone.position);
            _rope.connectedAnchor = anchor;
            _rope.projectionMode = JointProjectionMode.PositionAndRotation;
            _rope.projectionDistance = RopeProjectionMetres;
            _rope.xMotion = ConfigurableJointMotion.Limited;
            _rope.yMotion = ConfigurableJointMotion.Limited;
            _rope.zMotion = ConfigurableJointMotion.Limited;
            _rope.angularXMotion = ConfigurableJointMotion.Free;
            _rope.angularYMotion = ConfigurableJointMotion.Free;
            _rope.angularZMotion = ConfigurableJointMotion.Free;
            _rope.linearLimit = new SoftJointLimit { limit = _ropeLength, contactDistance = 0.05f };
            _rope.linearLimitSpring = new SoftJointLimitSpring { spring = RopeSpring, damper = RopeDamper };
            _rope.enablePreprocessing = true;
            _rope.enableCollision = false;
            IsRoped = true;
            IsFlyingLimp = false;
            _landed = 0.0f;
            return true;
        }

        /// <summary>One frame on the rope: the player's push on the torso, the rope reeled or paid out, the speed held down, the rope drawn; a release when asked.</summary>
        private void TickRoped(Vector2 moveInput, float yawDegrees, bool boostedReleasePressed, bool plainReleasePressed, bool reelHeld,
            bool payoutHeld, float deltaTime)
        {
            _elapsedSeconds += deltaTime;
            if (_rope == null || _ragdoll == null || !_ragdoll.IsLimp || (_vitals != null && _vitals.IsDead))
            {
                ReleaseRope(false);   // the body stood up or died under the rope: the rope goes
                return;
            }
            if (reelHeld)
            {
                _ropeLength = Mathf.Max(MinRopeLength, _ropeLength - ReelSpeed * deltaTime);
            }
            else if (payoutHeld)
            {
                _ropeLength = Mathf.Min(MaxRopeLength, _ropeLength + PayoutSpeed * deltaTime);
            }
            if (reelHeld || payoutHeld)
            {
                _rope.linearLimit = new SoftJointLimit { limit = _ropeLength, contactDistance = 0.05f };
            }
            Vector3 push = new LocomotionInput { Move = moveInput, YawDegrees = yawDegrees }.WorldDirection();
            if (push.sqrMagnitude > 0.0001f)
            {
                _ragdoll.Torso.AddForce(push * SwingAccel, ForceMode.Acceleration);
            }
            if (_hand != null && _handBone != null)
            {
                Vector3 toHook = _anchor - _handBone.position;
                if (toHook.sqrMagnitude > 0.01f)
                {
                    _hand.AddForce(toHook.normalized * ArmReachAccel, ForceMode.Acceleration);   // the arm up the rope
                }
            }
            foreach (Rigidbody body in _ragdoll.Bodies)
            {
                if (body != null && body.linearVelocity.sqrMagnitude > RopedMaxSpeed * RopedMaxSpeed)
                {
                    body.linearVelocity = body.linearVelocity.normalized * RopedMaxSpeed;
                }
            }
            _velocity = _ragdoll.Torso.linearVelocity;
            EquippedHook?.UpdateRope(_anchor);
            if (boostedReleasePressed)
            {
                ReleaseRope(true);
            }
            else if (plainReleasePressed)
            {
                ReleaseRope(false);
            }
        }

        /// <summary>The rope goes: the body keeps flying limp with the torso's velocity (a boosted release adds the boost's height to every body).</summary>
        private void ReleaseRope(bool boosted)
        {
            if (!IsRoped)
            {
                return;
            }
            IsRoped = false;
            IsActive = false;
            _groundTime = 0.0f;
            if (_rope != null)
            {
                Destroy(_rope);
                _rope = null;
            }
            Vector3 velocity = _ragdoll != null && _ragdoll.Torso != null ? _ragdoll.Torso.linearVelocity : _velocity;
            if (boosted && _ragdoll != null)
            {
                velocity.y += ReleaseBoost;
                foreach (Rigidbody body in _ragdoll.Bodies)
                {
                    if (body != null)
                    {
                        body.linearVelocity += Vector3.up * ReleaseBoost;
                    }
                }
            }
            _velocity = velocity;
            LastReleaseVelocity = velocity;
            EquippedHook?.HideRope();
            if (OrbitCamera != null)
            {
                OrbitCamera.Distance = _savedCameraDistance;
            }
            IsFlyingLimp = _ragdoll != null && _ragdoll.IsLimp;
            _landed = 0.0f;
        }

        /// <summary>After a release: still limp, the body lands and gets up by itself once it has lain still a moment.</summary>
        private void TickFlyingLimp(float deltaTime)
        {
            if (!IsFlyingLimp)
            {
                return;
            }
            if (_ragdoll == null || !_ragdoll.IsLimp)
            {
                IsFlyingLimp = false;
                return;
            }
            if (!_ragdoll.IsRestingOnGround)
            {
                _landed = 0.0f;
                return;
            }
            _landed += deltaTime;
            if (_landed >= LandSettleSeconds && !_ragdoll.IsStandingUp)
            {
                _ragdoll.BeginStandingUp(force: true);
            }
        }

        private void Update()
        {
            TickFlyingLimp(Time.deltaTime);
        }
    }
}
