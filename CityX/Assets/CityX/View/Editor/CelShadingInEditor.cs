using CityX.View;
using UnityEditor;

namespace CityX.ViewEditor
{
    /// <summary>The cel look (CelShading) on in the editor too, so the Scene view and the edit-mode captures look as the game does.</summary>
    [InitializeOnLoad]
    public static class CelShadingInEditor
    {
        static CelShadingInEditor()
        {
            CelShading.Set(true);
        }
    }
}
