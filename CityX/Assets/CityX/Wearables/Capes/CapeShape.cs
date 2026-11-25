using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// THE CAPE AT REST (docs/CAPE.md; owner, 2026-11-24: "make a basic cape"; 2026-11-25, with a picture of a LEGO minifigure's cloth cape: "make
    /// it fit something like this. no need to make the back wider just the shoulders neck part"): where each point sits on the standing body, in
    /// the body's own space (bind pose, metres, +z ahead, feet at 0). As a LEGO cape sits:
    /// <list type="bullet">
    /// <item>a YOKE over the shoulders (the first <see cref="YokeRows"/> rows of the grid), lying on the whole top of each shoulder from its
    /// front edge (<see cref="FrontEdge"/>, over the front of the shoulders) back to where the cloth falls (<see cref="BackEdge"/>), round
    /// behind the neck under the head; held to the body, as a LEGO cape's top is;</item>
    /// <item>a COLLAR closing it round the front of the neck under the chin (<see cref="CollarPoints"/> by 2), held to the body too;</item>
    /// <item>the CLOTH falling from the yoke's back edge behind the back and beside the arms to the calves, widening to the hem, which moves
    /// (<see cref="CapeCloth"/>).</item>
    /// </list>
    /// All of it the one colour. The edges were measured off the body (FusedBody, 2026-11-25): the shoulders' top is flat at 1.93 to 1.94 m out
    /// to 0.40 m, about 0.10 m in front of and behind the shoulder's middle, falling away at 0.43 m; the back of the neck is 0.14 m behind at
    /// 1.90 m; the head's ball (0.35 round about 2.25 m) comes down to 1.90 m. Pure, so it is tested without a scene.
    /// </summary>
    public static class CapeShape
    {
        public const int Columns = 15;

        /// <summary>Rows of the yoke (held), and rows of the cloth falling from it.</summary>
        public const int YokeRows = 5;
        public const int HangingRows = 16;
        public const int Rows = YokeRows + HangingRows;

        /// <summary>The yoke's front edge, half of it, from behind the neck (first) out to the shoulder's tip (last): round the side of the neck
        /// under the head, then along the front of the shoulder's top and down over its tip. Mirrored for the other side.</summary>
        public static readonly Vector3[] FrontEdge =
        {
            new Vector3(0.00f, 1.910f, -0.185f),
            new Vector3(0.11f, 1.918f, -0.155f),
            new Vector3(0.22f, 1.940f, 0.045f),
            new Vector3(0.34f, 1.900f, 0.130f),
            new Vector3(0.47f, 1.840f, 0.100f),
        };

        /// <summary>The yoke's back edge, where the cloth falls from: below the neck behind, along the back of the shoulders, over the tips.</summary>
        public static readonly Vector3[] BackEdge =
        {
            new Vector3(0.00f, 1.860f, -0.225f),
            new Vector3(0.11f, 1.880f, -0.210f),
            new Vector3(0.22f, 1.905f, -0.165f),
            new Vector3(0.34f, 1.905f, -0.150f),
            new Vector3(0.48f, 1.860f, -0.070f),
        };

        /// <summary>How much the yoke's middle rises above the straight way between its edges: over the round top of the shoulder, not through it
        /// (only over the shoulder: by the neck it would rise into the head).</summary>
        public const float YokeRise = 0.06f;

        /// <summary>The collar round the front of the neck: points along it, how far round it goes each way (degrees from straight ahead), its
        /// upper edge's reach out to the sides and forward, its middle's height and how much it rises to meet the yoke, how far down it lies and how
        /// much further out its lower edge is (it lies back on the top of the chest, facing up as the yoke does, so it is lit as the yoke is: stood
        /// upright it caught the light as a bright ring).</summary>
        public const int CollarPoints = 11;
        public const float CollarDegrees = 80.0f;
        public const float CollarSide = 0.225f;
        public const float CollarFront = 0.205f;
        public const float CollarHeight = 1.935f;
        public const float CollarRise = 0.02f;
        public const float CollarDrop = 0.075f;
        public const float CollarSpread = 1.14f;

        /// <summary>From the yoke's back edge to the hem, metres: the hem at the calves.</summary>
        public const float Length = 1.30f;

        /// <summary>How wide the hem is, half of it (the back as it was: the owner kept it).</summary>
        public const float HemHalfWidth = 0.60f;

        /// <summary>How far behind the body the middle of the hanging cloth lies, and how much nearer its sides come round.</summary>
        public const float HangBehind = 0.35f;
        public const float SidesComeRound = 0.19f;

        /// <summary>How much of the cloth's length it takes to go from the yoke's back edge out to where it hangs.</summary>
        public const float FallsAway = 0.15f;

        /// <summary>How far down the cloth it is held firmly to its shape (the shoulders' drape), as a share of its length.</summary>
        public const float ShoulderShare = 0.2f;

        public const int GridPoints = Columns * Rows;
        public const int Points = GridPoints + CollarPoints * 2;

        public static int GridIndex(int column, int row) => row * Columns + column;

        /// <summary>A point of the collar: <paramref name="bottom"/> its lower edge (a little further out), else its upper.</summary>
        public static int CollarIndex(int along, bool bottom) => GridPoints + (bottom ? CollarPoints : 0) + along;

        /// <summary>A half-edge at <paramref name="u"/> (-1 one shoulder's tip, 0 behind the neck, 1 the other's): a smooth curve through it.</summary>
        public static Vector3 Along(Vector3[] half, float u)
        {
            float along = Mathf.Abs(u) * (half.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(along), half.Length - 2);
            float f = along - i;
            Vector3 Point(int k)
            {
                if (k < 0) return new Vector3(-half[-k].x, half[-k].y, half[-k].z);   // the other side's, mirrored: the curve crosses the middle level
                return half[Mathf.Min(k, half.Length - 1)];
            }
            Vector3 p0 = Point(i - 1), p1 = Point(i), p2 = Point(i + 1), p3 = Point(i + 2);
            Vector3 p = 0.5f * (2.0f * p1 + (-p0 + p2) * f + (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * f * f + (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * f * f * f);
            if (u < 0.0f) p.x = -p.x;
            return p;
        }

        /// <summary>A point of the cape at rest: the yoke's rows across the shoulders, then the cloth falling from its back edge.</summary>
        public static Vector3 GridPoint(int column, int row)
        {
            float u = column / (Columns - 1.0f) * 2.0f - 1.0f;   // -1 one shoulder's tip, +1 the other's
            Vector3 back = Along(BackEdge, u);
            if (row < YokeRows)
            {
                float s = row / (YokeRows - 1.0f);
                float overShoulder = Mathf.SmoothStep(0.0f, 1.0f, (Mathf.Abs(u) - 0.3f) / 0.3f);
                return Vector3.Lerp(Along(FrontEdge, u), back, s) + Vector3.up * (YokeRise * overShoulder * Mathf.Sin(s * Mathf.PI));
            }
            float t = (row - (YokeRows - 1)) / (float)HangingRows;   // past the yoke: 0 its back edge, 1 the hem
            float hangX = u * Mathf.Lerp(BackEdge[BackEdge.Length - 1].x + 0.02f, HemHalfWidth, t);
            float hangZ = -HangBehind - 0.05f * t + SidesComeRound * u * u;
            float away = Mathf.SmoothStep(0.0f, 1.0f, t / FallsAway);
            return new Vector3(Mathf.Lerp(back.x, hangX, away), back.y - t * Length, Mathf.Lerp(back.z, hangZ, away));
        }

        /// <summary>A point of the collar at rest.</summary>
        public static Vector3 CollarPoint(int along, bool bottom)
        {
            float share = along / (CollarPoints - 1.0f) * 2.0f - 1.0f;
            float angle = share * CollarDegrees * Mathf.Deg2Rad;
            float lies = 1.0f - 0.85f * share * share;   // lying back on the chest in front, narrowing to its ends on the shoulders
            float reach = bottom ? 1.0f + (CollarSpread - 1.0f) * lies : 1.0f;
            float y = CollarHeight + CollarRise * share * share - (bottom ? CollarDrop * lies : 0.0f);
            return new Vector3(CollarSide * reach * Mathf.Sin(angle), y, CollarFront * reach * Mathf.Cos(angle));
        }

        /// <summary>The yoke's front corners, on the shoulders' tips.</summary>
        public static Vector3 Corner(bool first) => GridPoint(first ? 0 : Columns - 1, 0);

        /// <summary>Every point at rest, in the list's order: the grid, then the collar's upper and lower edges.</summary>
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
            for (int along = 0; along < CollarPoints; along++)
            {
                rest[CollarIndex(along, false)] = CollarPoint(along, false);
                rest[CollarIndex(along, true)] = CollarPoint(along, true);
            }
            return rest;
        }

        // ---- fitted to the body like a skin suit (owner, 2026-11-25: "make it fit to his shoulders like a skin suit. like the same dimensions of
        // ---- the shoulder just a tiny bit bigger so its not clipping through") ----

        /// <summary>How far off the body's surface the yoke and the collar lie, metres.</summary>
        public const float SkinGap = 0.015f;

        /// <summary>Whether a point is held on the body (the yoke and the collar), and so fitted to it.</summary>
        public static bool IsOnBody(int point) => point >= GridPoints || point / Columns < YokeRows;

        /// <summary>
        /// The cape at rest fitted to a body (<paramref name="skin"/>, <paramref name="normals"/>: its mesh at rest, in body space): each point of
        /// the yoke and the collar put <see cref="SkinGap"/> off the body's surface under it (the plane of the body's nearest point; the head's
        /// ball left out, and kept clear of), the yoke smoothed so it lies as cloth does; the falling cloth's first rows moved with the yoke's
        /// back edge. <paramref name="nearest"/> gives each fitted point's nearest body point (its bones are the ones it then follows), -1 for
        /// the rest.
        /// </summary>
        public static Vector3[] FitToBody(Vector3[] rest, Vector3[] skin, Vector3[] normals, out int[] nearest)
        {
            var head = new Vector3(0.0f, CityX.Character.BodyRig.HeadCentreY, 0.0f);
            float headClear = CityX.Character.FusedBodyShape.HeadRadius;
            var near = new System.Collections.Generic.List<int>();
            for (int i = 0; i < skin.Length; i++)
            {
                if (skin[i].y > 1.5f && Vector3.Distance(skin[i], head) > headClear + 0.005f) near.Add(i);
            }
            var fitted = (Vector3[])rest.Clone();
            nearest = new int[rest.Length];
            for (int i = 0; i < rest.Length; i++) nearest[i] = -1;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < rest.Length; i++)
                {
                    if (!IsOnBody(i)) continue;
                    int k = Nearest(fitted[i], skin, near);
                    Vector3 n = normals[k].normalized;
                    Vector3 onSurface = fitted[i] - n * Vector3.Dot(fitted[i] - skin[k], n);
                    Vector3 p = onSurface + n * SkinGap;
                    Vector3 fromHead = p - head;
                    if (fromHead.magnitude < headClear + SkinGap) p = head + fromHead.normalized * (headClear + SkinGap);
                    fitted[i] = p;
                    nearest[i] = k;
                }
                if (pass < 2) SmoothYoke(fitted);
            }
            // The falling cloth's first rows follow its top, the yoke's back edge, where it moved (the move fading out down the cloth).
            for (int column = 0; column < Columns; column++)
            {
                Vector3 moved = fitted[GridIndex(column, YokeRows - 1)] - rest[GridIndex(column, YokeRows - 1)];
                for (int row = YokeRows; row < Rows; row++)
                {
                    float t = (row - (YokeRows - 1)) / (float)HangingRows;
                    fitted[GridIndex(column, row)] += moved * (1.0f - Mathf.SmoothStep(0.0f, 1.0f, t / FallsAway));
                }
            }
            return fitted;
        }

        private static int Nearest(Vector3 p, Vector3[] skin, System.Collections.Generic.List<int> among)
        {
            int best = among[0];
            float bestDistance = float.MaxValue;
            foreach (int i in among)
            {
                float d = (skin[i] - p).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Each inside point of the yoke drawn halfway to its neighbours' middle: a body's small bumps not copied into the cloth.</summary>
        private static void SmoothYoke(Vector3[] points)
        {
            var smoothed = (Vector3[])points.Clone();
            for (int row = 0; row < YokeRows; row++)
            {
                for (int column = 1; column + 1 < Columns; column++)
                {
                    Vector3 sum = points[GridIndex(column - 1, row)] + points[GridIndex(column + 1, row)];
                    int count = 2;
                    if (row > 0) { sum += points[GridIndex(column, row - 1)]; count++; }
                    if (row + 1 < YokeRows) { sum += points[GridIndex(column, row + 1)]; count++; }
                    smoothed[GridIndex(column, row)] = Vector3.Lerp(points[GridIndex(column, row)], sum / count, 0.5f);
                }
            }
            System.Array.Copy(smoothed, points, points.Length);
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

        /// <summary>The drive at a share of the falling cloth's length (0 the yoke's back edge, 1 the hem).</summary>
        public static float DriveAt(float t) => t <= ShoulderShare
            ? Mathf.Lerp(DriveAtTop, DriveWhereItHangs, t / ShoulderShare)
            : Mathf.Lerp(DriveWhereItHangs, DriveAtHem, (t - ShoulderShare) / (1.0f - ShoulderShare));

        /// <summary>
        /// The cape's cloth starting at <paramref name="rest"/> (<see cref="RestPoints"/> placed on the body): the weave, its diagonals and fold
        /// links; the yoke and the collar held (the body carries them); each falling point tethered to its column's top, the yoke's back edge.
        /// </summary>
        public static CapeCloth MakeCloth(Vector3[] rest)
        {
            var cloth = new CapeCloth(rest, Columns, Rows);
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    int i = GridIndex(column, row);
                    if (row < YokeRows)
                    {
                        cloth.Hold(i, rest[i]);
                        continue;
                    }
                    if (column + 1 < Columns) cloth.Join(i, GridIndex(column + 1, row), WeaveStiffness);
                    cloth.Join(i, GridIndex(column, row - 1), WeaveStiffness);
                    if (column + 1 < Columns)
                    {
                        cloth.Join(i, GridIndex(column + 1, row - 1), ShearStiffness);
                        cloth.Join(GridIndex(column + 1, row), GridIndex(column, row - 1), ShearStiffness);
                    }
                    if (column + 2 < Columns) cloth.Join(i, GridIndex(column + 2, row), FoldStiffness);
                    cloth.Join(i, GridIndex(column, row - 2), FoldStiffness);
                    cloth.Drive[i] = DriveAt((row - (YokeRows - 1)) / (float)HangingRows);
                }
            }
            // Long-range tethers: each falling point no further from the yoke's back edge than the cloth between them.
            for (int column = 0; column < Columns; column++)
            {
                float along = 0.0f;
                for (int row = YokeRows; row < Rows; row++)
                {
                    along += Vector3.Distance(rest[GridIndex(column, row - 1)], rest[GridIndex(column, row)]);
                    cloth.TetherTo(GridIndex(column, row), GridIndex(column, YokeRows - 1), along * CapeCloth.TetherGive);
                }
            }
            for (int i = GridPoints; i < Points; i++)
            {
                cloth.Hold(i, rest[i]);
            }
            return cloth;
        }
    }
}
