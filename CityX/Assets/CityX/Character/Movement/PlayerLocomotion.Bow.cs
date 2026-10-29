using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HOLDING, NOCKING AND DRAWING A BOW (docs/ARCHERY.md; owner: "make the drawback animations with the string on the bow, maybe research red
    /// dead as a guide"). Red Dead's archer: the bow arm straight out along the aim with the bow upright in the fist; the string hand reaches
    /// over the shoulder to the quiver for an arrow, brings it to the string, then draws it back along the aim to the cheek, the elbow high and
    /// out; on the loose the hand flicks back off the string. Keyed here over the draw (0..1): the first <see cref="BowNockShare"/> of it is the
    /// nock (to the quiver and back to the string), the rest the pull. Posed by two-bone IK after the straight wrists (which would undo the
    /// fists' turns), the bow itself placed in the fist from here (it is parented to the hand only to follow it), on this body and on other
    /// players' copies alike: whoever has the bow calls <see cref="HoldBow"/> every frame it is up and <see cref="LooseBow"/> when it shoots.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How long the arms take to come up to the aim (and down), seconds.</summary>
        public const float BowRaiseSeconds = 0.15f;

        /// <summary>How long one <see cref="HoldBow"/> keeps the bow up, seconds.</summary>
        public const float BowHoldSeconds = 0.1f;

        /// <summary>How far before the left shoulder the bow hand is held, metres (on the built body).</summary>
        public const float BowArmReach = 0.56f;

        /// <summary>How far back along the aim the string hand comes at full draw, metres, and how far it rises and comes right, to the cheek.</summary>
        public const float BowDrawLength = 0.62f;

        public static readonly Vector3 BowCheek = new Vector3(0.16f, 0.14f, 0.0f);

        /// <summary>How far behind the bow hand the string hand rests with the bow undrawn, metres: the nock, just behind the grip.</summary>
        public const float BowNockBehind = 0.10f;

        /// <summary>The share of the draw spent nocking (over the shoulder to the quiver and back to the string); the rest is the pull.</summary>
        public const float BowNockShare = 0.3f;

        /// <summary>The quiver, from the head: this far behind along the aim and this far to the right, metres.</summary>
        public const float BowQuiverBehind = 0.28f;

        public const float BowQuiverRight = 0.2f;

        /// <summary>The string hand's flick off the string on the loose: this far back and out, over this long, seconds.</summary>
        public static readonly Vector3 BowReleaseFlick = new Vector3(0.05f, 0.02f, -0.09f);

        public const float BowReleaseSeconds = 0.22f;

        /// <summary>How fast the draw follows the draw it is told, per second (a copy's, and the eased one here).</summary>
        public const float BowDrawFollowPerSecond = 6.0f;

        private Transform _bow;
        private float _bowRaise;
        private float _bowHold;
        private float _bowDraw;
        private float _bowDrawWanted;
        private float _bowRelease;
        private Vector3 _bowAim = Vector3.forward;

        /// <summary>A bow is in the left hand.</summary>
        public bool HoldsBow => _bow != null;

        /// <summary>How far the bow is up at the aim, 0 hanging .. 1 aimed.</summary>
        public float BowRaise => _bowRaise;

        /// <summary>How far along the draw the body is, 0..1 (eased): the nock first, then the pull.</summary>
        public float BowDraw => _bowDraw;

        /// <summary>How far the string is pulled back, 0..1: none until the arrow is nocked, full at the cheek.</summary>
        public float BowPull => Mathf.Clamp01((_bowDraw - BowNockShare) / (1.0f - BowNockShare));

        /// <summary>The string hand has an arrow (it reached the quiver).</summary>
        public bool BowArrowInHand => _bowDraw >= BowNockShare * 0.5f;

        /// <summary>The way the bow aims (unit), as last told.</summary>
        public Vector3 BowAim => _bowAim;

        /// <summary>Where the string hand holds the string now (world): the nock, where the arrow sits.</summary>
        public Vector3 BowNock => _handR != null ? _handR.position : transform.position;

        public void SetBow(Transform bow)
        {
            _bow = bow;
            _bowRaise = 0.0f;
            _bowDraw = 0.0f;
            _bowDrawWanted = 0.0f;
            _bowRelease = 0.0f;
        }

        public void ClearBow(Transform bow)
        {
            if (bow == null || _bow == bow)
            {
                _bow = null;
                _bowRaise = 0.0f;
                _bowDraw = 0.0f;
            }
        }

        /// <summary>The bow is up at <paramref name="aim"/>, drawn <paramref name="draw"/> (0..1): called every frame it is aimed or drawn.</summary>
        public void HoldBow(Vector3 aim, float draw)
        {
            _bowHold = BowHoldSeconds;
            if (aim.sqrMagnitude > 1e-6f)
            {
                _bowAim = aim.normalized;
            }
            _bowDrawWanted = Mathf.Clamp01(draw);
        }

        /// <summary>The arrow is loosed: the draw is gone at once and the string hand flicks back off the string.</summary>
        public void LooseBow()
        {
            _bowDraw = 0.0f;
            _bowDrawWanted = 0.0f;
            _bowRelease = BowReleaseSeconds;
        }

        private void ApplyBowPose()
        {
            if (_bow == null || _leftUpperArm == null || _leftLowerArm == null || _handL == null || _rightUpperArm == null || _rightLowerArm == null || _handR == null)
            {
                return;
            }
            float deltaTime = Mathf.Max(0.0f, Time.deltaTime);
            bool wanted = _bowHold > 0.0f;
            _bowHold = Mathf.Max(0.0f, _bowHold - deltaTime);
            _bowRelease = Mathf.Max(0.0f, _bowRelease - deltaTime);
            _bowRaise = Mathf.MoveTowards(_bowRaise, wanted ? 1.0f : 0.0f, deltaTime / BowRaiseSeconds);
            _bowDraw = Mathf.MoveTowards(_bowDraw, wanted ? _bowDrawWanted : 0.0f, BowDrawFollowPerSecond * deltaTime);
            float raise = _bowRaise * _bowRaise * (3.0f - 2.0f * _bowRaise);
            float scale = WorldHeight / BodyRig.StandingHeight;

            Vector3 aim = _bowAim;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, aim);
            up = up.sqrMagnitude > 1e-6f ? up.normalized : transform.up;
            Vector3 right = Vector3.Cross(up, aim).normalized;

            if (raise > 0.0f)
            {
                // The bow arm: straight out along the aim from the left shoulder, the fist upright on the grip, palm to the aim, thumb up.
                Vector3 bowHand = _leftUpperArm.position + aim * (BowArmReach * scale);
                TwoBoneIK.Solve(_leftUpperArm, _leftLowerArm, _handL, bowHand, -right + Vector3.down * 0.5f, raise);
                // The hand bone: +X back up the arm on the left hand, -Y the palm, +Z the thumb (HandGrip).
                _handL.rotation = Quaternion.Slerp(_handL.rotation, Quaternion.LookRotation(up, -aim), raise);

                // The string hand: at the nock behind the grip; over the shoulder to the quiver and back while nocking; then drawn back along the
                // aim and up to the cheek; flicked back off the string on the loose. The elbow high and out.
                Vector3 nock = bowHand - aim * (BowNockBehind * scale);
                Vector3 cheek = bowHand - aim * (BowDrawLength * scale) + right * (BowCheek.x * scale) + up * (BowCheek.y * scale);
                Vector3 quiver = (_head != null ? _head.position : _rightUpperArm.position + up * (0.3f * scale))
                    - aim * (BowQuiverBehind * scale) + right * (BowQuiverRight * scale);
                Vector3 stringHand;
                if (_bowDraw < BowNockShare)
                {
                    float t = _bowDraw / BowNockShare;
                    stringHand = t < 0.5f
                        ? Vector3.Lerp(nock, quiver, Mathf.SmoothStep(0.0f, 1.0f, t * 2.0f))
                        : Vector3.Lerp(quiver, nock, Mathf.SmoothStep(0.0f, 1.0f, (t - 0.5f) * 2.0f));
                }
                else
                {
                    stringHand = Vector3.Lerp(nock, cheek, Mathf.SmoothStep(0.0f, 1.0f, BowPull));
                }
                if (_bowRelease > 0.0f)
                {
                    float flick = Mathf.Sin(_bowRelease / BowReleaseSeconds * Mathf.PI);
                    stringHand += (right * BowReleaseFlick.x + up * BowReleaseFlick.y + aim * BowReleaseFlick.z) * (scale * flick);
                }
                TwoBoneIK.Solve(_rightUpperArm, _rightLowerArm, _handR, stringHand, right + Vector3.up * 0.6f, raise);
                // Palm to the archer (the fingers hooked on the string), thumb up.
                _handR.rotation = Quaternion.Slerp(_handR.rotation, Quaternion.LookRotation(up, aim), raise);
            }

            // The bow in the left fist (its origin the grip's middle, +Y up its limbs, +Z the way it shoots): raised, upright along the aim;
            // lowered, upright at the side facing the way the body does.
            Vector3 palm = _handL.position + _handL.rotation * new Vector3(-PalmOffsetMetres * scale, 0.0f, 0.0f);
            Vector3 bodyForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            Quaternion lowered = Quaternion.LookRotation(bodyForward.sqrMagnitude > 1e-6f ? bodyForward.normalized : Vector3.forward, Vector3.up);
            Quaternion raised = Quaternion.LookRotation(aim, up);
            _bow.SetPositionAndRotation(palm, Quaternion.Slerp(lowered, raised, raise));
        }
    }
}
