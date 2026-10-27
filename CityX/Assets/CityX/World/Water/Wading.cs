using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// How water moves a body, as pure rules (docs/WATER.md). Depth is a share of the body's own height (an ankle is a tenth of it, a knee a
    /// quarter, a waist under a half), so a giant wades through what drowns a tiny body.
    ///
    /// WADING slows a body in step with the depth, plainly and predictably (the complaint about every swamp is slowing that feels
    /// arbitrary), and weakens its jump. FLOATING ("treading") is only for water over the chest, which only a flood makes (owner: "the sewer
    /// biome is just meant to have like water up to your ankles or knees", "only ... the great flood ... very basic hold space to go up"): the
    /// body sinks slowly, rises while Space is held to float with its head out, and moves slowly. It starts and stops at different depths, so
    /// it never flickers at the line (the waterline thrashing every swimming system warns about).
    /// </summary>
    public static class Wading
    {
        /// <summary>The share of its walking or running speed a body keeps with water at its chest (<see cref="ChestDepth"/>) and deeper.</summary>
        public const float SlowestWade = 0.45f;

        /// <summary>The share of its jump's launch a body keeps with water at its chest.</summary>
        public const float WeakestJump = 0.5f;

        /// <summary>Water this deep (a share of the body's height) is at its chest: wading is as slow as it gets.</summary>
        public const float ChestDepth = 0.6f;

        /// <summary>A body starts floating once the water is this deep, and stops once it is shallower than <see cref="StopFloatingDepth"/>.</summary>
        public const float StartFloatingDepth = 0.64f;

        public const float StopFloatingDepth = 0.56f;

        /// <summary>Floating, this much of the body is under the surface: the water at its shoulders, the head out.</summary>
        public const float FloatingDepth = 0.74f;

        /// <summary>How fast a floating body sinks with Space let go, and rises with it held, metres a second (by a 2.6 m body: a share of height).</summary>
        public const float SinkRate = 0.22f;

        public const float RiseRate = 0.7f;

        /// <summary>How fast a floating body moves through the water, as a share of its walking speed.</summary>
        public const float FloatingSpeed = 0.55f;

        /// <summary>Water deeper than this (a share of the body's height) breaks a fall: landing in it does not hurt.</summary>
        public const float BreaksFallDepth = 0.4f;

        /// <summary>How wet a body gets standing in water this deep (the rain's 0..1 wetness, PlayerWetness): soaked by the time it is at its waist.</summary>
        public static float SoakedBy(float depthShare)
        {
            return Mathf.Clamp01(depthShare / 0.4f);
        }

        /// <summary>How fast water soaks a body, wetness a second (far faster than rain).</summary>
        public const float SoakRate = 1.5f;

        /// <summary>How much of a water body's flow carries a body: all of it floating, in step with the depth wading (none at the ankles).</summary>
        public static float FlowCarry(float depthShare, bool floating)
        {
            return floating ? 1.0f : Mathf.Clamp01(depthShare / ChestDepth);
        }

        /// <summary>The share of its speed a body keeps wading this deep: 1 dry, falling in a straight line to <see cref="SlowestWade"/> at the chest.</summary>
        public static float SpeedShare(float depthShare)
        {
            return Mathf.Lerp(1.0f, SlowestWade, Mathf.Clamp01(depthShare / ChestDepth));
        }

        /// <summary>The share of its jump's launch a body keeps wading this deep.</summary>
        public static float JumpShare(float depthShare)
        {
            return Mathf.Lerp(1.0f, WeakestJump, Mathf.Clamp01(depthShare / ChestDepth));
        }

        /// <summary>Whether a body floats in water this deep, given whether it was floating already (the two depths apart, so it holds).</summary>
        public static bool Floats(bool wasFloating, float depthShare)
        {
            return wasFloating ? depthShare >= StopFloatingDepth : depthShare >= StartFloatingDepth;
        }

        /// <summary>
        /// A floating body's upward speed, metres a second: rising while Space is held, never past where it floats with its head out (eased in
        /// over the last stretch so it does not bob on the line), sinking slowly while it is not.
        /// </summary>
        public static float FloatingClimb(float depthShare, float height, bool holdingUp)
        {
            if (!holdingUp)
            {
                return -SinkRate * height;
            }
            float below = (depthShare - FloatingDepth) * height;   // how far under its floating height the body is, metres
            if (below <= 0.0f)
            {
                return Mathf.Max(below * 4.0f, -SinkRate * height);   // a little above it (a wave of the flood dropped): settles back
            }
            return Mathf.Min(RiseRate * height, below * 4.0f);
        }
    }
}
