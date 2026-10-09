namespace CityX.World
{
    /// <summary>
    /// The one piece of WindowGrid the plan code needs (BuildingSite.Contacts names a wall). The real WindowGrid uses UnityEngine, which this
    /// tool does not link; the enum values must match CityX/Assets/CityX/World/WindowGrid.cs.
    /// </summary>
    public static class WindowGrid
    {
        public enum Wall
        {
            West = 0,
            East = 1,
            South = 2,
            North = 3,
        }
    }
}
