using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// A shirt print's Inspector: its fields, and the shirt printed again a moment after they stop changing (a print is a 2048-texel PNG
    /// written and imported, too slow to do on every step of a dragged number). The worn shirt shows it at once, in Play mode too: the
    /// texture is updated in place.
    /// </summary>
    [CustomEditor(typeof(ShirtPrint))]
    public sealed class ShirtPrintInspector : Editor
    {
        /// <summary>How long the fields must be still before the shirt is printed again.</summary>
        private const double SettleSeconds = 0.3;

        private static ShirtPrint _pending;
        private static double _printAt;

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
            {
                Schedule((ShirtPrint)target);
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Change a number and the shirt is printed again a moment later; in Play mode, on the shirt being worn.",
                MessageType.Info);
            if (GUILayout.Button("Print the shirt now"))
            {
                TShirtBuilder.Print((ShirtPrint)target);
            }
        }

        private static void Schedule(ShirtPrint print)
        {
            if (_pending == null)
            {
                EditorApplication.update += PrintWhenSettled;
            }
            _pending = print;
            _printAt = EditorApplication.timeSinceStartup + SettleSeconds;
        }

        private static void PrintWhenSettled()
        {
            if (EditorApplication.timeSinceStartup < _printAt)
            {
                return;
            }
            EditorApplication.update -= PrintWhenSettled;
            ShirtPrint print = _pending;
            _pending = null;
            if (print != null)
            {
                AssetDatabase.SaveAssetIfDirty(print);
                TShirtBuilder.Print(print);
            }
        }
    }
}
