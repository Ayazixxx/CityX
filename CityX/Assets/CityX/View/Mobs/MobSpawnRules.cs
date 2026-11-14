using CityX.Character;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The rules MobSpawner keeps (docs/MOB_SPAWNING.md), as plain numbers and sums: how far from a player an enemy appears, how often,
    /// which enemy a family is, and how far before it is taken away again. (How many may be alive is the world's: MobHabitats.)
    /// </summary>
    public static class MobSpawnRules
    {
        // The /spawn names (EnemyNetwork's kinds, the console's).
        public const string Ghost = "wraith";

        public const string Monster = "crimsonclaw";

        public const string Swordsman = "swordsman";

        public const string Gunner = "shoota";

        /// <summary>The share of the humanoids that carry a gun; the rest carry a sword.</summary>
        public const float GunnerShare = 0.4f;

        /// <summary>
        /// Out of doors an enemy appears no nearer the player than this, metres: far enough down the street not to pop up in the player's
        /// face (Minecraft's is 24 blocks). Indoors it appears from much nearer, behind walls.
        /// </summary>
        public const float OutdoorsFromMetres = 45.0f;

        public const float IndoorsFromMetres = 18.0f;

        /// <summary>And no further than this, metres over the ground, on a floor no more than this much above or below the player.</summary>
        public const float ToMetres = 95.0f;

        public const float WithinHeightMetres = 45.0f;

        /// <summary>After an enemy has appeared in a habitat, the next appears there no sooner than this, seconds: a cleared street fills again over a while, not at once.</summary>
        public const float SecondsBetweenSpawns = 4.0f;

        /// <summary>How many random spots are rolled at each look for a habitat that has room, before giving up until the next.</summary>
        public const int SpotsRolledEachLook = 8;

        /// <summary>The enemy that a member of <paramref name="family"/> is, by a roll of 0 to 1.</summary>
        public static string KindFor(MobFamily family, float roll)
        {
            switch (family)
            {
                case MobFamily.Ghost: return Ghost;
                case MobFamily.Monster: return Monster;
                default: return roll < GunnerShare ? Gunner : Swordsman;
            }
        }

        /// <summary>How far above the ground <paramref name="kind"/> is put down, metres: the floaters at the height they hover, the walkers just clear of the floor.</summary>
        public static float HeightOffGround(string kind)
        {
            switch (kind)
            {
                case Ghost: return WraithDrift.HoverHeight;
                case Monster: return 3.0f;
                default: return 0.05f;
            }
        }

        /// <summary>How near the player an enemy of <paramref name="habitat"/> may appear, metres.</summary>
        public static float FromMetres(MobHabitat habitat) => habitat == MobHabitat.Indoors ? IndoorsFromMetres : OutdoorsFromMetres;

        /// <summary>
        /// A random spot round a player at <paramref name="viewer"/>, between <paramref name="from"/> and <paramref name="to"/> metres off
        /// over the ground, any way round, every part of that ring as likely as any other (<paramref name="turn"/> and
        /// <paramref name="reach"/>: two rolls of 0 to 1).
        /// </summary>
        public static Vector2 SpotRound(Vector3 viewer, float from, float to, float turn, float reach)
        {
            float angle = turn * Mathf.PI * 2.0f;
            float distance = Mathf.Sqrt(Mathf.Lerp(from * from, to * to, reach));
            return new Vector2(viewer.x + Mathf.Cos(angle) * distance, viewer.z + Mathf.Sin(angle) * distance);
        }

        /// <summary>Whether an enemy at <paramref name="mob"/> is too far from a player at <paramref name="viewer"/> to be kept.</summary>
        public static bool TooFar(Vector3 mob, Vector3 viewer) => Vector3.Distance(mob, viewer) > MobHabitats.CrowdMetres;
    }
}
