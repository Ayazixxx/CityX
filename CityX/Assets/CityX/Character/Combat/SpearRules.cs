using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE SPEAR'S NUMBERS (docs/SPEAR.md; owner, 2026-11-18: "it should be mostly thrusting stabs but some swinging as well maybe the heavy
    /// attacks are swings, also should be usable with shields"). What games give a spear against a sword (Elden Ring, Nioh, For Honor, Monster
    /// Hunter's lance): the longest reach, quick straight thrusts that hit a narrow line and each do a little less than a cut, a slow wide
    /// sweep for crowds, and a place beside a shield. All mine but the owner's shape of it.
    /// </summary>
    public static class SpearRules
    {
        /// <summary>What its blows are scaled from: the one-handed sword's.</summary>
        public const float BaseDamage = CombatTuning.OneHandedSwordDamage;

        /// <summary>How far ahead a spear catches a target, as a share of a sword's reach (SoulsSwordCombat.ReachMetres).</summary>
        public const float ReachShare = 1.35f;

        /// <summary>
        /// How far the shaft leans from straight out past the thumb toward the fingertips in the fist, degrees: it lies diagonally across the
        /// palm (a sabre's grip, not a hammer's), so the forearm comes in behind it at an angle and a thrust does not wring the wrist.
        /// </summary>
        public const float GripTiltDegrees = 35.0f;

        /// <summary>
        /// The front (left) hand is not fixed to the shaft: it stays before its own shoulder and the shaft SLIDES through it as the rear hand
        /// thrusts, as a spear is really used. Where it likes to be, from the left shoulder in the body's axes (toward the middle, down, ahead),
        /// metres on the body at its built height. Only a little toward the middle: nearer it, with the shaft across the front for the sweep, the
        /// arm had to pass through the chest to reach it...
        /// </summary>
        public static readonly Vector3 FrontHandFromShoulder = new Vector3(0.12f, -0.50f, 0.42f);

        /// <summary>...and how near to the rear hand and how far up the shaft from it the front hand may be, metres.</summary>
        public const float FrontHandNearest = 0.24f;

        public const float FrontHandFurthest = 0.80f;
    }
}
