using CityX.Character;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// THE ONE PLACE THE PLAYER'S INNATE ABILITIES ARE PUT ON A BODY, called for BOTH kinds of body a player can have: the scene's baked player
    /// (the host's, or the only one in singleplayer) and the NetworkedPlayer.prefab every joining player is spawned as. Before this the casters
    /// were added only to the baked body, so a player who joined had no spells at all; a spell that exists for one body and not the other is
    /// not multiplayer.
    ///
    /// ADDING A NEW ABILITY: write the component (it must implement IReplicatedAbility, which is what makes it visible to other players --
    /// see that interface and docs/ABILITIES_MULTIPLAYER.md), then add it HERE, once. AbilityReplicationGuardTests fails an ability class
    /// that does not implement the interface and fails when an ability type is missing from NetworkedPlayer.prefab.
    ///
    /// Idempotent (a component already on the body is reused, its references refreshed), and it loads the projectile prefabs by their asset
    /// paths, so it does not depend on the Weapons builders having just run in the same process -- but it does depend on them having run
    /// once: build the Weapons assets before calling this.
    /// </summary>
    public static class PlayerAbilityWiring
    {
        private const string FireballPrefabPath = "Assets/CityX/Weapons/FireballProjectile.prefab";
        private const string FireballCastClipPath = "Assets/CityX/Weapons/Audio/fireball_cast.mp3";
        private const string IceShardPrefabPath = "Assets/CityX/Weapons/IceShard.prefab";
        private const string PoisonGasPrefabPath = "Assets/CityX/Weapons/PoisonGasCloud.prefab";
        private const string SwordWavePrefabPath = "Assets/CityX/Weapons/SwordWave.prefab";
        private const string AcidRainPrefabPath = "Assets/CityX/Weapons/AcidRainStorm.prefab";

        /// <summary>Puts every innate ability on <paramref name="player"/> and gives each its prefabs.</summary>
        public static void AddAbilities(GameObject player)
        {
            // --- the fireball (key 1). Its cast sound is the body's own non-positional sound, heard from inside it.
            var fireball = GetOrAdd<FireballCaster>(player);
            fireball.FireballPrefab = Load(FireballPrefabPath) ?? fireball.FireballPrefab;
            if (fireball.CastAudio == null)
            {
                var castAudio = player.AddComponent<AudioSource>();
                castAudio.playOnAwake = false;
                castAudio.spatialBlend = 0.0f;
                fireball.CastAudio = castAudio;
            }
            fireball.CastAudio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(FireballCastClipPath);

            // --- the ice shards (key 3).
            var iceShards = GetOrAdd<IceShardCaster>(player);
            iceShards.ShardPrefab = Load(IceShardPrefabPath) ?? iceShards.ShardPrefab;

            // --- the poison gas (key 4).
            var poisonGas = GetOrAdd<PoisonBreathCaster>(player);
            poisonGas.CloudPrefab = Load(PoisonGasPrefabPath) ?? poisonGas.CloudPrefab;

            // --- the sword's crescent wave (key 7, with the sword in hand).
            var swordWave = GetOrAdd<SwordWaveCaster>(player);
            swordWave.WavePrefab = Load(SwordWavePrefabPath) ?? swordWave.WavePrefab;

            // --- acid rain (key 5): the acid tree's god-tier storm, a test ability on a spare key for now.
            var acidRain = GetOrAdd<AcidRainCaster>(player);
            acidRain.StormPrefab = Load(AcidRainPrefabPath) ?? acidRain.StormPrefab;

            // Bloodrage (key 8) and the sword spin (key 2) are components of Player.prefab itself (PlayerRigBuilder), so every body has them.
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
