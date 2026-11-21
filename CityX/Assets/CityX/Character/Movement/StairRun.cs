using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HOW FAR SINCE THE BODY WAS RUNNING A STAIR (owner, 2026-11-19: "i accidentally vault when running up stairs sometimes"; PlayerVaulting
    /// waits on it). The walk's own stair sense (GroundGlide.IsStairs) wants a moment of steady stepped slope, and a sprint is up a short flight
    /// before it has one; the capsule takes each riser over two or three frames, so a riser is not one frame's jump either. So the rise is
    /// measured as the automatic climb measures it (PlayerClimbing.TrackGround): on the ground, the feet going up faster than
    /// <see cref="RiseSpeed"/> over a short stretch of time is a stair (a kerb is over in a frame, a street's hill rises slower). Pure.
    /// </summary>
    public sealed class StairRun
    {
        /// <summary>The feet rising faster than this (m/s) is a stair: a sprint up the city's (0.63 m a metre) rises 4.7 m/s, a walk 2.5, a 20% hill at a sprint 1.5.</summary>
        public const float RiseSpeed = 1.6f;

        /// <summary>The rise is measured over at least this long (seconds): a single riser stepped up, a kerb, is over before it counts.</summary>
        public const float AtLeastSeconds = 0.15f;

        /// <summary>A measure longer ago than this (seconds, the body off the ground meanwhile) is forgotten.</summary>
        public const float AtMostSeconds = 0.6f;

        private float _since = float.PositiveInfinity;
        private float _fromHeight;
        private bool _rising;

        /// <summary>The body is running a stair now.</summary>
        public bool OnStair { get; private set; }

        /// <summary>How far (metres along the ground) the body has gone since it was last on a stair: 0 on one. Pressed against a wall, it goes nowhere.</summary>
        public float MetresOff { get; private set; } = float.PositiveInfinity;

        /// <summary>
        /// One frame: the feet's height, how far the body went along the ground (<paramref name="along"/>), whether it is on the ground, and whether the
        /// walk takes it for a stair.
        /// </summary>
        public void Step(float height, float along, bool onGround, bool walkSaysStairs, float deltaTime)
        {
            _since += deltaTime;
            if (onGround && _since >= AtLeastSeconds)
            {
                _rising = _since <= AtMostSeconds && (height - _fromHeight) / _since > RiseSpeed;
                _since = 0.0f;
                _fromHeight = height;
            }
            else if (!onGround)
            {
                _rising = false;
                _since = float.PositiveInfinity;   // a jump's landing on something higher is no rise to measure from
            }
            OnStair = _rising || walkSaysStairs;
            MetresOff = OnStair ? 0.0f : MetresOff + along;
        }

        /// <summary>Forgets it all (a teleport, a respawn).</summary>
        public void Reset()
        {
            _since = MetresOff = float.PositiveInfinity;
            OnStair = _rising = false;
        }
    }
}
