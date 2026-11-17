using System.IO;
using CityX.Character;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Adjusting how the sword and the pistol are held, in the Unity editor.
    ///
    /// While the game runs, a held weapon is an ordinary object under the right hand and it carries an
    /// orange "left hand" marker. Select either (the buttons below, or the CityX > Weapon Grips menu),
    /// move and rotate it with the Scene view gizmos, watch the hands follow, and press Save: what it is
    /// at is written into the WeaponGrips asset, which outlives Play Mode and is used every time the
    /// weapon is equipped afterwards. The same idea as placing a hairstyle (see CharacterHairEditor).
    /// </summary>
    public static class WeaponGripEditorTools
    {
        /// <summary>The settings asset, made on first use if this is a fresh project (and put in force so edits reach the running game).</summary>
        public static WeaponGripSettings EnsureAsset()
        {
            WeaponGripSettings current = WeaponGripSettings.Current;
            if (AssetDatabase.Contains(current))
            {
                return current;
            }

            var existing = AssetDatabase.LoadAssetAtPath<WeaponGripSettings>(WeaponGripSettings.AssetPath);
            if (existing != null)
            {
                WeaponGripSettings.Current = existing;
                return existing;
            }

            Directory.CreateDirectory("Assets/CityX/Character/Resources");
            var created = ScriptableObject.CreateInstance<WeaponGripSettings>();
            EditorUtility.CopySerialized(current, created);   // keep whatever is in force (a fresh default, or a test's)
            AssetDatabase.CreateAsset(created, WeaponGripSettings.AssetPath);
            AssetDatabase.SaveAssets();
            WeaponGripSettings.Current = created;
            return created;
        }

        /// <summary>Batch entry point: makes the asset (no-op when it is already there).</summary>
        public static void CreateAssetFromBatch()
        {
            WeaponGripSettings asset = EnsureAsset();
            Debug.Log($"CityX: weapon grip settings at {WeaponGripSettings.AssetPath} (sword tuned: {asset.SwordTuned}, pistol tuned: {asset.PistolTuned}, torso {asset.TorsoFollowStrength})");
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void Persist(WeaponGripSettings settings)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The sword in a body's hand: a two-handed one carries its left-hand marker, a one-handed one has none but is still under the
        /// hand (it used to be missed, and the menu said no sword was held with the combat sword or the crowbar in hand).
        /// </summary>
        private static SwordWeapon HeldSword()
        {
            SwordWeapon oneHanded = null;
            foreach (SwordWeapon sword in Object.FindObjectsByType<SwordWeapon>(FindObjectsSortMode.None))
            {
                if (sword.LeftHandGrip != null)
                {
                    return sword;
                }
                if (oneHanded == null && sword.OneHanded && sword.transform.parent != null && sword.GetComponentInParent<Animator>() != null)
                {
                    oneHanded = sword;
                }
            }
            return oneHanded;
        }

        private static PistolWeapon HeldPistol()
        {
            foreach (PistolWeapon pistol in Object.FindObjectsByType<PistolWeapon>(FindObjectsSortMode.None))
            {
                if (pistol.LeftHandGrip != null)
                {
                    return pistol;
                }
            }
            return null;
        }

        // FIRST IN THE CityX MENU (priority): it has well over a hundred entries, and the grips sat somewhere down the list where the owner
        // could not find them (2026-10-16: "I dont see weapon grips").
        public const int MenuPriority = -1000;

        [MenuItem("CityX/Weapon Grips/Select the held sword", false, MenuPriority)]
        private static void SelectHeldSword()
        {
            SwordWeapon sword = HeldSword();
            if (sword == null)
            {
                EditorUtility.DisplayDialog("No sword held", "Press Play, then equip a sword in the game (press ` and type /equip spikesword, " +
                    "/equip spikesword, /equip combatsword, /equip bat or /equip crowbar).", "OK");
                return;
            }
            Selection.activeGameObject = sword.gameObject;
            SceneView.FrameLastActiveSceneView();
        }

        [MenuItem("CityX/Weapon Grips/Select the held pistol", false, MenuPriority + 1)]
        private static void SelectHeldPistol()
        {
            PistolWeapon pistol = HeldPistol();
            if (pistol == null)
            {
                EditorUtility.DisplayDialog("No pistol held", "Press Play, then equip the pistol in the game (press ` and type /equip pistol).", "OK");
                return;
            }
            Selection.activeGameObject = pistol.gameObject;
            SceneView.FrameLastActiveSceneView();
        }

        [MenuItem("CityX/Weapon Grips/Open the grip settings (torso amount, saved holds)", false, MenuPriority + 2)]
        private static void OpenSettings()
        {
            WeaponGripSettings settings = EnsureAsset();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        // ---- the Inspector sections ----

        internal static void DrawSword(SwordWeapon sword)
        {
            EditorGUILayout.Space(10.0f);
            EditorGUILayout.LabelField("Sword hold", EditorStyles.boldLabel);
            DrawBatSize(sword);

            if (sword.OneHanded)
            {
                DrawOneHandedSword(sword);
                return;
            }

            if (!Application.isPlaying || sword.LeftHandGrip == null)
            {
                EditorGUILayout.HelpBox(
                    "Press Play and equip the sword (press ` in the game and type /equip spikesword). While it is held, " +
                    "use CityX > Weapon Grips > Select the held sword, then drag it and its orange left-hand marker " +
                    "in the Scene view. This section appears on the sword and on the marker.", MessageType.Info);
                return;
            }

            WeaponGripSettings settings = EnsureAsset();
            EditorGUILayout.LabelField("Saved hold", settings.SwordTuned ? "yes -- hand tuned" : "no -- derived from the animation");

            if (GUILayout.Button("Select the sword (Move / Rotate it: the RIGHT hand's hold, and which way the edge faces)"))
            {
                Selection.activeGameObject = sword.gameObject;
                SceneView.FrameLastActiveSceneView();
            }
            if (GUILayout.Button("Select the left-hand marker (Move it: where the LEFT hand holds)"))
            {
                Selection.activeGameObject = sword.LeftHandGrip.gameObject;
                SceneView.FrameLastActiveSceneView();
            }

            if (GUILayout.Button("Save this hold"))
            {
                settings.SaveSword(sword.transform, sword.LeftHandGrip);
                Persist(settings);
                Debug.Log($"CityX: saved the sword hold -- sword at {settings.SwordPosition} rotated {settings.SwordEuler} on the right hand, left hand at {settings.SwordLeftHand} on the sword");
            }
            using (new EditorGUI.DisabledScope(!settings.SwordTuned))
            {
                if (GUILayout.Button("Go back to the derived hold"))
                {
                    settings.ResetSword();
                    Persist(settings);
                    sword.ApplyGrip();
                }
            }

            DrawTorso(settings);
            EditorGUILayout.HelpBox(
                "Move slides the sword through both hands; Rotate turns the blade (its edge is the gold-trimmed side; on the spike sword, the spikes). " +
                "While the sword or the marker is selected the hands hold still, so you see the sword move in them; deselect it and the stance " +
                "points the blade again with the new hold. Select the orange marker to move the left hand along the handle; it follows the marker " +
                "wherever the swing takes the sword. Save keeps it after you stop the game.",
                MessageType.None);
        }

        /// <summary>A one-handed sword has no left-hand marker: only the sword itself is moved and turned on the right hand.</summary>
        private static void DrawOneHandedSword(SwordWeapon sword)
        {
            if (!Application.isPlaying || sword.transform.parent == null)
            {
                EditorGUILayout.HelpBox(
                    "Press Play and equip the sword (press ` in the game and type /equip combatsword). While it is held, select it and " +
                    "drag or rotate it in the Scene view: it is held in the right hand alone.", MessageType.Info);
                return;
            }

            WeaponGripSettings settings = EnsureAsset();
            EditorGUILayout.LabelField("Saved hold", settings.OneHandTuned ? "yes -- hand tuned" : "no -- derived from the hand's shape");

            if (GUILayout.Button("Select the sword (Move / Rotate it: the hand's hold, and which way the edge faces)"))
            {
                Selection.activeGameObject = sword.gameObject;
                SceneView.FrameLastActiveSceneView();
            }
            if (GUILayout.Button("Save this hold"))
            {
                settings.SaveOneHand(sword.transform);
                Persist(settings);
                Debug.Log($"CityX: saved the one-handed sword hold -- sword at {settings.OneHandPosition} rotated {settings.OneHandEuler} on the right hand");
            }
            using (new EditorGUI.DisabledScope(!settings.OneHandTuned))
            {
                if (GUILayout.Button("Go back to the derived hold"))
                {
                    settings.ResetOneHand();
                    Persist(settings);
                    sword.ApplyGrip();
                }
            }
            DrawTorso(settings);
        }

        internal static void DrawPistol(PistolWeapon pistol)
        {
            EditorGUILayout.Space(10.0f);
            EditorGUILayout.LabelField("Pistol: the free (left) hand", EditorStyles.boldLabel);

            if (!Application.isPlaying || pistol.LeftHandGrip == null)
            {
                EditorGUILayout.HelpBox(
                    "Press Play and equip the pistol (press ` in the game and type /equip pistol), hold right-click to aim, " +
                    "then use CityX > Weapon Grips > Select the held pistol and drag the orange left-hand marker. " +
                    "(The right hand's hold is tuned with the /pistol console command as before.)", MessageType.Info);
                return;
            }

            WeaponGripSettings settings = EnsureAsset();
            EditorGUILayout.LabelField("Saved hold", settings.PistolTuned ? "yes -- hand tuned" : "no -- derived from measurements");

            if (GUILayout.Button("Select the left-hand marker (Move it: where the free hand holds the gun)"))
            {
                Selection.activeGameObject = pistol.LeftHandGrip.gameObject;
                SceneView.FrameLastActiveSceneView();
            }
            if (GUILayout.Button("Save this hold"))
            {
                settings.SavePistol(pistol.LeftHandGrip);
                Persist(settings);
                Debug.Log($"CityX: saved the pistol's free-hand point: {settings.PistolLeftHand} (pistol space)");
            }
            using (new EditorGUI.DisabledScope(!settings.PistolTuned))
            {
                if (GUILayout.Button("Go back to the derived hold"))
                {
                    settings.ResetPistol();
                    Persist(settings);
                    pistol.ApplyGrip();
                }
            }
            EditorGUILayout.HelpBox(
                "The free hand only comes up while you aim (hold right-click), so aim while you drag the marker.", MessageType.None);
        }

        /// <summary>
        /// The bat's size slider (owner, 2026-11-16: "let me adjust the baseball bat size in unity"), on the bat in hand while the game runs and on the prefab
        /// when it does not: dragged, every bat changes at once, and the size is saved in the WeaponGrips asset, so it is kept after the game stops.
        /// </summary>
        private static void DrawBatSize(SwordWeapon sword)
        {
            if (sword.GetComponent<BatSize>() == null)
            {
                return;
            }
            WeaponGripSettings settings = EnsureAsset();
            EditorGUI.BeginChangeCheck();
            float size = EditorGUILayout.Slider("Bat size", settings.BaseballBatSize, WeaponGripSettings.SmallestSize, WeaponGripSettings.LargestSize);
            if (EditorGUI.EndChangeCheck())
            {
                settings.BaseballBatSize = size;
                Persist(settings);
            }
            EditorGUILayout.HelpBox("1 is the size the bat was built at. The hands stay on the handle and a swing reaches as far as the bigger or smaller barrel. " +
                "Kept after you stop the game. Use this, not the Transform's Scale: scaling the Transform moves the bat off the hands.", MessageType.None);
        }

        private static void DrawTorso(WeaponGripSettings settings)
        {
            EditorGUI.BeginChangeCheck();
            float strength = EditorGUILayout.Slider("Swing torso follow", settings.TorsoFollowStrength, 0.0f, 1.0f);
            if (EditorGUI.EndChangeCheck())
            {
                settings.TorsoFollowStrength = strength;
                Persist(settings);
            }
        }
    }

    [CustomEditor(typeof(SwordWeapon))]
    public sealed class SwordWeaponEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            WeaponGripEditorTools.DrawSword((SwordWeapon)target);
        }
    }

    [CustomEditor(typeof(PistolWeapon))]
    public sealed class PistolWeaponEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            WeaponGripEditorTools.DrawPistol((PistolWeapon)target);
        }
    }

    /// <summary>
    /// Tells the running game when a held sword's hold is being adjusted -- the sword, or its left-hand marker, selected in Play Mode --
    /// so the sword pose keeps the hand still and the gizmo visibly moves the sword IN the hand (WeaponGripSettings.HoldBeingEdited).
    /// Deselect it and the stance aims the blade again, with the new hold.
    /// </summary>
    [InitializeOnLoad]
    internal static class SwordHoldEditWatch
    {
        /// <summary>
        /// A two-handed sword selected itself (not its marker): moving it slides it through BOTH hands. The left hand's marker is a child of the sword, so
        /// it was carried along and the left hand with it, and past the left arm's reach the right arm was drawn after it (owner, 2026-11-16, of the bat:
        /// "when i try to adjust position the hands move with it and they are stuck not holding it properly"). The marker is held where it was against
        /// the right hand; Save then keeps where the left hand ends up on the sword.
        /// </summary>
        private static SwordWeapon _sliding;

        private static Vector3 _markerInRightHand;

        static SwordHoldEditWatch()
        {
            Selection.selectionChanged += Refresh;
            EditorApplication.playModeStateChanged += _ => Refresh();
            EditorApplication.update += KeepTheLeftHand;
        }

        private static void Refresh()
        {
            GameObject selected = Selection.activeGameObject;
            WeaponGripSettings.HoldBeingEdited = Application.isPlaying && selected != null
                && selected.GetComponentInParent<SwordWeapon>() != null
                && selected.GetComponentInParent<Animator>() != null;   // a sword in a body's hand, not one lying on the ground
            SwordWeapon sword = WeaponGripSettings.HoldBeingEdited ? selected.GetComponent<SwordWeapon>() : null;
            _sliding = sword != null && sword.LeftHandGrip != null && sword.transform.parent != null ? sword : null;
            if (_sliding != null)
            {
                _markerInRightHand = _sliding.transform.parent.InverseTransformPoint(_sliding.LeftHandGrip.position);
            }
        }

        private static void KeepTheLeftHand()
        {
            if (!Application.isPlaying || _sliding == null || _sliding.LeftHandGrip == null || _sliding.transform.parent == null)
            {
                return;
            }
            _sliding.LeftHandGrip.position = _sliding.transform.parent.TransformPoint(_markerInRightHand);
        }
    }

    /// <summary>The marker's own Inspector carries the same Save button, so it can be saved from whichever of the two is selected.</summary>
    [CustomEditor(typeof(GripMarker))]
    public sealed class GripMarkerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var marker = (GripMarker)target;
            var sword = marker.GetComponentInParent<SwordWeapon>();
            if (sword != null)
            {
                WeaponGripEditorTools.DrawSword(sword);
                return;
            }
            var pistol = marker.GetComponentInParent<PistolWeapon>();
            if (pistol != null)
            {
                WeaponGripEditorTools.DrawPistol(pistol);
            }
        }
    }
}
