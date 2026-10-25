using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Puts the city's air (Shaders/CityXHaze.shader) into the frame: a material for it and a Full Screen Pass renderer feature on the PC renderer that
    /// draws it before post-processing, reading the depth texture (which the PC pipeline asset already has on). Run by CityMassingSceneBuilder.Build
    /// every rebuild (FullScreenPassSetup). The haze's numbers are globals DayNightCycle sets each frame; with none set (any other scene) the pass
    /// leaves the image alone.
    /// </summary>
    public static class HazeSetup
    {
        public const string MaterialPath = "Assets/CityX/View/Materials/CityXHaze.mat";
        public const string FeatureName = "CityXHaze";

        public static Material EnsureMaterial() => FullScreenPassSetup.EnsureMaterial("CityX/Haze", MaterialPath, "CityXHaze");

        public static void EnsureFeature()
        {
            FullScreenPassSetup.Ensure(FeatureName, EnsureMaterial(), FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing,
                ScriptableRenderPassInput.Depth);
        }
    }
}
