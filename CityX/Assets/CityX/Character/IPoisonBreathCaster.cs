using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The poison breath ability (key 4): the character SCREAMS and a cloud of purple gas comes out of their mouth. The same split IFireballCaster
    /// makes: PlayerInputReader (Character) asks, the implementation in CityX.Weapons owns the cloud prefab. An innate ability: found once with
    /// GetComponent, nothing to equip.
    /// </summary>
    public interface IPoisonBreathCaster
    {
        bool CanCast { get; }

        /// <summary>True while the scream plays (folded into the same "casting" checks the fireball uses, so it interrupts jetpack thrust).</summary>
        bool IsCasting { get; }

        /// <summary>Always 0: the scream is an animation of its own, so there is no raised-arm cast pose to drive (PlayerLocomotion.SetCastPose).</summary>
        float CastFraction { get; }

        void Tick(float deltaTime);

        /// <summary>
        /// Starts the scream. The breath leaves the MOUTH a moment into it, when the head snaps forward; <paramref name="origin"/> and
        /// <paramref name="direction"/> are only the fallback (a body with no animator breathes from where it stands). False on cooldown or when dead.
        /// </summary>
        bool TryCast(Vector3 origin, Vector3 direction, Collider[] ignoreColliders);

        /// <summary>
        /// While the scream winds up, the point the crosshair is on in the world: the breath is aimed from the mouth at that point the moment it
        /// leaves, not at the direction the crosshair had at the press.
        /// </summary>
        void Aim(Vector3 aimPoint);
    }
}
