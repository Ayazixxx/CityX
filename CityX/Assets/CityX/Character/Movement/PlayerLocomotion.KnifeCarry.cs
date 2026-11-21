using UnityEngine;

namespace CityX.Character
{
    /// <summary>A knife carried at the side with its blade turned out (KnifeCarry): the forearm rolled, after the clips and before the fingers.</summary>
    public sealed partial class PlayerLocomotion
    {
        /// <summary>How long the turn takes to come in when a knife is drawn, seconds.</summary>
        public const float KnifeCarryTurnSeconds = 0.25f;

        private float _knifeCarry;

        private void ApplyKnifeCarry()
        {
            ISwordBlade blade = HeldSword;
            bool carried = blade != null && blade.IsKnife && _posedSword != null && _rightLowerArm != null && _handR != null;
            // An attack, a guard or a baked pose has the arm: the turn gives way as they come in.
            float wanted = carried
                ? (1.0f - (_swordFrame.Active ? Mathf.Clamp01(_swordFrame.Weight) : 0.0f)) * (1.0f - BakedArmsWeight)
                : 0.0f;
            _knifeCarry = Mathf.MoveTowards(_knifeCarry, carried ? 1.0f : 0.0f, Mathf.Max(0.0f, Time.deltaTime) / KnifeCarryTurnSeconds);
            float weight = Mathf.Min(_knifeCarry, wanted);
            if (weight <= 0.001f)
            {
                return;
            }
            Vector3 forearm = _handR.position - _rightLowerArm.position;
            if (forearm.sqrMagnitude < 1e-8f)
            {
                return;
            }
            float roll = KnifeCarry.RollDegrees(forearm, _posedSword.up, transform.TransformDirection(KnifeCarry.Point), KnifeCarry.MostRollDegrees);
            _rightLowerArm.rotation = Quaternion.AngleAxis(roll * weight * weight * (3.0f - 2.0f * weight), forearm.normalized) * _rightLowerArm.rotation;
        }
    }
}
