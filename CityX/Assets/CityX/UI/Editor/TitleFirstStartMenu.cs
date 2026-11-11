using CityX.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CityX.UIEditor
{
    /// <summary>
    /// Plays the title as a first boot: the empty conveyor belt with its prompt, characters made or not (owner, 2026-11-11: "make it so i can
    /// still boot from the first start scene where you control the conveyer belt somehow (for test purposes) because that is meant to be the
    /// first boot screen when no other characters have been made yet"). F8 on the title does the same while it plays.
    /// </summary>
    public static class TitleFirstStartMenu
    {
        private const string TitleScene = "Assets/CityX/Scenes/Title.unity";

        [MenuItem("CityX/Play Title As First Start")]
        public static void Play()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            EditorSceneManager.OpenScene(TitleScene);
            PlayerPrefs.SetInt(TitleScreen.FirstStartRequest, 1);
            PlayerPrefs.Save();
            EditorApplication.EnterPlaymode();
        }
    }
}
