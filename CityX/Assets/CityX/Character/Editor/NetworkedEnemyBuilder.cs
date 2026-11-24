using CityX.Character;
using CityX.Weapons;
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
    /// A body enemy (the swordsman, the Shoota, the giant: the player's own body) gets what a player's body gets to be seen by others -- its
    /// transform, its animator and its named swings, shots and tells, its damage relay and health -- and its brain runs on the owner alone
    /// (NetworkedEnemy).
    /// The Wraith, which poses itself, gets its transform and its own relay and health (NetworkedWraith).
    /// </summary>
    public static class NetworkedEnemyBuilder
    {
        public const string SwordsmanPath = "Assets/CityX/Character/Swordsman.prefab";
        public const string ShootaPath = "Assets/CityX/Character/Shoota.prefab";
        public const string WraithPath = "Assets/CityX/Character/Wraith.prefab";
        public const string GiantPath = "Assets/CityX/Character/Giant.prefab";
        public const string NetworkedSwordsmanPath = "Assets/CityX/Character/NetworkedSwordsman.prefab";
        public const string CrazyGuyPath = "Assets/CityX/Character/CrazyGuy.prefab";
        public const string NetworkedCrazyGuyPath = "Assets/CityX/Character/NetworkedCrazyGuy.prefab";
        public const string HobosFolder = "Assets/CityX/Character/Hobos";
        public const string NetworkedShootaPath = "Assets/CityX/Character/NetworkedShoota.prefab";
        public const string NetworkedWraithPath = "Assets/CityX/Character/NetworkedWraith.prefab";
        public const string NetworkedGiantPath = "Assets/CityX/Character/NetworkedGiant.prefab";
        public const string NetworkedWraithSummonPath = "Assets/CityX/Character/NetworkedWraithSummon.prefab";

        /// <summary>The summon's kind: what the Wraith Staff asks the session for (WraithSummonCaster.SummonKind).</summary>
        public const string WraithSummonKind = "wraith_summon";
        public const string CrimsonClawPath = "Assets/CityX/Character/CrimsonClaw.prefab";
        public const string NetworkedCrimsonClawPath = "Assets/CityX/Character/NetworkedCrimsonClaw.prefab";
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";

        /// <summary>Builds them all and returns them by their /spawn names, for the scene's EnemyNetwork.</summary>
        public static EnemyNetwork.Kind[] BuildAll()
        {
            var kinds = new System.Collections.Generic.List<EnemyNetwork.Kind>();
            Add(kinds, "swordsman", BuildBody(SwordsmanPath, NetworkedSwordsmanPath, typeof(SwordsmanBrain)));
            Add(kinds, "crazyguy", BuildBody(CrazyGuyPath, NetworkedCrazyGuyPath, typeof(SwordsmanBrain)));
            for (int look = 0; look < HoboKinds.Looks.Length; look++)
            {
                for (int weapon = 0; weapon < HoboKinds.Weapons.Length; weapon++)
                {
                    string name = HoboKinds.PrefabNameOf(look, weapon);
                    Add(kinds, HoboKinds.KindOf(look, weapon),
                        BuildBody($"{HobosFolder}/{name}.prefab", $"{HobosFolder}/Networked{name}.prefab", typeof(SwordsmanBrain)));
                }
            }
            Add(kinds, "shoota", BuildBody(ShootaPath, NetworkedShootaPath, typeof(ShootaBrain)));
            Add(kinds, "wraith", BuildWraith());
            Add(kinds, WraithSummonKind, BuildWraithSummon());
            Add(kinds, "giant", BuildBody(GiantPath, NetworkedGiantPath, typeof(GiantBrain)));
            Add(kinds, "crimsonclaw", BuildCrimsonClaw());
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
                root.AddComponent<NetworkedCorpseLoot>();    // its gear taken off it once, on every screen (docs/LOOTING.md, bodies)
                NetworkedPlayerWiring.AddRagdollSync(root);   // a fallen enemy lies the same on every screen
                var enemy = root.AddComponent<NetworkedEnemy>();
                var brainComponent = root.GetComponent(brain) as Behaviour;
                enemy.OwnerOnly = brainComponent != null ? new[] { brainComponent } : System.Array.Empty<Behaviour>();
            });
        }

        /// <summary>The Crimson Claw: run by its owner's machine, a copy on every other (NetworkedCrimsonClaw).</summary>
        private static NetworkObject BuildCrimsonClaw()
        {
            return Build(CrimsonClawPath, NetworkedCrimsonClawPath, root =>
            {
                root.AddComponent<NetworkedCrimsonClaw>();
                root.AddComponent<NetworkedEnemy>();
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

        /// <summary>The Wraith Staff's summon: the Wraith, a summon from the start on every machine (Wraith.StartsSummoned).</summary>
        private static NetworkObject BuildWraithSummon()
        {
            return Build(WraithPath, NetworkedWraithSummonPath, root =>
            {
                root.GetComponent<Wraith>().StartsSummoned = true;
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
                root.AddComponent<NetworkedBodyMotion>();
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
