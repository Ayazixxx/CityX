using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE ARM'S SWING OF A THROW (docs/BOOMERANG.md): the right hand comes over the shoulder from behind the head, out along the aim and down across the
    /// body, the torso turning with it. Posed after the Animator, on this body and on other players' copies alike: whoever throws calls
    /// <see cref="PlayThrowSwing"/>.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>Terraria's use time, 20 ticks.</summary>
        public const float ThrowSwingSeconds = 1.0f / 3.0f;

        /// <summary>The hand from the right shoulder, metres on the built body (right, up, along the aim): behind the head, let go, finished across the body.</summary>
        public static readonly Vector3 ThrowHandBehind = new Vector3(0.3f, 0.55f, -0.3f);

        public static readonly Vector3 ThrowHandLetGo = new Vector3(0.1f, 0.25f, 0.85f);

        public static readonly Vector3 ThrowHandFinish = new Vector3(-0.15f, -0.35f, 0.6f);

        /// <summary>When the hand is at <see cref="ThrowHandLetGo"/>, as a share of the swing.</summary>
        public const float ThrowLetGoShare = 0.35f;

        /// <summary>The torso's turn, degrees: right shoulder back at the start, forward at the finish.</summary>
        public const float ThrowTorsoBack = 25.0f;

        public const float ThrowTorsoForward = -20.0f;

        /// <summary>The swing comes in over this share of it, and goes back out to the Animator's pose over the same at its end.</summary>
        public const float ThrowBlendShare = 0.2f;

        private float _throwSwingLeft;
        private Vector3 _throwAim = Vector3.forward;

        /// <summary>The arm is swinging a throw.</summary>
        public bool IsThrowSwinging => _throwSwingLeft > 0.0f;

        /// <summary>Swings the right arm, throwing along <paramref name="aim"/>.</summary>
        public void PlayThrowSwing(Vector3 aim)
        {
            if (aim.sqrMagnitude > 1e-6f)
            {
                _throwAim = aim.normalized;
            }
            _throwSwingLeft = ThrowSwingSeconds;
        }

        private void ApplyThrowSwing()
        {
            if (_throwSwingLeft <= 0.0f || _rightUpperArm == null || _rightLowerArm == null || _handR == null)
            {
                return;
            }
            _throwSwingLeft = Mathf.Max(0.0f, _throwSwingLeft - Mathf.Max(0.0f, Time.deltaTime));
            float t = 1.0f - _throwSwingLeft / ThrowSwingSeconds;
            float weight = Mathf.SmoothStep(0.0f, 1.0f, Mathf.Min(t, 1.0f - t) / ThrowBlendShare);
            float scale = WorldHeight / BodyRig.StandingHeight;

            Vector3 aim = _throwAim;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, aim);
            up = up.sqrMagnitude > 1e-6f ? up.normalized : transform.up;
            Vector3 right = Vector3.Cross(up, aim).normalized;

            bool early = t < ThrowLetGoShare;
            float leg = Mathf.SmoothStep(0.0f, 1.0f, early ? t / ThrowLetGoShare : (t - ThrowLetGoShare) / (1.0f - ThrowLetGoShare));
            if (_spine != null)
            {
                float turn = Mathf.Lerp(ThrowTorsoBack, ThrowTorsoForward, t);
                _spine.rotation = Quaternion.AngleAxis(turn * weight, Vector3.up) * _spine.rotation;
            }
            Vector3 hand = early ? Vector3.Lerp(ThrowHandBehind, ThrowHandLetGo, leg) : Vector3.Lerp(ThrowHandLetGo, ThrowHandFinish, leg);
            Vector3 target = _rightUpperArm.position + (right * hand.x + up * hand.y + aim * hand.z) * scale;
            TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, target, right + Vector3.down * 0.5f, weight);
        }
    }
}
