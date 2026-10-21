using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.View
{
    /// <summary>
    /// Which side of a wall a light is on, as URP rendering layers that the city's facade shader reads (Shaders/FacadeForwardPass.hlsl). A light
    /// marked <see cref="Interior"/> lights only the city's surfaces inside buildings (SurfaceLook's indoors flag) and one marked
    /// <see cref="Exterior"/> only the ones outside, so a lit room never shines through its own walls onto the pavement or the building next door,
    /// and a lamp over a door does not light the room behind the wall. Everything else (bodies, props) is lit by both, by URP's own layer test.
    /// A light left unmarked (a muzzle flash, a fireball) lights everything, as it always did.
    /// </summary>
    public static class CityLightLayers
    {
        /// <summary>URP's default rendering layer: every renderer is on it.</summary>
        public const uint Default = 1u << 0;

        /// <summary>A light inside a building. MUST match kInteriorLightLayer in FacadeForwardPass.hlsl.</summary>
        public const uint Interior = 1u << 1;

        /// <summary>A light outside. MUST match kExteriorLightLayer in FacadeForwardPass.hlsl.</summary>
        public const uint Exterior = 1u << 2;

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
