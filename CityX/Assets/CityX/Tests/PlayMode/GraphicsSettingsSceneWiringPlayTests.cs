using System.Collections;
using System.IO;
using CityX.Core;
using CityX.UI;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The graphics page in the two REAL saved scenes (docs/GRAPHICS_SETTINGS.md): GRAPHICS opens it over the settings, its arrows switch the real
    /// effects and save the choice, the other settings saving after it keep it, and BACK (or Escape on the title screen) returns to the settings.
    /// </summary>
    public sealed class GraphicsSettingsSceneWiringPlayTests
    {
        private const string TitlePath = "Assets/CityX/Scenes/Title.unity";
        private const string CityPath = "Assets/CityX/Scenes/CityMassing.unity";

        private Scene _scene;
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            GameSettings.DirectoryOverride = _directory;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GraphicsQuality.Restore();
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1.0f;
            GameSettings.DirectoryOverride = ScratchDataFixture.Folder;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private IEnumerator Load(string path)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(path);
#else
            yield break;
#endif
        }

        private static void Click(Button button)
        {
            Assert.IsNotNull(button, "the button is missing from the scene");
            Assert.IsTrue(button.isActiveAndEnabled && button.interactable, $"{button.name} cannot be pressed");
            button.onClick.Invoke();
        }

        private static int RowOf(string name)
        {
            for (int row = 0; row < GraphicsMenu.Rows.Length; row++)
            {
                if (GraphicsMenu.Rows[row].Name == name) return row;
            }
            Assert.Fail($"no {name} row");
            return -1;
        }

        private static bool FeatureOn(string name)
        {
            foreach (ScriptableRendererFeature feature in Resources.FindObjectsOfTypeAll<ScriptableRendererFeature>())
            {
                if (feature.name == name) return feature.isActive;
            }
            Assert.Fail($"no {name} renderer feature");
            return false;
        }

        [UnityTest]
        public IEnumerator OnTheTitleScreenTheArrowsSwitchTheRealEffectAndTheOtherSettingsKeepIt()
        {
#if UNITY_EDITOR
            yield return Load(TitlePath);
            var title = Object.FindFirstObjectByType<TitleScreen>(FindObjectsInactive.Include);
            var view = Object.FindFirstObjectByType<TitleSettingsView>(FindObjectsInactive.Include);
            Assert.IsNotNull(view, "no settings view in the title scene");
            Assert.IsNotNull(view.Graphics, "the graphics page is not wired");
            title.Show(TitleScreen.Panel.Settings);
            yield return null;

            Click(view.GraphicsButton);
            yield return null;
            GraphicsSettingsPanel page = view.Graphics;
            Assert.IsTrue(page.IsOpen, "GRAPHICS did not open the page");
            Assert.IsFalse(view.MasterSlider.gameObject.activeInHierarchy, "the settings' own controls show through the graphics page");
            Assert.AreEqual(GraphicsMenu.Rows.Length, page.Values.Length, "a row of the menu has no row on the page");
            Assert.AreEqual("HIGH", page.Values[GraphicsMenu.PresetRow].text);

            int shafts = RowOf("LIGHT SHAFTS");
            Assert.IsTrue(FeatureOn(GraphicsQuality.LightShaftsFeature), "setup: the light shafts were off");
            Click(page.NextButtons[shafts]);
            Assert.AreEqual("OFF", page.Values[shafts].text);
            Assert.IsFalse(FeatureOn(GraphicsQuality.LightShaftsFeature), "the click did not switch the light shafts off");
            Assert.AreEqual("CUSTOM", page.Values[GraphicsMenu.PresetRow].text);
            Assert.IsFalse(GameSettings.Load().Graphics.LightShafts, "the choice was not saved");
            Assert.AreEqual(GraphicsMenu.Rows[shafts].Description, page.Description.text, "the line under the rows does not say what the row does");

            title.Back();   // Escape: one step back, to the settings
            yield return null;
            Assert.IsFalse(page.IsOpen);
            Assert.IsTrue(view.MasterSlider.gameObject.activeInHierarchy, "the settings did not come back");
            Assert.IsTrue(view.isActiveAndEnabled, "Escape on the graphics page left the settings");

            Click(view.FpsNextButton);   // the other settings save the whole file
            Assert.IsFalse(GameSettings.Load().Graphics.LightShafts, "saving the other settings threw the graphics choice away");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator InThePauseMenuThePresetSetsThePipelineAndBackReturnsToTheSettings()
        {
#if UNITY_EDITOR
            yield return Load(CityPath);
            var menu = Object.FindFirstObjectByType<PauseMenuView>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "no pause menu in the city scene");
            Assert.IsNotNull(menu.Graphics, "the graphics page is not wired");
            menu.Open();
            Click(menu.SettingsButton);
            Click(menu.GraphicsButton);
            yield return null;
            GraphicsSettingsPanel page = menu.Graphics;
            Assert.IsTrue(page.IsOpen);

            Click(page.NextButtons[GraphicsMenu.PresetRow]);   // HIGH wraps round to LOW
            Assert.AreEqual("LOW", page.Values[GraphicsMenu.PresetRow].text);
            var pipeline = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            Assert.AreEqual(1, pipeline.msaaSampleCount, "LOW did not switch the anti-aliasing off");
            Assert.IsFalse(FeatureOn(GraphicsQuality.AmbientOcclusionFeature), "LOW still draws the ambient occlusion");
            Assert.AreEqual(GraphicsPreset.Low, GameSettings.Load().Graphics.Preset);

            Click(page.BackButton);
            Assert.IsFalse(page.IsOpen);
            Assert.IsTrue(menu.MasterSlider.gameObject.activeInHierarchy, "BACK did not bring the settings back");

            Click(menu.GraphicsButton);
            menu.Close();
            menu.Open();   // the menu always opens on the pause panel, the graphics page closed
            Assert.IsFalse(page.IsOpen, "the graphics page was still open when the menu came back");
            menu.Close();
#else
            yield break;
#endif
        }
    }
}
