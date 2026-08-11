namespace CityX.Core
{
    /// <summary>
    /// Which character is being played, across the scene load.
    ///
    /// STATIC, WHICH IS THE POINT AND NOT LAZINESS. The menu and the world are
    /// separate scenes, so nothing in one can hold a reference into the other;
    /// something has to outlive the load. The alternatives are a
    /// DontDestroyOnLoad object, which is a GameObject that has to be found by
    /// name and can end up duplicated, or writing the choice to disk and
    /// reading it back, which makes "who is playing" a file format. One static
    /// field is the smallest thing that does the job.
    ///
    /// Deliberately holds the WHOLE save rather than an id: the world needs the
    /// name, the colour and the earned stats immediately, and looking them up
    /// again would mean the world scene knowing about the store.
    /// </summary>
    public static class PlaySession
    {
        /// <summary>
        /// The chosen character, or null when the world is opened directly --
        /// which happens constantly, because pressing play in the editor on the
        /// city scene skips the menu entirely. Everything that reads this must
        /// cope with null and fall back to a default body rather than refusing
        /// to start.
        /// </summary>
        public static CharacterSave Character;

        /// <summary>
        /// True when this character has never been played before, so the world
        /// can give them an entrance. Cleared once used.
        /// </summary>
        public static bool IsNewCharacter;

        public static void Begin(CharacterSave character, bool isNew)
        {
            Character = character;
            IsNewCharacter = isNew;
        }

        public static void Clear()
        {
            Character = null;
            IsNewCharacter = false;
        }
    }
}
