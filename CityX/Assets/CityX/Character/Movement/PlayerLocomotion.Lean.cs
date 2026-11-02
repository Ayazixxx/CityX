using CityX.Core;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE BODY LEAN (BodyLean): the visible body tilts into its acceleration and its turns, about its feet.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        private readonly BodyLean _lean = new BodyLean();

        private Vector3 _leanLastPosition;

        private bool _leanHasPosition;

        private Quaternion _rootRestRotation = Quaternion.identity;

        private bool _hasRootRest;

        /// <summary>The body's lean now (BodyLean), degrees; for tests.</summary>
        public float LeanDegrees => _lean.Degrees;

        public Vector3 LeanTilt => _lean.Tilt;

        /// <summary>
        /// Tilts the whole visible body about its feet by BodyLean: into acceleration, braking and turns, a little into speed. From the body's own
        /// measured motion (so a copy of another player's body, moved by the network, leans the same), on the ground in the plain walk/run blend
        /// only (the synced Grounded parameter), upright again anywhere else. The visual root is reset first every frame, so nothing compounds.
        /// </summary>
        private void ApplyBodyLean(bool animatorLive)
        {
            if (_animator == null)
            {
                return;
            }
            float deltaTime = Time.deltaTime;
            Vector3 position = transform.position;
            Vector3 velocity = Vector3.zero;
            if (_leanHasPosition && deltaTime > 0.0f)
            {
                Vector3 moved = position - _leanLastPosition;
                if (moved.sqrMagnitude > TeleportMetres * TeleportMetres)
                {
                    _lean.Reset();   // a teleport or respawn is not a sprint
                }
                else
                {
                    velocity = moved / deltaTime;
                }
            }
            _leanLastPosition = position;
            _measuredVelocity = velocity;
            _leanHasPosition = true;

            // A baked move carries its own lean (an Octaslash dash along the ground is 52 m/s, which would tip the running lean right over).
            bool leaning = animatorLive && !IsSitting && !InCarSeat
                && _animator.GetBool(GroundedParameter) && LocomotionClipsOwnTheSpine && _bakedPoseWeight <= 0.0f;
            // THE SLOPE UNDER THE FEET (from the foot placement's ground rays): walking uphill leans into the hill, downhill a little back.
            Vector3 slopeLean = Vector3.zero;
            var flatVelocity = new Vector3(velocity.x, 0.0f, velocity.z);
            if (leaning && flatVelocity.sqrMagnitude > 0.25f && _groundNormal.y > 0.5f)
            {
                Vector3 direction = flatVelocity.normalized;
                float rise = -(_groundNormal.x * direction.x + _groundNormal.z * direction.z) / _groundNormal.y;   // the slope's tangent along the travel, uphill positive
                float slopeDegrees = Mathf.Atan(rise) * Mathf.Rad2Deg;
                float degrees = slopeDegrees > 0.0f ? slopeDegrees * BodyLean.UphillLeanFraction : slopeDegrees * BodyLean.DownhillLeanFraction;
                slopeLean = direction * degrees;
            }
            _lean.Step(velocity, leaning, deltaTime, slopeLean);
            Quaternion world = _lean.Rotation();
            Transform root = _animator.transform;
            if (!_hasRootRest)
            {
                _rootRestRotation = root.localRotation;
                _hasRootRest = true;
            }
            // ON TOP OF WHATEVER ELSE TILTED THE BODY THIS FRAME, not instead of it. The jetpack leans this same transform in its Update, and
            // writing rest-plus-lean here every LateUpdate stood a flying body bolt upright (owner, 2026-10-10: "the jetpack lean is broken the
            // character stays upright"). So last frame's lean is taken back off first -- leaving the jetpack's tilt if it set one this frame, or
            // what was under the lean if nothing did -- and this frame's goes on. Within a hair of rest counts as rest, so float error never
            // builds up in a body that only ever leans.
            Quaternion under = Quaternion.Inverse(_leanApplied) * root.localRotation;
            if (Quaternion.Angle(under, _rootRestRotation) < 0.05f)
            {
                under = _rootRestRotation;
            }
            _leanApplied = Quaternion.Inverse(transform.rotation) * world * transform.rotation;
            root.localRotation = _leanApplied * under;
        }

        /// <summary>The lean put on the visual root last frame, in its parent's space, to be taken off again before the next.</summary>
        private Quaternion _leanApplied = Quaternion.identity;
    }
}
