using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// A flood's water level over time: it rises, holds, and recedes (docs/WATER.md). The groundwork for the Great Flood: a flood is a
    /// <see cref="WaterBody"/> whose surface follows this, and every machine in a session computes the same level from one start time, so
    /// the network carries one message, never the water (how Fortnite's flooded island and every rising-water mode work, against the GPU
    /// floods of single-player games).
    /// </summary>
    public readonly struct FloodTimeline
    {
        public readonly float RiseSeconds;
        public readonly float HoldSeconds;
        public readonly float RecedeSeconds;

        /// <summary>How high it rises over the ground it starts on, metres.</summary>
        public readonly float Height;

        public FloodTimeline(float riseSeconds, float holdSeconds, float recedeSeconds, float height)
        {
            RiseSeconds = Mathf.Max(0.01f, riseSeconds);
            HoldSeconds = Mathf.Max(0.0f, holdSeconds);
            RecedeSeconds = Mathf.Max(0.01f, recedeSeconds);
            Height = height;
        }

        public float TotalSeconds => RiseSeconds + HoldSeconds + RecedeSeconds;

        /// <summary>How high the water stands over its ground <paramref name="seconds"/> after it began: eased in and out, 0 before and after.</summary>
        public float LevelAt(float seconds)
        {
            if (seconds <= 0.0f || seconds >= TotalSeconds)
            {
                return 0.0f;
            }
            if (seconds < RiseSeconds)
            {
                return Height * Mathf.SmoothStep(0.0f, 1.0f, seconds / RiseSeconds);
            }
            if (seconds < RiseSeconds + HoldSeconds)
            {
                return Height;
            }
            return Height * Mathf.SmoothStep(1.0f, 0.0f, (seconds - RiseSeconds - HoldSeconds) / RecedeSeconds);
        }

        public bool IsOver(float seconds) => seconds >= TotalSeconds;
    }
}
