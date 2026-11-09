using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A LADDER CLIMBER'S HANDS AND FEET, as a pure function of how high the body is on the ladder (docs/SEWERS.md; owner, 2026-11-09:
    /// "make ladder climbing so make that look good with hand ik"). Nothing is counted up as the climb goes, so the pose cannot drift from
    /// the rungs however long the ladder is, climbing down is climbing up run backwards, and another player's copy of the body, told only
    /// which ladder it is on, puts its own hands and feet on the same rungs.
    ///
    /// The gait is a climber's: each limb takes every other rung, the diagonal pairs move together (the left hand with the right foot),
    /// one pair at a time, so three points always hold; and the hand of a pair lets go and lands a moment before its foot, as the studies of
    /// real ladder climbing find. Heights are in rungs: rung 0 is the lowest, the floor is one rung's height below it.
    /// </summary>
    public static class LadderGait
    {
        /// <summary>Where one hand or foot is.</summary>
        public struct Hold
        {
            /// <summary>The rung it is on, or its place between two while it steps (a foot under the lowest rung stands on the floor, at -1).</summary>
            public float Rung;

            /// <summary>0 on a rung, rising to 1 in the middle of a step: how far off the ladder the limb swings round to the next.</summary>
            public float Lift;
        }

        /// <summary>The share of its two-rung cycle a limb spends stepping.</summary>
        public const float StepShare = 0.3f;

        /// <summary>How much earlier in the cycle a hand steps than the foot it moves with.</summary>
        public const float HandLead = 0.08f;

        /// <summary>
        /// The lowest a foot is ever looked for under the body's own feet, in rungs. With the step's share above, a planted foot is left
        /// from a fifth of a rung over the body's feet (the leg nearly straight) to a rung and a half over them (the thigh level).
        /// </summary>
        public const float FootLow = -0.4f;

        /// <summary>
        /// How far above a body's feet its hands hold, in standing heights, at the middle of their travel: by the top of the head. This
        /// body's arms are long, and held lower they fold up.
        /// </summary>
        public const float HandsOverFeet = 0.94f;

        /// <summary>The floor's height in rungs: a ladder's first rung is one rung's height up.</summary>
        public const float Floor = -Ladders.FirstRung / Ladders.Pitch;

        /// <summary>A body's height on a ladder in rungs, from how far its feet are over the ladder's foot.</summary>
        public static float RungsUp(float feetOverFoot) => (feetOverFoot - Ladders.FirstRung) / Ladders.Pitch;

        /// <summary>How many rungs over its foot's rung the hand that moves with it holds, for a body this tall.</summary>
        public static int HandRungsOverFoot(float standingHeight) => Mathf.Max(2, Mathf.RoundToInt(HandsOverFeet * standingHeight / Ladders.Pitch - 0.9f));

        /// <summary>
        /// Where in its cycle a foot is (0 just planted, 1 about to be planted two rungs up), and the rung it stands on or last stood on.
        /// The left foot takes the even rungs, the right the odd ones.
        /// </summary>
        private static float Cycle(float body, bool left, out int rung)
        {
            float c = (body + FootLow - (left ? 0.0f : 1.0f)) * 0.5f;
            int whole = Mathf.FloorToInt(c);
            rung = whole * 2 + (left ? 2 : 3);
            return c - whole;
        }

        private static Hold Stepping(float from, float to, float phase, float begins)
        {
            float u = Mathf.Clamp01((phase - begins) / StepShare);
            float eased = u * u * (3.0f - 2.0f * u);
            return new Hold { Rung = Mathf.Lerp(from, to, eased), Lift = u > 0.0f && u < 1.0f ? Mathf.Sin(u * Mathf.PI) : 0.0f };
        }

        /// <summary>A foot's hold for a body <paramref name="body"/> rungs up a ladder of <paramref name="rungs"/> rungs.</summary>
        public static Hold Foot(float body, bool left, int rungs)
        {
            float phase = Cycle(body, left, out int rung);
            return Stepping(OnLadder(rung, rungs), OnLadder(rung + 2, rungs), phase, 1.0f - StepShare);
        }

        /// <summary>A hand's hold: on the rung <paramref name="overFoot"/> over the other side's foot, stepping with it and a moment before it.</summary>
        public static Hold Hand(float body, bool left, int overFoot, int rungs)
        {
            float phase = Cycle(body, !left, out int rung);
            return Stepping(OnLadder(rung + overFoot, rungs), OnLadder(rung + overFoot + 2, rungs), phase, 1.0f - StepShare - HandLead);
        }

        /// <summary>A rung that is on the ladder: the floor under the lowest, the top rung over the highest.</summary>
        private static float OnLadder(int rung, int rungs) => rung < 0 ? Floor : Mathf.Min(rung, rungs - 1);

        /// <summary>
        /// The height nearest <paramref name="body"/> at which all four limbs are on their rungs (there is one every rung): where a climber
        /// who stops comes to rest, having finished the step. Kept between the lowest and highest the body may be.
        /// </summary>
        public static float RestNear(float body, float lowest, float highest)
        {
            // Both pairs hold through the first part of each half cycle, before the next hand lets go: rest in the middle of it.
            float still = (0.5f - StepShare - HandLead) - FootLow;
            float rest = Mathf.Round(body - still) + still;
            if (rest < lowest) rest = Mathf.Ceil(lowest - still) + still;
            if (rest > highest) rest = Mathf.Floor(highest - still) + still;
            return Mathf.Clamp(rest, lowest, highest);
        }

        /// <summary>Whether every limb is on a rung (or the floor) at this height.</summary>
        public static bool AllHold(float body, int overFoot, int rungs) =>
            Foot(body, true, rungs).Lift <= 0.0f && Foot(body, false, rungs).Lift <= 0.0f
            && Hand(body, true, overFoot, rungs).Lift <= 0.0f && Hand(body, false, overFoot, rungs).Lift <= 0.0f;

        /// <summary>How far the hips sway to the side of the foot that carries the weight, -1 (left) to 1.</summary>
        public static float Sway(float body)
        {
            float phase = Cycle(body, true, out _);
            return -Mathf.Sin((phase - 0.1f) * 2.0f * Mathf.PI);
        }
    }
}
