using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How loud and how clear a nearby player's voice is (docs/VOICE.md; GAME_DESIGN "Social": proximity voice fades with distance, muffled through
    /// walls): full within a few steps, fading to nothing at <see cref="RangeMetres"/>, quieter and duller for every wall between.
    /// </summary>
    public static class VoiceHearing
    {
        /// <summary>Full volume this close, metres (the game's sizes are about 1.3x the real world's).</summary>
        public const float FullVolumeMetres = 4.0f;

        /// <summary>Heard no further than this, metres: about a street's width and a half, a shout's carry in a city.</summary>
        public const float RangeMetres = 40.0f;

        /// <summary>The volume left through each wall.</summary>
        public const float WallVolume = 0.45f;

        /// <summary>Walls past this many count no more (the voice is already a murmur).</summary>
        public const int MaxWalls = 3;

        /// <summary>The low-pass cutoff in the open, and through one wall and more, Hz: a voice through a wall keeps its boom and loses its words.</summary>
        public const float OpenCutoffHz = 22000.0f;

        public const float OneWallCutoffHz = 1400.0f;

        public const float WallsCutoffHz = 700.0f;

        /// <summary>The volume, 0..1, of a voice <paramref name="metres"/> away through <paramref name="walls"/> walls.</summary>
        public static float VolumeAt(float metres, int walls)
        {
            if (metres >= RangeMetres)
            {
                return 0.0f;
            }
            float fade = Mathf.Clamp01((metres - FullVolumeMetres) / (RangeMetres - FullVolumeMetres));
            float open = (1.0f - fade) * (1.0f - fade);
            for (int i = 0; i < Mathf.Min(walls, MaxWalls); i++)
            {
                open *= WallVolume;
            }
            return open;
        }

        public static float CutoffFor(int walls) => walls <= 0 ? OpenCutoffHz : walls == 1 ? OneWallCutoffHz : WallsCutoffHz;
    }
}
