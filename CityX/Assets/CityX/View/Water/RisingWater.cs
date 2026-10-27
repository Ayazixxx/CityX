using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Water whose surface follows a <see cref="FloodTimeline"/> over the ground it started on, gone once the flood is over: the groundwork
    /// for the Great Flood (docs/WATER.md). The level is a pure function of the time since it began, so a session can raise the same flood on
    /// every machine from one start time.
    /// </summary>
    [RequireComponent(typeof(WaterSurface))]
    public sealed class RisingWater : MonoBehaviour
    {
        private WaterSurface _surface;
        private FloodTimeline _timeline;
        private float _groundY;
        private float _startTime;

        /// <summary>Raises <paramref name="surface"/>'s water from <paramref name="groundY"/> on <paramref name="timeline"/>, starting at <paramref name="startTime"/> (Time.time).</summary>
        public static RisingWater Begin(WaterSurface surface, float groundY, FloodTimeline timeline, float startTime)
        {
            var rising = surface.gameObject.AddComponent<RisingWater>();
            rising._surface = surface;
            rising._groundY = groundY;
            rising._timeline = timeline;
            rising._startTime = startTime;
            rising.Step();
            return rising;
        }

        /// <summary>How high the water stands over its ground now.</summary>
        public float Level => _timeline.LevelAt(Time.time - _startTime);

        private void Update()
        {
            Step();
        }

        private void Step()
        {
            float seconds = Time.time - _startTime;
            if (_timeline.IsOver(seconds))
            {
                Destroy(gameObject);
                return;
            }
            _surface.Body.SurfaceY = _groundY + _timeline.LevelAt(seconds);
        }
    }
}
