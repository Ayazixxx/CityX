namespace CityX.World
{
    /// <summary>
    /// The world the currently-running game is actually built from, if any.
    ///
    /// A PLAIN STATIC HOLDER, THE SAME SHAPE AS CityX.Core.PlaySession, and
    /// for the same reason: something has to bridge two assemblies that
    /// cannot reference each other directly. CityStreamer (CityX.View, the
    /// one thing that actually owns building the real world) sets this once
    /// it exists; CityX.Character -- which deliberately has no reference to
    /// CityX.View, see that assembly's own .asmdef -- reads it when it
    /// needs to ask "what does the real ground look like right now" without
    /// waiting on whatever chunk mesh has or has not streamed in yet.
    /// </summary>
    public static class CurrentWorld
    {
        public static CityWorld Instance;
    }
}
