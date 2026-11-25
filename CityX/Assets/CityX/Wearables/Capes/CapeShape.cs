using CityX.Character;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// THE CAPE AT REST (docs/CAPE.md; owner, 2026-11-24: "make a basic cape ... make it tied around the front"): where each point of the cloth sits
    /// on the standing body, in the body's own space (bind pose, metres, +z ahead, feet at 0). A grid of <see cref="Columns"/> by
    /// <see cref="Rows"/> points: its top row wraps round the back of the neck and over the shoulders to the front of each shoulder, where two
    /// cords run round the chest to a knot at the throat, and two short ends hang from the knot. Below the shoulders the cloth falls behind the
    /// back to the calves, widening to the hem. The cloth moves (<see cref="CapeCloth"/>); this is the shape it is drawn back towards and starts
    /// from. Pure, so it is tested without a scene.
    /// </summary>
    public static class CapeShape
    {
        public const int Columns = 13;
        public const int Rows = 18;

        /// <summary>The top edge: its height, how far it reaches out over the shoulders (x) and back behind the neck (z), and how far round.</summary>
        public const float TopHeight = BodyRig.ShoulderY - 0.06f;
        public const float TopHalfWidth = BodyRig.ShoulderX + 0.02f;
        public const float TopBehind = 0.25f;
        public const float WrapDegrees = 100.0f;

        /// <summary>From the top edge to the hem, metres: the hem at the calves.</summary>
        public const float Length = 1.22f;

        /// <summary>How wide the hem is, half of it.</summary>
        public const float HemHalfWidth = 0.58f;

        /// <summary>How far behind the body the middle of the hanging cloth lies, and how much nearer its sides come round.</summary>
        public const float HangBehind = 0.35f;
        public const float SidesComeRound = 0.19f;

        /// <summary>How far down from the top the cloth turns from lying on the shoulders to hanging.</summary>
        public const float ShoulderShare = 0.22f;

        /// <summary>The knot at the throat, in front of the chest under the head.</summary>
        public static readonly Vector3 Knot = new Vector3(0.0f, TopHeight - 0.08f, FusedBodyShape.TorsoRadius + 0.04f);

        /// <summary>Points in each cord from a shoulder to the knot (both ends included), and in each end hanging from the knot (the knot not).</summary>
        public const int CordPoints = 7;
        public const int EndPoints = 5;

        /// <summary>The spacing of an end's points, and how much longer than the way round each cord is (so it lies a little loose).</summary>
        public const float EndSpacing = 0.04f;
        public const float CordSlack = 1.04f;

        /// <summary>The cords' thickness.</summary>
        public const float CordRadius = 0.017f;
        public const float KnotRadius = 0.038f;

        public static int GridIndex(int column, int row) => row * Columns + column;

        /// <summary>A point of the cloth at rest.</summary>
        public static Vector3 GridPoint(int column, int row)
        {
            float u = column / (Columns - 1.0f) * 2.0f - 1.0f;   // -1 one edge, +1 the other
            float t = row / (Rows - 1.0f);                      // 0 the top, 1 the hem
            float theta = u * WrapDegrees * Mathf.Deg2Rad;
            var top = new Vector3(TopHalfWidth * Mathf.Sin(theta), 0.0f, -TopBehind * Mathf.Cos(theta));
            float halfWidth = Mathf.Lerp(TopHalfWidth + 0.05f, HemHalfWidth, t);
            var hanging = new Vector3(u * halfWidth, 0.0f, -HangBehind - 0.05f * t + SidesComeRound * u * u);
            float s = Mathf.SmoothStep(0.0f, 1.0f, t / ShoulderShare);
            Vector3 p = Vector3.Lerp(top, hanging, s);
            p.y = TopHeight - t * Length;
            return p;
        }

        /// <summary>The cloth's top-edge corners, where the cords start: the first and last points of the top row.</summary>
        public static Vector3 Corner(bool first) => GridPoint(first ? 0 : Columns - 1, 0);

        /// <summary>A point of a cord, from its shoulder corner (0) round the chest to the knot (<see cref="CordPoints"/> - 1).</summary>
        public static Vector3 CordPoint(bool first, int index)
        {
            Vector3 corner = Corner(first);
            float s = index / (CordPoints - 1.0f);
            float from = Mathf.Atan2(corner.x, corner.z), to = 0.0f;
            float angle = Mathf.Lerp(from, to, s);
            float radius = Mathf.Lerp(new Vector2(corner.x, corner.z).magnitude, Knot.z, s);
            return new Vector3(Mathf.Sin(angle) * radius, Mathf.Lerp(corner.y, Knot.y, s), Mathf.Cos(angle) * radius);
        }

        // ---- every point of the cape in one list: the grid, the knot, the cords' middles, the ends ----

        public const int GridPoints = Columns * Rows;
        public const int KnotIndex = GridPoints;

        /// <summary>Points in the whole cape.</summary>
        public const int Points = GridPoints + 1 + 2 * (CordPoints - 2) + 2 * EndPoints;

        /// <summary>A cord's middle point (1 .. <see cref="CordPoints"/> - 2; its ends are the cloth's corner and the knot).</summary>
        public static int CordIndex(bool first, int index) => KnotIndex + 1 + (first ? 0 : CordPoints - 2) + index - 1;

        public static int EndIndex(bool first, int index) => KnotIndex + 1 + 2 * (CordPoints - 2) + (first ? 0 : EndPoints) + index;

        /// <summary>Every point at rest, in the list's order.</summary>
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
            rest[KnotIndex] = Knot;
            foreach (bool first in new[] { true, false })
            {
                for (int i = 1; i < CordPoints - 1; i++) rest[CordIndex(first, i)] = CordPoint(first, i);
                for (int i = 0; i < EndPoints; i++) rest[EndIndex(first, i)] = EndPoint(first, i);
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
        public const float DriveOnCords = 0.04f;

        /// <summary>The drive at a row's share of the way down (0 the top, 1 the hem).</summary>
        public static float DriveAt(float t) => t <= ShoulderShare
            ? Mathf.Lerp(DriveAtTop, DriveWhereItHangs, t / ShoulderShare)
            : Mathf.Lerp(DriveWhereItHangs, DriveAtHem, (t - ShoulderShare) / (1.0f - ShoulderShare));

        /// <summary>
        /// The cape's cloth starting at <paramref name="rest"/> (<see cref="RestPoints"/> placed on the body): the weave, its diagonals and
        /// fold links, each point tethered to its column's top, the top edge and the knot held (the body carries them), the cords and ends.
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
            cloth.Hold(KnotIndex, rest[KnotIndex]);
            foreach (bool first in new[] { true, false })
            {
                int previous = GridIndex(first ? 0 : Columns - 1, 0);
                for (int i = 1; i < CordPoints - 1; i++)
                {
                    int point = CordIndex(first, i);
                    cloth.Join(previous, point, 1.0f, CordSlack);
                    cloth.Drive[point] = DriveOnCords;
                    previous = point;
                }
                cloth.Join(previous, KnotIndex, 1.0f, CordSlack);
                previous = KnotIndex;
                for (int i = 0; i < EndPoints; i++)
                {
                    int point = EndIndex(first, i);
                    cloth.Join(previous, point, 1.0f);
                    previous = point;
                }
            }
            return cloth;
        }

        /// <summary>A point of an end hanging from the knot (0 the first below it).</summary>
        public static Vector3 EndPoint(bool first, int index)
        {
            float side = first ? -1.0f : 1.0f;
            return Knot + new Vector3(side * 0.012f * (index + 1), -EndSpacing * (index + 1), 0.006f * (index + 1));
        }
    }
}
