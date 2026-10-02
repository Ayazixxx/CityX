using System;
using System.Collections.Generic;

namespace CityX.World.Plan
{
    /// <summary>
    /// Every tunable of the city plan in one place.
    ///
    /// DETERMINISM RULES (the plan is grown by a chaotic process, so a single last-bit difference between a
    /// server and a client becomes a different city):
    ///  * double arithmetic with + - * / and Math.Sqrt only. No Sin/Cos/Atan2/Pow/Exp -- angles come from
    ///    PlanMath's literal tables and PseudoAngle;
    ///  * integer hashing for every random decision, keyed by something stable (a node's creation index,
    ///    a coordinate), never by iteration order of a Dictionary or HashSet;
    ///  * a priority queue broken by a creation sequence number, so two items of equal priority always pop in the
    ///    same order.
    /// If a platform's JIT ever fuses multiply-adds the plan can still diverge; the plan is small enough (a few
    /// thousand edges) that a server can simply send it. Nothing here depends on that, it is only the fallback.
    /// </summary>
    public sealed class PlanConfig
    {
        /// <summary>The square of world the plan covers, centred on the origin: from -Half to +Half on each axis.</summary>
        public double Half = 1500.0;

        // ---- terrain ----
        public double SeaLevel = 0.0;
        public double HillAmplitude = 55.0;
        public double HillWavelength = 1500.0;

        /// <summary>Wavelength of the river-carving noise; its zero crossings are the rivers.</summary>
        public double RiverWavelength = 1700.0;

        /// <summary>Half the river band in noise units. About 0.035 gives rivers in the 60-100 m range.</summary>
        public double RiverBand = 0.035;

        /// <summary>Below this noise value the land is a lake or sea.</summary>
        public double LakeLevel = -0.62;

        // ---- population ----
        public double PopulationWavelength = 1900.0;

        /// <summary>How strongly the origin is pulled into a downtown (spawn is always downtown).</summary>
        public double OriginBoost = 0.75;
        public double OriginRadius = 650.0;

        // ---- road widths (carriageway half width) and the pavement either side ----
        public double HighwayHalfWidth = 10.0;
        public double ArterialHalfWidth = 7.5;
        public double StreetHalfWidth = 5.5;
        public double SidewalkWidth = 3.0;

        // ---- growth ----
        public int MaxNodes = 9000;

        /// <summary>Segments shorter than this are never placed.</summary>
        public double MinSegment = 28.0;

        /// <summary>A new road end this close to an existing junction joins it.</summary>
        public double SnapNode = 36.0;

        /// <summary>A new road end this close to an existing road is joined onto it as a T.</summary>
        public double SnapEdge = 24.0;

        /// <summary>Roads leaving a junction closer than this (cosine) to an existing one are rejected as slivers.</summary>
        public double MaxSliverCos = 0.87;

        public double MaxBridge = 280.0;

        /// <summary>The classic city is 1: every length in the plan is generated this many times larger (see <see cref="CityScale"/>).</summary>
        public double Scale = 1.0;

        /// <summary>How much taller buildings are than in the classic city.</summary>
        public double HeightScale = 1.0;

        /// <summary>
        /// The configuration of the game's own city at a scale: the classic numbers with every length multiplied (roads only by the square root,
        /// terrain height likewise), so the plan is the classic plan drawn bigger.
        /// </summary>
        public static PlanConfig For(CityScale scale)
        {
            double s = scale.Footprint;
            double r = scale.RoadWidth;
            var cfg = new PlanConfig { Scale = s, HeightScale = scale.Height };
            cfg.Half = 2500.0 * s;
            cfg.HillAmplitude = 30.0 * r;
            cfg.HillWavelength *= s;
            cfg.RiverWavelength *= s;
            cfg.PopulationWavelength *= s;
            cfg.OriginRadius *= s;
            cfg.HighwayHalfWidth *= r;
            cfg.ArterialHalfWidth *= r;
            cfg.StreetHalfWidth *= r;
            cfg.SidewalkWidth *= r;
            cfg.MinSegment *= s;
            cfg.SnapNode *= s;
            cfg.SnapEdge *= s;
            cfg.MaxBridge *= s;
            return cfg;
        }
    }

