using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE TRAIL OF EMBERS A HOT CHAIN LEAVES (docs/CHAIN.md). Owner, 2026-11-22, of loose sparks let go off the chain: "instead of little bits
    /// of embers that fly off it. make it a line of embers ... that follows the trajectory of the chain. sort of like this little white trail
    /// that is on the swords ... but instead of just being at the tip of it make it line as long as the chain that leaves an ember trail as it
    /// swings and fades away not just sparks flying all over"; "get rid of the white trail make its a longways trail of embers". And, having
    /// played a first go whose embers went out a head's width behind the chain: "there was no visible trail it needs to be much longer make the
    /// trail last like almost the length of the swing".
    ///
    /// So the embers are LAID, not thrown: set down still, evenly over everything the chain crosses, from the fist to its far end, as many on
    /// each square metre at any speed or frame rate, and each burns for about as long as a swing takes to go through: what shows is the whole
    /// arc the chain has just swung, in embers, going out from where the swing began. A chain that hardly moves lays none.
    /// </summary>
    public sealed partial class FireLook
    {
        /// <summary>Embers laid on each square metre the chain crosses.</summary>
        public const float TrailEmbersPerSquareMetre = 30.0f;

        /// <summary>How long an ember burns, seconds: a lash's strike and follow-through together take a fifth of a second.</summary>
        public const float TrailShortestSeconds = 0.24f;

        public const float TrailLongestSeconds = 0.42f;

        /// <summary>A stretch lays nothing under the first speed, m/s, and all it can from the second.</summary>
        public const float TrailFromSpeed = 2.0f;

        public const float TrailFullSpeed = 6.0f;

        /// <summary>How big an ember is, metres, and its colour as it is laid (it reddens and shrinks as it goes out: the embers' own way).</summary>
        public const float TrailEmberSmallest = 0.05f;

        public const float TrailEmberLargest = 0.09f;

        public static readonly Color TrailEmberColour = new Color(1.0f, 0.5f, 0.22f);

        private float _trailDebt;

        /// <summary>
        /// The embers a stretch of hot chain lays this frame: it was from <paramref name="fromWas"/> to <paramref name="toWas"/> a frame ago
        /// and is from <paramref name="from"/> to <paramref name="to"/> now, on a body of <paramref name="scale"/>'s size.
        /// </summary>
        public void EmberTrail(Vector3 fromWas, Vector3 toWas, Vector3 from, Vector3 to, float scale, float deltaTime)
        {
            scale = Mathf.Max(0.01f, scale);
            deltaTime = Mathf.Max(deltaTime, 1e-4f);
            float length = Vector3.Distance(from, to) / scale;
            float gone = ((from - fromWas).magnitude + (to - toWas).magnitude) * 0.5f / scale;
            _trailDebt += length * gone * TrailEmbersPerSquareMetre * Mathf.InverseLerp(TrailFromSpeed, TrailFullSpeed, gone / deltaTime);
            if (_trailDebt < 1.0f)
            {
                return;
            }
            var ember = new ParticleSystem.EmitParams { applyShapeToPosition = false, velocity = Vector3.zero, startColor = TrailEmberColour };
            while (_trailDebt >= 1.0f)
            {
                _trailDebt -= 1.0f;
                float along = Random.value;
                ember.position = Vector3.Lerp(Vector3.Lerp(from, to, along), Vector3.Lerp(fromWas, toWas, along), Random.value);
                ember.startSize = Random.Range(TrailEmberSmallest, TrailEmberLargest) * scale;
                ember.startLifetime = Random.Range(TrailShortestSeconds, TrailLongestSeconds);
                _embers.Emit(ember, 1);
            }
        }
    }
}
