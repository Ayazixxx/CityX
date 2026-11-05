using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// When a foot comes down, from nothing but how high it is over the body's feet each frame, so it works on any body, a clone or a copy, whatever is moving its legs
    /// (docs/SNOW.md): a foot that has been lifted and is back near the lowest it goes has landed. Pure.
    /// </summary>
    public sealed class FootprintStepper
    {
        /// <summary>Higher than this over its lowest, metres, the foot is in the air.</summary>
        public const float LiftedMetres = 0.07f;

        /// <summary>Back within this of its lowest it has landed.</summary>
        public const float LandedMetres = 0.03f;

        /// <summary>How fast the lowest it goes is forgotten, m/s: the ground slopes, and a crouch or a stair moves the feet against the body for good.</summary>
        public const float LowestRisesPerSecond = 0.06f;

        private float _lowest = float.PositiveInfinity;
        private bool _lifted;

        /// <summary>The foot is <paramref name="height"/> over the body's root now: true on the one frame it lands.</summary>
        public bool Step(float height, float deltaTime)
        {
            _lowest = Mathf.Min(height, _lowest + LowestRisesPerSecond * Mathf.Max(0.0f, deltaTime));
            if (!_lifted)
            {
                _lifted = height > _lowest + LiftedMetres;
                return false;
            }
            if (height <= _lowest + LandedMetres)
            {
                _lifted = false;
                return true;
            }
            return false;
        }
    }
}
