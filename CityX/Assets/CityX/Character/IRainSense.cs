using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What a character can tell about the rain from where it stands: whether
    /// it is raining at all, how hard it is falling on a point, and whether a
    /// point is under cover. An interface so the pedestrian brain, which "knows
    /// nothing about colliders" and is stepped in tests without a scene, can be
    /// handed a fake weather.
    /// </summary>
    public interface IRainSense
    {
        /// <summary>0 when it is not raining, up to 1 in a downpour.</summary>
        float Precipitation { get; }

        /// <summary>How hard rain is falling ON this point: the precipitation, cut off under cover.</summary>
        float RainOn(Vector3 worldPoint);

        /// <summary>0 in the open, 1 under a roof.</summary>
        float Shelter(Vector3 worldPoint);
    }

    /// <summary>The real weather, read from <see cref="CurrentWeather"/>.</summary>
    public sealed class CurrentWeatherSense : IRainSense
    {
        public static readonly CurrentWeatherSense Instance = new CurrentWeatherSense();

        public float Precipitation => CurrentWeather.Intensity;

        public float RainOn(Vector3 worldPoint) => CurrentWeather.RainOn(worldPoint);

        public float Shelter(Vector3 worldPoint) => CurrentWeather.Shelter(worldPoint);
    }
}
