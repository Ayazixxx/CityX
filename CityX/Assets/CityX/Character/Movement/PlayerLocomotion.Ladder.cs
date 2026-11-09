using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// ON A LADDER (PlayerLadder, LadderGait): each hand closed round its rung and each foot standing on its own, placed after the Animator
    /// by two-bone IK from nothing but how high the body is on the ladder, so the owner's body and every copy of it hold the same rungs.
    /// </summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How far each hand holds from the middle of a rung, and each foot stands, for a body of the standing height the rig is built at.</summary>
        public const float LadderHandApart = 0.24f;
        public const float LadderFootApart = 0.13f;

        /// <summary>The rung lies across the palm this far along the hand from the wrist, and this far into it.</summary>
        public const float LadderGripAlong = 0.085f;
        public const float LadderGripDeep = 0.035f;

        /// <summary>The ball of the foot is on the rung: the ankle is this far back from it.</summary>
        public const float LadderAnkleBack = 0.1f;

        /// <summary>How far a stepping hand and foot swing back off the ladder at the middle of the step.</summary>
        public const float LadderHandLift = 0.07f;
        public const float LadderFootLift = 0.09f;

        /// <summary>Half a rung's thickness: a sole stands this far over the rung's middle.</summary>
        public const float LadderRungHalf = 0.022f;

        /// <summary>
        /// The hips sway this far toward the foot that carries the weight; the shoulders hang this far back from over the hips, as a
        /// climber's do (the hips stay close to the rungs, the arms take the lean); the toes turn out this far.
        /// </summary>
        public const float LadderHipSway = 0.035f;
        public const float LadderLeanBackDegrees = 8.0f;
        public const float LadderToeOutDegrees = 12.0f;

        /// <summary>The head looks up the ladder climbing, down it going down, by this much.</summary>
        public const float LadderHeadLookDegrees = 16.0f;

        /// <summary>Sliding down, the hands hold the rails this many rungs over the feet, which ride them outside.</summary>
        public const float LadderSlideHandsOver = 6.2f;
        public const float LadderSlideFeetUp = 0.5f;

        private Ladder _ladder;
        private float _ladderWeight;
        private float _ladderBlend;
        private float _ladderTravel;
        private bool _ladderSliding;

        /// <summary>True while the hands are on a ladder's rungs: they close round them (ApplyFingerGrips).</summary>
        public bool LadderHandsGrip => _ladderBlend > 0.5f && _ledgeHandsWeight < 0.5f;

        /// <summary>How strongly the limbs are on a ladder right now (0 to 1).</summary>
        public float LadderWeight => _ladderBlend;

        /// <summary>
        /// Puts the limbs on a ladder (weight 0 lets go). <paramref name="travel"/> is which way the body is going, 1 up to -1 down, for the
        /// head; <paramref name="sliding"/> has the hands and feet on the rails.
        /// </summary>
        public void SetLadder(in Ladder ladder, float weight, float travel = 0.0f, bool sliding = false)
        {
            _ladderWeight = Mathf.Clamp01(weight);
            if (_ladderWeight > 0.0f)
            {
                _ladder = ladder;
                _ladderTravel = Mathf.Clamp(travel, -1.0f, 1.0f);
                _ladderSliding = sliding;
            }
        }

        /// <summary>After the Animator and the straightened wrists: the four limbs go to the ladder.</summary>
        private void ApplyLadderLimbs()
        {
            bool bones = _leftUpperArm != null && _leftLowerArm != null && _handL != null && _rightUpperArm != null && _rightLowerArm != null && _handR != null
                && _leftUpperLeg != null && _leftLowerLeg != null && _leftFoot != null && _rightUpperLeg != null && _rightLowerLeg != null && _rightFoot != null;
            _ladderBlend = Mathf.MoveTowards(_ladderBlend, bones && _ladderWeight > 0.01f ? _ladderWeight : 0.0f, Time.deltaTime * 6.0f);
            if (!bones || _ladderBlend <= 0.0f)
            {
                return;
            }
            float weight = Mathf.SmoothStep(0.0f, 1.0f, _ladderBlend);
            float size = StandingHeight / BodyRig.StandingHeight;
            Vector3 back = _ladder.Normal;   // from the ladder toward the body
            Vector3 right = Vector3.Cross(back, Vector3.up).normalized;   // the body's right as it faces the ladder
            int rungs = _ladder.RungCount;
            float body = LadderGait.RungsUp(transform.position.y - _ladder.Foot.y);
            int over = LadderGait.HandRungsOverFoot(StandingHeight);

            // The hips sway over the foot that carries the weight, the shoulders hang back, the head looks where the climb is going.
            if (_hips != null && !_ladderSliding)
            {
                _hips.position += right * (LadderGait.Sway(body) * LadderHipSway * size * weight);
            }
            if (_spine != null)
            {
                _spine.rotation = Quaternion.AngleAxis(-LadderLeanBackDegrees * weight, right) * _spine.rotation;
            }
            if (_head != null)
            {
                _head.rotation = Quaternion.AngleAxis(-_ladderTravel * LadderHeadLookDegrees * weight, right) * _head.rotation;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side < 0;
                Transform thigh = left ? _leftUpperLeg : _rightUpperLeg, shin = left ? _leftLowerLeg : _rightLowerLeg, foot = left ? _leftFoot : _rightFoot;
                LadderGait.Hold hold = _ladderSliding
                    ? new LadderGait.Hold { Rung = body + LadderSlideFeetUp }
                    : LadderGait.Foot(body, left, rungs);
                bool onFloor = !_ladderSliding && hold.Rung <= LadderGait.Floor + 0.001f;
                float apart = (_ladderSliding ? Ladders.RungHalfWidth + 0.05f : LadderFootApart * size) * side;
                // The sole: on the rung's top, or on the floor a little out from the ladder's foot.
                Vector3 sole = onFloor
                    ? _ladder.Foot + back * (0.2f * size) + right * apart
                    : _ladder.Rung(hold.Rung) + Vector3.up * LadderRungHalf + right * apart;
                Vector3 toes = Quaternion.AngleAxis(LadderToeOutDegrees * side, Vector3.up) * -back;
                Vector3 ankle = sole + Vector3.up * (BodyRig.FootY * size) + back * ((LadderAnkleBack + hold.Lift * LadderFootLift) * size);
                Vector3 knee = -back * 0.5f + right * (0.85f * side) + Vector3.up * 0.2f;
                TwoBoneIK.Solve(thigh, shin, foot, ankle, knee, weight);
                foot.rotation = Quaternion.Slerp(foot.rotation, Quaternion.LookRotation(toes, Vector3.up), weight);
            }

            // A hand reaching for a ledge (the manhole's rim at the top) is the ledge's, not the ladder's.
            float arms = weight * (1.0f - Mathf.Clamp01(_ledgeHandsWeight));
            if (arms <= 0.001f)
            {
                return;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side < 0;
                Transform upper = left ? _leftUpperArm : _rightUpperArm, lower = left ? _leftLowerArm : _rightLowerArm, hand = left ? _handL : _handR;
                LadderGait.Hold hold = _ladderSliding
                    ? new LadderGait.Hold { Rung = body + LadderSlideHandsOver }
                    : LadderGait.Hand(body, left, over, rungs);
                float apart = (_ladderSliding ? Ladders.RungHalfWidth : LadderHandApart * size) * side;
                Vector3 rung = _ladder.Rung(hold.Rung) + right * apart;
                // The fingers go up and over a rung held overhead, forward and over one held at the chest; the palm is turned to the rung.
                float overhead = Mathf.Clamp01((rung.y - upper.position.y) / (0.6f * size));
                Vector3 fingers = Vector3.Slerp(-back, Vector3.up, 0.25f + 0.6f * overhead).normalized;
                if (_ladderSliding)
                {
                    fingers = Vector3.Slerp(Vector3.up, -right * side, 0.35f).normalized;   // wrapped round the rail from outside
                }
                Vector3 toward = -back - Vector3.up;
                Vector3 palm = (toward - fingers * Vector3.Dot(toward, fingers)).normalized;
                Vector3 wrist = rung - fingers * (LadderGripAlong * size) - palm * (LadderGripDeep * size) + back * (hold.Lift * LadderHandLift * size);
                Vector3 elbow = Vector3.down * 0.7f + right * (0.7f * side) + back * 0.25f;
                TwoBoneIK.Solve(upper, lower, hand, wrist, elbow, arms);
                // The rig's own hand: the fingers run +X on the right hand and -X on the left, the palm is -Y.
                Vector3 x = left ? -fingers : fingers;
                hand.rotation = Quaternion.Slerp(hand.rotation, Quaternion.LookRotation(Vector3.Cross(x, -palm), -palm), arms);
            }
        }
    }
}
