using CityX.Character;
using FishNet.Component.Transforming;
using FishNet.Object;
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
                // NetworkTransform's own _clientAuthoritative defaults to
                // true, which is exactly what an owner-driven body needs;
                // nothing here has to configure it further.
                NetworkObject networkObject = root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>();
                root.AddComponent<NetworkedPlayerIdentity>();
                root.AddComponent<NetworkedAnimatorSync>();

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
    }
}
