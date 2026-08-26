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
        /// True for as long as the thrust key is held.
        /// </summary>
        void Tick(bool thrustHeld, float deltaTime);
    }
}
