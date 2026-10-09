using System;

namespace CityX.World
{
    /// <summary>
    /// The outline of a round or many-sided building (BuildingSite.Sides): a regular polygon of <see cref="Sides"/> sides round the building's centre, in its
    /// own frame, with one side facing +Z (the front, where the door is). The building's Width and Depth are the circle the corners lie on.
    ///
    /// EACH SIDE IS BUILT AS A SHORT FLAT WALL (CityMeshBuilder turns the frame to it), so windows you can shoot, doors and frames work on a round tower exactly as
    /// on a box; a side's windows are the cells of window "wall" <see cref="WindowGrid.PolygonSide"/>(i).
    ///
    /// Angles are measured from +X toward +Z. Side i faces the direction at 90 degrees + i * 360 / Sides. Uses Math.Cos/Sin: this is drawing and hit-testing,
    /// done the same way by everyone; the plan itself (where buildings are) stays free of trigonometry.
    /// </summary>
    public readonly struct RoundFootprint
    {
        public readonly int Sides;

        /// <summary>From the centre to a corner.</summary>
        public readonly float Radius;

        /// <summary>From the centre to the middle of a side: where the walls stand.</summary>
        public readonly float Apothem;

        public readonly float SideLength;

        /// <summary>
        /// Half the side of the square that fits inside the outline with room to spare: where the stairs go, and the roof plant. 0.70 of the apothem fits
        /// every polygon the city uses (6, 8, 12, 16, 20 sides).
        /// </summary>
        public readonly float InnerHalf;

        public const float InnerShare = 0.70f;

        public RoundFootprint(int sides, float radius)
        {
            Sides = sides;
            Radius = radius;
            Apothem = radius * (float)BuildingSite.CosPiOver(sides);
            SideLength = 2.0f * radius * (float)Math.Sin(Math.PI / sides);
            InnerHalf = Apothem * InnerShare;
        }

        public static RoundFootprint Of(BuildingSite site) => new RoundFootprint(site.Sides, site.Params.Width * 0.5f);

        /// <summary>The direction side i faces, as an angle.</summary>
        public double SideAngle(int side) => Math.PI * 0.5 + side * 2.0 * Math.PI / Sides;

        /// <summary>The outward direction of side i (unit, in the building's own frame).</summary>
        public void SideNormal(int side, out float nx, out float nz)
        {
            double a = SideAngle(side);
            nx = (float)Math.Cos(a);
            nz = (float)Math.Sin(a);
        }

        /// <summary>The angle of corner k, which lies between side k and side k + 1.</summary>
        public double CornerAngle(int corner) => Math.PI * 0.5 + (corner + 0.5) * 2.0 * Math.PI / Sides;

        /// <summary>The side a direction from the centre passes through.</summary>
        public int SideAt(double angle)
        {
            double step = 2.0 * Math.PI / Sides;
            int k = (int)Math.Round((angle - Math.PI * 0.5) / step);
            return ((k % Sides) + Sides) % Sides;
        }

        /// <summary>The side nearest a point given relative to the centre in the building's own frame.</summary>
        public int SideAt(float x, float z) => SideAt(Math.Atan2(z, x));

        /// <summary>How far the outline is from the centre in a direction.</summary>
        public float BoundaryRadius(double angle)
        {
            return Apothem / (float)Math.Cos(angle - SideAngle(SideAt(angle)));
        }
    }
}
