using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// THE CAPE AT REST (docs/CAPE.md; owner, 2026-11-24: "make a basic cape", then, drawing it on a picture of the body from the front and from
    /// behind: "get rid of the yellow string and make it like my drawing", "model it based on my design"): where each point of the cloth sits on
    /// the standing body, in the body's own space (bind pose, metres, +z ahead, feet at 0). A grid of <see cref="Columns"/> by <see cref="Rows"/>
    /// points. Its top edge lies ON the shoulders, as the owner drew it: from one shoulder's tip along the top of the shoulder in to under the
    /// head, round behind the neck, and out along the other shoulder to its tip, so from in front it shows as a band over each shoulder (a
    /// rolled hem, <see cref="EdgeRadius"/>). Below the shoulders the cloth falls behind the back to the calves, widening to the hem. The cloth
    /// moves (<see cref="CapeCloth"/>); this is the shape it is drawn back towards and starts from. Pure, so it is tested without a scene.
    /// </summary>
    public static class CapeShape
    {
        public const int Columns = 15;
        public const int Rows = 18;

        /// <summary>
        /// The top edge, half of it, from behind the neck (first) out to the shoulder's tip (last), measured off the body (FusedBody, 2026-11-25:
        /// the shoulders' top is flat at 1.93 to 1.94 m out to 0.40 m, falling away at 0.43; the back of the neck at 1.90 m is 0.14 m behind; the
        /// head's ball, 0.35 round about 2.25 m, comes down to 1.90 m). Each point 3 cm off the body and clear of the head; mirrored for the other
        /// side.
        /// </summary>
        public static readonly Vector3[] TopEdge =
        {
            new Vector3(0.00f, 1.910f, -0.185f),
            new Vector3(0.11f, 1.925f, -0.165f),
            new Vector3(0.22f, 1.950f, -0.080f),
            new Vector3(0.33f, 1.975f, -0.005f),
            new Vector3(0.44f, 1.945f, 0.000f),
        };

        /// <summary>The rolled hem along the top edge, its thickness: the band over the shoulders in the owner's drawing.</summary>
        public const float EdgeRadius = 0.022f;

        /// <summary>From the top edge to the hem, metres: the hem at the calves.</summary>
        public const float Length = 1.32f;

        /// <summary>How wide the hem is, half of it.</summary>
        public const float HemHalfWidth = 0.60f;

        /// <summary>How far behind the body the middle of the hanging cloth lies, and how much nearer its sides come round.</summary>
        public const float HangBehind = 0.35f;
        public const float SidesComeRound = 0.19f;

        /// <summary>How far down from the top the cloth lies over the shoulders (where it is held firmly to its shape).</summary>
        public const float ShoulderShare = 0.2f;

        /// <summary>
        /// How much of the cloth's length goes back over the shoulders, in a quarter circle from the top edge to behind them (it falls from
        /// there): a straight line from the top edge to behind the shoulder cut through the shoulder's round top.
        /// </summary>
        public const float OverTheShoulder = 0.2f;

        public static int GridIndex(int column, int row) => row * Columns + column;

        /// <summary>The top edge at <paramref name="u"/> (-1 one shoulder's tip, 0 behind the neck, 1 the other's): a smooth curve through <see cref="TopEdge"/>.</summary>
        public static Vector3 TopPoint(float u)
        {
            float along = Mathf.Abs(u) * (TopEdge.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(along), TopEdge.Length - 2);
            float f = along - i;
            Vector3 p0 = Edge(i - 1), p1 = Edge(i), p2 = Edge(i + 1), p3 = Edge(i + 2);
            // Catmull-Rom through the points (the point before the first is its mirror: the curve crosses the middle level).
            Vector3 p = 0.5f * (2.0f * p1 + (-p0 + p2) * f + (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * f * f + (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * f * f * f);
            if (u < 0.0f) p.x = -p.x;
            return p;
        }

        private static Vector3 Edge(int i)
        {
            if (i < 0)
            {
                Vector3 mirror = TopEdge[-i];
                return new Vector3(-mirror.x, mirror.y, mirror.z);
            }
            return TopEdge[Mathf.Min(i, TopEdge.Length - 1)];
        }

        /// <summary>A point of the cloth at rest.</summary>
        public static Vector3 GridPoint(int column, int row)
        {
            float u = column / (Columns - 1.0f) * 2.0f - 1.0f;   // -1 one edge, +1 the other
            float t = row / (Rows - 1.0f);                      // 0 the top, 1 the hem
            Vector3 top = TopPoint(u);
            float hangX = u * Mathf.Lerp(TopEdge[TopEdge.Length - 1].x + 0.04f, HemHalfWidth, t);
            float hangZ = -HangBehind - 0.05f * t + SidesComeRound * u * u;
            float back = Mathf.Max(0.05f, top.z - hangZ);
            if (t <= OverTheShoulder)
            {
                // Over the shoulder: a quarter circle back and down from the top edge.
                float a = t / OverTheShoulder * Mathf.PI * 0.5f;
                return new Vector3(Mathf.Lerp(top.x, hangX, Mathf.Sin(a)), top.y - back * (1.0f - Mathf.Cos(a)), top.z - back * Mathf.Sin(a));
            }
            float below = (t - OverTheShoulder) / (1.0f - OverTheShoulder) * (Length - back * Mathf.PI * 0.5f);
            return new Vector3(hangX, top.y - back - below, hangZ);
        }

        /// <summary>The top edge's ends: the cape's corners on the shoulders' tips.</summary>
        public static Vector3 Corner(bool first) => GridPoint(first ? 0 : Columns - 1, 0);

        public const int Points = Columns * Rows;

        /// <summary>Every point at rest, in the grid's order.</summary>
        public static Vector3[] RestPoints()
        {
            var rest = new Vector3[Points];
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    rest[GridIndex(column, row)] = GridPoint(column, row);
                }
            }
            return rest;
        }

        /// <summary>Stiffness of the links across and down the weave, across its diagonals, and across two points (its resistance to folding).</summary>
        public const float WeaveStiffness = 1.0f;
        public const float ShearStiffness = 0.6f;
        public const float FoldStiffness = 0.22f;

        /// <summary>
        /// How much of the way back to its shape at rest a point is drawn each step: firmly where the cape lies over the shoulders, gently where
        /// it starts to hang, hardly at the hem (it swings most there). At 90 steps a second a share s pulls like a spring of s x 8100 per
        /// second squared: 0.002 is about one and a half times gravity a metre out of place, 0.0004 a third of it.
        /// </summary>
        public const float DriveAtTop = 0.06f;
        public const float DriveWhereItHangs = 0.002f;
        public const float DriveAtHem = 0.0004f;

        /// <summary>The drive at a row's share of the way down (0 the top, 1 the hem).</summary>
        public static float DriveAt(float t) => t <= ShoulderShare
            ? Mathf.Lerp(DriveAtTop, DriveWhereItHangs, t / ShoulderShare)
            : Mathf.Lerp(DriveWhereItHangs, DriveAtHem, (t - ShoulderShare) / (1.0f - ShoulderShare));

        /// <summary>
        /// The cape's cloth starting at <paramref name="rest"/> (<see cref="RestPoints"/> placed on the body): the weave, its diagonals and fold
        /// links, each point tethered to its column's top, and the top edge held (the body carries it).
        /// </summary>
        public static CapeCloth MakeCloth(Vector3[] rest)
        {
            var cloth = new CapeCloth(rest, Columns, Rows);
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    int i = GridIndex(column, row);
                    if (column + 1 < Columns) cloth.Join(i, GridIndex(column + 1, row), WeaveStiffness);
                    if (row + 1 < Rows) cloth.Join(i, GridIndex(column, row + 1), WeaveStiffness);
                    if (column + 1 < Columns && row + 1 < Rows)
                    {
                        cloth.Join(i, GridIndex(column + 1, row + 1), ShearStiffness);
                        cloth.Join(GridIndex(column + 1, row), GridIndex(column, row + 1), ShearStiffness);
                    }
                    if (column + 2 < Columns) cloth.Join(i, GridIndex(column + 2, row), FoldStiffness);
                    if (row + 2 < Rows) cloth.Join(i, GridIndex(column, row + 2), FoldStiffness);
                    cloth.Drive[i] = DriveAt(row / (Rows - 1.0f));
                }
            }
            // Long-range tethers: each point no further from its column's top than the cloth between them.
            for (int column = 0; column < Columns; column++)
            {
                float along = 0.0f;
                for (int row = 1; row < Rows; row++)
                {
                    along += Vector3.Distance(rest[GridIndex(column, row - 1)], rest[GridIndex(column, row)]);
                    cloth.TetherTo(GridIndex(column, row), GridIndex(column, 0), along * CapeCloth.TetherGive);
                }
                cloth.Hold(GridIndex(column, 0), rest[GridIndex(column, 0)]);
            }
            return cloth;
        }
    }
}
