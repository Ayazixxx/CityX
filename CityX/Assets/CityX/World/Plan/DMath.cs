using System;

namespace CityX.World.Plan
{
    /// <summary>
    /// Sine, cosine and arctangent from plain arithmetic, the same to the last bit on every machine.
    ///
    /// The plan is a pure function of the seed that every player computes for themselves (server and clients never send geometry), and
    /// Math.Sin/Math.Cos/Math.Atan2 are not guaranteed bit-identical across platforms and runtimes -- which is why the rest of the plan turns
    /// only by tabled 5-degree steps (PlanMath.RotateSteps). The expressways need real curves (the ring, a ramp's arcs, a loop), so they get
    /// these: range reduction by exact multiples, then a fixed-length polynomial, using only +, -, *, / and Math.Sqrt (which IEEE 754
    /// requires to be correctly rounded). Accurate to about 1e-15 -- far below a millimetre on any curve in the city.
    /// </summary>
    public static class DMath
    {
        public const double Pi = 3.14159265358979323846;
        public const double TwoPi = 6.28318530717958647692;
        public const double HalfPi = 1.57079632679489661923;
        private const double QuarterPi = 0.78539816339744830962;
        private const double SixthPi = 0.52359877559829887308;
        private const double Sqrt3 = 1.73205080756887729353;
        private const double TanTwelfthPi = 0.26794919243112270647;   // 2 - sqrt(3)

        /// <summary>An angle brought into -pi..pi.</summary>
        public static double Wrap(double a)
        {
            double k = Math.Floor((a + Pi) / TwoPi);
            return a - k * TwoPi;
        }

        public static double Sin(double a)
        {
            a = Wrap(a);
            // Into -pi/2..pi/2 by sin(pi - a) = sin(a).
            if (a > HalfPi) a = Pi - a;
            else if (a < -HalfPi) a = -Pi - a;
            if (a > QuarterPi) return CosPoly(HalfPi - a);
            if (a < -QuarterPi) return -CosPoly(HalfPi + a);
            return SinPoly(a);
        }

        public static double Cos(double a)
        {
            a = Wrap(a);
            double sign = 1.0;
            if (a > HalfPi)
            {
                a = Pi - a;
                sign = -1.0;
            }
            else if (a < -HalfPi)
            {
                a = -Pi - a;
                sign = -1.0;
            }
            if (a > QuarterPi) return sign * SinPoly(HalfPi - a);
            if (a < -QuarterPi) return sign * SinPoly(HalfPi + a);
            return sign * CosPoly(a);
        }

        /// <summary>Taylor series of sine to x^17, for |x| at most pi/4 (error under 1e-17).</summary>
        private static double SinPoly(double x)
        {
            double x2 = x * x;
            return x * (1.0 - x2 / 6.0 * (1.0 - x2 / 20.0 * (1.0 - x2 / 42.0 * (1.0 - x2 / 72.0 * (1.0 - x2 / 110.0 * (1.0 - x2 / 156.0 * (1.0 - x2 / 210.0 * (1.0 - x2 / 272.0))))))));
        }

        /// <summary>Taylor series of cosine to x^18, for |x| at most pi/4.</summary>
        private static double CosPoly(double x)
        {
            double x2 = x * x;
            return 1.0 - x2 / 2.0 * (1.0 - x2 / 12.0 * (1.0 - x2 / 30.0 * (1.0 - x2 / 56.0 * (1.0 - x2 / 90.0 * (1.0 - x2 / 132.0 * (1.0 - x2 / 182.0 * (1.0 - x2 / 240.0 * (1.0 - x2 / 306.0))))))));
        }

        /// <summary>The angle of (x, y) from the +X axis toward +Y, -pi..pi.</summary>
        public static double Atan2(double y, double x)
        {
            if (x == 0.0 && y == 0.0) return 0.0;
            double ax = Math.Abs(x), ay = Math.Abs(y);
            // atan of the smaller over the larger (0..1), then by symmetry to the right octant and quadrant.
            bool swap = ay > ax;
            double z = swap ? ax / ay : ay / ax;
            double r = AtanUnit(z);
            if (swap) r = HalfPi - r;
            if (x < 0.0) r = Pi - r;
            return y < 0.0 ? -r : r;
        }

        /// <summary>atan(z) for z in 0..1: reduced below tan(pi/12) by atan(z) = pi/6 + atan((sqrt3 z - 1)/(sqrt3 + z)), then its series.</summary>
        private static double AtanUnit(double z)
        {
            double offset = 0.0;
            if (z > TanTwelfthPi)
            {
                z = (Sqrt3 * z - 1.0) / (Sqrt3 + z);
                offset = SixthPi;
            }
            double z2 = z * z;
            // z - z^3/3 + z^5/5 - ... to z^29: |z| <= 0.268 leaves an error under 1e-18.
            double sum = 0.0;
            double term = z;
            for (int k = 0; k < 15; k++)
            {
                double piece = term / (2 * k + 1);
                sum += (k & 1) == 0 ? piece : -piece;
                term *= z2;
            }
            return offset + sum;
        }

        /// <summary>The unit direction at an angle.</summary>
        public static Vec2 Dir(double angle) => new Vec2(Cos(angle), Sin(angle));

        /// <summary>The angle of a direction.</summary>
        public static double Angle(Vec2 v) => Atan2(v.Y, v.X);

        /// <summary>The signed turn from one direction to another, -pi..pi (positive counter-clockwise).</summary>
        public static double Turn(Vec2 from, Vec2 to) => Atan2(Vec2.Cross(from, to), Vec2.Dot(from, to));
    }
}
