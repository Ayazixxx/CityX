using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What PlayerInputReader needs to cast the fireball spell, without
    /// depending on the Weapons assembly -- same layering reason
    /// IJetpack/IEquippableGun/IGrappleLauncher exist: Character depends on
    /// nothing, Weapons depends on Character, never the other way round.
    /// FireballCaster (in CityX.Weapons) implements this.
    ///
    /// AN INNATE ABILITY, NOT AN EQUIPPED ONE -- unlike EquippedJetpack/
    /// EquippedGun, which are set by a weapon's own OnEquip the moment it's
    /// picked up, PlayerInputReader finds its FireballCaster once in Awake
    /// via GetComponent&lt;IFireballCaster&gt;(), because the spell is
    /// always available and there is no equip event to hang the wiring off.
    /// </summary>
    public interface IFireballCaster
    {
        bool CanCast { get; }

        /// <summary>
        /// True for a short cosmetic window after a successful cast -- folded
        /// into PlayerInputReader's own jetpack-suppression check the same
        /// way gun-aiming already is, so a cast interrupts jetpack thrust the
        /// same way aiming does. The actual visual (a raised right hand) is
        /// driven by <see cref="CastFraction"/> instead, not this bool.
        /// </summary>
        bool IsCasting { get; }

        /// <summary>
        /// 1 immediately after a cast, easing to 0 as the same cosmetic
        /// window <see cref="IsCasting"/> above tracks runs out -- drives
        /// PlayerLocomotion.SetCastPose, which layers a raised-arm pose
        /// scaled by this fraction on top of whatever the locomotion
        /// animator is already doing, rather than replacing it with a whole
        /// separate Animator state.
        /// </summary>
        float CastFraction { get; }

        void Tick(float deltaTime);

        /// <summary>
        /// Origin/direction are handed in rather than read from a camera
        /// here -- same split PistolWeapon.Fire and GrappleSwing.TryFire
        /// already use, so this stays drivable by a test without a real
        /// Camera.main. Returns false (and does nothing) if the cooldown
        /// has not elapsed or the caster is dead.
        /// </summary>
        bool TryCast(Vector3 origin, Vector3 direction, Collider[] ignoreColliders);
    }
}
