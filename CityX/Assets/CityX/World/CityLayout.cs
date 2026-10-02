namespace CityX.World
{
    /// <summary>
    /// What is left of the old square-grid layout: the size of a streaming chunk.
    ///
    /// The city's real layout is now the plan (World/Plan): roads, blocks and lots at any angle. A chunk is only the unit the
    /// streamer builds, the map draws and the weather is tracked in.
    /// </summary>
    public static class CityLayout
    {
        /// <summary>Side of a streaming chunk, metres.</summary>
        public const float ChunkSize = 24.0f;
    }
}
