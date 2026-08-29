using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What PlayerInputReader needs from an equipped flamethrower, without
    /// depending on the Weapons assembly -- same layering reason
    /// IEquippableGun/IJetpack exist: Character depends on nothing, Weapons
    /// depends on Character, never the other way round. Flamethrower (in
    /// CityX.Weapons) implements this.
    ///
    /// NOT IEquippableGun -- a flamethrower has no magazine, no reload, and
    /// fires continuously for as long as the trigger is held rather than
    /// once per click, so PistolWeapon's own single Fire() call per press
    /// does not fit. Closer in shape to IJetpack's own "Tick every frame
    /// with the held-key state" contract than to a gun's.
    /// </summary>
    public interface IEquippableFlamethrower
    {
        /// <summary>
        /// Called every frame while equipped. <paramref name="triggerHeld"/>
        /// is true for as long as left-click is held -- the stream starts
        /// and stops with it, the same way Jetpack's own thrust does with
        /// Space. <paramref name="origin"/>/<paramref name="direction"/> are
        /// the camera's own current position/facing, same convention
        /// PistolWeapon.Fire already uses ("the camera is genuinely where
        /// the player is aiming"). <paramref name="wielder"/> is handed in
        /// for its Strength stat and so the stream never damages its own
        /// holder.
        /// </summary>
        void Tick(bool triggerHeld, Vector3 origin, Vector3 direction, GameObject wielder, float deltaTime);
    }
}
