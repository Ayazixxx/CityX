using System.Reflection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Forces a NetworkObject through the exact same "populate WasActiveDuringEdit,
    /// and optionally assign a real SceneId" work its own private OnValidate
    /// does -- the thing Fish-Networking's own "Reserialize NetworkObjects"
    /// menu tool runs for you by hand, which nothing calls automatically
    /// during a headless -batchmode script's own AddComponent call.
    ///
    /// REFLECTION, NOT SendMessage. NetworkObject.ReserializeEditorSetValues
    /// is `internal` (visible to Fish-Networking's own assembly, not this
    /// project's), so it can't be called directly -- but SendMessage("OnValidate", ...)
    /// is the wrong way around that: Unity's ShouldRunBehaviour() silently
    /// refuses to run a private-message dispatch on a non-ExecuteInEditMode
    /// MonoBehaviour outside Play Mode (a barely-visible "Assertion failed"
    /// in the log, not an exception), so OnValidate never actually ran, and
    /// every NetworkObject built this way kept SceneId permanently 0 --
    /// which is exactly the "is expected to be initialized but was not"
    /// error every real Play session then hit. Invoking the method directly
    /// via reflection has no such gate.
    /// </summary>
    public static class NetworkedPlayerReserialize
    {
        /// <summary>
        /// For a genuine scene object: assigns a real SceneId first (the
        /// static, whole-scene path the official Reserialize tool itself
        /// uses), THEN reserializes with setSceneId left false -- calling
        /// the per-object instance path with setSceneId: true instead looked
        /// right but silently did nothing here, because its own SceneId
        /// assignment is gated behind a static "only once every 250ms of
        /// real editor time" throttle (NetworkObject.Serialized.cs's own
        /// _lastSceneIdAutomaticRebuildTime) that a single batch-mode call
        /// never clears on its own.
        /// </summary>
        public static void ReserializeSceneObject(NetworkObject networkObject, Scene scene)
        {
            MethodInfo createSceneId = typeof(NetworkObject).GetMethod(
                "CreateSceneId",
                BindingFlags.NonPublic | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Scene), typeof(bool), typeof(int).MakeByRefType() },
                modifiers: null);
            if (createSceneId == null)
            {
                Debug.LogError("CityX: FishNet.Object.NetworkObject.CreateSceneId(Scene, bool, out int) " +
                                "was not found by reflection -- has the FishNet package changed?");
                return;
            }
            object[] args = { scene, true, 0 };
            createSceneId.Invoke(null, args);

            ReserializeEditorSetValues(networkObject, setWasActiveDuringEdit: true, setSceneId: false);
        }

        public static void ReserializeEditorSetValues(NetworkObject networkObject, bool setWasActiveDuringEdit, bool setSceneId)
        {
            MethodInfo method = typeof(NetworkObject).GetMethod(
                "ReserializeEditorSetValues",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                Debug.LogError("CityX: FishNet.Object.NetworkObject.ReserializeEditorSetValues " +
                                "was not found by reflection -- has the FishNet package changed?");
                return;
            }
            method.Invoke(networkObject, new object[] { setWasActiveDuringEdit, setSceneId });
        }
    }
}
