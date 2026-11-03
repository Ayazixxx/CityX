using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE STEP UP (<see cref="StepUpProbe"/>): a knee-high step in the way -- a doorstep, a shop window's sill, a low wall -- is stepped onto in
    /// the stride instead of stopping the body dead (owner, 2026-10-14: "im getting stuck when running into small obstacles like this when i should
    /// just be running right over this"). The body is lifted along a 45 degree line as it closes on the face, so it is up as it gets there, carried
    /// level until its middle is over the top, then set down onto it. The controller's own one-frame step (0.4 m) is held at next to nothing
    /// meanwhile, or it would pop the body up the rest of the way in a single frame. It counts as being on the ground throughout (a jump, the
    /// footsteps, the animator's Grounded): it is a stride, not a hop, and other players (who get the Grounded parameter and the transform) see a
    /// body stepping up, not one falling.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How far the body rises for each metre it closes on a step: 1 is a 45 degree line, up just as it reaches the face.</summary>
        public const float StepUpRisePerMetre = 1.0f;

        /// <summary>The slowest the body rises onto a step, metres a second (pressing on from standing against it).</summary>
        public const float StepUpMinRiseSpeed = 3.0f;

        /// <summary>The longest a step up may take, seconds: whatever holds the body longer than this has stopped it, and the step lets go.</summary>
        public const float StepUpMaxSeconds = 0.6f;

        /// <summary>After a step up that did not get the body onto the top, this long passes before another is tried, seconds (no bobbing at a step it cannot take).</summary>
        public const float StepUpRetrySeconds = 0.35f;

        /// <summary>The body rises this far over the top, metres (over the controller's 3 cm skin), and is carried level there until over it.</summary>
        private const float StepUpClearance = 0.05f;

        /// <summary>The body is over the top when its middle is this far past the face, metres: its feet are above the top.</summary>
        private const float StepUpOverMetres = 0.05f;

        /// <summary>How fast the body is set down onto the top once over it, metres a second: the speed a grounded body is held to the ground with.</summary>
        private const float StepUpSettleSpeed = 2.0f;

        /// <summary>What the controller may step on its own while the body is lifted: next to nothing, or it would pop the rest in one frame.</summary>
        private const float StepUpControllerStep = 0.01f;

        private bool _steppingUp;
        private bool _stepUpOver;
        private float _stepUpTopY;
        private Vector3 _stepUpFace;
        private Vector3 _stepUpNormal;
        private float _stepUpSeconds;
        private int _stepUpFrame;
        private float _stepUpRestingOffset;
        private float _stepUpRetry;

        /// <summary>True while the body is being lifted onto a step. It counts as on the ground (<see cref="IsGrounded"/>).</summary>
        public bool IsSteppingUp => _steppingUp;

        /// <summary>
        /// Before the move: starts a step up when one is close enough ahead of <paramref name="travel"/> (a 45 degree line from the body to the top),
        /// and gives this frame's vertical motion -- <paramref name="verticalMotion"/> itself when no step is being taken.
        /// </summary>
        private float TickStepUp(Vector3 travel, bool moving, bool grounded, bool rooted, float verticalMotion, float deltaTime)
        {
            if (_steppingUp && Time.frameCount - _stepUpFrame > 1)
            {
                EndStepUp();   // a frame went by without a Tick: a vault, a climb or the ragdoll had the body
            }
            _stepUpRetry = Mathf.Max(0.0f, _stepUpRetry - deltaTime);
            var level = new Vector3(travel.x, 0.0f, travel.z);
            float speed = level.magnitude;

            if (!_steppingUp)
            {
                if (!grounded || rooted || !moving || _stepUpRetry > 0.0f || speed < 0.5f || deltaTime <= 0.0f)
                {
                    return verticalMotion;
                }
                float thisFrame = speed * deltaTime;
                // Measured from what the body stands on, not the bottom of its capsule: perched on a stair's nosing the rounded bottom dips below
                // the tread it stands on, and an ordinary 0.22 m riser read as a 0.3 m step (SmallObstaclePlayTests' stairs, in full runs).
                Vector3 feet = transform.position;
                feet.y = Mathf.Max(feet.y, StandingOnY(feet));
                if (!StepUpProbe.TryFind(feet, level, _controller.height * BodyScale, _controller.radius * BodyScale, thisFrame, transform, out StepUpProbe.Step step) ||
                    step.Gap > step.Rise / StepUpRisePerMetre + thisFrame)
                {
                    return verticalMotion;
                }
                _steppingUp = true;
                _stepUpOver = false;
                _stepUpTopY = step.TopY;
                _stepUpFace = step.FacePoint;
                _stepUpNormal = step.Normal;
                _stepUpSeconds = 0.0f;
                _stepUpRestingOffset = _controller.stepOffset;
                _controller.stepOffset = StepUpControllerStep;
            }

            _stepUpFrame = Time.frameCount;
            if (_stepUpOver)
            {
                _verticalVelocity = 0.0f;
                return -StepUpSettleSpeed;   // over the top: set down onto it (AfterStepUpMove ends the step once it stands there)
            }

            // It lets go when the keys do, when the body turns away from the step, and when something has held it too long.
            bool towardIt = speed > 0.1f && Vector3.Dot(level / speed, -_stepUpNormal) > StepUpProbe.MinFacingDot * 0.5f;
            if (rooted || !moving || !towardIt || _stepUpSeconds > StepUpMaxSeconds)
            {
                EndStepUp();
                _stepUpRetry = StepUpRetrySeconds;
                return verticalMotion;
            }
            _verticalVelocity = 0.0f;
            float remaining = _stepUpTopY + StepUpClearance - transform.position.y;
            if (remaining <= 0.0f)
            {
                return 0.0f;   // up: carried level until the feet are over the top (AfterStepUpMove)
            }
            float riseSpeed = Mathf.Max(StepUpMinRiseSpeed, speed * StepUpRisePerMetre);
            return Mathf.Min(riseSpeed, remaining / deltaTime);
        }

        /// <summary>
        /// After the move: once the body is up and its feet are over the top it is set down; the step up ends when it stands there (or has sunk past
        /// a thin top's far side, an ordinary drop), at a ceiling, or when it has taken too long.
        /// </summary>
        private void AfterStepUpMove(float deltaTime)
        {
            if (!_steppingUp)
            {
                return;
            }
            _stepUpSeconds += deltaTime;
            if (_stepUpOver)
            {
                if (_controller.isGrounded || transform.position.y < _stepUpTopY - 0.02f || _stepUpSeconds > StepUpMaxSeconds)
                {
                    EndStepUp();
                }
                return;
            }
            if ((LastMoveCollisionFlags & CollisionFlags.Above) != 0)
            {
                EndStepUp();
                _stepUpRetry = StepUpRetrySeconds;
                return;
            }
            bool up = transform.position.y >= _stepUpTopY + StepUpClearance - 0.005f;
            bool over = Vector3.Dot(transform.position - _stepUpFace, -_stepUpNormal) >= StepUpOverMetres;
            _stepUpOver = up && over;
        }

        private static readonly RaycastHit[] GroundHits = new RaycastHit[8];

        /// <summary>
        /// The height of what the body stands on: a sphere a little narrower than the body swept down from the middle of its rounded bottom, its
        /// first touch. On flat ground that is the ground; perched on an edge (a stair's nosing, a kerb) it is the edge, which the capsule's lowest
        /// point dips below. float.MinValue when there is nothing under it.
        /// </summary>
        private float StandingOnY(Vector3 feet)
        {
            float radius = _controller.radius * BodyScale;
            Vector3 from = feet + Vector3.up * (radius + 0.02f * BodyScale);
            int count = Physics.SphereCastNonAlloc(from, radius * 0.95f, Vector3.down, GroundHits, 0.3f * BodyScale, CityX.World.GlassFracture.AllButShards, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            float y = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = GroundHits[i];
                if (hit.collider == null || hit.distance <= 0.0f || hit.distance >= nearest || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                nearest = hit.distance;
                y = hit.point.y;
            }
            return y;
        }

        /// <summary>
        /// The controller's own step on open ground, metres: a kerb, a stair's riser (set by ApplyHeight). World metres, so it goes with the
        /// body's size (Grow and Shrink) -- but Unity refuses a step taller than the controller's own UNSCALED height whatever its scale, so a
        /// giant's resting step stops there (2.3 m: a car) and the step up, scaled, takes anything taller.
        /// </summary>
        private float RestingStepOffset =>
            Mathf.Min(Mathf.Min(0.4f, StandingHeight * 0.25f) * BodyScale, (_controller != null ? _controller.height : StandingHeight) * 0.9f);

        private static readonly RaycastHit[] CeilingHits = new RaycastHit[8];

        /// <summary>
        /// UNDER A LOW CEILING THE CONTROLLER'S STEP IS CUT TO THE ROOM OVER THE HEAD. Before every sideways move the CharacterController lifts the
        /// body by its whole step offset to see whether it can step up; with a ceiling closer than that over the head the lift met it and the body
        /// could not move at all, not even on flat ground -- under the top of a shop window it had just stepped into (0.2 m over the head) it stood
        /// on the sill, held. The step is cut to the free height over the head each move (it needs no more: a step up the head has no room for
        /// cannot be taken anyway). The lift happens where the body is and the sideways move at the lifted height, so the room is measured over the
        /// whole patch the head can sweep this move -- the body's width plus the move plus a margin, every way -- not only straight up: under a
        /// shop's canopy (seed 74102958, building 5248) the lifted head met the canopy's front edge 0.3 m ahead and 2.75 m up, was pushed back,
        /// and the body shook on the spot instead of walking in under it. The step up holds its own.
        /// </summary>
        private void FitStepUnderCeiling(Vector3 move)
        {
            if (_steppingUp || _controller == null)
            {
                return;
            }
            float rest = RestingStepOffset;
            float skin = _controller.skinWidth;
            // The controller's own sizes are the body's (they scale with it, BodyScale); its step and skin are world metres.
            float scale = BodyScale;
            float reach = _controller.radius * scale + new Vector3(move.x, 0.0f, move.z).magnitude + skin + 0.05f * scale;
            Vector3 headTop = transform.position + Vector3.up * ((_controller.center.y + _controller.height * 0.5f) * scale + skin);
            float free = FreeOverPatch(headTop, reach, rest + 0.05f);
            _controller.stepOffset = Mathf.Clamp(free - 0.02f, 0.02f, rest);
        }

        /// <summary>
        /// How far a flat square, <paramref name="halfWidth"/> either way of <paramref name="from"/> (the top of the head), rises before it meets
        /// something, up to <paramref name="most"/>. What it overlaps already (a wall the body is brushing) is not over it, and the body's own
        /// capsule and limbs are not anything.
        /// </summary>
        private float FreeOverPatch(Vector3 from, float halfWidth, float most)
        {
            var half = new Vector3(halfWidth, 0.01f, halfWidth);
            int count = Physics.BoxCastNonAlloc(from, half, Vector3.up, CeilingHits, Quaternion.Euler(0.0f, transform.eulerAngles.y, 0.0f), most,
                CityX.World.GlassFracture.AllButShards, QueryTriggerInteraction.Ignore);
            float free = most;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = CeilingHits[i];
                if (hit.collider == null || hit.distance <= 0.0f || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                free = Mathf.Min(free, hit.distance);
            }
            return free;
        }

        /// <summary>Hands the body back to gravity and gives the controller its own step again.</summary>
        private void EndStepUp()
        {
            if (!_steppingUp)
            {
                return;
            }
            _steppingUp = false;
            _stepUpOver = false;
            if (_controller != null)
            {
                _controller.stepOffset = _stepUpRestingOffset;
            }
        }
    }
}