    /// <summary>
    /// The ground, the water and the population, as functions of position alone.
    ///
    /// THESE ARE GLOBAL AND SEAMLESS, which is the whole trick for an endless world: a river or a hill does not
    /// care which region of road network it is in, because it is noise, so two neighbouring regions agree about
    /// the land without exchanging anything. Only the roads need care at region borders.
    /// </summary>
    public sealed class PlanFields
    {
        private readonly PlanConfig _cfg;
        private readonly uint _seedHills;
        private readonly uint _seedRiver;
        private readonly uint _seedPop;
        private readonly uint _seedLake;

        public PlanFields(int worldSeed, PlanConfig cfg)
        {
            _cfg = cfg;
            uint s = unchecked((uint)worldSeed);
            _seedHills = PlanNoise.Mix(s, 0x68696C6CU);   // "hill"
            _seedRiver = PlanNoise.Mix(s, 0x72697665U);   // "rive"
            _seedPop = PlanNoise.Mix(s, 0x706F7075U);     // "popu"
            _seedLake = PlanNoise.Mix(s, 0x6C616B65U);    // "lake"
        }

        public PlanConfig Config => _cfg;

        /// <summary>The river noise: rivers lie where its absolute value is small.</summary>
        public double RiverNoise(double x, double y)
        {
            double k = 1.0 / _cfg.RiverWavelength;
            return PlanNoise.Fbm(x * k, y * k, 2, _seedRiver);
        }

        /// <summary>Lake/sea noise: the land is under water where this is below LakeLevel.</summary>
        public double LakeNoise(double x, double y)
        {
            double k = 1.0 / (_cfg.HillWavelength * 1.7);
            return PlanNoise.Fbm(x * k, y * k, 3, _seedLake);
        }

        public bool IsWater(double x, double y)
        {
            if (Math.Abs(RiverNoise(x, y)) < _cfg.RiverBand)
            {
                return true;
            }
            return LakeNoise(x, y) < _cfg.LakeLevel;
        }

        public bool IsWater(Vec2 p) => IsWater(p.X, p.Y);

        /// <summary>Ground height in metres above sea level (the water's own bed is below zero).</summary>
        public double Height(double x, double y)
        {
            double k = 1.0 / _cfg.HillWavelength;
            double hills = PlanNoise.Fbm(x * k, y * k, 4, _seedHills);   // -1..1
            double h = _cfg.SeaLevel + 18.0 + hills * _cfg.HillAmplitude;

            double river = Math.Abs(RiverNoise(x, y));
            if (river < _cfg.RiverBand * 2.2)
            {
                // The banks shelve down into the river.
                double t = river / (_cfg.RiverBand * 2.2);
                double bank = _cfg.SeaLevel - 6.0 + (h - _cfg.SeaLevel + 6.0) * t * t;
                if (bank < h)
                {
                    h = bank;
                }
            }
            return h;
        }

        /// <summary>How much of a city this place wants to be, 0 to 1. Water is zero.</summary>
        public double Population(double x, double y)
        {
            if (IsWater(x, y))
            {
                return 0.0;
            }
            double k = 1.0 / _cfg.PopulationWavelength;
            double n = PlanNoise.Fbm(x * k, y * k, 3, _seedPop) * 0.5 + 0.5;   // 0..1
            n = PlanMath.Clamp01((n - 0.18) * 1.55);
            n = n * n * (3.0 - 2.0 * n);

            // The origin is always downtown.
            double d2 = (x * x + y * y) / (_cfg.OriginRadius * _cfg.OriginRadius);
            double boost = _cfg.OriginBoost / ((1.0 + d2) * (1.0 + d2));
            double pop = n + boost * (1.0 - n * 0.35);

            // Steep ground is not built on.
            return PlanMath.Clamp01(pop);
        }

        public double Population(Vec2 p) => Population(p.X, p.Y);

        /// <summary>How steep the ground is here, as rise over run measured across ~30 m.</summary>
        public double Slope(double x, double y)
        {
            double e = 15.0 * _cfg.Scale;
            double dx = Height(x + e, y) - Height(x - e, y);
            double dy = Height(x, y + e) - Height(x, y - e);
            return Math.Sqrt(dx * dx + dy * dy) / (2.0 * e);
        }

