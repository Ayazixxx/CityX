using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// THE NUMBERS OF FIRE (docs/FIRE.md; owner, 2026-10-28: "function like the gta v fire where it kinda spreads but doesnt really damage
    /// terrain just kind of leaves a black burned spot"), and the rules made of them, with no scene: the ground is cells of
    /// <see cref="CellMetres"/>, each with gasoline on it or not; fuel is seconds of burning; a gasoline cell lights its neighbours, a bare
    /// one never does, so the fire's footprint is the fuel and a fringe round it, never the whole street.
    /// </summary>
    public static class FireRules
    {
        public const float CellMetres = 0.75f;

        /// <summary>Fire spreads across a floor this much higher or lower (a kerb, a step), never off a roof's edge.</summary>
        public const float StepMetres = 1.0f;

        /// <summary>The floor under a cell is looked for from this far above the height asked for, this far down.</summary>
        public const float ProbeAboveMetres = 1.2f;

        public const float ProbeReachMetres = 3.2f;

        /// <summary>Less gasoline than this on a cell is nothing (a drop of a pour's last tick).</summary>
        public const float MinLitres = 0.03f;

        /// <summary>A cell holds this much; the rest spills into the cells round it, so pouring on one spot makes a puddle (GTA's bigger puddle).</summary>
        public const float MaxLitres = 1.5f;

        /// <summary>How long a cell with a trace of gasoline burns (a trail laid at a walk), and one with a full litre on it (a puddle).</summary>
        public const float TrailFuelSeconds = 12.0f;

        public const float PuddleFuelSeconds = 30.0f;

        /// <summary>Bare pavement set alight (a fireball, the flame staff held on it) burns this long and dies.</summary>
        public const float BareFuelSeconds = 4.5f;

        /// <summary>A lit cell's flames rise to full height over this long, and die down over the last <see cref="DyingSeconds"/> of its fuel.</summary>
        public const float FlareSeconds = 0.4f;

        public const float DyingSeconds = 3.0f;

        /// <summary>The height of a bare cell's fire against a gasoline cell's.</summary>
        public const float BareHeat = 0.7f;

        /// <summary>How often the fire looks at its neighbours.</summary>
        public const float SpreadTickSeconds = 0.1f;

        /// <summary>A burning gasoline cell's chance a second, at full heat, to light a gasoline neighbour: a trail burns at a fast walk, 2-3 m/s.</summary>
        public const float SpreadPerSecond = 8.0f;

        /// <summary>A bare neighbour's share of that chance: the fringe round the fuel catches over seconds, not at once.</summary>
        public const float BareCatchShare = 0.05f;

        /// <summary>A cell spreads only once its fire is this high.</summary>
        public const float SpreadHeat = 0.25f;

        /// <summary>In full rain a burning cell's fuel goes this many times faster, and bare cells do not catch at all.</summary>
        public const float RainBurnFactor = 2.0f;

        /// <summary>Rain harder than this keeps bare pavement from catching.</summary>
        public const float RainThatStopsBareFire = 0.5f;

        /// <summary>A doused cell stays too wet to catch this long.</summary>
        public const float WetSeconds = 8.0f;

        /// <summary>The most cells burning at once (the budget: each one is flames, embers and smoke every frame).</summary>
        public const int MaxBurningCells = 160;

        /// <summary>A body's feet find the burning cell under them within this much height.</summary>
        public const float BodyReachMetres = 1.2f;

        /// <summary>Whoever stands in the fire burns this much a second, in ticks (a spell: no stagger, no blood).</summary>
        public const float DamagePerSecond = 10.0f;

        public const float BodyTickSeconds = 0.25f;

        /// <summary>Out of the fire a body keeps burning this long, this hard.</summary>
        public const float AfterburnSeconds = 4.0f;

        public const float AfterburnDamagePerSecond = 4.0f;

        /// <summary>Standing in water this deep puts a burning body out at once; rain puts it out over <see cref="RainDouseSeconds"/>.</summary>
        public const float DouseDepthMetres = 0.25f;

        public const float RainDouseSeconds = 2.0f;

        /// <summary>The black spot a burned cell leaves stays this long (owner: "make the burn marks fade after 1 minute"), then fades over <see cref="ScorchFadeSeconds"/>.</summary>
        public const float ScorchLifeSeconds = 60.0f;

        public const float ScorchFadeSeconds = 15.0f;

        /// <summary>The jerry can: GTA's holds 20 litres; held to pour, it lays this much a second on the ground this far ahead of its spout.</summary>
        public const float CanLitres = 20.0f;

        public const float PourLitresPerSecond = 0.5f;

        public const float PourMetresAhead = 0.8f;

        /// <summary>How often a stream of fire (the flame staff, the flamethrower) lights what it touches, and how far round the point.</summary>
        public const float IgniteTickSeconds = 0.1f;

        public const float StreamIgniteRadius = 0.9f;

        /// <summary>A bullet, a burning body or lightning lights the fuel this far round its point.</summary>
        public const float SparkIgniteRadius = 0.5f;

        /// <summary>A fireball lights the fuel this far round where it bursts, at a plain ball's size.</summary>
        public const float FireballIgniteRadius = 1.5f;

        /// <summary>Water (the water staff's jet) puts out the fire this far round where it lands.</summary>
        public const float DouseRadius = 1.6f;

        /// <summary>How long a cell with this much gasoline on it burns.</summary>
        public static float FuelSecondsFor(float litres)
        {
            return litres < MinLitres ? 0.0f : Mathf.Lerp(TrailFuelSeconds, PuddleFuelSeconds, Mathf.Clamp01(litres));
        }

        /// <summary>How high a cell's fire is, 0..1: rising over the flare, full, and dying down as the last fuel goes.</summary>
        public static float HeatAt(float ageSeconds, float fuelLeft, bool gasoline)
        {
            float top = gasoline ? 1.0f : BareHeat;
            float flare = Mathf.Clamp01(ageSeconds / FlareSeconds);
            float dying = fuelLeft < DyingSeconds ? Mathf.Lerp(0.2f, 1.0f, Mathf.Clamp01(fuelLeft / DyingSeconds)) : 1.0f;
            return top * flare * dying;
        }

        /// <summary>The chance a burning cell at this heat lights a neighbour over <paramref name="seconds"/>.</summary>
        public static float CatchChance(float sourceHeat, bool neighbourHasGasoline, float seconds)
        {
            float rate = SpreadPerSecond * Mathf.Clamp01(sourceHeat) * (neighbourHasGasoline ? 1.0f : BareCatchShare);
            return 1.0f - Mathf.Exp(-rate * Mathf.Max(0.0f, seconds));
        }

        /// <summary>How much faster fuel burns off in this much rain.</summary>
        public static float BurnFactorIn(float rain)
        {
            return 1.0f + (RainBurnFactor - 1.0f) * Mathf.Clamp01(rain);
        }
    }
}
