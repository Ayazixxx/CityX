using CityX.Character;

namespace CityX.Weapons
{
    /// <summary>
    /// The Giant Boomerang's three clips (docs/BOOMERANG.md; made in Blender, CityX/SourceArt/GiantBoomerang/giant_boomerang_pose.py): their names, and the
    /// moments in them the game acts on. Keep these in step with the script's MOVES (each clip's seconds, and the share of it its grab and its release keys
    /// sit at).
    /// </summary>
    public static class GiantBoomerangMoves
    {
        public const string MotionPath = "GiantBoomerang/GiantBoomerangMotion";

        /// <summary>From the back to the aim: its last frame is the aim, held for as long as right-click is. Played backward, it is put away again.</summary>
        public const string Draw = "giant_draw";

        public const string ThrowAimed = "giant_throw_aimed";

        public const string ThrowQuick = "giant_throw_quick";

        /// <summary>The draw: the hand closes on the grip on the back this far in, seconds.</summary>
        public const float DrawGrabSeconds = 0.42f * 0.45f;

        /// <summary>The aimed throw lets it go this far in.</summary>
        public const float AimedReleaseSeconds = 0.30f * 0.60f;

        /// <summary>The quick throw takes it off the back, and lets it go, this far in.</summary>
        public const float QuickGrabSeconds = 0.22f * 0.75f;

        public const float QuickReleaseSeconds = 0.55f * 0.75f;

        public static BakedMotion Motion => BakedMotion.Load(MotionPath);
    }
}
