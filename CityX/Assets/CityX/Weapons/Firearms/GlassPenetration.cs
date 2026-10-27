using CityX.World;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A bullet goes on through a window (owner, 2026-10-26: "make it so the bullet travels through the window but does a little less damage"):
    /// it breaks the pane as it always did and carries on along its line, ten per cent weaker for every pane (owner: "lets make it 10% less damage
    /// for all guns for now"). Counter-Strike takes about that off a rifle round through glass, far less than through a wall; a wall still stops a
    /// shot here. ONE WINDOW AT MOST (owner: "make it so a bullet that has gone through 1 window cant go through another it just stops at whatever
    /// surface it hits unless its a player/mob/npc then the bullet lands and does damage"): a second pane takes the bullet (its hole) and stops it.
    /// </summary>
    public static class GlassPenetration
    {
        /// <summary>The share of its damage a bullet keeps through each pane.</summary>
        public const float DamageKeptPerPane = 0.9f;

        /// <summary>The most panes one bullet breaks: the first it goes through, the second it stops in.</summary>
        public const int MaxPanes = 2;

        /// <summary>How far past a pane the bullet's line is taken up again, metres: clear of both faces of the glass.</summary>
        public const float PastGlass = 0.02f;

        /// <summary>A bullet's damage after <paramref name="panes"/> panes, as a share of its own.</summary>
        public static float DamageShare(int panes) => Mathf.Pow(DamageKeptPerPane, Mathf.Max(0, panes));

        /// <summary>The next thing along a shot's line from a point, within a distance; the shooter's own trace (what it skips is its own).</summary>
        public delegate bool Trace(Vector3 from, float distance, out RaycastHit hit);

        /// <summary>
        /// Carries a shot that met <paramref name="hit"/> on through every pane of glass in its way. With <paramref name="breaks"/> each pane takes
        /// the bullet (the shooter's machine); without, the panes are only looked for (another player's copy of the shot, drawing its trail).
        /// True when the shot ends on something that is not glass -- a body, a prop: <paramref name="hit"/>, the caller's to deal with. False when
        /// it ends on a wall, in its last pane or in the air. <paramref name="end"/> is where its line ends, for the trail;
        /// <paramref name="rangeLeft"/> how much further than <paramref name="hit"/> the shot reaches.
        /// </summary>
        public static bool Follow(ref RaycastHit hit, Vector3 direction, float rangeLeft, bool breaks, Trace trace, out int panes, out Vector3 end)
        {
            panes = 0;
            end = hit.point;
            while (true)
            {
                var surface = hit.collider.GetComponentInParent<IBreakableSurface>();
                if (surface == null)
                {
                    return true;
                }
                bool glass = breaks ? surface.TakeBlow(hit.point, direction, BlowStrength.Bullet) : surface.IsGlassAt(hit.point);
                if (!glass)
                {
                    return false;   // the city's wall round its windows: the shot stops there
                }
                panes++;
                if (panes >= MaxPanes)
                {
                    return false;
                }
                Vector3 from = hit.point + direction * PastGlass;
                rangeLeft -= PastGlass;
                if (rangeLeft <= 0.0f || !trace(from, rangeLeft, out RaycastHit next))
                {
                    end = from + direction * Mathf.Max(0.0f, rangeLeft);
                    return false;
                }
                rangeLeft -= next.distance;
                hit = next;
                end = hit.point;
            }
        }
    }
}
