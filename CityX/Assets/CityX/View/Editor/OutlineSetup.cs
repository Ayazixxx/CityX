using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Puts the cel look's thick black lines (Shaders/CityXOutline.shader, CelShading) into the frame: a Full Screen Pass renderer feature drawn
    /// after the opaque world and before the glass and the air, reading the depth and the normals. Run by CityMassingSceneBuilder.Build every
    /// rebuild (FullScreenPassSetup).
    /// </summary>
    public static class OutlineSetup
    {
        public const string MaterialPath = "Assets/CityX/View/Materials/CityXOutline.mat";
        public const string FeatureName = "CityXOutline";

        public static Material EnsureMaterial() => FullScreenPassSetup.EnsureMaterial("CityX/Outline", MaterialPath, "CityXOutline");

        public static void EnsureFeature()
        {
            FullScreenPassSetup.Ensure(FeatureName, EnsureMaterial(), FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingTransparents,
                ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }
    }
}
