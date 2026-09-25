using CityX.Character;
using FishNet.Component.Observing;
using FishNet.Component.Transforming;
using FishNet.Object;
using FishNet.Observing;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// A SEPARATE prefab, derived from the real Player.prefab, that carries
    /// FishNet's own networking components -- used only for spawning a
    /// joining peer's own body (see NetworkedPlayerSpawner). Deliberately
    /// NOT folded into PlayerRigBuilder's own shared Player.prefab: that
    /// prefab is Object.Instantiate'd directly by roughly 80 pre-existing
    /// PlayMode tests with no NetworkManager or FishNet spawn pipeline
    /// involved at all, and NetworkObject.Awake() unconditionally logs an
    /// error ("is expected to be initialized but was not") for any instance
    /// that is neither a real scene object nor spawned through FishNet's own
    /// object pool -- a plain Instantiate always hits that gap, and Unity's
    /// Test Framework fails a test on any unexpected error log. Every one of
    /// those tests needs to keep instantiating a completely plain player,
    /// exactly as before this feature existed; this prefab is what the
    /// actual multiplayer path uses instead.
    ///
    /// The scene's own single BAKED player (CityMassingSceneBuilder) is a
    /// different case and is NOT built from this class -- it gets the same
    /// four components added directly to that one scene instance, which is
    /// a genuine scene object (a real SceneId, assigned via OnValidate) and
    /// does not hit the same Awake() gap a bare clone does.
    /// </summary>
    public static class NetworkedPlayerBuilder
    {
        private const string PrefabPath = "Assets/CityX/Character/NetworkedPlayer.prefab";
        private const string ViewDistanceConditionPath = "Assets/CityX/Character/PlayerViewDistance.asset";

        [MenuItem("CityX/Rebuild Networked Player")]
        public static GameObject Build()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogError("CityX: no Player.prefab -- run CityX/Rebuild Player Rig first.");
                return null;
            }

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            try
            {
                // THE ANIMATOR CONTROLLER MUST SURVIVE THIS PREFAB VARIANT.
                // Found by the first real over-the-network test of jump/punch
                // sync: the saved NetworkedPlayer.prefab carried an explicit
                // `m_Controller: {fileID: 0}` override, so every spawned
                // player clone -- what a friend actually sees and controls --
                // had an Animator playing NOTHING (Unity logs "Animator is not
                // playing an AnimatorController" every frame). No earlier test
                // ever looked at an animator on a spawned clone. Restoring the
                // base prefab's controller onto the instance removes the
                // bogus override at save time.
                Animator baseAnimator = playerPrefab.GetComponentInChildren<Animator>(true);
                Animator instanceAnimator = root.GetComponentInChildren<Animator>(true);
                RuntimeAnimatorController controller = baseAnimator != null
                    ? baseAnimator.runtimeAnimatorController
                    : null;
                if (controller == null)
                {
                    controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        "Assets/CityX/Character/PlayerAnimator.controller");
                }
                if (instanceAnimator != null && controller != null)
                {
                    instanceAnimator.runtimeAnimatorController = controller;
                }
                else
                {
                    Debug.LogError("CityX: could not restore the Animator controller onto the networked player.");
                }

                // NetworkTransform's own _clientAuthoritative defaults to
                // true, which is exactly what an owner-driven body needs;
                // nothing here has to configure it further.
                NetworkObject networkObject = root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>();
                root.AddComponent<NetworkedPlayerIdentity>();
                root.AddComponent<NetworkedAnimatorSync>();
                AddViewDistanceObserver(root);

                // Same reasoning as the scene-baked player's own doc comment
                // in CityMassingSceneBuilder: Unity normally calls
                // OnValidate the moment a component is added in the Editor,
                // which is what actually populates a NetworkObject's
                // WasActiveDuringEdit flag -- but that never fires during a
                // headless -batchmode script's own AddComponent call.
                // ReserializeEditorSetValues (what OnValidate itself calls)
                // is internal, not private, but still not visible across an
                // assembly boundary -- reflection reaches it directly. A
                // FIRST ATTEMPT used SendMessage("OnValidate", ...) instead
                // (dispatching by name, the same way Unity's own engine
                // invokes every other Message-based callback) and it looked
                // like it worked (no exception), but it silently did
                // nothing: SendMessage on a non-ExecuteInEditMode
                // MonoBehaviour outside Play Mode fails Unity's own internal
                // ShouldRunBehaviour() check (visible as a benign-looking
                // "Assertion failed" in the log, easy to miss), so
                // OnValidate never actually ran. Calling the C# method
                // directly has no such gate.
                NetworkedPlayerReserialize.ReserializeEditorSetValues(
                    networkObject, setWasActiveDuringEdit: true, setSceneId: false);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"CityX: networked player written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Adds a NetworkObserver gated on NetworkedPlayerIdentity.ViewRadius
        /// so a client only observes -- and therefore only ever
        /// instantiates -- OTHER players within that distance, the same
        /// FishNet mechanism (NetworkObserver + DistanceCondition) CityX.
        /// Spike's own SpikeAssetBuilder already proved works for both
        /// NPC-shaped and player-shaped entities. Shared by both places a
        /// real player's networking components get added -- this derived
        /// clone prefab, and CityMassingSceneBuilder's own baked scene
        /// player -- so the two can never drift to different radii.
        ///
        /// _observerConditions IS internal, same as SpikeAssetBuilder's own
        /// AssignObserverCondition found -- SerializedProperty is the
        /// supported editor-side way to write it, not reflection.
        ///
        /// A REAL, ON-DISK ASSET (created once, then reloaded), not a
        /// runtime ScriptableObject.CreateInstance -- CityX.Spike's own
        /// SpikeWorld.LoadAndRegister doc comment already flags why:
        /// runtime-registered/created assets only line up between processes
        /// (or, worse, between two separately-constructed collections in
        /// the SAME process -- confirmed the hard way while proving this
        /// mechanism for players in InterestManagementPlayerToPlayer
        /// PlayTests) that happen to share the exact same instance. A real
        /// asset persisted at ViewDistanceConditionPath sidesteps that
        /// entirely, the same way SpikeAssetBuilder's own generated
        /// SpikeViewDistance.asset does.
        /// </summary>
        public static void AddViewDistanceObserver(GameObject playerObject)
        {
            DistanceCondition condition = AssetDatabase.LoadAssetAtPath<DistanceCondition>(ViewDistanceConditionPath);
            if (condition == null)
            {
                condition = ScriptableObject.CreateInstance<DistanceCondition>();
                AssetDatabase.CreateAsset(condition, ViewDistanceConditionPath);
            }
            condition.SetMaximumDistance(NetworkedPlayerIdentity.ViewRadius);
            EditorUtility.SetDirty(condition);

            NetworkObserver observer = playerObject.GetComponent<NetworkObserver>();
            if (observer == null)
            {
                observer = playerObject.AddComponent<NetworkObserver>();
            }

            SerializedObject so = new SerializedObject(observer);
            SerializedProperty conditions = so.FindProperty("_observerConditions");
            if (conditions == null)
            {
                Debug.LogError("CityX: NetworkObserver has no _observerConditions field -- FishNet's internals changed.");
                return;
            }
            conditions.arraySize = 1;
            conditions.GetArrayElementAtIndex(0).objectReferenceValue = condition;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
