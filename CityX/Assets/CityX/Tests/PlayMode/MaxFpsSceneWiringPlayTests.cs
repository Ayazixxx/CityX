using System.Collections;
using System.IO;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The frame-rate cap in the two REAL saved scenes: the title screen's settings and the in-game pause
    /// menu's. Clicking the arrows must change the actual cap the game runs under and write it to the settings
    /// file -- a wired button that changes a label and nothing else is the failure this is for.
    /// </summary>
    public sealed class MaxFpsSceneWiringPlayTests
    {
        private const string TitlePath = "Assets/CityX/Scenes/Title.unity";
        private const string CityPath = "Assets/CityX/Scenes/CityMassing.unity";

        private Scene _scene;
        private string _directory;
        private int _targetBefore;
        private int _vSyncBefore;

        [SetUp]
        public void SetUp()
        {
            _targetBefore = Application.targetFrameRate;
            _vSyncBefore = QualitySettings.vSyncCount;
            _directory = Path.Combine(Path.GetTempPath(), "CityXTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            GameSettings.DirectoryOverride = _directory;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
            Application.targetFrameRate = _targetBefore;
            QualitySettings.vSyncCount = _vSyncBefore;
            GameSettings.DirectoryOverride = null;
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
            Assert.IsTrue(button.interactable, "the button is not interactable");
            button.onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator TheTitleScreensArrowsChangeTheRealCapAndSaveIt()
        {
#if UNITY_EDITOR
            yield return Load(TitlePath);
            var view = Object.FindFirstObjectByType<TitleSettingsView>(FindObjectsInactive.Include);
            Assert.IsNotNull(view, "no settings view in the title scene");
            // The settings panel starts hidden; a player opens it before pressing anything on it.
            view.gameObject.SetActive(true);
            yield return null;
            Assert.IsNotNull(view.FpsLabel, "the MAX FPS value label is not wired");
            Assert.AreEqual(0, view.MaxFps, "a fresh install starts uncapped");
            Assert.AreEqual("UNLIMITED", view.FpsLabel.text);

            Click(view.FpsNextButton);   // wraps from UNLIMITED round to the lowest cap
            Assert.AreEqual(30, view.MaxFps);
            Assert.AreEqual(30, Application.targetFrameRate, "the click did not change the frame cap the game runs under");
            Assert.AreEqual(0, QualitySettings.vSyncCount);
            Assert.AreEqual("30 FPS", view.FpsLabel.text);
            Assert.AreEqual(30, GameSettings.Load().MaxFps, "the choice was not saved");

            Click(view.FpsNextButton);
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreEqual(60, GameSettings.Load().MaxFps);

            Click(view.FpsPrevButton);
            Click(view.FpsPrevButton);   // 30 -> UNLIMITED
            Assert.AreEqual(-1, Application.targetFrameRate, "unlimited must remove the cap");
            Assert.AreEqual("UNLIMITED", view.FpsLabel.text);
            Assert.AreEqual(0, GameSettings.Load().MaxFps);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ASavedCapIsPutIntoForceAtStartupNotOnlyWhenASettingsScreenIsOpened()
        {
#if UNITY_EDITOR
            GameSettings.Save(new GameSettingsData { MaxFps = 90 });
            Application.targetFrameRate = -1;

            DisplaySettings.ApplySavedMaxFps();   // what the game runs once, before any scene loads
            yield return null;

            Assert.AreEqual(90, Application.targetFrameRate, "the saved cap was not applied at startup");
            Assert.AreEqual(0, QualitySettings.vSyncCount);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ThePauseMenusArrowsChangeTheRealCapAndSaveIt()
        {
#if UNITY_EDITOR
            yield return Load(CityPath);
            var menu = Object.FindFirstObjectByType<PauseMenuView>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "no pause menu in the city scene");
            Assert.IsNotNull(menu.FpsLabel, "the MAX FPS value label is not wired");
            Assert.AreEqual(0, menu.MaxFps);

            Click(menu.FpsNextButton);
            Assert.AreEqual(30, Application.targetFrameRate);
            Assert.AreEqual("30 FPS", menu.FpsLabel.text);
            Assert.AreEqual(30, GameSettings.Load().MaxFps);

            Click(menu.FpsPrevButton);   // back to UNLIMITED
            Assert.AreEqual(-1, Application.targetFrameRate);
            Assert.AreEqual(0, GameSettings.Load().MaxFps);
#else
            yield break;
#endif
        }
    }
}
