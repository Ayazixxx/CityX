namespace CityX.Wearables
{
    /// <summary>How much of a cape's cloth is worked out this frame.</summary>
    public enum CapeDetailLevel
    {
        /// <summary>Every step, the links put right <see cref="CapeCloth.Iterations"/> times: the cape close up.</summary>
        Full,

        /// <summary>Every step, the links put right only <see cref="CapeDetail.LightIterations"/> times: the tethers still keep it from stretching.</summary>
        Light,

        /// <summary>No cloth at all: the cape hangs at rest and goes where the body goes, drawn once.</summary>
        Still,
    }

    /// <summary>
    /// A cape's level of detail (owner, 2026-11-25: "the cape physics look really good but would that be a risk for performance in an mmo. it
    /// doesnt need to be THAT good"): the full cloth close to the camera, a lighter one further off, none past that or off the screen, so a
    /// crowd of capes costs about what the few near ones do.
    /// </summary>
    public static class CapeDetail
    {
        /// <summary>Within this of the camera, metres, the cloth runs in full.</summary>
        public const float FullWithin = 20.0f;

        /// <summary>Within this, the lighter cloth; past it the cape hangs still.</summary>
        public const float LightWithin = 45.0f;

        public const int LightIterations = 3;

        public static CapeDetailLevel For(float metresFromCamera, bool seen)
        {
            if (!seen || metresFromCamera > LightWithin)
            {
                return CapeDetailLevel.Still;
            }
            return metresFromCamera > FullWithin ? CapeDetailLevel.Light : CapeDetailLevel.Full;
        }

        /// <summary>How many times the links are put right a step at this level (none when still).</summary>
        public static int IterationsAt(CapeDetailLevel level) =>
            level == CapeDetailLevel.Full ? CapeCloth.Iterations : level == CapeDetailLevel.Light ? LightIterations : 0;
    }
}
