using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.View
{
    /// <summary>
    /// What a light is to the city's shaders, as URP rendering layers they read (Shaders/CityXToonLighting.hlsl).
    ///
    /// Which side of a wall it is on: a light marked <see cref="Interior"/> lights only the city's surfaces inside buildings (SurfaceLook's
    /// indoors flag) and one marked <see cref="Exterior"/> only the ones outside, so a lit room never shines through its own walls onto the
    /// pavement or the building next door, and a lamp over a door does not light the room behind the wall. Everything else (bodies, props) is
    /// lit by both, by URP's own layer test.
    ///
    /// Whether somebody carries it: under the ground a light marked <see cref="Beam"/> (a torch) is as bright on what it falls on far off
    /// as near.
    ///
    /// A light left unmarked (a muzzle flash, a fireball) lights everything, as it always did.
    /// </summary>
    public static class CityLightLayers
    {
        /// <summary>URP's default rendering layer: every renderer is on it.</summary>
        public const uint Default = 1u << 0;

        /// <summary>A light inside a building. MUST match CITYX_INTERIOR_LIGHT_LAYER in CityXToonLighting.hlsl.</summary>
        public const uint Interior = 1u << 1;

        /// <summary>A light outside. MUST match CITYX_EXTERIOR_LIGHT_LAYER in CityXToonLighting.hlsl.</summary>
        public const uint Exterior = 1u << 2;

        /// <summary>
        /// A light somebody carries: a torch's beam, and the glow round it. Under the ground the city's shaders do not let it fall off
        /// with the distance (CityXThrowBeam): it is as bright on a wall far off as on one near, to the end of its range. By the square of
        /// the distance a beam bright enough to show the far end of a tunnel burns the wall beside the hand white, and one that spares
        /// the wall is gone ten metres on. Under the sky it falls off as any light does. MUST match CITYX_BEAM_LIGHT_LAYER in
        /// CityXToonLighting.hlsl.
        /// </summary>
        public const uint Beam = 1u << 3;

        /// <summary>Marks a light as inside a building: it lights the rooms and the people in them, never the outside of the city.</summary>
        public static void MarkInside(Light light)
        {
            Set(light, Default | Interior);
        }

        /// <summary>Marks a light as outside: it lights the street and the people on it, never the inside of a building.</summary>
        public static void MarkOutside(Light light)
        {
            Set(light, Default | Exterior);
        }

        /// <summary>Marks a light as a carried one: it lights everything, inside and out, and under the ground as brightly far off as near.</summary>
        public static void MarkBeam(Light light)
        {
            Set(light, Default | Beam);
        }

        /// <summary>The rendering layers a light is on.</summary>
        public static uint Of(Light light)
        {
            return light.GetUniversalAdditionalLightData().renderingLayers;
        }

        private static void Set(Light light, uint layers)
        {
            light.GetUniversalAdditionalLightData().renderingLayers = layers;
        }
    }
}
