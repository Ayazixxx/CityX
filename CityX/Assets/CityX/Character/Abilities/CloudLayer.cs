using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHERE THE CLOUDS ARE (owner, 2026-10-15: "this game does go infinitely upward so make it appear around cloud height lets just say around
    /// the height lightning strikes appear in real life"). A thundercloud's base is about one and a half kilometres up, so storm spells (Self
    /// Smite's bolt, Acid Rain's clouds) come from <see cref="HeightMetres"/> above the ground under them. Somebody flying up near that height
    /// or past it still gets their cloud overhead, <see cref="HeightMetres"/> above themselves: the world has no ceiling, so neither does this.
    /// </summary>
    public static class CloudLayer
    {
        /// <summary>The cloud base above the ground, metres: real thunderclouds' bases sit at about 1 to 2 km.</summary>
        public const float HeightMetres = 1500.0f;

        /// <summary>The nearest the cloud may be over somebody flying, metres; any higher than this under it and the cloud moves up with them.</summary>
        public const float MinClearanceMetres = 200.0f;

        /// <summary>How far down the ground is looked for, metres; with nothing under (a void), the ground is taken as where the point is.</summary>
        public const float GroundSearchMetres = 3000.0f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>
        /// The cloud base's height for somebody at <paramref name="y"/> over ground at <paramref name="groundY"/>: <see cref="HeightMetres"/>
        /// over the ground, or over them once they fly within <see cref="MinClearanceMetres"/> of that.
        /// </summary>
        public static float BaseOver(float groundY, float y)
        {
            float layer = groundY + HeightMetres;
            return y > layer - MinClearanceMetres ? y + HeightMetres : layer;
        }

        /// <summary>The cloud base over a point: the ground under it found by a ray straight down, skipping <paramref name="ignore"/>'s own colliders.</summary>
        public static float BaseOver(Vector3 point, Transform ignore = null)
        {
            return BaseOver(GroundUnder(point, ignore), point.y);
        }

        /// <summary>
        /// The height of the first solid thing under <paramref name="point"/> (triggers and anything under <paramref name="ignore"/> skipped:
        /// a body's own feet), or the point's own height when there is nothing.
        /// </summary>
        public static float GroundUnder(Vector3 point, Transform ignore = null)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 0.5f, Vector3.down, Hits, GroundSearchMetres, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float ground = point.y;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider == null || hit.distance >= best || (ignore != null && hit.collider.transform.IsChildOf(ignore)))
                {
                    continue;
                }
                best = hit.distance;
                ground = hit.point.y;
            }
            return ground;
        }
    }
}
