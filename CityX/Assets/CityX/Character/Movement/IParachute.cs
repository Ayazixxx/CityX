namespace CityX.Character
{
    /// <summary>
    /// What PlayerInputReader needs from an equipped parachute, without
    /// depending on the Weapons assembly -- same layering reason IJetpack
    /// exists: Character depends on nothing, Weapons depends on Character,
    /// never the other way round. Parachute (in CityX.Weapons) implements
    /// this.
    /// </summary>
    public interface IParachute
    {
        /// <summary>
        /// Called every frame while equipped, grounded or not -- matches
        /// IJetpack.Tick's own "called every frame, not just while active"
        /// shape, for the same reason: this is also where a parachute
        /// notices it has landed and needs to auto-close itself, which has
        /// to keep happening on every grounded frame, not just the one
        /// frame a toggle press happened to land on.
        /// <paramref name="toggledThisFrame"/> is true for exactly the one
        /// frame Space was freshly pressed while airborne with this
        /// equipped -- a toggle, not a hold, so this flips open to closed
        /// or back again rather than meaning "thrusting" the way
        /// IJetpack.Tick's own thrustHeld does.
        /// <paramref name="grounded"/> is PlayerLocomotion's own current
        /// grounded state, for the auto-close-on-landing behaviour.
        /// </summary>
        void Tick(bool toggledThisFrame, bool grounded, float deltaTime);
    }
}
