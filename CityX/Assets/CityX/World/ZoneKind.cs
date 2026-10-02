namespace CityX.World
{
    /// <summary>
    /// What a stretch of the city IS -- its land use and how dense it is.
    ///
    /// Declaration order is not meaningful, but never renumber: a zone is
    /// hashed and compared by name across CityLayout, CityBlocks and
    /// CityChunkGenerator.
    /// </summary>
    public enum ZoneKind
    {
        /// <summary>The downtown peak: tightest towers, the supertall landmarks, hardly any open ground.</summary>
        Core,

        /// <summary>Dense mixed commercial fabric around a core: towers and slabs among mid-rise.</summary>
        Urban,

        /// <summary>The everyday city: mid-rise blocks, a fair share of low-rise, some parks.</summary>
        Residential,

        /// <summary>A pocket of small blocks and low buildings, the historic quarter.</summary>
        OldTown,

        /// <summary>Wide, low, blank-faced sheds on big lots, with chimneys and water towers.</summary>
        Industrial,

        /// <summary>The thin edge between urban clusters: low-rise, big blocks, lots of green.</summary>
        Outskirts,
    }
}
