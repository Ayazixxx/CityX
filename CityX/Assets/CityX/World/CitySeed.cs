namespace CityX.World
{
    /// <summary>
    /// The seed of the game's own city: the one number the scenes, the editor tools and the tests all build from. A change to how the city
    /// is generated rerolls it here (CLAUDE.md), and the scenes are rebuilt to take it.
    /// </summary>
    public static class CitySeed
    {
        public const int Default = 48398181;
    }
}