        /// <summary>
        /// The urban centres: the high points of the population map, found on a coarse grid, strongest first and
        /// never closer together than <paramref name="separation"/>.
        /// </summary>
        public List<Vec2> FindCentres(double half, double step, double separation, double minimum, int max)
        {
            var found = new List<KeyValuePair<double, Vec2>>();
            int n = (int)(2.0 * half / step);
            for (int iy = 1; iy < n; iy++)
            {
                for (int ix = 1; ix < n; ix++)
                {
                    double x = -half + ix * step;
                    double y = -half + iy * step;
                    double p = Population(x, y);
                    if (p < minimum)
                    {
                        continue;
                    }
                    bool peak = true;
                    for (int oy = -1; oy <= 1 && peak; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if ((ox != 0 || oy != 0) && Population(x + ox * step, y + oy * step) > p)
                            {
                                peak = false;
                                break;
                            }
                        }
                    }
                    if (peak)
                    {
                        found.Add(new KeyValuePair<double, Vec2>(p, new Vec2(x, y)));
                    }
                }
            }

            // Strongest first; ties by position so the order is the same everywhere.
            found.Sort((a, b) =>
            {
                int c = b.Key.CompareTo(a.Key);
                if (c != 0) return c;
                c = a.Value.X.CompareTo(b.Value.X);
                return c != 0 ? c : a.Value.Y.CompareTo(b.Value.Y);
            });

            var centres = new List<Vec2>();
            foreach (var candidate in found)
            {
                bool tooClose = false;
                foreach (Vec2 c in centres)
                {
                    if ((c - candidate.Value).Length < separation)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose)
                {
                    centres.Add(candidate.Value);
                    if (centres.Count >= max)
                    {
                        break;
                    }
                }
            }
            return centres;
        }
    }

    /// <summary>
    /// Gradient noise from integer hashing and arithmetic only (see PlanConfig's determinism rules).
    /// </summary>
    public static class PlanNoise
    {
        // Twelve gradient directions as literals (the classic Perlin set, scaled to unit-ish length).
        private static readonly double[] GradX = { 1, -1, 1, -1, 1, -1, 1, -1, 0, 0, 0, 0 };
        private static readonly double[] GradY = { 1, 1, -1, -1, 0, 0, 0, 0, 1, -1, 1, -1 };

        public static uint Mix(uint a, uint b)
        {
            unchecked
            {
                uint h = a * 0x9E3779B1U ^ b * 0x85EBCA77U;
                h ^= h >> 15;
                h *= 0x2C1B3C6DU;
                h ^= h >> 12;
                h *= 0x297A2D39U;
                h ^= h >> 15;
                return h;
            }
        }

        private static double Gradient(int ix, int iy, double dx, double dy, uint seed)
        {
            uint h = Mix(Mix(seed, unchecked((uint)ix)), unchecked((uint)iy));
            int g = (int)(h % 12U);
            return GradX[g] * dx + GradY[g] * dy;
        }

        private static double Fade(double t) => t * t * t * (t * (t * 6.0 - 15.0) + 10.0);

        private static int FloorInt(double v)
        {
            int i = (int)v;
            return v < i ? i - 1 : i;
        }

        /// <summary>One octave, roughly -1..1.</summary>
        public static double Noise(double x, double y, uint seed)
        {
            int x0 = FloorInt(x);
            int y0 = FloorInt(y);
            double fx = x - x0;
            double fy = y - y0;

            double n00 = Gradient(x0, y0, fx, fy, seed);
            double n10 = Gradient(x0 + 1, y0, fx - 1.0, fy, seed);
            double n01 = Gradient(x0, y0 + 1, fx, fy - 1.0, seed);
            double n11 = Gradient(x0 + 1, y0 + 1, fx - 1.0, fy - 1.0, seed);

            double u = Fade(fx);
            double v = Fade(fy);
            double nx0 = n00 + (n10 - n00) * u;
            double nx1 = n01 + (n11 - n01) * u;
            return nx0 + (nx1 - nx0) * v;
        }

        /// <summary>Layered noise, normalised to roughly -1..1.</summary>
        public static double Fbm(double x, double y, int octaves, uint seed)
        {
            double sum = 0.0;
            double amp = 1.0;
            double norm = 0.0;
            double fx = x, fy = y;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(fx, fy, Mix(seed, (uint)o)) * amp;
                norm += amp;
                amp *= 0.5;
                fx = fx * 2.0 + 17.3;
                fy = fy * 2.0 + 9.1;
            }
            return sum / norm * 1.35;   // a single Perlin octave rarely strays past +-0.75; stretch it toward +-1
        }
    }
}
