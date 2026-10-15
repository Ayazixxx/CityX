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
    /// Also here, from the owner's forwarded movement advice (2026-10-13): the LEAN TURNS ABOUT THE HEAD (the root is shifted so the head stays put and the
    /// feet trail), the feet are placed further along a long stride than the clip has them (STRIDE WARPING, <see cref="GaitStrides"/>: the sprint's
    /// extra 20%), and when strafing the LEADING FOOT turns toward the way the body goes.
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
        private float _strideScale = 1.0f;
        private static readonly RaycastHit[] FootHits = new RaycastHit[8];
        private static readonly string[] GaitTrees = { "Locomotion", "SwordLocomotion", "GunLocomotion", "Crouched" };

        /// <summary>The planting bookkeeping (locks, hip drop), for tests and captures.</summary>
        public FootPlanting FootPlanting => _feet;

        /// <summary>The ground's normal under the feet, smoothed; up when there is none or the feet are not being placed.</summary>
        public Vector3 GroundNormal => _groundNormal;

        /// <summary>True while the feet are being placed on the ground this frame.</summary>
        public bool FootIkActive { get; private set; }

        /// <summary>Lets a capture, a test or the console (/footik) switch the foot placement off to compare.</summary>
        public bool FootIkEnabled = true;

        /// <summary>How much further along the body's travel the feet are placed than the clip has them (GaitStrides), smoothed.</summary>
        public float StrideScale => _strideScale;

        /// <summary>How far the sole may be tilted to follow a slope, degrees.</summary>
        public const float MaxSoleTiltDegrees = 40.0f;

        /// <summary>A body that turns faster than this on the spot (degrees in one frame) lets its planted feet go: feet held in the world would twist the legs.</summary>
        public const float ReleaseLocksYawDegreesPerFrame = 1.5f;

        /// <summary>...while moving slower than this (a walking body turns with its steps, and its locks are what stop the slide).</summary>
        public const float ReleaseLocksBelowSpeed = 0.4f;

        /// <summary>
        /// The controller reports "not grounded" for a frame at every stair riser and now and then on a slope: as long as ground is within
        /// <see cref="GraceGroundMetres"/> under a foot the body counts as on the ground, for this long at most. A real jump counts at once.
        /// </summary>
        public const float LongestGraceSeconds = 0.5f;

        public const float GraceGroundMetres = 0.12f;

        /// <summary>A body rising faster than this is jumping, grace or no grace.</summary>
        public const float JumpingUpSpeed = 1.0f;

        /// <summary>Strafing, the leading foot turns this much of the way toward the travel, up to <see cref="LeadFootMaxYawDegrees"/>.</summary>
        public const float LeadFootYawShare = 0.6f;

        public const float LeadFootMaxYawDegrees = 35.0f;

        /// <summary>The stride scale follows the gait's over this long.</summary>
        public const float StrideSmoothingSeconds = 0.2f;

        private void ApplyFootIK()
        {
            if (_animator == null)
            {
                return;
            }
            Transform root = _animator.transform;

            // THE LEAN TURNS ABOUT THE HEAD: ApplyBodyLean turned the drawn root about its origin at the feet; shifted here (after ClampFeetAboveGround,
            // which resets the root's position) so the head is back where it was and it is the feet that have moved: the owner's forwarded advice, "make
            // the lean rotate from the head ... the head stays where it was".
            if (_leanApplied != Quaternion.identity)
            {
                Vector3 pivot = Vector3.up * (BodyLean.PivotHeight * root.localScale.y);
                root.localPosition += pivot - _leanApplied * pivot;
            }

            bool bones = _leftUpperLeg != null && _leftLowerLeg != null && _leftFoot != null && _rightUpperLeg != null && _rightLowerLeg != null && _rightFoot != null;
            float deltaTime = Time.deltaTime;
            if (!bones || deltaTime <= 0.0f)
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
            var flat = new Vector3(moved.x, 0.0f, moved.z);
            float speed = flat.magnitude / deltaTime;
            Vector3 heading = speed > 0.5f ? flat / (speed * deltaTime) : Vector3.zero;

            // The stride for this gait (GaitStrides), from the Animator's own blend position: a copy of another player's body has the same.
            float strideTarget = 1.0f;
            if (_animator.runtimeAnimatorController != null)
            {
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
                foreach (string tree in GaitTrees)
                {
                    if (info.IsName(tree))
                    {
                        strideTarget = GaitStrides.Current.StrideFor(tree, new Vector2(_animator.GetFloat(MoveXParameter), _animator.GetFloat(MoveZParameter)));
                        break;
                    }
                }
            }
            _strideScale = Mathf.Lerp(_strideScale, strideTarget, 1.0f - Mathf.Exp(-deltaTime / StrideSmoothingSeconds));

            float soleOffset = BodyRig.FootY * _leftFoot.lossyScale.y;
            // THE CLIP'S FLOOR IS THE CONTROLLER'S BOTTOM, not the drawn root: ClampFeetAboveGround has just lifted the root by however far a clip
            // sank its lowest foot (the charged jump: a metre), and measuring the other foot's lift against that lifted root made it look planted
            // over a half-metre drop, which dropped the hips through the floor.
            float floorY = transform.position.y;
            FootPlanting.Sample left = SampleFoot(0, _leftFoot, _leftUpperLeg, _leftLowerLeg, floorY, heading);
            FootPlanting.Sample right = SampleFoot(1, _rightFoot, _rightUpperLeg, _rightLowerLeg, floorY, heading);

            // In the air: the controller says so AND no ground is right there under a foot (a riser's edge, a slope's flicker), or for too long; a jump at once.
            bool grounded = _animator.GetBool(GroundedParameter);
            float verticalSpeed = moved.y / deltaTime;
            _footIkAirSeconds = grounded ? 0.0f : _footIkAirSeconds + deltaTime;
            bool groundNear = (left.HasGround && floorY - left.Ground.y < GraceGroundMetres) || (right.HasGround && floorY - right.Ground.y < GraceGroundMetres);
            // (A jump is rising while NOT grounded: the controller stepping up a stair riser rises 15 m/s for one frame and says grounded.)
            bool inAir = !grounded && (verticalSpeed > JumpingUpSpeed || !groundNear || _footIkAirSeconds > LongestGraceSeconds);
            if (inAir)
            {
                // The impact speed is the fastest the body fell: the frame it lands on is cut short by the ground.
                _footIkFallSpeed = Mathf.Max(_footIkFallSpeed, -verticalSpeed);
            }
            bool active = FootIkEnabled && !inAir && !IsSitting && !InCarSeat && !IsResized   // its rays are metres for a 2.6 m body
                && _climbWallWeight <= 0.01f && _ledgeHandsWeight <= 0.01f && (_bakedPoseWeight <= 0.0f || BakedPoseIsUpperBodyOnly)
                && (_controller == null || _controller.enabled)
                && !_animator.GetBool(DancingParameter) && !_animator.GetBool(BreakdancingParameter)
                && _animator.GetInteger(PushUpPhaseParameter) == 0;   // the push-up clips put the feet (and the hands) where they go
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

            bool releaseLocks = Mathf.Abs(yawDelta) > ReleaseLocksYawDegreesPerFrame && speed < ReleaseLocksBelowSpeed;
            _feet.Step(deltaTime, active, left, right, floorY, soleOffset, releaseLocks, moved / deltaTime);

            // The slope under the body, for the lean: the feet's grounds averaged, smoothed.
            Vector3 normal = Vector3.zero;
            if (active && left.HasGround) normal += left.Normal;
            if (active && right.HasGround) normal += right.Normal;
            if (normal.sqrMagnitude < 0.25f) normal = Vector3.up;
            _groundNormal = Vector3.Slerp(_groundNormal, normal.normalized, 1.0f - Mathf.Exp(-deltaTime / 0.15f));

            float drop = _feet.TotalDrop;
            if (active)
            {
                drop += PoseHipDrop;   // a sword attack sinking into its cut: the legs bend, the feet stay planted
            }
            if (drop > 0.0f)
            {
                root.localPosition += Vector3.down * drop;
            }

            // Strafing, the leading foot turns toward the way the body goes (the owner's forwarded advice): it opens the legs and reads as a sidestep.
            float side = heading != Vector3.zero ? Vector3.Dot(heading, transform.right) : 0.0f;
            Quaternion lean = _lean.Rotation();
            SolveLeg(0, _leftUpperLeg, _leftLowerLeg, _leftFoot, -transform.right, lean, side < -0.25f ? LeadYaw(_leftFoot, heading, -side) : 0.0f);
            SolveLeg(1, _rightUpperLeg, _rightLowerLeg, _rightFoot, transform.right, lean, side > 0.25f ? LeadYaw(_rightFoot, heading, side) : 0.0f);
        }

        private float _headForwardBlend;
        private float _swordHeadBlend;

        /// <summary>
        /// THE HEAD OVER A SWORD (owner, 2026-10-14, a screenshot of the greatsword held up and the face tipped to the sky: "why is he looking
        /// upward. make him looking straight like the idle position ... make sure the head isnt too far forward or backward once rotating it
        /// ... use the idle head positioning as a guide that one is fine"). The sword's standing clip (2hsw_idle, Mixamo) tips the neck back
        /// about 27 degrees and the face up about 45 more than the idle does, which also carries the head 15 cm back over the hips. The idle's
        /// own carriage, measured from it (SwordIdleHeadPlayTests): the neck this far forward of upright ...
        /// </summary>
        public const float IdleNeckLeanDegrees = 3.0f;

        /// <summary>... and the head bone's forward this far above the level (the rig's face looks straight ahead with its bone tipped so).</summary>
        public const float IdleFacePitchDegrees = 11.0f;

        /// <summary>How quickly the steady head fades in and out, per second.</summary>
        public const float HeadForwardBlendPerSecond = 6.0f;

        /// <summary>
        /// THE HEAD LOOKS STRAIGHT AHEAD while walking and running (owner, 2026-10-13: "I don't like how the head looks side to side when running, it should
        /// just look forward"). The clips swing the head with each stride and the lean tips it into turns; here its turn and roll are taken out against the
        /// body's own facing and the world's up, keeping only its up-down angle (the clip's, the lean's and the posture's), then the posture's own turn and
        /// tilt go back on. Hair is on the head bone and follows. Only in the walk/run blends, on the ground; an aim (ApplyGunAimPose, run after) still
        /// turns the head to the sights.
        /// </summary>
        private void ApplyHeadForward()
        {
            if (_head == null || _animator == null)
            {
                return;
            }
            bool locomotion = false;
            if (_animator.runtimeAnimatorController != null)
            {
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(0);
                foreach (string tree in GaitTrees)
                {
                    locomotion |= info.IsName(tree);
                }
            }
            bool wanted = locomotion && !IsSitting && !InCarSeat && _climbWallWeight <= 0.01f && _ledgeHandsWeight <= 0.01f && _bakedPoseWeight <= 0.0f;   // a reload looks at the gun (its own neck and head)
            _headForwardBlend = Mathf.MoveTowards(_headForwardBlend, wanted ? 1.0f : 0.0f, HeadForwardBlendPerSecond * Time.deltaTime);
            if (_headForwardBlend <= 0.0f)
            {
                return;
            }
            Quaternion body = Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f);
            Vector3 face = Quaternion.Inverse(body) * _head.forward;
            float level = Mathf.Sqrt(face.x * face.x + face.z * face.z);
            if (level < 1e-4f)
            {
                return;
            }
            Vector3 ahead = new Vector3(0.0f, face.y, level);   // the same up-down angle, pointed straight ahead
            BodyPostureSettings posture = BodyPostureSettings.Current;
            Quaternion target = body * Quaternion.Euler(0.0f, posture.HeadTurnRight, 0.0f) * Quaternion.LookRotation(ahead, Vector3.up)
                * Quaternion.Euler(0.0f, 0.0f, -posture.HeadTiltRight);
            _head.rotation = Quaternion.Slerp(_head.rotation, target, Mathf.SmoothStep(0.0f, 1.0f, _headForwardBlend));
        }

        /// <summary>How far the leading foot turns toward the travel: a share of the angle between its toes and the heading, scaled by how sideways the travel is.</summary>
        /// <summary>
        /// Standing or walking with a sword, the neck and the head are carried as at idle (<see cref="IdleNeckLeanDegrees"/>,
        /// <see cref="IdleFacePitchDegrees"/>), in the body's own frame: the neck first, so the head sits over the shoulders where it does at idle,
        /// then the face, keeping the heading ApplyHeadForward gave it. Eased in and out like it. A cut (ApplySwordPose, after this) still turns
        /// the torso, and the head goes with it.
        /// </summary>
        private void ApplySwordHeadCarriage()
        {
            if (_head == null || _neck == null || _animator == null)
            {
                return;
            }
            bool swordGait = _animator.runtimeAnimatorController != null && _animator.GetCurrentAnimatorStateInfo(0).IsName("SwordLocomotion");
            bool wanted = swordGait && !IsSitting && !InCarSeat && _climbWallWeight <= 0.01f && _ledgeHandsWeight <= 0.01f && _bakedPoseWeight <= 0.0f;
            _swordHeadBlend = Mathf.MoveTowards(_swordHeadBlend, wanted ? 1.0f : 0.0f, HeadForwardBlendPerSecond * Time.deltaTime);
            if (_swordHeadBlend <= 0.0f)
            {
                return;
            }
            float w = Mathf.SmoothStep(0.0f, 1.0f, _swordHeadBlend);
            Quaternion body = Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f);

            // The neck: its line, from the neck joint to the head's centre, stood up to the idle's lean.
            Vector3 line = _head.position - _neck.position;
            if (line.sqrMagnitude > 1e-8f)
            {
                Vector3 upright = body * (Quaternion.AngleAxis(IdleNeckLeanDegrees, Vector3.right) * Vector3.up);
                _neck.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(line, upright), w) * _neck.rotation;
            }

            // The face: tipped to the idle's own angle, keeping its heading.
            Vector3 face = Quaternion.Inverse(body) * _head.forward;
            float heading = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
            Vector3 level = body * (Quaternion.Euler(-IdleFacePitchDegrees, heading, 0.0f) * Vector3.forward);
            _head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(_head.forward, level), w) * _head.rotation;
        }

        private static float LeadYaw(Transform foot, Vector3 heading, float side)
        {
            Vector3 toes = foot.forward;
            toes.y = 0.0f;
            if (toes.sqrMagnitude < 1e-4f)
            {
                return 0.0f;
            }
            float toward = Vector3.SignedAngle(toes.normalized, heading, Vector3.up);
            return Mathf.Clamp(toward * LeadFootYawShare * Mathf.Clamp01(side), -LeadFootMaxYawDegrees, LeadFootMaxYawDegrees);
        }

        /// <summary>
        /// The clip's ankle (its offset from the body along the travel scaled by the stride) and the ground under it (under its lock while it is held):
        /// a ray from above the highest step a foot could be on. The foot's lowest point this frame comes from its rotation (toe, heel or sole,
        /// whichever is lowest: a running foot strikes toe first).
        /// </summary>
        private FootPlanting.Sample SampleFoot(int index, Transform foot, Transform upper, Transform lower, float floorY, Vector3 heading)
        {
            Vector3 animated = foot.position;
            // STRIDE WARPING: the foot's offset from the body's centre along its travel, scaled (the sprint's feet reach 20% further than the run clip's).
            if (heading != Vector3.zero && Mathf.Abs(_strideScale - 1.0f) > 0.001f)
            {
                float along = Vector3.Dot(animated - transform.position, heading);
                animated += heading * (along * (_strideScale - 1.0f));
            }
            var sample = new FootPlanting.Sample
            {
                Animated = animated,
                Normal = Vector3.up,
                Hip = upper.position,
                LegLength = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, foot.position),
            };
            float scale = foot.lossyScale.y;
            float lowest = float.MaxValue;
            foreach (float along in new[] { FusedBodyShape.FootToeCentreForward + FusedBodyShape.FootToeRadius, 0.06f, FusedBodyShape.FootHeelCentreForward - FusedBodyShape.FootHeelRadius })
            {
                Vector3 corner = foot.position + foot.rotation * (new Vector3(0.0f, -BodyRig.FootY, along) * scale);
                lowest = Mathf.Min(lowest, corner.y);
            }
            sample.SoleBelowAnkle = Mathf.Max(0.01f, foot.position.y - lowest);

            Vector3 probe = _feet.ProbeAt(index, animated);
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
        /// world rather than turned with the shin, the whole-body lean taken back off it (a sole stays flat on the ground while the body leans), the
        /// sole tilted onto the slope, and the leading foot of a strafe turned toward the travel by <paramref name="yawDegrees"/>.
        /// </summary>
        private void SolveLeg(int index, Transform upper, Transform lower, Transform foot, Vector3 outward, Quaternion lean, float yawDegrees)
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
            Quaternion wanted = Quaternion.AngleAxis(yawDegrees, Vector3.up) * tilt * unlean * footBefore;
            foot.rotation = Quaternion.Slerp(footBefore, wanted, weight);
        }
    }
}
