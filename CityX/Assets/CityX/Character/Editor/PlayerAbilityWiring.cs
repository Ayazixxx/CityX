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
        private const string StaffFlamePrefabPath = "Assets/CityX/Weapons/StaffFlame.prefab";
        private const string ArrowPrefabPath = "Assets/CityX/Weapons/Arrow.prefab";
        private const string PebblePrefabPath = "Assets/CityX/Weapons/Pebble.prefab";
        private const string ThrownGrenadePrefabPath = "Assets/CityX/Weapons/ThrownGrenade.prefab";
        private const string ThrownSemtexPrefabPath = "Assets/CityX/Weapons/ThrownSemtex.prefab";
        private const string ThrownBoomerangPrefabPath = "Assets/CityX/Weapons/ThrownBoomerang.prefab";
        private const string ThrownGiantBoomerangPrefabPath = "Assets/CityX/Weapons/ThrownGiantBoomerang.prefab";
        private const string WraithPrefabPath = "Assets/CityX/Character/Wraith.prefab";
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
            fireball.LightMaterial = EnsureSelfSmiteLightMaterial() ?? fireball.LightMaterial;   // the Fireball Staff's charge glow

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
            octaslash.SlashMaterial = SwordSlashMaterialBuilder.Ensure();

            // --- the stab dash (Sword tree, sword in hand): the Mii Swordfighter's Power Thrust. Its look is its Blender animation, in Resources.
            GetOrAdd<StabDashAbility>(player);

            // --- ladders (docs/SEWERS.md): anyone climbs one, and every copy of the body shows it on the same rungs.
            GetOrAdd<PlayerLadder>(player);

            // --- Self Smite (Electromancer, key 9 on the default bar): Pikachu's Thunder. Its bolt is drawn in code; only the thunder is an asset.
            var selfSmite = GetOrAdd<SelfSmiteAbility>(player);
            selfSmite.StrikeSound = AssetDatabase.LoadAssetAtPath<AudioClip>(ThunderStrikeClipPath) ?? selfSmite.StrikeSound;
            selfSmite.LightMaterial = EnsureSelfSmiteLightMaterial() ?? selfSmite.LightMaterial;

            // --- Arcane Orb (Arcanist) and the Arcane Staff's shots: drawn and voiced in code, its glows from Self Smite's light.
            var arcaneOrb = GetOrAdd<ArcaneOrbCaster>(player);
            arcaneOrb.LightMaterial = EnsureSelfSmiteLightMaterial() ?? arcaneOrb.LightMaterial;

            // --- Arcane Barrier (Arcanist, 2026-10-28): the wall of aura, drawn and voiced in code, its glows from the same light.
            var arcaneBarrier = GetOrAdd<ArcaneBarrierCaster>(player);
            arcaneBarrier.LightMaterial = EnsureSelfSmiteLightMaterial() ?? arcaneBarrier.LightMaterial;

            // --- the Water Staff's jet (2026-10-28): drawn and voiced in code, nothing to load.
            GetOrAdd<WaterJetCaster>(player);

            // --- the Flame Staff's stream (2026-10-28): the flamethrower's fire, on a prefab of its own at the staff's head.
            var flameStream = GetOrAdd<FlameStreamCaster>(player);
            flameStream.FlamePrefab = Load(StaffFlamePrefabPath) ?? flameStream.FlamePrefab;

            // --- the Arcane Beam Staff's beam and the Staff of Healing's (2026-10-29, docs/STAFFS.md, docs/HEALING.md): drawn in code.
            GetOrAdd<ArcaneBeamCaster>(player);
            GetOrAdd<HealingBeamCaster>(player);

            // --- the Healing Aura (the Cleric, 2026-10-29, docs/HEALING.md): a green circle round the caster, drawn in code.
            GetOrAdd<HealingAuraAbility>(player);
            // --- Glacier Spikes (the Cryomancer, 2026-11-12, docs/GLACIER_SPIKES.md): crystals drawn in code, the ice's material in Resources.
            var glacierSpikes = GetOrAdd<GlacierSpikesAbility>(player);
            glacierSpikes.LightMaterial = EnsureSelfSmiteLightMaterial() ?? glacierSpikes.LightMaterial;   // its mist fades where it meets the street
            // --- the Fire Enhancement, the flashlight's word to other players, and the Frost Staff's spray (2026-10-29): drawn in code.
            GetOrAdd<FireEnhancementAbility>(player);
            GetOrAdd<FlashlightSync>(player);
            GetOrAdd<FrostSprayCaster>(player);

            // --- the Magic Wand's and the Magic Staff's speck of dust (2026-10-30, docs/MAGIC_WAND.md): drawn in code, its glows from the same light.
            var magicSpeck = GetOrAdd<MagicSpeckCaster>(player);
            magicSpeck.LightMaterial = EnsureSelfSmiteLightMaterial() ?? magicSpeck.LightMaterial;

            // --- the Wraith Staff's summon (2026-11-08, docs/WRAITH_STAFF.md): the Wraith, made here when there is no session to ask.
            var summon = GetOrAdd<WraithSummonCaster>(player);
            summon.WraithPrefab = Load(WraithPrefabPath) ?? summon.WraithPrefab;

            // --- the bow's draw and arrows as other players see them (2026-10-28, docs/ARCHERY.md): a copy looses the bow's own arrow.
            var archery = GetOrAdd<Archery>(player);
            archery.ArrowPrefab = Load(ArrowPrefabPath) ?? archery.ArrowPrefab;
            archery.PebblePrefab = Load(PebblePrefabPath) ?? archery.PebblePrefab;   // a copy's slingshot flings one

            // --- grenades as other players see them (2026-10-29, docs/GRENADE.md): a copy throws the same grenade, and it goes off where it did.
            var grenades = GetOrAdd<GrenadeThrower>(player);
            grenades.ThrownPrefab = Load(ThrownGrenadePrefabPath) ?? grenades.ThrownPrefab;
            grenades.StickyPrefab = Load(ThrownSemtexPrefabPath) ?? grenades.StickyPrefab;

            // --- the boomerang (2026-11-05, docs/BOOMERANG.md): whether it is out, and a copy of it flying on other players' screens.
            var boomerang = GetOrAdd<BoomerangThrower>(player);
            boomerang.ThrownPrefab = Load(ThrownBoomerangPrefabPath) ?? boomerang.ThrownPrefab;
            boomerang.GiantThrownPrefab = Load(ThrownGiantBoomerangPrefabPath) ?? boomerang.GiantThrownPrefab;

            // --- Grow and Shrink (Wizard; Grow on key 0 of the default bar): drawn and voiced in code, nothing to load.
            GetOrAdd<BodySizeAbility>(player);

            // --- the fire as other players see it (2026-10-28, docs/FIRE.md): pours, lightings and dousings, nothing to load.
            GetOrAdd<FireSync>(player);

            // --- the grapple swing (2026-10-29, docs/GRAPPLE_RAGDOLL.md): on every body, so a joining player can swing and its rope is drawn on
            // every other player's copy. Was only on the host's baked body.
            GetOrAdd<GrappleSwing>(player);

            // --- a bullet the guard turns aside flies off (the Block Chance roll or a parry): drawn in code, nothing to load.
            GetOrAdd<BulletRicochet>(player);

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
