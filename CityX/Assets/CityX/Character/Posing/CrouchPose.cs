using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The crouch's shape (docs/CROUCH.md), what CrouchBaker bakes into the crouch clips from the game's own walk and idle: the hips down and a
    /// little back, the whole upper body leaned forward with them (this body's torso is one piece on the hips), the head turned back up to
    /// look ahead, and the legs folded under it with the feet where the walk puts them -- each clip's steps fitted to the crouching speed,
    /// lifted a little less, and standing still a little wider apart. The knees go forward and a little out.
    ///
    /// Source's crouch is a lowered walk with the knees deep and the back bent over them; this body's legs are a third of its height (the hips
    /// at 0.9 m of 2.6), so the drop is the legs' and most of the height comes off with the lean.
    /// </summary>
    public static class CrouchPose
    {
        /// <summary>How far the hips come down, metres (the body at its built height).</summary>
        public const float HipsDrop = 0.38f;

        /// <summary>How far the hips sit back behind the feet, metres: the lean's weight over the feet.</summary>
        public const float HipsBack = 0.12f;

        /// <summary>How far forward the hips pitch the whole upper body, degrees.</summary>
        public const float LeanDegrees = 24.0f;

        /// <summary>How much of the lean the neck turns back, so the eyes look ahead rather than at the ground.</summary>
        public const float HeadLevelShare = 0.85f;

        /// <summary>How high a stepping foot lifts against the walk's own.</summary>
        public const float LiftShare = 0.8f;

        /// <summary>Standing still crouched, each foot this much further out than the idle's, metres.</summary>
        public const float StanceWiden = 0.06f;

        /// <summary>How far outward the knees point, as a share of forward.</summary>
        public const float KneeOut = 0.12f;

        /// <summary>
        /// Where a foot goes in the crouch: the walk's foot <paramref name="foot"/> drawn toward (or out from) its own middle <paramref name="centre"/>
        /// along the ground by <paramref name="stride"/> (the baker fits each clip's steps to the crouching speed), its lift over <paramref name="ground"/>
        /// by <see cref="LiftShare"/>; standing still, out to the side (<paramref name="side"/>: -1 the body's -x leg, +1 the +x one) by
        /// <see cref="StanceWiden"/>.
        /// </summary>
        public static Vector3 FootTarget(Vector3 foot, Vector3 centre, float ground, bool standingStill, float side, float stride)
        {
            Vector3 along = foot - centre;
            float x = centre.x + along.x * stride + (standingStill ? side * StanceWiden : 0.0f);
            float z = centre.z + along.z * stride;
            float y = ground + Mathf.Max(0.0f, foot.y - ground) * LiftShare;
            return new Vector3(x, y, z);
        }

        /// <summary>The way a knee is bent toward: forward, and out on its own side.</summary>
        public static Vector3 KneeHint(float side) => new Vector3(side * KneeOut, 0.0f, 1.0f);
    }
}
