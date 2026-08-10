using System.IO;
using FishNet.Component.Observing;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Observing;
using FishNet.Transporting.Tugboat;
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

        /// <summary>FishNet's auto-populated prefab collection, created on import.</summary>
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

        [MenuItem("CityX/Spike/Rebuild Spike Assets")]
        public static void BuildAll()
        {
            EnsureFolder(GeneratedFolder);

            DistanceCondition viewDistance = BuildViewDistanceCondition();
            BuildAgentPrefab(viewDistance);
            BuildPlayerPrefab();
            BuildPredictedPlayerPrefab();
            BuildNetworkManagerPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CityX.Spike] rebuilt spike assets in {GeneratedFolder}");
        }

        /// <summary>
        /// The predicted player. No NetworkTransform: prediction owns this
        /// object's position, and a transform syncer fighting the reconcile
        /// for control of the same value is a classic source of jitter.
        /// </summary>
        private static void BuildPredictedPlayerPrefab()
        {
            GameObject go = new GameObject("SpikePredictedPlayer");
            try
            {
                NetworkObject nob = go.AddComponent<NetworkObject>();
                go.AddComponent<SpikePredictedPlayer>();

                // NetworkObject._enablePrediction defaults to FALSE, and
                // without it the [Replicate]/[Reconcile] pair half-works in a
                // way that looks fine: the client still predicts its own
                // movement and still matches the server, because both run the
                // same deterministic code from the same inputs. What it does
                // NOT do is reconcile -- so the first time anything diverges
                // (a dropped packet, a collision, another player) the client
                // stays wrong forever with nothing to pull it back.
                //
                // This was caught by asserting reconcile count > 0 rather than
                // just "client and server agree". Agreement was true and
                // meaningless.
                SerializedObject so = new SerializedObject(nob);
                SerializedProperty enablePrediction = so.FindProperty("_enablePrediction");
                if (enablePrediction == null)
                {
                    Debug.LogError("[CityX.Spike] NetworkObject has no '_enablePrediction' field -- FishNet's internals changed.");
                }
                else
                {
                    enablePrediction.boolValue = true;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(go, $"{GeneratedFolder}/SpikePredictedPlayer.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// A NetworkManager configured for measurement.
        ///
        /// Two things here can only be set by authoring, which is why this is
        /// a generated prefab rather than an AddComponent in a test:
        ///
        ///   _persistence = AllowMultiple -- FishNet destroys extra
        ///   NetworkManagers by default. Measuring real per-client bandwidth
        ///   needs a genuine server AND a genuine remote client, and in host
        ///   mode the local client short-circuits the socket, so the numbers
        ///   would be fiction. Two managers in one process, talking over
        ///   loopback, gives real serialization and real bytes.
        ///
        ///   _spawnablePrefabs -- assigned up front so instantiating this
        ///   prefab does not trip NetworkManager's "SpawnablePrefabs is null"
        ///   error the way building one by hand does.
        /// </summary>
        private static void BuildNetworkManagerPrefab()
        {
            PrefabObjects defaultPrefabs = AssetDatabase.LoadAssetAtPath<PrefabObjects>(DefaultPrefabObjectsPath);
            if (defaultPrefabs == null)
            {
                Debug.LogError($"[CityX.Spike] no prefab collection at {DefaultPrefabObjectsPath}; " +
                               "the generated NetworkManager will log errors when instantiated.");
            }

            GameObject go = new GameObject("SpikeNetworkManager");
            try
            {
                NetworkManager nm = go.AddComponent<NetworkManager>();
                go.AddComponent<Tugboat>();

                SerializedObject so = new SerializedObject(nm);
                SetEnumProperty(so, "_persistence", (int)NetworkManager.PersistenceType.AllowMultiple);
                if (defaultPrefabs != null)
                {
                    SerializedProperty prefabs = so.FindProperty("_spawnablePrefabs");
                    if (prefabs != null)
                    {
                        prefabs.objectReferenceValue = defaultPrefabs;
                    }
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(go, $"{GeneratedFolder}/SpikeNetworkManager.prefab");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void SetEnumProperty(SerializedObject so, string path, int value)
        {
            SerializedProperty property = so.FindProperty(path);
            if (property == null)
            {
                Debug.LogError($"[CityX.Spike] NetworkManager has no '{path}' field -- FishNet's internals changed.");
                return;
            }
            property.enumValueIndex = value;
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
