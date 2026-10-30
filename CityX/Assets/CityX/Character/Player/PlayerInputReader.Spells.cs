using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// THE SPELLS: the innate casters found on the body in Awake, and how a key aims and casts each of them.
    /// </summary>
    public sealed partial class PlayerInputReader
    {
        /// <summary>
        /// Found once via GetComponent in Awake, not set externally like
        /// EquippedGun/EquippedJetpack -- see IFireballCaster's own doc
        /// comment for why: the spell is an innate ability with no equip
        /// event to hang the wiring off, so this is discovered the same
        /// way PlayerVitals/MeleeAttacker already are.
        /// </summary>
        private IFireballCaster _fireballCaster;

        private IIceShardCaster _iceShards;

        private IArcaneOrbCaster _arcaneOrb;

        private IArcaneBarrierCaster _arcaneBarrier;

        private IPoisonBreathCaster _poisonBreath;

        private ISwordWaveCaster _swordWave;

        private IAcidRainCaster _acidRain;

        private SelfSmiteAbility _selfSmite;

        private BodySizeAbility _bodySize;
        private HealingAuraAbility _healingAura;
        private FireEnhancementAbility _fireEnhancement;

        private OctaslashAbility _octaslash;

        private StabDashAbility _stabDash;

        /// <summary>5: ACID RAIN, a green storm called down where the crosshair points (no further than its reach), burning everyone out in the open.</summary>
        private void CastAcidRain(Transform aimSource)
        {
            if (_acidRain == null || aimSource == null)
            {
                return;
            }
            _acidRain.TryCast(aimSource.position, aimSource.forward, GetComponentsInChildren<Collider>());
        }

        /// <summary>The raised-hand pose strength of whichever spell is being cast (fireball, ice shards, arcane orb or poison gas).</summary>
        private float SpellCastFraction =>
            Mathf.Max(
                Mathf.Max(_fireballCaster != null ? _fireballCaster.CastFraction : 0.0f, _iceShards != null ? _iceShards.CastFraction : 0.0f),
                Mathf.Max(Mathf.Max(_poisonBreath != null ? _poisonBreath.CastFraction : 0.0f, _bodySize != null ? _bodySize.CastFraction : 0.0f),
                    _arcaneOrb != null ? _arcaneOrb.CastFraction : 0.0f));

        private bool IsCastingSpell =>
            (_fireballCaster != null && _fireballCaster.IsCasting) || (_iceShards != null && _iceShards.IsCasting)
            || (_poisonBreath != null && _poisonBreath.IsCasting) || (_arcaneOrb != null && _arcaneOrb.IsCasting);

        /// <summary>Roughly chest height: where a spell leaves the body (MeleeAttacker.SwingOrigin and a thrown weapon use the same fraction).</summary>
        private Vector3 SpellChest => transform.position + Vector3.up * ((_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.68f);

        /// <summary>
        /// Where a spell from the bar leaves and which way it goes: from the chest down the crosshair; with a Magic Wand in hand (docs/MAGIC_WAND.md;
        /// owner, 2026-10-30: "when you use hotbar spells will be casted from the wand"), from its tip, the wand snapped out to point at what the
        /// crosshair is on, and straight at that.
        /// </summary>
        private void SpellLaunch(Transform aimSource, out Vector3 origin, out Vector3 direction)
        {
            Vector3 chest = SpellChest;
            if (_locomotion == null || _locomotion.Wand == null)
            {
                origin = chest;
                direction = ConvergedDirection(chest, aimSource);
                return;
            }
            Vector3 aimPoint = CrosshairPoint(chest, aimSource.position, aimSource.forward, transform);
            origin = _locomotion.PointWandNow(aimPoint);
            Vector3 toAim = aimPoint - origin;
            direction = toAim.sqrMagnitude > 0.0001f ? toAim.normalized : aimSource.forward;
        }

        /// <summary>While a spell is going out through the wand, the wand follows the crosshair.</summary>
        private void AimWand(Transform aimSource)
        {
            if (_locomotion == null || _locomotion.Wand == null || aimSource == null || !IsCastingSpell)
            {
                return;
            }
            _locomotion.WandAimPoint = CrosshairPoint(SpellChest, aimSource.position, aimSource.forward, transform);
        }

        /// <summary>Arcane Orb from the bar: down the crosshair, from wherever the camera is looking (<see cref="SpellLaunch"/>).</summary>
        private void CastArcaneOrb(Transform aimSource)
        {
            if (_arcaneOrb == null || aimSource == null)
            {
                return;
            }
            SpellLaunch(aimSource, out Vector3 origin, out Vector3 direction);
            _arcaneOrb.TryCast(origin, direction, GetComponentsInChildren<Collider>());
        }

        /// <summary>Arcane Barrier from the bar: the wall rises before the body, turned to where the camera looks (docs/ARCANE_BARRIER.md).</summary>
        private void RaiseArcaneBarrier(Transform aimSource)
        {
            if (_arcaneBarrier == null || aimSource == null)
            {
                return;
            }
            _arcaneBarrier.TryRaise(aimSource.forward);
        }

        /// <summary>While the scream winds up, the breath follows the crosshair: the point it is on in the world, handed in every frame.</summary>
        private void AimPoisonBreath(Transform aimSource)
        {
            if (_poisonBreath == null || aimSource == null || !_poisonBreath.IsCasting)
            {
                return;
            }
            _poisonBreath.Aim(aimSource.position + aimSource.forward * PlayerLocomotion.GunAimConvergenceDistance);
        }

        /// <summary>4: the character screams and breathes a cloud of poison gas out of their mouth at the crosshair (the fallback origin is the chest).</summary>
        private void CastPoisonBreath(Transform aimSource)
        {
            if (_poisonBreath == null || aimSource == null)
            {
                return;
            }
            float height = (_locomotion != null ? _locomotion.WorldHeight : 1.8f) * 0.68f;
            Vector3 origin = transform.position + Vector3.up * height;
            _poisonBreath.TryCast(origin, ConvergedDirection(origin, aimSource), GetComponentsInChildren<Collider>());
        }

        /// <summary>3: starts an ice shard volley down the crosshair, from wherever the camera is looking (<see cref="SpellLaunch"/>).</summary>
        private void CastIceShards(Transform aimSource)
        {
            if (_iceShards == null || aimSource == null)
            {
                return;
            }
            SpellLaunch(aimSource, out Vector3 origin, out Vector3 direction);
            _iceShards.TryCast(origin, direction, GetComponentsInChildren<Collider>());
        }

        /// <summary>While a volley is going out its remaining shards follow the crosshair.</summary>
        private void AimIceShards(Transform aimSource)
        {
            if (_iceShards == null || aimSource == null || !_iceShards.IsVolleying)
            {
                return;
            }
            SpellLaunch(aimSource, out Vector3 origin, out Vector3 direction);
            _iceShards.Aim(origin, direction);
        }

        private BloodrageAbility _bloodrageAbility;
    }
}
