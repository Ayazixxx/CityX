using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// ZOOMING THE CREATOR'S CAMERA TOWARD WHAT THE MOUSE IS OVER (owner, 2026-11-05: "can you make it zoom into wherever you mouse is hovering? so
    /// you can zoom into the head or feet or anywhere you want to focus"). A camera moved along the line from itself through a point keeps that
    /// point at the same place on the screen, so that is how it moves. The further in it is, the further off the middle it may be, and at its
    /// starting distance or beyond it is back in the middle: zooming out always brings the whole body back. All in the camera's parent's space,
    /// the body standing about z = 0 and the camera at +z or -z.
    /// </summary>
    public static class CreatorZoom
    {
        /// <summary>One notch of the wheel: this share of the way to what the mouse is over.</summary>
        public const float StepShare = 0.15f;

        /// <summary>It stops this far short of what the mouse is over, wherever that is (an arm can point at the camera).</summary>
        public const float NearestToFocusMetres = 0.45f;

        /// <summary>How far off the middle it may be when fully in: enough for the feet (it starts 1.6 m above them) and the top of the head.</summary>
        public const float ReachMetres = 1.6f;

        /// <summary>The camera one notch toward <paramref name="focus"/> (or away from it), held inside the limits without leaving the line through it.</summary>
        public static Vector3 Step(Vector3 camera, Vector3 focus, bool closer, Vector3 home, float nearestDepth, float farthestDepth)
        {
            Vector3 away = camera - focus;
            float distance = away.magnitude;
            if (distance < 1e-4f)
            {
                return camera;
            }

            float share = closer
                ? Mathf.Min(1.0f, Mathf.Max(1.0f - StepShare, NearestToFocusMetres / distance))
                : 1.0f / (1.0f - StepShare);

            // The depth limits, as limits on how far along the line it goes, so reaching them stops it rather than sliding it off the line.
            float side = camera.z < 0.0f ? -1.0f : 1.0f;
            float awayDepth = away.z * side, focusDepth = focus.z * side;
            if (awayDepth > 1e-4f)
            {
                float nearestShare = (nearestDepth - focusDepth) / awayDepth;
                float farthestShare = (farthestDepth - focusDepth) / awayDepth;
                share = closer ? Mathf.Min(1.0f, Mathf.Max(share, nearestShare)) : Mathf.Max(1.0f, Mathf.Min(share, farthestShare));
            }
            return Kept(focus + away * share, home, nearestDepth, farthestDepth);
        }

        /// <summary>The camera held inside its depth limits, and only as far off the middle as its depth allows.</summary>
        public static Vector3 Kept(Vector3 camera, Vector3 home, float nearestDepth, float farthestDepth)
        {
            float side = camera.z < 0.0f ? -1.0f : 1.0f;
            float depth = Mathf.Clamp(Mathf.Abs(camera.z), nearestDepth, farthestDepth);
            float reach = ReachMetres * Mathf.InverseLerp(Mathf.Abs(home.z), nearestDepth, depth);
            return new Vector3(
                Mathf.Clamp(camera.x, home.x - reach, home.x + reach),
                Mathf.Clamp(camera.y, home.y - reach, home.y + reach),
                side * depth);
        }
    }
}
