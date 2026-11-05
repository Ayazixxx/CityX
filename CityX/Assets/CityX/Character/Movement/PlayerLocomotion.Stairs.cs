using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// STAIRS (owner, 2026-11-02: "make going up and down stairs look less jank", docs/STAIRS.md). Going down, the capsule is held to the ground instead of flying off each
    /// tread; up or down, the body and the camera glide along the blurred ground (<see cref="GroundGlide"/>) instead of following the capsule's lurches.
    /// Read from the body's own position and the ground in LateUpdate, like the foot placement, so a copy of another player's body glides too with nothing sent.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>The furthest down a walking body is held to the ground, metres: a riser or two at a run, a kerb. A taller drop is a fall.</summary>
        public const float StickToGroundMetres = 0.45f;

        /// <summary>A surface tipped further than this from flat is not ground to be held to (the normal's height).</summary>
        private const float StickMinNormalY = 0.3f;

        private readonly GroundGlide _glide = new GroundGlide();
        private readonly StairSteps _stairSteps = new StairSteps();
        private readonly float[] _glideGround = new float[GroundGlide.SampleAlong.Length];
        private StairSteps.GroundAt _stairGround;
        private bool _glideRead;
        private static readonly RaycastHit[] StickHits = new RaycastHit[8];

        /// <summary>The stepping feet on a stair (StairSteps), for tests and captures.</summary>
        public StairSteps StairSteps => _stairSteps;

        /// <summary>A stair's feet read the ground this far above and below where they are.</summary>
        private const float StairRayMetres = 1.2f;

        /// <summary>Moving more sideways than this share of forward or back, the feet are left to the clip: the stepper's feet go one in front of the other.</summary>
        private const float StairStepsLeastForwardShare = 0.6f;

        /// <summary>How far above the capsule the body is drawn, metres (below when negative): the camera goes with it.</summary>
        public float GlideOffset => _glide.Offset;

        /// <summary>The ground's rise per metre along the travel, read past its steps: 0 on the flat, about 0.63 up a stair, negative down one.</summary>
        public float GroundSlope => _glide.Slope;

        /// <summary>
        /// WALKING DOWN, THE BODY STAYS ON THE GROUND. The controller knows only how to step up: off each tread it was falling, 73 of 88 frames in the air down a flight at a
        /// walk and all of them at a sprint, hanging over the stairs, unable to jump, its lean flicking between the ground's and the air's. Having been on the ground and
        /// having left it this move, not by jumping, it is put down on whatever is close under it.
        /// </summary>
        private void StickToGround(bool wasGrounded, float verticalMotion, bool flying)
        {
            if (!wasGrounded || _controller.isGrounded || _steppingUp || verticalMotion > 0.0f || flying || HasForcedVelocity || IsFloating)
            {
                return;
            }
            float radius = _controller.radius * BodyScale;
            Vector3 from = transform.position + Vector3.up * (radius + 0.02f * BodyScale);
            float reach = StickToGroundMetres * BodyScale;
            int count = Physics.SphereCastNonAlloc(from, radius * 0.95f, Vector3.down, StickHits, reach, CityX.World.GlassFracture.AllButShards, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = StickHits[i];
                if (hit.collider == null || hit.distance <= 0.0f || hit.distance >= nearest || hit.normal.y < StickMinNormalY
                    || hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<IDamageable>() != null)
                {
                    continue;
                }
                nearest = hit.distance;
            }
            if (nearest < float.MaxValue)
            {
                LastMoveCollisionFlags |= _controller.Move(Vector3.down * nearest);
            }
        }

        /// <summary>Reads the ground along the way the body goes (ahead by what the glide will lag) and steps the glide. A body that has not moved reads nothing new.</summary>
        private void StepGlide(Vector3 position, Vector3 moved, float deltaTime, bool onGround)
        {
            var flat = new Vector3(moved.x, 0.0f, moved.z);
            float distance = flat.magnitude;
            float speed = distance / deltaTime;
            float lead = speed > 0.5f ? speed * GroundGlide.EaseSeconds : 0.0f;
            if (onGround && (!_glideRead || distance > 0.0005f))
            {
                Vector3 along = speed > 0.5f ? flat / distance : transform.forward;
                for (int i = 0; i < _glideGround.Length; i++)
                {
                    Vector3 at = position + along * (GroundGlide.SampleAlong[i] + lead);
                    at.y = position.y + GlideRayAbove;
                    _glideGround[i] = GroundUnder(at, GlideRayAbove * 2.0f, out RaycastHit hit) ? hit.point.y : float.NaN;
                }
                _glideRead = true;
            }
            _glide.Step(position.y, _glideGround, _controller != null ? _controller.skinWidth : 0.0f, lead, onGround, deltaTime);
        }

        private const float GlideRayAbove = 0.9f;

        /// <summary>How much of a stair's angle the torso bends over the legs going up, and back from them going down.</summary>
        public const float TorsoUphillShare = 0.35f;

        public const float TorsoDownhillShare = 0.15f;

        private const float TorsoBendSeconds = 0.2f;

        private float _stairTorsoDegrees;

        /// <summary>The ground's height near a point, for the stair's feet.</summary>
        private bool StairGround(Vector3 at, out float height)
        {
            bool found = GroundUnder(new Vector3(at.x, at.y + StairRayMetres, at.z), StairRayMetres * 2.0f, out RaycastHit hit) && hit.normal.y >= 0.5f;
            height = found ? hit.point.y : 0.0f;
            return found;
        }

        /// <summary>
        /// One frame of the stair's feet: on a stair, on the ground, going forward or back and not in a sword fight or a crouch, they are StairSteps' (the legs all IK, SolveLeg).
        /// </summary>
        private void StepStairFeet(bool active, float footwork, Vector3 heading, float speed, float yaw, float floorY, float soleOffset, float deltaTime)
        {
            bool forwardOrBack = heading == Vector3.zero || Mathf.Abs(Vector3.Dot(heading, transform.forward)) >= StairStepsLeastForwardShare;
            float wanted = active && _glide.IsStairs && footwork <= 0.01f && forwardOrBack && !IsCrouching && !IsImpactFrozen ? 1.0f : 0.0f;
            Vector3 hips = 0.5f * (_leftUpperLeg.position + _rightUpperLeg.position);
            var body = new StairSteps.Body
            {
                Centre = new Vector3(hips.x, floorY, hips.z),
                Along = heading != Vector3.zero ? heading : transform.forward,
                Speed = heading != Vector3.zero ? speed : 0.0f,
                Slope = _glide.Slope,
                Yaw = yaw,
                LeftHip = _leftUpperLeg.position,
                RightHip = _rightUpperLeg.position,
                HalfWidth = 0.5f * Vector3.Distance(_leftUpperLeg.position, _rightUpperLeg.position),
                LegLength = Vector3.Distance(_leftUpperLeg.position, _leftLowerLeg.position) + Vector3.Distance(_leftLowerLeg.position, _leftFoot.position),
                SoleHeight = soleOffset,
                BallForward = FusedBodyShape.FootToeCentreForward * _leftFoot.lossyScale.y,
                ToeTip = FusedBodyShape.FootToeRadius * _leftFoot.lossyScale.y,
                HeelBack = (FusedBodyShape.FootToeCentreForward - FusedBodyShape.FootHeelCentreForward + FusedBodyShape.FootHeelRadius) * _leftFoot.lossyScale.y,
                ClipLeft = _leftFoot.position,
                ClipRight = _rightFoot.position,
            };
            if (_stairGround == null)
            {
                _stairGround = StairGround;
            }
            _stairSteps.Step(deltaTime, wanted, body, _stairGround);
            ShowStairClip(speed, deltaTime);
        }

        /// <summary>The Animator layers playing the recorded stair clips over the base (PlayerRigBuilder.AddStairLayers), and their state: the hips, spine and head, and the arms.</summary>
        public const string StairBodyLayerName = "StairBody";

        public const string StairArmsLayerName = "StairArms";

        public const string StairStateName = "Stairs";

        public const string StairPhaseName = "StairPhase";

        public const string StairFacingName = "StairFacing";

        public const string StairRunName = "StairRun";

        private static readonly int StairPhaseParameter = Animator.StringToHash(StairPhaseName);
        private static readonly int StairFacingParameter = Animator.StringToHash(StairFacingName);
        private static readonly int StairRunParameter = Animator.StringToHash(StairRunName);

        /// <summary>The clips come in and go over this long, seconds, and the body's pace changes over this long.</summary>
        private const float StairClipFadeSeconds = 0.2f;

        private const float StairPaceSeconds = 0.25f;

        private int _stairBodyLayer = -2;
        private int _stairArmsLayer = -2;
        private float _stairClip;
        private float _stairPace = 1.0f;

        /// <summary>How much of the upper body is the stair clip's just now, 0 to 1, for tests and captures.</summary>
        public float StairClipShare => _stairClip;

        /// <summary>
        /// THE RECORDED BODY ON A STAIR (owner, 2026-11-04: "should we just use mixamo stairs animations"): walking along one the hips, spine, head and free arms are the clip's, in time
        /// with the feet. Read from the stepper in LateUpdate, like the feet, so a copy of another player's body plays it too with nothing sent.
        /// </summary>
        private void ShowStairClip(float speed, float deltaTime)
        {
            if (_stairBodyLayer == -2 && _animator.runtimeAnimatorController != null)
            {
                _stairBodyLayer = _animator.GetLayerIndex(StairBodyLayerName);
                _stairArmsLayer = _animator.GetLayerIndex(StairArmsLayerName);
            }
            if (_stairBodyLayer < 0)
            {
                return;
            }
            float wanted = speed >= StairSteps.StandingSpeed ? _stairSteps.Weight : 0.0f;
            _stairClip = Mathf.MoveTowards(_stairClip, wanted, deltaTime / StairClipFadeSeconds);
            _animator.SetLayerWeight(_stairBodyLayer, _stairClip);
            // The arms only when nothing is using them: a weapon's carry is the base layer's.
            bool handsFree = !_animator.GetBool(SwordEquippedParameter) && !_animator.GetBool(Sword1HEquippedParameter) && !_animator.GetBool(GunEquippedParameter)
                && !_animator.GetBool(AimingParameter) && !_animator.GetBool(ShieldRaisedParameter);
            _animator.SetLayerWeight(_stairArmsLayer, handsFree ? _stairClip : 0.0f);
            if (_stairClip > 0.0f)
            {
                _animator.SetFloat(StairPhaseParameter, _stairSteps.Phase);
                _animator.SetFloat(StairFacingParameter, _stairSteps.FacesUphill ? 1.0f : -1.0f);
                _animator.SetFloat(StairRunParameter, StairGait.RunShare(speed));
            }
        }

        /// <summary>A body takes a stair at a stair's pace (the owner's reference clip: about three steps a second). Every body alike, so no enemy outruns a player up one.</summary>
        private float StairPaceShare(bool sprinting, float deltaTime)
        {
            bool onStair = _glide.IsStairs && (_controller.isGrounded || _steppingUp) && !IsCrouching;
            _stairPace = Mathf.MoveTowards(_stairPace, onStair ? StairGait.SpeedShare(sprinting) : 1.0f, deltaTime / StairPaceSeconds);
            return _stairPace;
        }

        /// <summary>The share of the slope's effects in force, from none at a gentle ramp to all on a stair.</summary>
        private float SlopeShare => Mathf.InverseLerp(FootPlanting.SlopeStartsLimiting, FootPlanting.SlopeLimitsFully, Mathf.Abs(_glide.Slope));

        /// <summary>
        /// ON A STAIR THE TORSO BENDS FORWARD OVER THE LEGS (back a little going down): at the spine, not by tipping the whole body as the lean does, which swings the feet out
        /// behind the head and off the steps they are placed on. The legs are already solved: nothing below the spine moves.
        /// </summary>
        private void ApplyStairTorso(float speed, float deltaTime)
        {
            float degrees = Mathf.Atan(_glide.Slope) * Mathf.Rad2Deg;
            // (The clip has its own lean while it plays.)
            float wanted = degrees * (degrees > 0.0f ? TorsoUphillShare : TorsoDownhillShare) * SlopeShare * Mathf.Clamp01(speed) * (1.0f - _stairClip);
            _stairTorsoDegrees = Mathf.Lerp(_stairTorsoDegrees, wanted, 1.0f - Mathf.Exp(-deltaTime / TorsoBendSeconds));
            if (_spine != null && Mathf.Abs(_stairTorsoDegrees) > 0.01f)
            {
                _spine.rotation = Quaternion.AngleAxis(_stairTorsoDegrees, transform.right) * _spine.rotation;
            }
        }
    }
}
