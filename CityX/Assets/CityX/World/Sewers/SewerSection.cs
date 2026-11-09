using System;
using System.Collections.Generic;
using CityX.World.Plan;

namespace CityX.World
{
    /// <summary>How big a tunnel is: by the road over it, as Paris sizes its galleries by the street (docs/SEWERS.md).</summary>
    public enum SewerKind
    {
        /// <summary>Under an ordinary street.</summary>
        Gallery = 0,

        /// <summary>Under an arterial.</summary>
        Collector = 1,

        /// <summary>Under a boulevard: the flood tunnels.</summary>
        Trunk = 2,
    }

    /// <summary>What a tunnel is built as. A family picked by the seed, so the underground is not one tunnel repeated.</summary>
    public enum SewerStyle
    {
        /// <summary>Straight brick walls under a half-round brick vault.</summary>
        BrickVault = 0,

        /// <summary>Taller stone walls under a shallow stone arch.</summary>
        StoneArch = 1,

        /// <summary>A round concrete pipe with a flat floor laid in it.</summary>
        RoundPipe = 2,

        /// <summary>A concrete box with its top corners cut off.</summary>
        ConcreteBox = 3,
    }

    /// <summary>What one face of a tunnel's section is, for its look.</summary>
    public enum SewerSurface
    {
        Walkway = 0,
        ChannelSide = 1,
        ChannelBed = 2,
        Wall = 3,
        Vault = 4,
    }

    /// <summary>
    /// A tunnel's cross-section: the outline of the space inside it, looking along the tunnel, x across (the left negative) and y up from the
    /// walkways. Every tunnel has the owner's reference shape in it: a channel down the middle and a walkway each side
    /// (docs/research/sewers_2026_11_09/refs). The sizes are for the third-person camera, which wants about 5 m of width and 4.5 m of height.
    /// </summary>
    public sealed class SewerSection
    {
        public readonly SewerKind Kind;
        public readonly SewerStyle Style;

        /// <summary>The widest the inside gets, each side of the middle.</summary>
        public readonly double HalfWidth;

        /// <summary>Half the width of the floor (the walkways' outer edges).</summary>
        public readonly double FloorHalfWidth;

        /// <summary>How high the crown is over the walkways.</summary>
        public readonly double Crown;

        public readonly double ChannelHalfWidth;
        public readonly double ChannelDepth;

        /// <summary>The outline, a closed loop: along the channel's bed left to right, up the right side, over the top and down the left.</summary>
        public readonly Vec2[] Outline;

        /// <summary><c>Surfaces[i]</c> is the face from <c>Outline[i]</c> to the next point.</summary>
        public readonly SewerSurface[] Surfaces;

        /// <summary>The opening it leaves in a wall across it: from the right foot of the wall over the crown to the left foot, at the walkways' height.</summary>
        public readonly Vec2[] Opening;

        private SewerSection(SewerKind kind, SewerStyle style, double channelHalf, double channelDepth, List<Vec2> upper, List<SewerSurface> upperSurfaces)
        {
            Kind = kind;
            Style = style;
            ChannelHalfWidth = channelHalf;
            ChannelDepth = channelDepth;
            Opening = upper.ToArray();
            FloorHalfWidth = upper[0].X;

            var outline = new List<Vec2> { new Vec2(-channelHalf, -channelDepth), new Vec2(channelHalf, -channelDepth), new Vec2(channelHalf, 0.0) };
            var surfaces = new List<SewerSurface> { SewerSurface.ChannelBed, SewerSurface.ChannelSide, SewerSurface.Walkway };
            outline.AddRange(upper);
            surfaces.AddRange(upperSurfaces);
            surfaces.Add(SewerSurface.Walkway);
            outline.Add(new Vec2(-channelHalf, 0.0));
            surfaces.Add(SewerSurface.ChannelSide);
            Outline = outline.ToArray();
            Surfaces = surfaces.ToArray();

            foreach (Vec2 p in Outline)
            {
                HalfWidth = Math.Max(HalfWidth, Math.Abs(p.X));
                Crown = Math.Max(Crown, p.Y);
            }
        }

        /// <summary>The widest any tunnel of a kind is, each side of its middle: what the plan lays out, whatever the style.</summary>
        public static double HalfWidthOf(SewerKind kind) => kind == SewerKind.Trunk ? 4.6 : kind == SewerKind.Collector ? 3.6 : 3.0;

        private static readonly SewerSection[] Sections = BuildAll();

        public static SewerSection For(SewerKind kind, SewerStyle style) => Sections[(int)kind * 4 + (int)style];

