using CityX.World.Plan;
using UnityEditor;
using UnityEngine;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Picks the city's scale (see <see cref="CityScale"/>) for the editor: CityX/City Scale/ Classic, Large, Huge, Colossal. The choice is kept in
    /// EditorPrefs and applied when the editor loads and every time Play starts, so switching back to the classic city is one click.
    /// A built player uses CityScale.Default, or the CITYX_CITY_SCALE environment variable.
    /// </summary>
    [InitializeOnLoad]
    public static class CityScaleMenu
    {
        private const string Key = "CityX.CityScale";

        static CityScaleMenu()
        {
            Apply();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                {
                    Apply();
                }
            };
        }

        private static void Apply()
        {
            // An environment override (batch runs, tests) beats the menu choice.
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CITYX_CITY_SCALE")))
            {
                return;
            }
            string saved = EditorPrefs.GetString(Key, "");
            if (CityScale.TryParse(saved, out CityScale scale))
            {
                CityScale.Active = scale;
            }
        }

        private static void Choose(CityScale scale)
        {
            EditorPrefs.SetString(Key, scale.Name);
            CityScale.Active = scale;
            Debug.Log($"CityX: city scale {scale}. Takes effect the next time the world is built (press Play).");
        }

        private static bool Is(CityScale scale) => CityScale.Active.Name == scale.Name;

        [MenuItem("CityX/City Scale/Classic (the city as it was)")]
        private static void Classic() => Choose(CityScale.Classic);

        [MenuItem("CityX/City Scale/Classic (the city as it was)", true)]
        private static bool ClassicCheck() { Menu.SetChecked("CityX/City Scale/Classic (the city as it was)", Is(CityScale.Classic)); return true; }

        [MenuItem("CityX/City Scale/Large (2.5x wide, 2x tall)")]
        private static void Large() => Choose(CityScale.Large);

        [MenuItem("CityX/City Scale/Large (2.5x wide, 2x tall)", true)]
        private static bool LargeCheck() { Menu.SetChecked("CityX/City Scale/Large (2.5x wide, 2x tall)", Is(CityScale.Large)); return true; }

        [MenuItem("CityX/City Scale/Huge (4x wide, 3x tall)")]
        private static void Huge() => Choose(CityScale.Huge);

        [MenuItem("CityX/City Scale/Huge (4x wide, 3x tall)", true)]
        private static bool HugeCheck() { Menu.SetChecked("CityX/City Scale/Huge (4x wide, 3x tall)", Is(CityScale.Huge)); return true; }

        [MenuItem("CityX/City Scale/Colossal (8x wide, 5x tall)")]
        private static void Colossal() => Choose(CityScale.Colossal);

        [MenuItem("CityX/City Scale/Colossal (8x wide, 5x tall)", true)]
        private static bool ColossalCheck() { Menu.SetChecked("CityX/City Scale/Colossal (8x wide, 5x tall)", Is(CityScale.Colossal)); return true; }
    }
}
