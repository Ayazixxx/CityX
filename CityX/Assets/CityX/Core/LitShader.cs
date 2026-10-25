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
    }
}
