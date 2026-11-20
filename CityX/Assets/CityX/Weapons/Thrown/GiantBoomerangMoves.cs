using CityX.Character;

namespace CityX.Weapons
{
    /// <summary>
    /// One giant boomerang's four clips: their names, and the moments in them the game acts on. Keep these in step with the script that made them (its
    /// MOVES: each clip's seconds, and the share of it its grab and its release keys sit at).
    /// </summary>
    public sealed class GiantBoomerangClips
    {
        public readonly string MotionPath;

        /// <summary>From the back to the aim: its last frame is the aim, held for as long as right-click is. Played backward, it is put away again.</summary>
        public readonly string Draw;

        public readonly string ThrowAimed;

        public readonly string ThrowQuick;

        /// <summary>Out for it as it comes home, caught, and on round into the aim (its last frame, as the draw's).</summary>
        public readonly string Catch;

        /// <summary>The draw: the hand closes on it on the back this far in, seconds.</summary>
        public readonly float DrawGrabSeconds;

        /// <summary>The aimed throw lets it go this far in.</summary>
        public readonly float AimedReleaseSeconds;

        /// <summary>The quick throw takes it off the back, and lets it go, this far in.</summary>
        public readonly float QuickGrabSeconds;

        public readonly float QuickReleaseSeconds;

        public GiantBoomerangClips(string motionPath, string draw, string throwAimed, string throwQuick, string @catch, float drawGrabSeconds,
            float aimedReleaseSeconds, float quickGrabSeconds, float quickReleaseSeconds)
        {
            MotionPath = motionPath;
            Draw = draw;
            ThrowAimed = throwAimed;
            ThrowQuick = throwQuick;
            Catch = @catch;
            DrawGrabSeconds = drawGrabSeconds;
            AimedReleaseSeconds = aimedReleaseSeconds;
            QuickGrabSeconds = quickGrabSeconds;
            QuickReleaseSeconds = quickReleaseSeconds;
        }

        public BakedMotion Motion => BakedMotion.Load(MotionPath);
    }

    /// <summary>
    /// The giant boomerangs' clips (docs/BOOMERANG.md), made in Blender: the bone one's (CityX/SourceArt/GiantBoomerang/giant_boomerang_pose.py; remade 2026-11-20
    /// from the owner's clip of a fighting game's thrower: over the shoulder for it, a lunge with it trailing low behind, whipped round and up), and the
    /// Iron Spiked one's own (CityX/SourceArt/IronSpikedBoomerang/iron_spiked_moves.py; owner, 2026-11-20: taken by its rope from above, held out wide
    /// behind as the anime's great shuriken is, whipped round flat).
    /// </summary>
    public static class GiantBoomerangMoves
    {
        /// <summary>
        /// The catch, either one's: the hand is out for it this far in, seconds. The move is begun this long before it gets home (ThrownBoomerang.ComingIn),
        /// so the hand is there as it comes in.
        /// </summary>
        public const float CatchSeconds = 0.30f * 0.45f;

        public static readonly GiantBoomerangClips Bone = new GiantBoomerangClips("GiantBoomerang/GiantBoomerangMotion",
            "giant_draw", "giant_throw_aimed", "giant_throw_quick", "giant_catch",
            drawGrabSeconds: 0.35f * 0.52f, aimedReleaseSeconds: 0.28f * 0.65f, quickGrabSeconds: 0.16f * 0.80f, quickReleaseSeconds: 0.56f * 0.80f);

        public static readonly GiantBoomerangClips IronSpiked = new GiantBoomerangClips("GiantBoomerang/IronSpikedBoomerangMotion",
            "iron_draw", "iron_throw_aimed", "iron_throw_quick", "iron_catch",
            drawGrabSeconds: 0.40f * 0.50f, aimedReleaseSeconds: 0.36f * 0.70f, quickGrabSeconds: 0.22f * 0.85f, quickReleaseSeconds: 0.60f * 0.85f);

        public static GiantBoomerangClips For(GiantBoomerangModel model) => model == GiantBoomerangModel.IronSpiked ? IronSpiked : Bone;
    }
}
