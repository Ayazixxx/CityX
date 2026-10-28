using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// ARCANE ORB'S NUMBERS (docs/ARCANE_ORB.md). A magenta ball that leaves cursed orbs circling whoever it hits, and they strike a few
    /// seconds later: Sephiroth's Shadow Flare in Smash Bros. Ultimate, whose timing the owner asked to follow (SmashWiki): a hit's orbs strike
    /// after 4 seconds; hitting the same target again before then adds orbs, up to 5, and pushes the strike back by half a second to three
    /// (by the shot's charge), but never past 7.5 seconds from the first hit; once they strike nothing more is added. The staff's charge is
    /// Shadow Flare's: 2 orbs from 17 frames held, 3 from 26 (at 60 a second).
    ///
    /// The damage, the mana and the pace are mine (the owner: how much each spell costs is decided later): the ball does a little damage and each
    /// orb more (Shadow Flare's 1.5% a sphere for the surge, 6.5% for each explosion, about the same ratio). The hotbar spell's rank sets its orbs
    /// (1 at rank 1, up to 3) and adds a tenth to its damage each rank; the staff charges instead, paying mana by the orb with no cooldown.
    /// </summary>
    public static class ArcaneOrbRules
    {
        /// <summary>From the first hit to the strike, seconds.</summary>
        public const float StrikeDelaySeconds = 4.0f;

        /// <summary>The latest a strike can be pushed back to, seconds from the first hit.</summary>
        public const float LatestStrikeSeconds = 7.5f;

        /// <summary>A re-hit pushes the strike back this much for a one-orb shot, up to <see cref="MostExtensionSeconds"/> for a full one.</summary>
        public const float LeastExtensionSeconds = 0.5f;

        public const float MostExtensionSeconds = 3.0f;

        /// <summary>The most orbs one caster's curse can hold on one target.</summary>
        public const int MaxOrbsOnOne = 5;

        /// <summary>The most orbs one shot places.</summary>
        public const int MaxOrbsPerShot = 3;

        /// <summary>The staff's charge: held this long the shot places 2 orbs, and 3 from <see cref="ThreeOrbChargeSeconds"/>.</summary>
        public const float TwoOrbChargeSeconds = 17.0f / 60.0f;

        public const float ThreeOrbChargeSeconds = 26.0f / 60.0f;

        /// <summary>
        /// The ball's speed (the fireball's) and how long it flies before fading out unspent, metres a second and seconds: 50 m, inside the 60 m
        /// round its caster that other machines believe a cast's news from (NetworkedAbilitySync.MaxOriginDistanceMetres).
        /// </summary>
        public const float BallSpeed = 20.0f;

        public const float BallLifetimeSeconds = 2.5f;

        /// <summary>The ball's own hit, for each orb it carries, before Spell Power.</summary>
        public const float BallDamagePerOrb = 3.0f;

        /// <summary>Each orb's strike, before Spell Power.</summary>
        public const float OrbDamage = 15.0f;

        /// <summary>What each rank past the first adds to the hotbar spell's damage, as a share.</summary>
        public const float DamagePerRank = 0.1f;

        /// <summary>The hotbar spell: its mana and cooldown, whatever its rank.</summary>
        public const float SpellManaCost = 20.0f;

        public const float SpellCooldownSeconds = 5.0f;

        /// <summary>The staff: mana for each orb a shot carries, and the quickest it shoots again (Shadow Flare's 38 frames uncharged).</summary>
        public const float StaffManaPerOrb = 8.0f;

        public const float StaffShotSeconds = 38.0f / 60.0f;

        /// <summary>The orbs a staff shot held for <paramref name="chargeSeconds"/> places.</summary>
        public static int OrbsForCharge(float chargeSeconds) =>
            chargeSeconds >= ThreeOrbChargeSeconds ? 3 : chargeSeconds >= TwoOrbChargeSeconds ? 2 : 1;

        /// <summary>The orbs the hotbar spell places at <paramref name="rank"/>: one a rank, up to <see cref="MaxOrbsPerShot"/>.</summary>
        public static int OrbsForRank(int rank) => Mathf.Clamp(rank, 1, MaxOrbsPerShot);

        /// <summary>The hotbar spell's damage at <paramref name="rank"/>, as a multiple of rank 1's.</summary>
        public static float DamageFactor(int rank) => 1.0f + DamagePerRank * (Mathf.Max(1, rank) - 1);

        /// <summary>How far a re-hit by a shot of <paramref name="orbsInShot"/> pushes the strike back, seconds.</summary>
        public static float ExtensionFor(int orbsInShot) =>
            Mathf.Lerp(LeastExtensionSeconds, MostExtensionSeconds, (Mathf.Clamp(orbsInShot, 1, MaxOrbsPerShot) - 1) / (float)(MaxOrbsPerShot - 1));

        /// <summary>A staff shot's mana.</summary>
        public static float StaffManaFor(int orbs) => orbs * StaffManaPerOrb;

        /// <summary>The most of <paramref name="wanted"/> orbs a staff shot can carry on <paramref name="mana"/> (0: not even one).</summary>
        public static int StaffOrbsAffordable(float mana, int wanted) =>
            Mathf.Clamp(Mathf.FloorToInt(mana / StaffManaPerOrb + 1e-4f), 0, Mathf.Max(0, wanted));
    }
}
