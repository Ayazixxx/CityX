using System;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>
    /// The frame a stretch of paving is laid in: where its pattern starts, which way its rows run, how far in its first joint lies and how
    /// much its rows are stretched to fit. The paving's pattern is flags <see cref="Sidewalks.FlagMetres"/> square with a joint along
    /// every whole flag, both ways.
    /// </summary>
    public readonly struct PavingFrame
    {
        public readonly Vec2 Origin;

        /// <summary>The way the rows run, a unit vector (along the kerb, for a sidewalk).</summary>
        public readonly Vec2 Along;

        /// <summary>Across the rows, a unit vector (in from the kerb).</summary>
        public readonly Vec2 Inward;

        /// <summary>How far in from the origin the first joint along the rows lies, metres.</summary>
        public readonly double FirstJoint;

        /// <summary>The rows' depth is a flag's divided by this.</summary>
        public readonly double Stretch;

        public PavingFrame(Vec2 origin, Vec2 along, Vec2 inward, double firstJoint, double stretch)
        {
            Origin = origin;
            Along = along;
            Inward = inward;
            FirstJoint = firstJoint;
            Stretch = stretch;
        }

        /// <summary>A point's place in the pattern, metres of it: a joint crosses wherever either is a whole number of flags.</summary>
        public void At(Vec2 point, out double along, out double across)
        {
            Vec2 from = point - Origin;
            along = Vec2.Dot(from, Along);
            across = (Vec2.Dot(from, Inward) - FirstJoint) * Stretch;
        }
    }

    /// <summary>
    /// THE SIDEWALK'S JOINTS RUN SQUARE TO ITS STREET (docs/AMERICAN_STREETS.md). An American sidewalk is concrete poured in place and
    /// scored into flags as it sets: the joints run straight across the walk and along it, whichever way the street runs. Laid by the
    /// world's own north and east, the joints crossed every street that is not due north or east at a slant, as slabs on a plaza do and
    /// no pour ever did. So each side of a block has its own frame: rows along the kerb, the first joint a kerb stone's width in from the
    /// edge (the kerb reads as a stone of its own), and the rows fitted so that the last joint is the back of the walk.
    /// A pure rule of the plan, like the kerb's corners (KerbCorners).
    /// </summary>
    public static class Sidewalks
    {
        /// <summary>A flag of sidewalk, metres square (5 ft at this world's scale, x 1.3): what the paving's texture draws (View/FacadeTextures).</summary>
        public const double FlagMetres = 2.0;

        /// <summary>The first joint along a sidewalk lies this far in from the kerb's face, metres (a 6 in kerb x 1.3).</summary>
        public const double KerbStoneWidth = 0.2;

        /// <summary>A pavement narrower than this is not laid as a sidewalk of its own, metres: there is no room behind the kerb for a row.</summary>
        public const double NarrowestLaid = KerbStoneWidth + FlagMetres * 0.5;

        /// <summary>A side shorter than this has no sidewalk of its own, metres.</summary>
        public const double ShortestLaid = 0.5;

        /// <summary>How many rows of flags a sidewalk of a width has behind its kerb stone: as near a flag deep each as fits.</summary>
        public static int Rows(double width) => Math.Max(1, (int)Math.Round((width - KerbStoneWidth) / FlagMetres));

        /// <summary>
        /// The sidewalk along one side of a block, from its kerb's corner <paramref name="side"/> to the next: the strip it covers (kerb,
        /// kerb, lot line, lot line, in the block's own order) and the frame its flags are laid in. False where there is none to lay: no
        /// kerb line, a side too short or too narrow, or one whose lot line was turned about by the corners at its ends.
        /// </summary>
        public static bool TryAlong(PlanBlock block, int side, out Vec2[] strip, out PavingFrame frame)
        {
            strip = null;
            frame = default;
            if (block?.Inner == null || block.Outer == null || block.Inner.Count != block.Outer.Count)
            {
                return false;
            }
            int next = (side + 1) % block.Inner.Count;
            Vec2 kerbFrom = block.Inner[side], kerbTo = block.Inner[next], lotFrom = block.Outer[side], lotTo = block.Outer[next];
            double length = (kerbTo - kerbFrom).Length;
            if (length < ShortestLaid)
            {
                return false;
            }
            Vec2 along = (kerbTo - kerbFrom) * (1.0 / length);
            Vec2 inward = along.Perp;
            double width = Vec2.Dot(lotFrom - kerbFrom, inward);
            if (width < 0.0)
            {
                inward = -inward;
                width = -width;
            }
            if (width < NarrowestLaid || Vec2.Dot(lotTo - lotFrom, along) < ShortestLaid)
            {
                return false;
            }
            strip = new[] { kerbFrom, kerbTo, lotTo, lotFrom };
            frame = new PavingFrame(kerbFrom, along, inward, KerbStoneWidth, Rows(width) * FlagMetres / (width - KerbStoneWidth));
            return true;
        }

        /// <summary>The frame of what lies behind a block's sidewalks (its forecourts and yards): along the block's longest side, nothing fitted.</summary>
        public static PavingFrame Behind(PlanBlock block)
        {
            int longest = 0;
            double most = -1.0;
            for (int i = 0; i < block.Inner.Count; i++)
            {
                double length = (block.Inner[(i + 1) % block.Inner.Count] - block.Inner[i]).SqrLength;
                if (length > most)
                {
                    most = length;
                    longest = i;
                }
            }
            Vec2 from = block.Inner[longest];
            Vec2 along = most > 1e-12 ? (block.Inner[(longest + 1) % block.Inner.Count] - from).Normalized : new Vec2(1.0, 0.0);
            return new PavingFrame(from, along, along.Perp, 0.0, 1.0);
        }
    }
}
