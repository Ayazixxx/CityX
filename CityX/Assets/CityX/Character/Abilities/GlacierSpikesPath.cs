using UnityEngine;

namespace CityX.Character
{
    /// <summary>What the line needs to know of the world: the ground under a point, and whether a wall stands between two points.</summary>
    public interface IGlacierGround
    {
        /// <summary>The highest ground from <paramref name="above"/> down <paramref name="depth"/> metres, if any.</summary>
        bool TryGround(Vector3 above, float depth, out float y);

        /// <summary>How far from <paramref name="from"/> toward <paramref name="to"/> before something solid (a wall), metres; the whole way when nothing stands there.</summary>
        float ClearDistance(Vector3 from, Vector3 to);
    }

    /// <summary>
    /// The ground along a Glacier Spikes line (docs/GLACIER_SPIKES.md): from just ahead of the caster's feet, along the camera's flat
    /// direction, looked at every <see cref="GlacierSpikesRules.GroundStep"/>; up kerbs and stairs and down drops, until a wall stands in the
    /// way, the ground falls away further than a drop, or the full <see cref="GlacierSpikesRules.Range"/> is reached. Pure: the world is
    /// asked through <see cref="IGlacierGround"/>. Reused, so walking it again makes no garbage.
    /// </summary>
    public sealed class GlacierSpikesPath
    {
        public static readonly int MaxSamples = Mathf.CeilToInt(GlacierSpikesRules.Range / GlacierSpikesRules.GroundStep) + 2;

        private readonly float[] _heights = new float[MaxSamples];
        private int _count;

        /// <summary>Where the line starts, on the ground, and which way it runs (flat).</summary>
        public Vector3 Start { get; private set; }

        public Vector3 Direction { get; private set; } = Vector3.forward;

        /// <summary>How long it is, metres: 0 when there was no ground to start on.</summary>
        public float Length { get; private set; }

        public bool IsCastable => Length >= GlacierSpikesRules.MinLength;

        /// <summary>
        /// Walks the line from <paramref name="feet"/> along <paramref name="look"/> (flattened). False when the feet are too far above any
        /// ground to cast on.
        /// </summary>
        public bool Walk(Vector3 feet, Vector3 look, IGlacierGround ground)
        {
            Length = 0.0f;
            _count = 0;
            look.y = 0.0f;
            if (look.sqrMagnitude < 1e-6f || ground == null)
            {
                return false;
            }
            Direction = look.normalized;
            if (!ground.TryGround(feet + Vector3.up * 0.5f, 0.5f + GlacierSpikesRules.GroundBelowFeet, out float underFeet))
            {
                return false;
            }
            Vector3 foot = new Vector3(feet.x, underFeet, feet.z);
            Vector3 start = foot + Direction * GlacierSpikesRules.StartAhead;
            float knee = GlacierSpikesRules.StepUp;
            if (Blocked(ground, foot + Vector3.up * knee, start + Vector3.up * knee)
                || !ground.TryGround(start + Vector3.up * knee, knee + GlacierSpikesRules.StepDown, out float y))
            {
                Start = foot;
                _heights[0] = foot.y;
                _count = 1;
                return true;   // a wall at the caster's nose: nothing to cast along
            }
            Follow(new Vector3(start.x, y, start.z), Direction, GlacierSpikesRules.Range, ground);
            return true;
        }

        /// <summary>
        /// Walks the line from <paramref name="start"/> (on the ground) along <paramref name="direction"/> (flat, unit) for at most
        /// <paramref name="most"/> metres: what another player's copy does with the line the caster's machine sent.
        /// </summary>
        public void Follow(Vector3 start, Vector3 direction, float most, IGlacierGround ground)
        {
            Start = start;
            direction.y = 0.0f;
            Direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            Length = 0.0f;
            _heights[0] = start.y;
            _count = 1;
            most = Mathf.Clamp(most, 0.0f, GlacierSpikesRules.Range);
            float knee = GlacierSpikesRules.StepUp;
            float step = GlacierSpikesRules.GroundStep;
            Vector3 previous = Start;
            while (_count < MaxSamples && Length < most)
            {
                float distance = Mathf.Min(_count * step, most);
                Vector3 next = Start + Direction * distance;
                next.y = previous.y;
                if (Blocked(ground, previous + Vector3.up * knee, next + Vector3.up * knee)
                    || !ground.TryGround(next + Vector3.up * knee, knee + GlacierSpikesRules.StepDown, out float nextY))
                {
                    break;
                }
                _heights[_count++] = nextY;
                Length = distance;
                previous = new Vector3(next.x, nextY, next.z);
            }
        }

        /// <summary>The ground's height <paramref name="distance"/> metres down the line (between the looks, the straight line between them).</summary>
        public float HeightAt(float distance)
        {
            if (_count == 0)
            {
                return Start.y;
            }
            float at = Mathf.Clamp(distance, 0.0f, Length) / GlacierSpikesRules.GroundStep;
            int below = Mathf.Min(Mathf.FloorToInt(at), _count - 1);
            int above = Mathf.Min(below + 1, _count - 1);
            return Mathf.Lerp(_heights[below], _heights[above], at - below);
        }

        private static bool Blocked(IGlacierGround ground, Vector3 from, Vector3 to) => ground.ClearDistance(from, to) < Vector3.Distance(from, to) - 1e-3f;

        /// <summary>The ground's height under <paramref name="point"/>, read off the line where it passes beside it.</summary>
        public float HeightBeside(Vector3 point) => HeightAt(Vector3.Dot(point - Start, Direction));
    }
}
