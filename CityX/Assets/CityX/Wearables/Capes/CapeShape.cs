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
    /// <item>the CLOTH falling from the yoke's back edge behind the back and beside the arms to the calves, widening to the hem, which swings
    /// (<see cref="CapeSwing"/>, <see cref="CapeSkin"/>).</item>
    /// </list>
    /// All of it the one colour. The edges were measured off the body (FusedBody, 2026-11-25): the shoulders' top is flat at 1.93 to 1.94 m out
    /// to 0.40 m, about 0.10 m in front of and behind the shoulder's middle, falling away at 0.43 m; the back of the neck is 0.14 m behind at
    /// 1.90 m; the head's ball (0.35 round about 2.25 m) comes down to 1.90 m. Pure, so it is tested without a scene.
    /// </summary>
    public static class CapeShape
    {
        public const int Columns = 21;

        /// <summary>Rows of the yoke (held), and rows of the cloth falling from it.</summary>
        public const int YokeRows = 5;
        public const int HangingRows = 16;
        public const int Rows = YokeRows + HangingRows;

        /// <summary>
        /// The yoke's front edge, half of it, from behind the neck (first) out to the shoulder's tip (last): round the side of the neck under the
        /// head, out from the collar's lower edge and along the front of the shoulder to its tip, as the owner drew it (2026-11-25: "follow the
        /// black line i drew and fill in the missing material": no skin between the collar and the yoke). Mirrored for the other side.
        /// </summary>
        public static readonly Vector3[] FrontEdge =
        {
            new Vector3(0.00f, 1.910f, -0.185f),
            new Vector3(0.13f, 1.905f, -0.140f),
            new Vector3(0.23f, FrontLineAt(0.23f), 0.160f),
            new Vector3(0.33f, FrontLineAt(0.33f), 0.135f),
            new Vector3(0.43f, FrontLineAt(0.43f), 0.085f),
        };

        /// <summary>
        /// The cape's lower edge in front, as the owner drew it (2026-11-25, a black line over a picture: "make it a straight line across",
        /// "trim it as needed and fill material as needed match the black line"): seen from in front, straight from each shoulder's tip down to the
        /// collar's lower edge in the middle. Its height there (<see cref="FrontLine"/>), and how much higher it is at the shoulder's tip
        /// (<see cref="FrontLineRise"/>, at <see cref="FrontLineTip"/> across).
        /// </summary>
        public const float FrontLine = 1.865f;
        public const float FrontLineRise = 0.07f;
        public const float FrontLineTip = 0.43f;

        /// <summary>The front line's height at <paramref name="x"/> across: a straight line from the middle up to each shoulder's tip.</summary>
        public static float FrontLineAt(float x) => FrontLine + FrontLineRise * Mathf.Min(1.0f, Mathf.Abs(x) / FrontLineTip);

        /// <summary>The yoke's back edge, where the cloth falls from: below the neck behind, along the back of the shoulders, over the tips.</summary>
        public static readonly Vector3[] BackEdge =
        {
            new Vector3(0.00f, 1.860f, -0.225f),
            new Vector3(0.11f, 1.880f, -0.210f),
            new Vector3(0.22f, 1.905f, -0.165f),
            new Vector3(0.34f, 1.905f, -0.150f),
            new Vector3(0.44f, 1.860f, -0.070f),
        };

        /// <summary>How much the yoke's middle rises above the straight way between its edges: over the round top of the shoulder, not through it
        /// (only over the shoulder: by the neck it would rise into the head).</summary>
        public const float YokeRise = 0.06f;

        /// <summary>The collar round the front of the neck: points along it, how far round it goes each way (degrees from straight ahead), its
        /// upper edge's reach out to the sides and forward (its ends tucked under the yoke, so no edge shows where they meet), its middle's height and
        /// how much it rises to meet the yoke, and how much further out its lower edge is, at <see cref="FrontLine"/> (it lies back on the top of the
        /// chest, facing up as the yoke does, so it is lit as the yoke is: stood upright it caught the light as a bright ring).</summary>
        public const int CollarPoints = 11;
        public const float CollarDegrees = 90.0f;
        public const float CollarSide = 0.225f;
        public const float CollarFront = 0.205f;
        public const float CollarHeight = 1.935f;
        public const float CollarRise = 0.02f;
        public const float CollarSpread = 1.14f;
        public const float CollarCornerReach = 0.35f;

        /// <summary>From the yoke's back edge to the hem, metres: the hem at the calves.</summary>
        public const float Length = 1.30f;

        /// <summary>How wide the hem is, half of it (the back as it was: the owner kept it).</summary>
        public const float HemHalfWidth = 0.60f;

        /// <summary>How far behind the body the middle of the hanging cloth lies, and how much nearer its sides come round.</summary>
        public const float HangBehind = 0.35f;
        public const float SidesComeRound = 0.19f;

        /// <summary>How much of the cloth's length it takes to go from the yoke's back edge out to where it hangs.</summary>
        public const float FallsAway = 0.15f;

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
            float lies = 1.0f - 0.4f * share * share;   // lying back on the chest in front, a little narrower where it meets the yoke
            // Its lower corners reach on out along the front line under the yoke (curled back round the neck, their ends showed as a notch).
            float reach = bottom ? 1.0f + (CollarSpread - 1.0f) * lies + CollarCornerReach * share * share * share * share : 1.0f;
            float x = CollarSide * reach * Mathf.Sin(angle);
            float y = bottom ? FrontLineAt(x) : CollarHeight + CollarRise * share * share;
            return new Vector3(x, y, CollarFront * reach * Mathf.Cos(angle));
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
        public const float SkinGap = 0.022f;

        /// <summary>How far off the body the collar lies: under the yoke where they overlap.</summary>
        public const float CollarGap = 0.012f;

        /// <summary>Whether a point is held on the body (the yoke and the collar), and so fitted to it.</summary>
        public static bool IsOnBody(int point) => point >= GridPoints || point / Columns < YokeRows;

        /// <summary>
        /// The cape at rest fitted to a body (<paramref name="skin"/>, <paramref name="normals"/>: its mesh at rest, in body space): each point of
        /// the yoke and the collar put <see cref="SkinGap"/> off the body's surface under it (the plane of the body's nearest point; the head's
        /// ball left out), the yoke smoothed so it lies as cloth does; the falling cloth's first rows moved with the yoke's
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
                    // The collar a little nearer the body than the yoke: where its ends tuck under the yoke the yoke covers them cleanly (at
                    // one distance the two fought and the outline drew specks).
                    Vector3 p = onSurface + n * (i >= GridPoints ? CollarGap : SkinGap);
                    // Off the body first: beside the neck the head sits almost on the shoulder, and pushed out of the head the cloth went into
                    // the shoulder (holes of skin) or its rows crossed (specks). Tucked a little under the head there, the head hides it.
                    fitted[i] = p;
                    nearest[i] = k;
                }
                if (pass < 2) SmoothYoke(fitted);
            }
            FollowYoke(fitted, rest);
            return fitted;
        }

        /// <summary>The falling cloth's first rows moved with its top, the yoke's back edge, where it moved from <paramref name="before"/> (the move fading out down the cloth).</summary>
        private static void FollowYoke(Vector3[] fitted, Vector3[] before)
        {
            for (int column = 0; column < Columns; column++)
            {
                Vector3 moved = fitted[GridIndex(column, YokeRows - 1)] - before[GridIndex(column, YokeRows - 1)];
                for (int row = YokeRows; row < Rows; row++)
                {
                    float t = (row - (YokeRows - 1)) / (float)HangingRows;
                    fitted[GridIndex(column, row)] += moved * (1.0f - Mathf.SmoothStep(0.0f, 1.0f, t / FallsAway));
                }
            }
        }

        // ---- over what is worn (owner, 2026-11-25: "make cape fit over shirt without clipping"; "the shoulders on the cape still clip through
        // ---- and i saw some of the lower back as well") ----

        /// <summary>How far outside the clothes under it (a shirt, a coat) the yoke and the collar lie, metres.</summary>
        public const float CoverGap = 0.012f;

        /// <summary>How far round a point, along the body, the clothes under it are looked at.</summary>
        public const float CoverReach = 0.06f;

        /// <summary>How far above the skin clothes are looked for under a point (a hood or a fur collar can stand well out).</summary>
        public const float CoverHeight = 0.2f;

        /// <summary>
        /// The cape fitted to the bare body (<see cref="FitToBody"/>: <paramref name="fitted"/>, with each point's nearest body point and the body's
        /// <paramref name="normals"/>) lifted over the clothes worn on it (<paramref name="cover"/>: their points at rest, in body space): each
        /// point of the yoke and the collar raised off the skin to <see cref="CoverGap"/> outside the highest of the clothes under it, the lifts
        /// spread across the yoke so the cloth does not crease where a seam or a collar stands out; the falling cloth's first rows follow.
        /// </summary>
        public static Vector3[] FitOver(Vector3[] fitted, int[] nearest, Vector3[] normals, Vector3[] cover)
        {
            var lifted = (Vector3[])fitted.Clone();
            if (cover == null || cover.Length == 0)
            {
                return lifted;
            }
            var high = new System.Collections.Generic.List<Vector3>();
            foreach (Vector3 c in cover)
            {
                if (c.y > 1.5f) high.Add(c);
            }
            var lift = new float[fitted.Length];
            var up = new Vector3[fitted.Length];
            for (int i = 0; i < fitted.Length; i++)
            {
                if (!IsOnBody(i) || nearest[i] < 0) continue;
                Vector3 n = normals[nearest[i]].normalized;
                float gap = i >= GridPoints ? CollarGap : SkinGap;
                Vector3 foot = fitted[i] - n * gap;
                float top = float.MinValue;
                foreach (Vector3 c in high)
                {
                    Vector3 d = c - foot;
                    float h = Vector3.Dot(d, n);
                    if (h < -0.02f || h > CoverHeight || h <= top) continue;
                    if ((d - n * h).sqrMagnitude < CoverReach * CoverReach) top = h;
                }
                lift[i] = top > float.MinValue ? Mathf.Max(0.0f, top + CoverGap - gap) : 0.0f;
                up[i] = n;
            }
            for (int pass = 0; pass < 2; pass++)
            {
                SpreadLift(lift);
            }
            for (int i = 0; i < fitted.Length; i++)
            {
                lifted[i] += up[i] * lift[i];
            }
            FollowYoke(lifted, fitted);
            return lifted;
        }

        /// <summary>Each lift raised to its neighbours' mean where that is higher, across the yoke and along the collar: a lift spreads and is never lowered.</summary>
        private static void SpreadLift(float[] lift)
        {
            var was = (float[])lift.Clone();
            for (int row = 0; row < YokeRows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    float sum = 0.0f;
                    int count = 0;
                    foreach ((int c, int r) in new[] { (column - 1, row), (column + 1, row), (column, row - 1), (column, row + 1) })
                    {
                        if (c < 0 || c >= Columns || r < 0 || r >= YokeRows) continue;
                        sum += was[GridIndex(c, r)];
                        count++;
                    }
                    int i = GridIndex(column, row);
                    lift[i] = Mathf.Max(was[i], sum / count);
                }
            }
            for (int along = 0; along < CollarPoints; along++)
            {
                foreach (bool bottom in new[] { false, true })
                {
                    float sum = was[CollarIndex(along, !bottom)];
                    int count = 1;
                    if (along > 0)
                    {
                        sum += was[CollarIndex(along - 1, bottom)];
                        count++;
                    }
                    if (along + 1 < CollarPoints)
                    {
                        sum += was[CollarIndex(along + 1, bottom)];
                        count++;
                    }
                    int i = CollarIndex(along, bottom);
                    lift[i] = Mathf.Max(was[i], sum / count);
                }
            }
        }

        /// <summary>How near the clothes a hanging point is looked at (further off, it is clear of them).</summary>
        public const float HangingReach = 0.1f;

        /// <summary>
        /// The hanging part kept outside the clothes (<paramref name="cover"/>, <paramref name="coverNormals"/>: their points at rest and which way
        /// each faces): a point nearer the clothes' surface than <see cref="CoverGap"/>, or under it, is moved out along it to that, the moves
        /// spread to the points round it so the cloth stays smooth. With no cloth to push it out as it swings, the cape at rest must clear them
        /// (it went into a shirt at the small of the back).
        /// </summary>
        public static Vector3[] ClearHanging(Vector3[] points, Vector3[] cover, Vector3[] coverNormals)
        {
            var cleared = (Vector3[])points.Clone();
            if (cover == null || cover.Length == 0 || coverNormals == null || coverNormals.Length != cover.Length)
            {
                return cleared;
            }
            float top = float.MinValue, bottom = float.MaxValue;
            for (int i = GridIndex(0, YokeRows); i < GridPoints; i++)
            {
                top = Mathf.Max(top, points[i].y);
                bottom = Mathf.Min(bottom, points[i].y);
            }
            var near = new System.Collections.Generic.List<int>();
            for (int j = 0; j < cover.Length; j++)
            {
                if (cover[j].y > bottom - HangingReach && cover[j].y < top + HangingReach && cover[j].z < 0.1f) near.Add(j);
            }
            if (near.Count == 0)
            {
                return cleared;
            }
            var lift = new float[points.Length];
            var way = new Vector3[points.Length];
            for (int i = GridIndex(0, YokeRows); i < GridPoints; i++)
            {
                int k = Nearest(points[i], cover, near);
                if ((cover[k] - points[i]).sqrMagnitude > HangingReach * HangingReach) continue;
                Vector3 n = coverNormals[k].normalized;
                float off = Vector3.Dot(points[i] - cover[k], n);
                if (off < CoverGap)
                {
                    lift[i] = CoverGap - off;
                    way[i] = n;
                }
            }
            for (int pass = 0; pass < 3; pass++)
            {
                var was = (float[])lift.Clone();
                var wayWas = (Vector3[])way.Clone();
                for (int row = YokeRows; row < Rows; row++)
                {
                    for (int column = 0; column < Columns; column++)
                    {
                        int i = GridIndex(column, row);
                        foreach ((int c, int r) in new[] { (column - 1, row), (column + 1, row), (column, row - 1), (column, row + 1) })
                        {
                            if (c < 0 || c >= Columns || r < YokeRows || r >= Rows) continue;
                            int j = GridIndex(c, r);
                            if (was[j] * 0.5f > lift[i])
                            {
                                lift[i] = was[j] * 0.5f;
                                way[i] = wayWas[j];
                            }
                        }
                    }
                }
            }
            for (int i = 0; i < points.Length; i++)
            {
                cleared[i] += way[i] * lift[i];
            }
            return cleared;
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

        /// <summary>How far out from behind the neck (as <see cref="Along"/>'s u) the yoke lies under the head.</summary>
        public const float NeckSide = 0.45f;

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
                    float u = Mathf.Abs(column / (Columns - 1.0f) * 2.0f - 1.0f);
                    if (row > 0 && row + 1 < YokeRows)
                    {
                        // Inside the yoke: across and down.
                        sum += points[GridIndex(column, row - 1)] + points[GridIndex(column, row + 1)];
                        count += 2;
                    }
                    else if (row == 0 && u < NeckSide)
                    {
                        // The front edge beside and behind the neck, under the head: smoothed down too, or the rows cross there where the head
                        // pushes the cloth out. Over the shoulders it is smoothed only along itself, so it keeps the owner's line.
                        sum += points[GridIndex(column, 1)];
                        count += 1;
                    }
                    smoothed[GridIndex(column, row)] = Vector3.Lerp(points[GridIndex(column, row)], sum / count, 0.5f);
                }
            }
            System.Array.Copy(smoothed, points, points.Length);
        }
    }
}
