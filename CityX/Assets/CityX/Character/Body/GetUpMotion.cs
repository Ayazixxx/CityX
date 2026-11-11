using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The get-up clips, authored in Blender for this body (SourceArt/GetUp; owner, 2026-11-10: "the limbs bend all awkwardly clearly in an
    /// unintented way", "i think it needs a better standing up animation"). One from lying on the back (the head up, a knee bent, rolled over
    /// the right side onto the hands and knees), one from lying face down (a push-up onto the hands and knees); from there both step the right
    /// foot forward, push off that knee and stand as the idle stands. A body is got up by the one nearer how it lies.
    /// </summary>
    public static class GetUpMotion
    {
        public const string Resource = "GetUp/GetUpMotion";

        public const string FromTheBack = "getup_back";

        public const string FromTheFront = "getup_front";

        /// <summary>Clip seconds before the end at which both clips are kneeling up off the hands, and standing.</summary>
        public const float KneelingBeforeTheEnd = 1.1f;

        public const float StandingBeforeTheEnd = 0.35f;

        public static BakedMotion Motion => BakedMotion.Load(Resource);

        /// <summary>The clip for a body whose chest faces <paramref name="chestFront"/>: from its back if it faces up at all.</summary>
        public static string For(Vector3 chestFront) => chestFront.y > 0.0f ? FromTheBack : FromTheFront;

        /// <summary>How long a clip runs, to its last frame, seconds at its own speed.</summary>
        public static float Seconds(string segment)
        {
            BakedMotion motion = Motion;
            return motion != null && motion.HasSegment(segment) ? (motion.SegmentFrames(segment) - 1) / (float)motion.FramesPerSecond : 0.0f;
        }

        /// <summary>The longer of the two clips, seconds at its own speed.</summary>
        public static float LongestSeconds => Mathf.Max(Seconds(FromTheBack), Seconds(FromTheFront));
    }
}
