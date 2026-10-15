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
        private const string SlashMaterialPath = "Assets/CityX/Character/SwordSpinTrail.mat";
        private const string ThunderStrikeClipPath = "Assets/CityX/UI/Audio/thunder_strike.ogg";
        private const string SelfSmiteLightMaterialPath = "Assets/CityX/Character/SelfSmiteLight.mat";

        /// <summary>How deep behind it a glow fades out where it meets something, metres.</summary>
        private const float SelfSmiteSoftFadeMetres = 1.2f;

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

            // --- Octaslash (key 6, sword in hand): Sephiroth's held up special.
            var octaslash = GetOrAdd<OctaslashAbility>(player);
            octaslash.SlashMaterial = AssetDatabase.LoadAssetAtPath<Material>(SlashMaterialPath) ?? octaslash.SlashMaterial;

            // --- Self Smite (Electromancer, key 9 on the default bar): Pikachu's Thunder. Its bolt is drawn in code; only the thunder is an asset.
            var selfSmite = GetOrAdd<SelfSmiteAbility>(player);
            selfSmite.StrikeSound = AssetDatabase.LoadAssetAtPath<AudioClip>(ThunderStrikeClipPath) ?? selfSmite.StrikeSound;
            selfSmite.LightMaterial = EnsureSelfSmiteLightMaterial() ?? selfSmite.LightMaterial;

            // --- Grow and Shrink (Wizard; Grow on key 0 of the default bar): drawn and voiced in code, nothing to load.
            GetOrAdd<BodySizeAbility>(player);

            // Bloodrage (key 8) and the sword spin (key 2) are components of Player.prefab itself (PlayerRigBuilder), so every body has them.
        }

        /// <summary>
        /// Self Smite's light: URP's unlit particle shader, additive, with SOFT PARTICLES on, so the burst's glow fades where it meets the street
        /// instead of being cut off in a hard line along it (rendered, 2026-10-15). An asset rather than made at runtime so a build keeps that
        /// shader variant. Holds no texture: SelfSmiteBolt gives each copy its colour and its soft spot.
        /// </summary>
        private static Material EnsureSelfSmiteLightMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SelfSmiteLightMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    Debug.LogWarning("CityX: no URP particle shader -- Self Smite's glow will be cut where it meets the street.");
                    return null;
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, SelfSmiteLightMaterialPath);
            }
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 2.0f);   // additive
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetFloat("_SoftParticlesEnabled", 1.0f);
            material.SetFloat("_SoftParticlesNearFadeDistance", 0.0f);
            material.SetFloat("_SoftParticlesFarFadeDistance", SelfSmiteSoftFadeMetres);
            material.SetVector("_SoftParticleFadeParams", new Vector4(0.0f, 1.0f / SelfSmiteSoftFadeMetres, 0.0f, 0.0f));
            material.EnableKeyword("_SOFTPARTICLES_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
