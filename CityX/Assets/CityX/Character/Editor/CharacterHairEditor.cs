using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// The Inspector for CharacterHair: how to place a hairstyle by eye.
    ///
    /// While the game is running, the worn hair is an ordinary object under the
    /// head bone. Select it (the button below does it for you), drag it with the
    /// Move, Rotate and Scale tools in the Scene view, and when it looks right press
    /// Save: what it is at is written into the HairFits asset, which lives on after
    /// Play Mode ends and is read by every head wearing the style.
    ///
    /// NO CONSOLE COMMANDS NEEDED. An in-game console makes you type numbers and
    /// guess; the gizmos let you see the hair move on the head.
    /// </summary>
    [CustomEditor(typeof(CityX.Character.CharacterHair))]
    public sealed class CharacterHairEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var hair = (CityX.Character.CharacterHair)target;

            EditorGUILayout.Space(10.0f);
            EditorGUILayout.LabelField("Hair placement", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Press Play, then come back to this component (on the player, or on the creator's body). " +
                    "The hair only exists while the game runs.", MessageType.Info);
                return;
            }

            // Which style is being worn -- and a way to change it without leaving the Inspector.
            string[] styles = CityX.Character.CharacterHair.ValidStyles;
            int current = System.Array.IndexOf(styles, hair.Style);
            int chosen = EditorGUILayout.Popup("Style", current, styles);
            if (chosen != current && chosen >= 0)
            {
                hair.SetStyle(styles[chosen]);
            }

            Transform worn = hair.Worn;
            if (worn == null)
            {
                EditorGUILayout.HelpBox("Bald -- pick a style above to place it.", MessageType.None);
                return;
            }

            bool tuned = CityX.Character.CharacterHair.IsTuned(hair.Style);
            EditorGUILayout.LabelField("Now", Fit(worn).ToString());
            EditorGUILayout.LabelField("Saved fit", tuned ? "yes -- hand tuned" : "no -- using the automatic fit");

            if (GUILayout.Button("Select the hair (then drag it in the Scene view)"))
            {
                Selection.activeGameObject = worn.gameObject;
                SceneView.FrameLastActiveSceneView();
            }

            if (GUILayout.Button($"Save this placement as the fit for \"{hair.Style}\""))
            {
                Save(hair.Style, Fit(worn));
            }

            using (new EditorGUI.DisabledScope(!tuned))
            {
                if (GUILayout.Button("Go back to the automatic fit"))
                {
                    CityX.Character.HairFitSettings settings = CityX.Character.HairFitSettings.Current;
                    CityX.Character.CharacterHair.ResetFit(hair.Style);
                    if (settings != null)
                    {
                        EditorUtility.SetDirty(settings);
                        AssetDatabase.SaveAssets();
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "Move the hair with the gizmos, then Save. It is kept after you stop the game, and every head " +
                "wearing this style uses it. To send me the numbers instead, copy the \"Now\" line above.",
                MessageType.None);
        }

        private static CityX.Character.HairFitSettings EnsureSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CityX.Character.HairFitSettings>(CityX.Character.HairFitSettings.AssetPath);
            if (existing != null)
            {
                return existing;
            }
            System.IO.Directory.CreateDirectory("Assets/CityX/Character/Resources");
            var created = ScriptableObject.CreateInstance<CityX.Character.HairFitSettings>();
            AssetDatabase.CreateAsset(created, CityX.Character.HairFitSettings.AssetPath);
            AssetDatabase.SaveAssets();
            return created;
        }

        /// <summary>What the worn hair is at now, angles wrapped to -180..180 so they read as small numbers.</summary>
        private static CityX.Character.CharacterHair.Fit Fit(Transform worn)
        {
            Vector3 euler = worn.localEulerAngles;
            euler.x = Wrap(euler.x);
            euler.y = Wrap(euler.y);
            euler.z = Wrap(euler.z);
            return new CityX.Character.CharacterHair.Fit(worn.localPosition, euler, worn.localScale);
        }

        private static float Wrap(float degrees)
        {
            degrees %= 360.0f;
            if (degrees > 180.0f) degrees -= 360.0f;
            if (degrees < -180.0f) degrees += 360.0f;
            return degrees;
        }

        private static void Save(string style, CityX.Character.CharacterHair.Fit fit)
        {
            CityX.Character.HairFitSettings settings = CityX.Character.HairFitSettings.Current;
            if (settings == null)
            {
                // The scene builders make the asset; make it now if this is a fresh project.
                settings = EnsureSettings();
                CityX.Character.HairFitSettings.Current = settings;
            }
            CityX.Character.CharacterHair.SaveFit(style, fit);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: saved the fit for \"{style}\": {fit}");
        }
    }
}
