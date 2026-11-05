using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Answers "is there cover overhead here" -- rain does not fall inside a
    /// building or under a highway deck, and everything that reacts to rain
    /// has to know where it is actually falling. Implemented by the rain
    /// system (CityX.View), which owns the sky-to-surface map; read from
    /// CityX.Character and CityX.Weapons through <see cref="CurrentWeather"/>,
    /// which has no reference to it. The same bridge shape as
    /// <see cref="CurrentWorld"/>.
    /// </summary>
    public interface IRainCover
    {
        /// <summary>
        /// 0 with open sky overhead, 1 with a roof, deck or ceiling over the
        /// point; in between at the ragged edge of an overhang.
        /// </summary>
        float Shelter(Vector3 worldPoint);
    }

    /// <summary>What the weather is to be: what falls, each 0..1, and how much snow lies (below 0: leave it as it is). <see cref="Dry"/> dries a soaked city at once.</summary>
    public struct WeatherOrder
    {
        public float Rain;
        public float Snow;
        public float Cover;
        public bool Dry;
    }

    /// <summary>The weather on this machine (CityX.View's SnowEffect in the game), which takes orders.</summary>
    public interface IWeatherControl
    {
        /// <summary>What it is now: what is falling or easing in, and how much lies.</summary>
        WeatherOrder Now { get; }

        void Apply(in WeatherOrder order);
    }

    /// <summary>Takes an order to every machine in a session (CityX.Character's NetworkedWeather).</summary>
    public interface IWeatherRelay
    {
        void Send(in WeatherOrder order);
    }

    /// <summary>
    /// The weather the running game is actually in, as a plain static holder
    /// (see <see cref="CurrentWorld"/> for why a static). RainEffect publishes
    /// it every frame while it exists and clears it when it goes away, so a
    /// scene with no rain -- which is every test that is not about rain --
    /// reads a perfectly dry, open world.
    /// </summary>
    public static class CurrentWeather
    {
        /// <summary>0 when it is not raining, up to 1 in a downpour: the precipitation NOW.</summary>
        public static float Intensity;

        /// <summary>0..1, how soaked the city is. Lags the rain both ways -- see <see cref="Weather.StepWetness"/>.</summary>
        public static float Wetness;

        /// <summary>The sky-to-surface map, or null when nothing is publishing one.</summary>
        public static IRainCover Cover;

        public static bool IsRaining => Intensity > 0.0f;

        /// <summary>Shelter at a point: 0 (open) when there is no cover map, so unmapped worlds behave as they always did.</summary>
        public static float Shelter(Vector3 worldPoint)
        {
            return Cover != null ? Mathf.Clamp01(Cover.Shelter(worldPoint)) : 0.0f;
        }

        /// <summary>How hard rain is actually falling ON a point: the precipitation, cut off under cover.</summary>
        public static float RainOn(Vector3 worldPoint)
        {
            return Intensity <= 0.0f ? 0.0f : Intensity * (1.0f - Shelter(worldPoint));
        }

        /// <summary>
        /// How wet the surface AT a point is: the city's wetness, but ground
        /// under cover has only been splashed and stays a good deal drier.
        /// </summary>
        public static float SurfaceWetness(Vector3 worldPoint)
        {
            if (Wetness <= 0.0f)
            {
                return 0.0f;
            }
            return Wetness * Mathf.Lerp(1.0f, 0.35f, Shelter(worldPoint));
        }

        /// <summary>Back to a dry, open world -- what a scene without rain reads.</summary>
        public static void Clear()
        {
            Intensity = 0.0f;
            Wetness = 0.0f;
            Cover = null;
        }

        // --- snow (docs/SNOW.md) ---

        /// <summary>0 when it is not snowing, up to 1 in a blizzard: the snow falling NOW.</summary>
        public static float SnowFalling;

        /// <summary>0..1, how much snow lies on the city. See <see cref="SnowRules.StepCover"/>.</summary>
        public static float SnowCover;

        /// <summary>Snow's own map of what is overhead, or null when nothing is publishing one.</summary>
        public static IRainCover SnowMap;

        /// <summary>This machine's weather, or null in a scene without it.</summary>
        public static IWeatherControl Control;

        /// <summary>The way to every machine, while in a session.</summary>
        public static IWeatherRelay Relay;

        /// <summary>Changes the weather: through the server in a session, so it changes for everyone; straight away alone.</summary>
        public static void Order(in WeatherOrder order)
        {
            if (Relay != null)
            {
                Relay.Send(order);
            }
            else
            {
                Control?.Apply(order);
            }
        }

        private static float SnowOpen(Vector3 worldPoint) => SnowMap != null ? 1.0f - Mathf.Clamp01(SnowMap.Shelter(worldPoint)) : 1.0f;

        /// <summary>How much snow lies on the ground at a point: none under a roof.</summary>
        public static float SnowUnderfoot(Vector3 worldPoint) => SnowCover <= 0.0f ? 0.0f : SnowCover * SnowOpen(worldPoint);

        /// <summary>How hard what is falling damps a flame at a point: rain, or snow as a share of rain.</summary>
        public static float QuenchingOn(Vector3 worldPoint)
        {
            float snow = SnowFalling <= 0.0f ? 0.0f : SnowFalling * SnowRules.QuenchShare * SnowOpen(worldPoint);
            return Mathf.Max(RainOn(worldPoint), snow);
        }

        /// <summary>No snow: what a scene without it reads.</summary>
        public static void ClearSnow()
        {
            SnowFalling = 0.0f;
            SnowCover = 0.0f;
            SnowMap = null;
        }
    }
}
