using System.IO;
using FishNet.Component.Observing;
using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Observing;
using UnityEditor;
using UnityEngine;

namespace CityX.Spike.EditorTools
{
    /// <summary>
    /// Generates the spike's prefabs and observer-condition assets from code.
    ///
    /// WHY THIS EXISTS rather than building the prefabs at runtime, which is
    /// what the spike did first:
    ///
    /// 1. NetworkObserver's condition list is `internal`. FishNet expects
    ///    conditions to be AUTHORED, not assigned at runtime, and there is no
    ///    public API to do it. SerializedProperty is the supported editor-side
    ///    way to write a serialized field, and is used here rather than
    ///    reflection.
    /// 2. Runtime-registered prefabs only agree between a server and a client
    ///    that share one process. Real prefab assets carry a stable prefab id,
    ///    which the multi-process test needs anyway. This was already a known
    ///    limitation; hitting the internal field just made it due sooner.
    ///
    /// Generated rather than hand-authored so the spike stays reviewable as
    /// text and reproducible from a clean checkout -- run the menu item, or
    /// call BuildAll headlessly with -executeMethod.
    /// </summary>
    public static class SpikeAssetBuilder
    {
        /// <summary>
        /// A Resources folder, so SpikeWorld can load these without a scene
        /// reference or an addressables dependency. Acceptable for a
        /// throwaway spike; real content would not ship this way.
        /// </summary>
        private const string GeneratedFolder = "Assets/CityX/Spike/Resources/CityX.Spike";

        public const string AgentPrefabResourcePath = "CityX.Spike/SpikeAgent";
        public const string PlayerPrefabResourcePath = "CityX.Spike/SpikePlayer";

        [MenuItem("CityX/Spike/Rebuild Spike Assets")]
        public static void BuildAll()
        {
            EnsureFolder(GeneratedFolder);

            DistanceCondition viewDistance = BuildViewDistanceCondition();
            BuildAgentPrefab(viewDistance);
            BuildPlayerPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CityX.Spike] rebuilt spike assets in {GeneratedFolder}");
        }

        private static DistanceCondition BuildViewDistanceCondition()
        {
            string path = $"{GeneratedFolder}/SpikeViewDistance.asset";
            DistanceCondition condition = AssetDatabase.LoadAssetAtPath<DistanceCondition>(path);
            if (condition == null)
            {
                condition = ScriptableObject.CreateInstance<DistanceCondition>();
                AssetDatabase.CreateAsset(condition, path);
            }
            condition.SetMaximumDistance(SpikeWorld.ObserverRadius);
            EditorUtility.SetDirty(condition);
            return condition;
        }

        private static void BuildAgentPrefab(DistanceCondition viewDistance)
        {
            GameObject go = new GameObject("SpikeAgent");
            try
            {
                go.AddComponent<NetworkObject>();
                go.AddComponent<NetworkTransform>();
                go.AddComponent<SpikeAgent>();

                NetworkObserver observer = go.AddComponent<NetworkObserver>();
                AssignObserverCondition(observer, viewDistance);

                PrefabUtility.SaveAsPrefabAsset(go, $"{GeneratedFolder}/SpikeAgent.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void BuildPlayerPrefab()
        {
            GameObject go = new GameObject("SpikePlayer");
            try
            {
                go.AddComponent<NetworkObject>();
                go.AddComponent<NetworkTransform>();
                go.AddComponent<SpikePlayer>();

                // Deliberately NO view-distance condition. Culling players
                // from each other is a real decision for later; leaving it off
                // keeps the measurement about entity culling only.
                PrefabUtility.SaveAsPrefabAsset(go, $"{GeneratedFolder}/SpikePlayer.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// NetworkObserver._observerConditions is internal, so it cannot be
        /// set through the public API. SerializedProperty writes the same
        /// serialized data the inspector would, which is the supported way to
        /// author this from an editor script.
        /// </summary>
        private static void AssignObserverCondition(NetworkObserver observer, ObserverCondition condition)
        {
            SerializedObject so = new SerializedObject(observer);
            SerializedProperty conditions = so.FindProperty("_observerConditions");
            if (conditions == null)
            {
                Debug.LogError("[CityX.Spike] NetworkObserver has no _observerConditions field -- FishNet's internals changed.");
                return;
            }
            conditions.arraySize = 1;
            conditions.GetArrayElementAtIndex(0).objectReferenceValue = condition;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
