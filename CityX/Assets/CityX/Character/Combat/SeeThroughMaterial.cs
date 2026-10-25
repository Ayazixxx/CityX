using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.Character
{
    /// <summary>
    /// Sets a URP particle material up to be seen through -- alpha-blended, or additive (it glows) -- as URP's own inspector would: its blend
    /// factors, no depth written, both faces, drawn after the opaque world. Setting the surface type alone (as the sparks once did) leaves the
    /// material drawing as a solid, so the cel look's black lines (CityXOutline, drawn on the opaque world) traced a thin spark until it was all
    /// line.
    /// </summary>
    public static class SeeThroughMaterial
    {
        public static void Apply(Material material, bool additive)
        {
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", additive ? 2.0f : 0.0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
