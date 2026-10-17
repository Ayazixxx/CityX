using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SWORD HAND SWINGS WITH THE WALK (owner, 2026-10-17: "when holding the combat sword the right hand appears frozen ... it should
    /// continue to move naturally with sword in hand"). The one-handed carry puts the hand at one point by the hip; this gives back the
    /// animation's own movement of that hand around its middle: each frame the clip's hand (body frame, from the shoulder) is fed in, a slow
    /// running average of it is its middle, and the difference is how far the walk has swung it, which is added to the carry's point. Standing
    /// still the clip barely moves the hand, so it barely moves; walking or running it swings as far as the clip swings it, in step.
    /// </summary>
    public sealed class CarrySwing
    {
        /// <summary>How slowly the middle follows the hand, seconds: longer than a step, so the swing itself is not averaged away.</summary>
        public const float MiddleSeconds = 0.6f;

        /// <summary>No swing goes further than this, metres (a clip change mid-stride must not fling the sword).</summary>
        public const float MaxSwingMetres = 0.35f;

        private Vector3 _middle;
        private bool _started;

        /// <summary>Starts again from the next sample (the carry picked up again).</summary>
        public void Reset()
        {
            _started = false;
        }

        /// <summary>The clip's hand this frame, body frame; returns how far it is swung from its middle, body frame, metres.</summary>
        public Vector3 Step(Vector3 clipHand, float dt)
        {
            if (!_started)
            {
                _middle = clipHand;
                _started = true;
                return Vector3.zero;
            }
            float k = 1.0f - Mathf.Exp(-Mathf.Max(0.0f, dt) / MiddleSeconds);
            _middle = Vector3.Lerp(_middle, clipHand, k);
            return Vector3.ClampMagnitude(clipHand - _middle, MaxSwingMetres);
        }
    }
}
