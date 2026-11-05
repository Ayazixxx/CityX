namespace CityX.World
{
    /// <summary>
    /// What players have changed in the world that is kept in memory for as long as the program runs: room lights switched, power
    /// restored, balcony doors opened, Old Guy's house (its fuse box, its broken planks and panes). It belongs to one world, not to the
    /// program: left alone, a character started after quitting another found the house already lit and broken into, and a player who
    /// had played alone first brought their open doors and lights into a friend's session, where nobody else saw them.
    /// </summary>
    public static class WorldMemory
    {
        /// <summary>A new world begins (the title screen comes up: before a first game, after quitting one, before joining someone else's).</summary>
        public static void Forget()
        {
            RoomLights.Clear();
            PowerGrid.Reset();
            BalconyDoorState.Shared.Clear();
            TutorialHouseDamage.Shared.Clear();
            TutorialHousePower.Shared.Reset();
        }
    }
}
