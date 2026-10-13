using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// FOOT IK (owner, 2026-10-13: "foot IK with ground detection, hip drop, and stride adjustment so the character walks/runs smoothly on slopes and
    /// stairs without sliding feet"). After the Animator, each frame on the ground: the ground under each foot is found with a ray, the hips are lowered
    /// so the lower foot can reach its ground, each leg is solved (two-bone IK, the project's own <see cref="TwoBoneIK"/>, after the Animator like every
    /// other procedural pose here) so its sole sits on its own ground, the sole is tilted to the surface, and a planted foot is held where it landed
    /// (<see cref="FootPlanting"/>) so nothing slides. A landing dips the hips (the knees give) and the slope under the feet leans the body
    /// (<see cref="BodyLean"/>). The whole thing is read from the body's own position and the ground, so a copy of another player's body, moved by
    /// the network, places its feet the same way: nothing to send.
    ///
    /// Off in the air, on a wall, over a ledge, in a vault or mantle (the controller is off), in a seat, in a dance (the body may be on the floor),
    /// and during a baked move; it fades in and out rather than snapping.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private readonly FootPlanting _feet = new FootPlanting();
        private Vector3 _footIkLastPosition;
        private float _footIkLastYaw;
        private bool _footIkHasLast;
        private bool _footIkWasInAir;
        private float _footIkAirSeconds;
        private float _footIkFallSpeed;
        private Vector3 _groundNormal = Vector3.up;
        private static readonly RaycastHit[] FootHits = new RaycastHit[8];

        /// <summary>
        /// The controller reports "not grounded" for a frame at every stair riser it steps up and now and then on a slope; the feet are only
        /// let go of the ground once it has said so for this long (or the body is really going up: a jump).
        /// </summary>
        public const float GroundedGraceSeconds = 0.12f;

        /// <summary>A body rising faster than this is jumping, grace or no grace.</summary>
        public const float JumpingUpSpeed = 1.0f;

        /// <summary>The grace holds only while ground is within this of the floor under a foot (a riser's edge), never over a real drop.</summary>
        public const float GraceGroundMetres = 0.12f;

        /// <summary>The planting bookkeeping (locks, hip drop), for tests and captures.</summary>
        public FootPlanting FootPlanting => _feet;

        /// <summary>The ground's normal under the feet, smoothed; up when there is none or the feet are not being placed.</summary>
        public Vector3 GroundNormal => _groundNormal;

        /// <summary>True while the feet are being placed on the ground this frame.</summary>
        public bool FootIkActive { get; private set; }

        /// <summary>Lets a capture or a test switch the foot placement off to compare.</summary>
        public bool FootIkEnabled = true;

        /// <summary>How far the sole may be tilted to follow a slope, degrees.</summary>
        public const float MaxSoleTiltDegrees = 40.0f;

        /// <summary>A body that turns faster than this on the spot (degrees in one frame) lets its planted feet go: feet held in the world would twist the legs.</summary>
        public const float ReleaseLocksYawDegreesPerFrame = 1.5f;

        /// <summary>...while moving slower than this (a walking body turns with its steps, and its locks are what stop the slide).</summary>
        public const float ReleaseLocksBelowSpeed = 0.4f;

        private void ApplyFootIK()
        {
            bool bones = _leftUpperLeg != null && _leftLowerLeg != null && _leftFoot != null && _rightUpperLeg != null && _rightLowerLeg != null && _rightFoot != null;
            float deltaTime = Time.deltaTime;
            if (_animator == null || !bones || deltaTime <= 0.0f)
            {
                return;
            }

            // The body's own motion, measured (so a network-moved copy sees the same): a landing, a turn on the spot, a teleport.
            Vector3 position = transform.position;
            float yaw = transform.eulerAngles.y;
            Vector3 moved = _footIkHasLast ? position - _footIkLastPosition : Vector3.zero;
            float yawDelta = _footIkHasLast ? Mathf.DeltaAngle(_footIkLastYaw, yaw) : 0.0f;
            _footIkLastPosition = position;
            _footIkLastYaw = yaw;
            _footIkHasLast = true;
            if (moved.sqrMagnitude > TeleportMetres * TeleportMetres)
            {
                _feet.Reset();
                moved = Vector3.zero;
            }

            Transform root = _animator.transform;
            float soleOffset = BodyRig.FootY * _leftFoot.lossyScale.y;
            // THE CLIP'S FLOOR IS THE CONTROLLER'S BOTTOM, not the drawn root: ClampFeetAboveGround has just lifted the root by however far a clip
            // sank its lowest foot (the charged jump: a metre), and measuring the other foot's lift against that lifted root made it look planted
            // over a half-metre drop, which dropped the hips through the floor.
            float floorY = transform.position.y;
            FootPlanting.Sample left = SampleFoot(0, _leftFoot, floorY);
            FootPlanting.Sample right = SampleFoot(1, _rightFoot, floorY);

            // In the air, with a grace period over the controller's one-frame "not grounded" at a stair riser -- but only while the ground is still
            // right there under a foot (a body that has stepped off a ledge, or is falling from where it was placed, must not reach half a metre down
            // for it); a real jump counts at once.
            bool grounded = _animator.GetBool(GroundedParameter);
            float verticalSpeed = moved.y / deltaTime;
            _footIkAirSeconds = grounded ? 0.0f : _footIkAirSeconds + deltaTime;
            bool groundNear = (left.HasGround && floorY - left.Ground.y < GraceGroundMetres) || (right.HasGround && floorY - right.Ground.y < GraceGroundMetres);
            bool inAir = !grounded && (_footIkAirSeconds > GroundedGraceSeconds || verticalSpeed > JumpingUpSpeed || !groundNear);
            if (inAir)
            {
                // The impact speed is the fastest the body fell: the frame it lands on is cut short by the ground.
                _footIkFallSpeed = Mathf.Max(_footIkFallSpeed, -verticalSpeed);
            }
            bool active = FootIkEnabled && !inAir && !IsSitting && !InCarSeat
                && _climbWallWeight <= 0.01f && _ledgeHandsWeight <= 0.01f && _bakedPoseWeight <= 0.0f
                && (_controller == null || _controller.enabled)
                && !_animator.GetBool(DancingParameter) && !_animator.GetBool(BreakdancingParameter);
            FootIkActive = active;

            if (!inAir && _footIkWasInAir)
            {
                if (active)
                {
                    _feet.Land(Mathf.Max(_footIkFallSpeed, -verticalSpeed));
                }
                _footIkFallSpeed = 0.0f;
            }
            _footIkWasInAir = inAir;

            var flat = new Vector3(moved.x, 0.0f, moved.z);
            bool releaseLocks = Mathf.Abs(yawDelta) > ReleaseLocksYawDegreesPerFrame && flat.magnitude / deltaTime < ReleaseLocksBelowSpeed;

            _feet.Step(deltaTime, active, left, right, floorY, soleOffset, releaseLocks, moved / deltaTime);

            // The slope under the body, for the lean: the feet's grounds averaged, smoothed.
            Vector3 normal = Vector3.zero;
            if (active && left.HasGround) normal += left.Normal;
            if (active && right.HasGround) normal += right.Normal;
            if (normal.sqrMagnitude < 0.25f) normal = Vector3.up;
            _groundNormal = Vector3.Slerp(_groundNormal, normal.normalized, 1.0f - Mathf.Exp(-deltaTime / 0.15f));

            float drop = _feet.TotalDrop;
            if (drop > 0.0f)
            {
                root.localPosition += Vector3.down * drop;
            }
            Quaternion lean = _lean.Rotation();
            SolveLeg(0, _leftUpperLeg, _leftLowerLeg, _leftFoot, -transform.right, lean);
            SolveLeg(1, _rightUpperLeg, _rightLowerLeg, _rightFoot, transform.right, lean);
        }

        /// <summary>
        /// The clip's ankle and the ground under it (under its lock while it is held): a ray from above the highest step a foot could be on. The
        /// foot's lowest point this frame comes from its rotation (toe, heel or sole, whichever is lowest: a running foot strikes toe first).
        /// </summary>
        private FootPlanting.Sample SampleFoot(int index, Transform foot, float floorY)
        {
            var sample = new FootPlanting.Sample { Animated = foot.position, Normal = Vector3.up };
            float scale = foot.lossyScale.y;
            float lowest = float.MaxValue;
            foreach (float along in new[] { FusedBodyShape.FootToeCentreForward + FusedBodyShape.FootToeRadius, 0.06f, FusedBodyShape.FootHeelCentreForward - FusedBodyShape.FootHeelRadius })
            {
                Vector3 corner = foot.position + foot.rotation * (new Vector3(0.0f, -BodyRig.FootY, along) * scale);
                lowest = Mathf.Min(lowest, corner.y);
            }
            sample.SoleBelowAnkle = Mathf.Max(0.01f, foot.position.y - lowest);
            Vector3 probe = _feet.ProbeAt(index, sample.Animated);
            float top = floorY + FootPlanting.MaxFootRaise + 0.1f;
            float length = FootPlanting.MaxFootRaise + 0.1f + FootPlanting.MaxHipDrop + 0.25f;
            if (GroundUnder(new Vector3(probe.x, top, probe.z), length, out RaycastHit hit) && hit.normal.y >= 0.5f)
            {
                sample.HasGround = true;
                sample.Ground = hit.point;
                sample.Normal = hit.normal;
            }
            return sample;
        }

        /// <summary>The nearest solid thing straight down that is not this body, a person or a trigger.</summary>
        private bool GroundUnder(Vector3 origin, float length, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, FootHits, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = FootHits[i];
                Collider c = h.collider;
                if (c == null || c.transform.IsChildOf(transform) || c.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                if (h.distance < nearest)
                {
                    nearest = h.distance;
                    best = h;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>
        /// Puts one ankle on its placement: the leg solved to reach it (keeping the clip's own knee direction), the foot kept at the clip's angle in the
        /// world rather than turned with the shin, the whole-body lean taken back off it (a sole stays flat on the ground while the body leans),
        /// and the sole tilted onto the slope.
        /// </summary>
        private void SolveLeg(int index, Transform upper, Transform lower, Transform foot, Vector3 outward, Quaternion lean)
        {
            FootPlanting.Placement placement = _feet.Of(index);
            float weight = placement.Weight;
            if (weight <= 0.001f)
            {
                return;
            }
            Vector3 hip = upper.position;
            Vector3 axis = foot.position - hip;
            Vector3 bow = lower.position - hip;
            if (axis.sqrMagnitude > 1e-6f)
            {
                bow -= axis * (Vector3.Dot(bow, axis) / axis.sqrMagnitude);
            }
            Vector3 hint = bow.sqrMagnitude > 1e-4f ? bow.normalized : (transform.forward + outward * 0.2f);
            Quaternion footBefore = foot.rotation;
            TwoBoneIK.Solve(upper, lower, foot, placement.Target, hint, weight);

            Quaternion tilt = Quaternion.FromToRotation(Vector3.up, placement.Normal);
            tilt = Quaternion.RotateTowards(Quaternion.identity, tilt, MaxSoleTiltDegrees);
            Quaternion unlean = Quaternion.Inverse(lean);
            Quaternion wanted = tilt * unlean * footBefore;
            foot.rotation = Quaternion.Slerp(footBefore, wanted, weight);
        }
    }
}
