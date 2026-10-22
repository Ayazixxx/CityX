namespace CityX.Core
{
    /// <summary>
    /// How a server judges a save's progress (docs/SERVER_SAVING.md, step 2): what changed since the last save it accepted must add up.
    /// The rules themselves (the XP curve, points per level, the skill tree) live with the game's characters, in the Character assembly,
    /// which installs them as <see cref="CharacterService.Rules"/>.
    /// </summary>
    public interface IProgressRules
    {
        /// <summary>
        /// Why <paramref name="reported"/> cannot follow <paramref name="stored"/> after <paramref name="secondsPlayed"/> seconds of real
        /// time, or null when it can.
        /// </summary>
        string Refusal(CharacterSave stored, CharacterSave reported, double secondsPlayed);
    }
}
