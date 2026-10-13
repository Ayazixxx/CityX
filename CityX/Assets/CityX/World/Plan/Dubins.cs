using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>A pose on the plan: a point and the unit direction travelled through it.</summary>
    public readonly struct Pose
    {
        public readonly Vec2 P;
        public readonly Vec2 Dir;

        public Pose(Vec2 p, Vec2 dir)
        {
            P = p;
            Dir = dir.Normalized;
        }

        public double Heading => DMath.Angle(Dir);
    }

    /// <summary>
    /// The shortest path between two poses that never turns tighter than a radius: Dubins' result (1957) that such a path is always one of
    /// six words of at most three pieces -- an arc Left or Right at the radius, or a Straight -- LSL, RSR, LSR, RSL, RLR, LRL.
    ///
    /// It is how an interchange's ramps and connectors are laid here, and why they come in every shape real ones do: leaving an expressway to
    /// meet a street square, a ramp is an S (RSL / LSR) that is nearly STRAIGHT when the street is close and long and CURVED when it is not; a
    /// left turn at a cloverleaf comes out as a LOOP (RSR turning 270 degrees); a slip road onto a parallel street is a gentle S. The closed
    /// forms follow Andrew Walker's well-known implementation (dubins.c); trig comes from DMath so every machine lays the same ramp.
    /// </summary>
    public sealed class DubinsPath
    {
        public enum Word { LSL, LSR, RSL, RSR, RLR, LRL }

        public readonly Pose Start;
        public readonly double Radius;
        public readonly Word Kind;

        /// <summary>The three pieces' lengths, as angles for the arcs (radians) and a distance over the radius for the straight.</summary>
        public readonly double T, P, Q;

        public DubinsPath(Pose start, double radius, Word kind, double t, double p, double q)
        {
            Start = start;
            Radius = radius;
            Kind = kind;
            T = t;
            P = p;
            Q = q;
        }

        public double Length => (T + P + Q) * Radius;

        /// <summary>How much the path turns in all, radians (a loop is about 4.7).</summary>
        public double TotalTurn
        {
            get
            {
                switch (Kind)
                {
                    case Word.RLR:
                    case Word.LRL:
                        return T + P + Q;
                    default:
                        return T + Q;
                }
            }
        }

        /// <summary>The piece types of each word, in order: 'L', 'S' or 'R'.</summary>
        private static string Pieces(Word w)
        {
            switch (w)
            {
                case Word.LSL: return "LSL";
                case Word.LSR: return "LSR";
                case Word.RSL: return "RSL";
                case Word.RSR: return "RSR";
                case Word.RLR: return "RLR";
                default: return "LRL";
            }
        }

        /// <summary>The pose this far along the path (clamped to its ends).</summary>
        public Pose At(double distance)
        {
            double along = Math.Max(0.0, Math.Min(Length, distance)) / Radius;   // in radius units
            string pieces = Pieces(Kind);
            double[] lengths = { T, P, Q };
            Vec2 p = Start.P;
            Vec2 d = Start.Dir;
            for (int i = 0; i < 3 && along > 0.0; i++)
            {
                double l = Math.Min(along, lengths[i]);
                Step(pieces[i], l, ref p, ref d);
                along -= l;
            }
            return new Pose(p, d);
        }

        /// <summary>Moves a pose along one piece by a length in radius units.</summary>
        private void Step(char piece, double l, ref Vec2 p, ref Vec2 d)
        {
            if (piece == 'S')
            {
                p += d * (l * Radius);
                return;
            }
            double sign = piece == 'L' ? 1.0 : -1.0;
            Vec2 toCentre = d.Perp * (Radius * sign);   // left of the direction for a left turn
            Vec2 centre = p + toCentre;
            double c = DMath.Cos(l * sign), s = DMath.Sin(l * sign);
            Vec2 r = p - centre;
            p = centre + new Vec2(r.X * c - r.Y * s, r.X * s + r.Y * c);
            d = new Vec2(d.X * c - d.Y * s, d.X * s + d.Y * c).Normalized;
        }

        /// <summary>Points along the path no further apart than <paramref name="step"/>, the ends included.</summary>
        public List<Pose> Sample(double step)
        {
            var poses = new List<Pose>();
            double length = Length;
            int n = Math.Max(1, (int)Math.Ceiling(length / step));
            for (int i = 0; i <= n; i++)
            {
                poses.Add(At(length * i / n));
            }
            return poses;
        }

        // ------------------------------------------------------------------ solving

        private static double Mod2Pi(double a)
        {
            double r = a - DMath.TwoPi * Math.Floor(a / DMath.TwoPi);
            return r >= DMath.TwoPi ? 0.0 : r;
        }

        private static double Acos(double x) => DMath.Atan2(Math.Sqrt(Math.Max(0.0, 1.0 - x * x)), x);

        /// <summary>
        /// The shortest path of the allowed words (all six when none are given) from one pose to another, or null when none exists. A word
        /// whose total turn is more than <paramref name="maxTurn"/> is not allowed.
        /// </summary>
        public static DubinsPath Shortest(Pose from, Pose to, double radius, double maxTurn = double.MaxValue, params Word[] allowed)
        {
            DubinsPath best = null;
            foreach (Word w in allowed != null && allowed.Length > 0 ? allowed : new[] { Word.LSL, Word.LSR, Word.RSL, Word.RSR, Word.RLR, Word.LRL })
            {
                DubinsPath candidate = Solve(from, to, radius, w);
                if (candidate == null || candidate.TotalTurn > maxTurn) continue;
                if (best == null || candidate.Length < best.Length - 1e-9) best = candidate;
            }
            return best;
        }

        /// <summary>The path of one word from one pose to another, or null when that word cannot join them.</summary>
        public static DubinsPath Solve(Pose from, Pose to, double radius, Word w)
        {
            Vec2 delta = to.P - from.P;
            double d = delta.Length / radius;
            double phi = d > 1e-12 ? DMath.Atan2(delta.Y, delta.X) : 0.0;
            double a = Mod2Pi(from.Heading - phi);
            double b = Mod2Pi(to.Heading - phi);
            double sa = DMath.Sin(a), sb = DMath.Sin(b), ca = DMath.Cos(a), cb = DMath.Cos(b);
            double cab = DMath.Cos(a - b);
            double t, p, q;
            switch (w)
            {
                case Word.LSL:
                {
                    double tmp0 = d + sa - sb;
                    double p2 = 2.0 + d * d - 2.0 * cab + 2.0 * d * (sa - sb);
                    if (p2 < 0.0) return null;
                    double tmp1 = DMath.Atan2(cb - ca, tmp0);
                    t = Mod2Pi(tmp1 - a);
                    p = Math.Sqrt(p2);
                    q = Mod2Pi(b - tmp1);
                    break;
                }
                case Word.RSR:
                {
                    double tmp0 = d - sa + sb;
                    double p2 = 2.0 + d * d - 2.0 * cab + 2.0 * d * (sb - sa);
                    if (p2 < 0.0) return null;
                    double tmp1 = DMath.Atan2(ca - cb, tmp0);
                    t = Mod2Pi(a - tmp1);
                    p = Math.Sqrt(p2);
                    q = Mod2Pi(tmp1 - b);
                    break;
                }
                case Word.LSR:
                {
                    double p2 = -2.0 + d * d + 2.0 * cab + 2.0 * d * (sa + sb);
                    if (p2 < 0.0) return null;
                    p = Math.Sqrt(p2);
                    double tmp2 = DMath.Atan2(-ca - cb, d + sa + sb) - DMath.Atan2(-2.0, p);
                    t = Mod2Pi(tmp2 - a);
                    q = Mod2Pi(tmp2 - b);
                    break;
                }
                case Word.RSL:
                {
                    double p2 = -2.0 + d * d + 2.0 * cab - 2.0 * d * (sa + sb);
                    if (p2 < 0.0) return null;
                    p = Math.Sqrt(p2);
                    double tmp2 = DMath.Atan2(ca + cb, d - sa - sb) - DMath.Atan2(2.0, p);
                    t = Mod2Pi(a - tmp2);
                    q = Mod2Pi(b - tmp2);
                    break;
                }
                case Word.RLR:
                {
                    double tmp0 = (6.0 - d * d + 2.0 * cab + 2.0 * d * (sa - sb)) / 8.0;
                    if (Math.Abs(tmp0) > 1.0) return null;
                    double ph = DMath.Atan2(ca - cb, d - sa + sb);
                    p = Mod2Pi(DMath.TwoPi - Acos(tmp0));
                    t = Mod2Pi(a - ph + Mod2Pi(p / 2.0));
                    q = Mod2Pi(a - b - t + Mod2Pi(p));
                    break;
                }
                default:   // LRL
                {
                    double tmp0 = (6.0 - d * d + 2.0 * cab + 2.0 * d * (sb - sa)) / 8.0;
                    if (Math.Abs(tmp0) > 1.0) return null;
                    double ph = DMath.Atan2(ca - cb, d + sa - sb);
                    p = Mod2Pi(DMath.TwoPi - Acos(tmp0));
                    t = Mod2Pi(-a - ph + p / 2.0);
                    q = Mod2Pi(Mod2Pi(b) - a - t + Mod2Pi(p));
                    break;
                }
            }
            return new DubinsPath(from, radius, w, t, p, q);
        }
    }
}
