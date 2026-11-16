using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// HOW THE FIRE SPELLS LOOK, IN NUMBERS (docs/FIRE.md, "The fire spells"; owner, 2026-11-12, of achrefelouafi's LinearAbiltyCastingThreeJS:
    /// "just make them all look better"). The Fireball's molten core heating as it flies, the wake of burning gas it leaves, its burst, the
    /// scorch and the shake; the Flame Staff's stream; and the fire spells' heat in bold bands (the cel look). Pure: the scene side is
    /// <see cref="FireLook"/>'s spell half, FireballProjectile, FireBurst and FlameStreamLook. Nothing here changes what a spell does.
    /// </summary>
    public static class SpellFire
    {
        // --- the fireball in flight ---

        /// <summary>How long a fireball takes to grow fully hot after it leaves the hand, seconds (the reference's meteor heats up over its whole run).</summary>
        public const float CoreHeatSeconds = 0.6f;

        /// <summary>How fast the core tumbles, degrees a second, so its seams turn.</summary>
        public const float CoreSpinDegreesPerSecond = 160.0f;

        /// <summary>A puff of burning gas is left every this many metres of a plain ball's flight, this big across, living this long.</summary>
        public const float WakeSpacingMetres = 0.12f;

        public const float WakePuffMetres = 0.42f;

        /// <summary>How far ahead of where the ball was each puff is left, metres on a plain ball, so the wake's tongues start on the ball.</summary>
        public const float WakeLeadMetres = 0.12f;
        public const float WakeShortestSeconds = 0.16f;
        public const float WakeLongestSeconds = 0.24f;

        /// <summary>
        /// How long each puff of burning gas is drawn against its width, along its flight (a tongue of fire: FireLook's spell systems), and how
        /// much longer for each metre a second it flies: the wake's lick back off the ball, a burst's fly out and round off as they slow, a
        /// stream's run together into one body.
        /// </summary>
        public const float WakeStretch = 2.2f;

        public const float BlastStretch = 1.15f;
        public const float BlastStretchPerSpeed = 0.09f;
        public const float JetStretch = 1.3f;
        public const float JetStretchPerSpeed = 0.07f;

        /// <summary>Embers, sparks and puffs of smoke shed by a plain ball each second.</summary>
        public const float WakeEmbersPerSecond = 26.0f;

        public const float WakeSparksPerSecond = 12.0f;
        public const float WakeSmokePerSecond = 7.0f;

        /// <summary>How hot the core is <paramref name="seconds"/> after the cast, 0..1: it leaves warm and arrives white-hot.</summary>
        public static float CoreHeatAt(float seconds) => Mathf.Pow(Mathf.Clamp01(seconds / CoreHeatSeconds), 0.7f);

        /// <summary>
        /// How many puffs of the wake to leave over <paramref name="metres"/> flown by a ball <paramref name="size"/> times a plain one, the part
        /// of a puff left over carried in <paramref name="debt"/>: so the wake is as thick at any frame rate.
        /// </summary>
        public static int WakePuffs(float metres, float size, ref float debt)
        {
            debt += Mathf.Max(0.0f, metres) / (WakeSpacingMetres * Mathf.Max(0.1f, size));
            int count = Mathf.FloorToInt(debt);
            debt -= count;
            return count;
        }

        // --- the burst ---

        /// <summary>A plain ball's burst: puffs of fire thrown out of it, sparks, embers, glowing cinders, puffs of smoke.</summary>
        public const int BurstPuffs = 16;

        public const int BurstSparks = 24;
        public const int BurstEmbers = 16;
        public const int BurstCinders = 7;
        public const int BurstSmoke = 6;

        /// <summary>How many of <paramref name="count"/> a ball <paramref name="size"/> times a plain one throws (a full charge, about twice).</summary>
        public static int Scaled(int count, float size) => Mathf.RoundToInt(count * Mathf.Lerp(1.0f, 2.0f, Charge(size)));

        /// <summary>A charged ball's size as a share of the fullest charge (1 to 2.5 times a plain ball: FireballCaster.StaffMostSize).</summary>
        public static float Charge(float size) => Mathf.Clamp01((size - 1.0f) / 1.5f);

        /// <summary>The black scorch a burst leaves on what it hit, metres across.</summary>
        public static float ScorchMetres(float size) => 1.5f * Mathf.Max(1.0f, size);

        /// <summary>
        /// The camera of a player near the burst shakes this hard at the middle, metres: a nudge for a plain ball, more for a charged one, both
        /// well under a grenade's (0.45 m) and Glacier Spikes' (0.22 m) end.
        /// </summary>
        public static float ShakeMetres(float size) => Mathf.Lerp(0.04f, 0.15f, Charge(size));

        /// <summary>And felt this far from it, metres.</summary>
        public static float ShakeReach(float size) => Mathf.Lerp(8.0f, 20.0f, Charge(size));

        public const float ShakeSeconds = 0.3f;

        // --- the flame staff's stream ---

        /// <summary>How fast the stream's gas flies, metres a second, and how many puffs of it a second, and of its white-hot core at the staff.</summary>
        public const float JetSpeed = 15.0f;

        public const float JetPuffsPerSecond = 110.0f;
        public const float JetCorePerSecond = 50.0f;
        public const float JetCoreSeconds = 0.12f;

        /// <summary>A puff's size as it leaves the staff, metres (it swells as it goes: FireLook's jet).</summary>
        public const float JetPuffMetres = 0.45f;

        /// <summary>Where the stream meets something: flames licking up, gas spreading over it, sparks, a second.</summary>
        public const float SplashFlamesPerSecond = 30.0f;

        public const float SplashGasPerSecond = 26.0f;
        public const float SplashSparksPerSecond = 8.0f;

        /// <summary>Embers and puffs of smoke a second off the stream, and its soft glow.</summary>
        public const float JetEmbersPerSecond = 14.0f;

        public const float JetSmokePerSecond = 5.0f;
        public const float JetGlowPerSecond = 12.0f;

        /// <summary>A wall the stream plays on is blackened every this many seconds at most (a smudge of soot, not a mark a frame).</summary>
        public const float SootEverySeconds = 0.5f;

        public const float SootMetres = 1.3f;

        /// <summary>
        /// How long a puff of the stream lives: its full <paramref name="fullSeconds"/>, or less when what it flies into is
        /// <paramref name="toEnd"/> metres off, so it dies where the stream meets the wall instead of going through it.
        /// </summary>
        public static float JetLife(float fullSeconds, float toEnd) => Mathf.Clamp(toEnd / JetSpeed, 0.02f, Mathf.Max(0.02f, fullSeconds));

        // --- the heat in bands ---

        /// <summary>Where each band of heat begins (CityXFireBands.hlsl: keep the two in step), and its colour (the reference's, sRGB).</summary>
        public static readonly float[] BandFrom = { 0.08f, 0.22f, 0.42f, 0.64f, 0.86f };

        public static readonly Color[] BandColours =
        {
            new Color(0.169f, 0.051f, 0.02f),   // #2b0d05, an ember's dark red
            new Color(1.0f, 0.239f, 0.063f),    // #ff3d10, red
            new Color(1.0f, 0.416f, 0.071f),    // #ff6a12, orange
            new Color(1.0f, 0.69f, 0.18f),      // #ffb02e, yellow
            new Color(1.0f, 0.843f, 0.51f),     // #ffd782, white-hot: a pale gold (the reference's #fff3d0 tone-maps to grey: CityXFireBands.hlsl)
        };

        /// <summary>The band <paramref name="heat"/> falls in: -1 below the first (nothing burns), 0 the darkest to 4 white-hot.</summary>
        public static int BandOf(float heat)
        {
            int band = -1;
            for (int i = 0; i < BandFrom.Length; i++)
            {
                if (heat >= BandFrom[i])
                {
                    band = i;
                }
            }
            return band;
        }

        /// <summary>The colour of <paramref name="heat"/>, its alpha 0 where nothing burns.</summary>
        public static Color Band(float heat)
        {
            int band = BandOf(heat);
            return band < 0 ? Color.clear : BandColours[band];
        }
    }
}