        private static SewerSection[] BuildAll()
        {
            var all = new SewerSection[12];
            for (int kind = 0; kind < 3; kind++)
            {
                for (int style = 0; style < 4; style++)
                {
                    all[kind * 4 + style] = Build((SewerKind)kind, (SewerStyle)style);
                }
            }
            return all;
        }

        private static SewerSection Build(SewerKind kind, SewerStyle style)
        {
            double half = HalfWidthOf(kind);
            // The crown over the walkways, and the channel, by size.
            double crown = kind == SewerKind.Trunk ? 6.6 : kind == SewerKind.Collector ? 6.2 : 5.2;
            double channelHalf = kind == SewerKind.Trunk ? 1.6 : kind == SewerKind.Collector ? 1.4 : 1.0;
            double channelDepth = kind == SewerKind.Trunk ? 0.4 : kind == SewerKind.Collector ? 0.6 : 0.45;
            var upper = new List<Vec2>();
            var surfaces = new List<SewerSurface>();
            switch (style)
            {
                case SewerStyle.RoundPipe:
                {
                    // A circle as wide as the kind allows, the floor laid across it a little under its middle.
                    double radius = half;
                    double centre = Math.Min(crown - radius, radius * 0.62);
                    double floorHalf = Math.Sqrt(radius * radius - centre * centre);
                    double from = Math.Atan2(-centre, floorHalf), to = Math.PI - from;
                    AddArc(upper, surfaces, 0.0, centre, radius, from, to, 20, SewerSurface.Vault);
                    break;
                }
                case SewerStyle.ConcreteBox:
                {
                    double haunch = 0.9;
                    upper.Add(new Vec2(half, 0.0));
                    surfaces.Add(SewerSurface.Wall);
                    upper.Add(new Vec2(half, crown - haunch));
                    surfaces.Add(SewerSurface.Vault);
                    upper.Add(new Vec2(half - haunch, crown));
                    surfaces.Add(SewerSurface.Vault);
                    upper.Add(new Vec2(-half + haunch, crown));
                    surfaces.Add(SewerSurface.Vault);
                    upper.Add(new Vec2(-half, crown - haunch));
                    surfaces.Add(SewerSurface.Wall);
                    upper.Add(new Vec2(-half, 0.0));
                    break;
                }
                default:
                {
                    // Straight walls, then an arc over: a whole half-circle for the brick vault, a shallow one for the stone arch.
                    double wall = style == SewerStyle.BrickVault ? half - 0.2 : half - 0.1;
                    double rise = style == SewerStyle.BrickVault ? wall : wall * 0.55;
                    double spring = crown - rise;
                    double radius = (wall * wall + rise * rise) / (2.0 * rise);
                    double centre = crown - radius;
                    double from = Math.Atan2(spring - centre, wall), to = Math.PI - from;
                    upper.Add(new Vec2(wall, 0.0));
                    surfaces.Add(SewerSurface.Wall);
                    AddArc(upper, surfaces, 0.0, centre, radius, from, to, style == SewerStyle.BrickVault ? 14 : 10, SewerSurface.Vault);
                    surfaces.Add(SewerSurface.Wall);
                    upper.Add(new Vec2(-wall, 0.0));
                    break;
                }
            }
            return new SewerSection(kind, style, channelHalf, channelDepth, upper, surfaces);
        }

        /// <summary>The points of an arc from one angle to another (anticlockwise, radians), and the face after each but the last.</summary>
        private static void AddArc(List<Vec2> points, List<SewerSurface> surfaces, double cx, double cy, double radius, double from, double to, int pieces, SewerSurface surface)
        {
            for (int i = 0; i <= pieces; i++)
            {
                double angle = from + (to - from) * i / pieces;
                points.Add(new Vec2(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle)));
                if (i < pieces)
                {
                    surfaces.Add(surface);
                }
            }
        }

        /// <summary>How high the inside is over the walkways at a distance across from the middle (0 outside it).</summary>
        public double HeightAt(double x)
        {
            double best = 0.0;
            for (int i = 0; i + 1 < Opening.Length; i++)
            {
                Vec2 a = Opening[i], b = Opening[i + 1];
                double lo = Math.Min(a.X, b.X), hi = Math.Max(a.X, b.X);
                if (x < lo - 1e-9 || x > hi + 1e-9) continue;
                double y = hi - lo < 1e-9 ? Math.Max(a.Y, b.Y) : a.Y + (b.Y - a.Y) * (x - a.X) / (b.X - a.X);
                best = Math.Max(best, y);
            }
            return best;
        }
    }
}
