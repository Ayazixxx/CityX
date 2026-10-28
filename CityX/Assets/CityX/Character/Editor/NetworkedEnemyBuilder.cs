using CityX.Character;
using CityX.Weapons;
using FishNet.Component.Transforming;
using FishNet.Managing.Object;
using FishNet.Object;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds the enemies a session shares (docs/MULTIPLAYER_STATE.md): a networked variant of each plain enemy prefab, as NetworkedPlayer is of
    /// Player, and lists them in FishNet's prefab collection so every machine can spawn them. The plain prefabs stay as they are for the tests
    /// and singleplayer (a NetworkObject outside a session logs an error and is switched off).
    ///
    /// A body enemy (the swordsman, the Shoota: the player's own body) gets what a player's body gets to be seen by others -- its transform, its
    /// animator and its named swings, shots and tells, its damage relay and health -- and its brain runs on the owner alone (NetworkedEnemy).
    /// The Wraith, which poses itself, gets its transform and its own relay and health (NetworkedWraith).
    /// </summary>
    public static class NetworkedEnemyBuilder
    {
        public const string SwordsmanPath = "Assets/CityX/Character/Swordsman.prefab";
        public const string ShootaPath = "Assets/CityX/Character/Shoota.prefab";
        public const string WraithPath = "Assets/CityX/Character/Wraith.prefab";
        public const string NetworkedSwordsmanPath = "Assets/CityX/Character/NetworkedSwordsman.prefab";
        public const string NetworkedShootaPath = "Assets/CityX/Character/NetworkedShoota.prefab";
        public const string NetworkedWraithPath = "Assets/CityX/Character/NetworkedWraith.prefab";
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";

        /// <summary>Builds all three and returns them by their /spawn names, for the scene's EnemyNetwork.</summary>
        public static EnemyNetwork.Kind[] BuildAll()
        {
            var kinds = new System.Collections.Generic.List<EnemyNetwork.Kind>();
            Add(kinds, "swordsman", BuildBody(SwordsmanPath, NetworkedSwordsmanPath, typeof(SwordsmanBrain)));
            Add(kinds, "shoota", BuildBody(ShootaPath, NetworkedShootaPath, typeof(ShootaBrain)));
            Add(kinds, "wraith", BuildWraith());
            return kinds.ToArray();
        }

        private static void Add(System.Collections.Generic.List<EnemyNetwork.Kind> kinds, string name, NetworkObject prefab)
        {
            if (prefab != null)
            {
                kinds.Add(new EnemyNetwork.Kind { Name = name, Prefab = prefab });
            }
        }

        private static NetworkObject BuildBody(string plainPath, string networkedPath, System.Type brain)
        {
            return Build(plainPath, networkedPath, root =>
            {
                // The controller by path: a variant saved from an instance has come out with an empty one before (NetworkedPlayerBuilder).
                var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
                foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                {
                    animator.runtimeAnimatorController = controller;
                }
                root.AddComponent<NetworkedAnimatorSync>();
                root.AddComponent<NetworkedDamage>();
                root.AddComponent<NetworkedAbilitySync>();   // what the body does that is drawn in code (a turned-aside bullet's streak)
                var enemy = root.AddComponent<NetworkedEnemy>();
                var brainComponent = root.GetComponent(brain) as Behaviour;
                enemy.OwnerOnly = brainComponent != null ? new[] { brainComponent } : System.Array.Empty<Behaviour>();
            });
        }

        private static NetworkObject BuildWraith()
        {
            return Build(WraithPath, NetworkedWraithPath, root =>
            {
                root.AddComponent<NetworkedWraith>();
                root.AddComponent<NetworkedEnemy>();
            });
        }

        private static NetworkObject Build(string plainPath, string networkedPath, System.Action<GameObject> addParts)
        {
            var plain = AssetDatabase.LoadAssetAtPath<GameObject>(plainPath);
            if (plain == null)
            {
                Debug.LogError($"CityX: no {plainPath} -- build the enemies before their networked variants.");
                return null;
            }
            var root = (GameObject)PrefabUtility.InstantiatePrefab(plain);
            try
            {
                root.name = System.IO.Path.GetFileNameWithoutExtension(networkedPath);
                NetworkObject networkObject = root.AddComponent<NetworkObject>();
                NetworkedPlayerWiring.TuneTransform(root.AddComponent<NetworkTransform>());
                addParts(root);
                NetworkedPlayerBuilder.AddViewDistanceObserver(root);
                NetworkedPlayerReserialize.ReserializeEditorSetValues(networkObject, setWasActiveDuringEdit: true, setSceneId: false);

                GameObject previous = AssetDatabase.LoadAssetAtPath<GameObject>(networkedPath);
                NetworkObject previousNetwork = previous != null ? previous.GetComponent<NetworkObject>() : null;
                if (previousNetwork != null)
                {
                    networkObject.SetAssetPathHash(previousNetwork.AssetPathHash);
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, networkedPath);
                var savedNetwork = saved != null ? saved.GetComponent<NetworkObject>() : null;
                Register(savedNetwork);
                Debug.Log($"CityX: networked enemy written to {networkedPath}");
                return savedNetwork;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Into FishNet's one prefab collection (every NetworkManager here points at it), once.</summary>
        private static void Register(NetworkObject prefab)
        {
            var collection = AssetDatabase.LoadAssetAtPath<DefaultPrefabObjects>(DefaultPrefabObjectsPath);
            if (prefab == null || collection == null)
            {
                Debug.LogError($"CityX: could not list {prefab} in {DefaultPrefabObjectsPath}: it cannot be spawned in a session.");
                return;
            }
            collection.AddObject(prefab, checkForDuplicates: true, initializeAdded: false);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
        }
    }
}
