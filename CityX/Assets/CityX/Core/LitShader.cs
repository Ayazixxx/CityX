using UnityEngine;

namespace CityX.Core
{
    /// <summary>
    /// The shader every lit, opaque or see-through material in the game is made with: CityX/Lit, URP's Lit with cel shading
    /// (View/Shaders/CityXLit.shader). Code that makes a material asks for it here, never for URP's Lit by name, so nothing made later slips
    /// out of the cel look (LitShaderGuardTests).
    /// </summary>
    public static class LitShader
    {
        public const string Name = "CityX/Lit";

        public static Shader Find() => Shader.Find(Name);

        /// <summary>
        /// No shine (owner, 2026-10-09: "get rid of the shinyness that is appearing on the character and the hair"): no gloss and no specular
        /// highlights. Even a little gloss shows as a sheen under a lamp at night, and a glossy piece of gore caught the sun as a pinpoint the
        /// bloom blew into a flashlight's glare (owner, 2026-11-06). The sky's reflection stays on, so rain (PlayerWetness raises the gloss)
        /// still makes a soaked body glisten in daylight.
        /// </summary>
        public static void MakeMatte(Material material)
        {
            material.SetFloat("_Smoothness", 0.0f);
            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0.0f);
            }
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
    }
}
