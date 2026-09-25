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
    }
}
