using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A WAND IN THE RIGHT HAND (docs/MAGIC_WAND.md), held as in Hogwarts Legacy: carried low at the side pointing ahead and down; to shoot or
    /// cast, the arm comes up and points it straight at what the crosshair is on (<see cref="WandAimPoint"/>), as a lit flashlight is pointed
    /// (PlayerLocomotion.HandHeldOut.cs). A spell cast through it leaves its tip, so the wand snaps out with the cast (<see cref="PointWandNow"/>)
    /// instead of easing up after the spell has gone. Another player's copy is pointed by the wand's shots (MagicSpeckCaster) and lifted by its
    /// spells' cast pose.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How long the wand takes to come up (and go back down), seconds.</summary>
        public const float WandRaiseSeconds = 0.08f;

        /// <summary>Pointed, where the hand goes from the right shoulder along the aim, and how far across and up off that line, metres (built height).</summary>
        public const float WandReach = 0.6f;

        public static readonly Vector3 WandHoldOffset = new Vector3(-0.04f, -0.08f, 0.0f);

        /// <summary>Carried, where the hand hangs from the right shoulder (across, up, forward), and how far below ahead the wand points, degrees.</summary>
        public static readonly Vector3 WandCarryHand = new Vector3(0.14f, -0.68f, 0.24f);

        public const float WandCarryDipDegrees = 55.0f;

        /// <summary>How long one <see cref="HoldWandOut"/> keeps the wand pointed, seconds: whoever wants it out calls it every frame.</summary>
        public const float WandHoldSeconds = 0.1f;

        /// <summary>How long an aim point holds once given, seconds: past it the wand points straight ahead (a copy told of a spell, not where).</summary>
        public const float WandAimFreshSeconds = 0.3f;

        private float _wandRaise;
        private float _wandHold;
        private float _wandAimAge = float.MaxValue;
        private Vector3 _wandAimPoint;

        /// <summary>The wand in this body's right hand, or null (set by the wand), and its tip.</summary>
        public Transform Wand { get; set; }

        public Transform WandTip { get; set; }

        /// <summary>Where the wand points when out: the crosshair's point (given every frame by the reader; on a copy, by its shots).</summary>
        public Vector3 WandAimPoint
        {
            get => _wandAimPoint;
            set
            {
                _wandAimPoint = value;
                _wandAimAge = 0.0f;
            }
        }

        /// <summary>The wand held out to shoot (the body then faces the crosshair, as with a staff or a gun).</summary>
        public bool WandPointing => Wand != null && _wandHold > 0.0f;

        /// <summary>How far up the wand is, 0 carried .. 1 pointed.</summary>
        public float WandRaise => _wandRaise;

        /// <summary>Keeps the wand pointed at <paramref name="aimPoint"/> a moment longer.</summary>
        public void HoldWandOut(Vector3 aimPoint)
        {
            WandAimPoint = aimPoint;
            _wandHold = WandHoldSeconds;
        }

        /// <summary>
        /// Points the wand at <paramref name="aimPoint"/> this very frame, no easing, and says where its tip is: something is leaving it. The arm
        /// is posed at once (and again the same after the Animator), so what leaves starts at the tip the frame shows.
        /// </summary>
        public Vector3 PointWandNow(Vector3 aimPoint)
        {
            HoldWandOut(aimPoint);
            _wandRaise = 1.0f;
            if (!CanPoseWand)
            {
                return transform.position + Vector3.up * (WorldHeight * 0.68f);
            }
            // The body faces where it points (Tick does too while the wand is out): turned first, so the arm is posed from the shoulder as drawn.
            Vector3 flat = Vector3.ProjectOnPlane(aimPoint - transform.position, Vector3.up);
            if (flat.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(flat);
            }
            PoseWand(1.0f);
            return WandTip != null ? WandTip.position : Wand.position;
        }

        private bool CanPoseWand => Wand != null && _rightUpperArm != null && _rightLowerArm != null && _handR != null;

        private void ApplyWandPose()
        {
            if (!CanPoseWand)
            {
                _wandRaise = 0.0f;
                return;
            }
            bool wanted = _wandHold > 0.0f || _castPoseFraction > 0.0f;
            float deltaTime = Mathf.Max(0.0f, Time.deltaTime);
            _wandHold = Mathf.Max(0.0f, _wandHold - deltaTime);
            _wandAimAge += deltaTime;
            _wandRaise = Mathf.MoveTowards(_wandRaise, wanted ? 1.0f : 0.0f, deltaTime / WandRaiseSeconds);
            PoseWand(_wandRaise * _wandRaise * (3.0f - 2.0f * _wandRaise));
        }

        /// <summary>The arm <paramref name="raise"/> of the way from carrying the wand to pointing it at <see cref="WandAimPoint"/>.</summary>
        private void PoseWand(float raise)
        {
            Vector3 carried = Quaternion.AngleAxis(WandCarryDipDegrees, transform.right) * transform.forward;
            Vector3 pointed = transform.forward;
            Vector3 toAim = _wandAimPoint - _rightUpperArm.position;
            if (_wandAimAge <= WandAimFreshSeconds && toAim.sqrMagnitude > 0.01f)
            {
                pointed = toAim.normalized;
            }
            Vector3 direction = Vector3.Slerp(carried, pointed, raise).normalized;
            HoldRightHandOut(direction, raise, WandCarryHand, WandReach, WandHoldOffset);
        }
    }
}
