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
        /// be facing. <paramref name="boostHeld"/> is the sprint key, held
        /// while thrusting -- an explicit new-mechanic ask ("shift is a
        /// boost when midair using jetpack"): amplifies the WASD lean while
        /// a direction is held, or climbs faster when none is, never both
        /// at once (see Jetpack.Tick's own doc comment). PlayerInputReader
        /// does not also treat this as an ordinary sprint while thrusting --
        /// see its own wantsSprint comment for why that would double up on
        /// one key with two unrelated meanings and drain stamina for a
        /// mechanic the explicit ask says should not cost any.
        /// </summary>
        void Tick(bool thrustHeld, Vector2 moveInput, bool boostHeld, float deltaTime);
    }
}
