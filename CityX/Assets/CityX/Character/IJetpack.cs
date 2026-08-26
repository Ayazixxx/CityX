using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What PlayerInputReader needs from an equipped jetpack, without
    /// depending on the Weapons assembly -- same layering reason
    /// IEquippableGun/IGrappleLauncher exist: Character depends on nothing,
    /// Weapons depends on Character, never the other way round. Jetpack (in
    /// CityX.Weapons) implements this.
    /// </summary>
    public interface IJetpack
    {
        /// <summary>
        /// Called every frame while equipped, in place of the ordinary
        /// charged-jump handling -- see PlayerInputReader's own dispatch.
        /// <paramref name="thrustHeld"/> is true for as long as the thrust
        /// key is held. <paramref name="moveInput"/> is the same raw WASD
        /// reading (x = strafe, y = forward/back, BEFORE the camera yaw is
        /// folded in) LocomotionInput.Move gets built from this same
        /// frame -- passed through so the jetpack can lean the body toward
        /// whatever direction WASD is actually asking for, in the body's
        /// own local space, independent of which way the camera happens to
        /// be facing.
        /// </summary>
        void Tick(bool thrustHeld, Vector2 moveInput, float deltaTime);
    }
}
