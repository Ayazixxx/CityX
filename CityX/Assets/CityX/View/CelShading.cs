using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.View
{
    /// <summary>
    /// The cel look, like Jet Set Radio's (owner, 2026-10-24: "I want to make my game cel shaded like jet set radio with thick black lines",
    /// "players and buildings and terrain, everything"): cel-shaded light on every lit surface (Shaders/CityXToonLighting.hlsl, through
    /// CityX/Lit and CityX/Facade) and thick black lines round and through everything (Shaders/CityXOutline.shader). On from the first frame;
    /// /celshade turns it off and on, /outlines sets the lines' width.
    ///
    /// All of it is one global shader keyword (CITYX_TOON) and a few global numbers, so it reaches every material, the ones made later too,
    /// without touching any; with the keyword off every surface lights exactly as it did before (URP's own lighting).
    /// </summary>
    public static class CelShading
    {
        public const string Keyword = "CITYX_TOON";

        // THE SUN (or the moon): a surface is lit or in shade. N.L times the shadow (0 facing away or shadowed, 1 facing it) past which a surface
        // is on its lit side, and half the width of the edge (a pixel or two on a body, so it does not stair-step).
        public const float SunEdge = 0.12f;
        public const float SunEdgeHalfWidth = 0.03f;
        // How much of the sun each side gets: the lit side a flat share (not N.L: one tone across a whole face, as cel art is painted), the
        // shade side a little, the sky's ambient doing the rest.
        public const float LitShare = 0.8f;
        public const float ShadeShare = 0.08f;

        // A LAMP has no numbers of its own: on a body it splits at the sun's edge into the sun's lit share and nothing, and fades with distance
        // as the light itself does; on the walls and the ground it is URP's own soft light (flat steps drew rings round every lamp).

        // A SHINY surface (wet, metal): one hard highlight where URP's specular passes this, this strong.
        public const float ShineAt = 3.0f;
        public const float ShineStrength = 3.0f;

        // THE LINES: width in pixels on a picture 1080 tall (scaled to the screen), the depth step that makes an outline (how far the inverse
        // depth bends from a flat surface's, against itself), the fold threshold (1 - cos of the angle a surface turns: 0.3 is 45 degrees),
        // and the distances they start to fade and are gone by.
        public const float DefaultOutlineWidth = 2.0f;
        public const float OutlineStep = 0.15f;
        public const float FoldThreshold = 0.3f;
        public const float OutlineFadeStart = 70.0f;
        public const float OutlineFadeEnd = 220.0f;
        public static readonly Color OutlineColour = Color.black;

        private static readonly int SunId = Shader.PropertyToID("_CityXToonSun");
        private static readonly int ShineId = Shader.PropertyToID("_CityXToonShine");
        private static readonly int OutlineId = Shader.PropertyToID("_CityXOutline");
        private static readonly int OutlineFadeId = Shader.PropertyToID("_CityXOutlineFade");
        private static readonly int OutlineColourId = Shader.PropertyToID("_CityXOutlineColor");

        /// <summary>Whether the cel look is on.</summary>
        public static bool Enabled => Shader.IsKeywordEnabled(GlobalKeyword.Create(Keyword));

        /// <summary>The lines' width in pixels on a picture 1080 tall; 0 draws none.</summary>
        public static float OutlineWidth { get; private set; } = DefaultOutlineWidth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnFromTheStart()
        {
            OutlineWidth = DefaultOutlineWidth;
            Set(true);
        }

        /// <summary>Turns the cel look on or off: the toon light and the lines together.</summary>
        public static void Set(bool on)
        {
            SetLook();
            Shader.SetKeyword(GlobalKeyword.Create(Keyword), on);
        }

        /// <summary>The lines' width in pixels on a picture 1080 tall (scaled to the screen); 0 draws none.</summary>
        public static void SetOutlineWidth(float pixels)
        {
            OutlineWidth = Mathf.Max(0.0f, pixels);
            SetLook();
        }

        private static void SetLook()
        {
            Shader.SetGlobalVector(SunId, new Vector4(SunEdge, SunEdgeHalfWidth, ShadeShare, LitShare));
            Shader.SetGlobalVector(ShineId, new Vector4(ShineAt, ShineStrength, 0.0f, 0.0f));
            Shader.SetGlobalVector(OutlineId, new Vector4(OutlineWidth, OutlineStep, FoldThreshold, OutlineWidth > 0.0f ? 1.0f : 0.0f));
            Shader.SetGlobalVector(OutlineFadeId, new Vector4(OutlineFadeStart, OutlineFadeEnd, 0.0f, 0.0f));
            Shader.SetGlobalColor(OutlineColourId, OutlineColour);
        }
    }
}
