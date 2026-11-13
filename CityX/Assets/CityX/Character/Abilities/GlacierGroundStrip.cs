using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A see-through strip laid on the ground along a Glacier Spikes line, as wide as the spikes' band (docs/GLACIER_SPIKES.md): the aiming
    /// arrow (with a pointed tip past the end) and the frost the front leaves behind it. Its colour is in its vertices, five across (soft
    /// outer edge, bright edge, middle, bright edge, soft outer edge), so it needs no texture. Built in world space; laying and painting it
    /// again makes no garbage.
    /// </summary>
    public sealed class GlacierGroundStrip
    {
        public const int Columns = 5;

        /// <summary>
        /// The most the ground beside the line may differ from the line's own and still carry the strip's edge, metres: a kerb. Deeper (a
        /// basement's well beside a pavement) and the edge stays at the line's height, or the strip would dive under the pavement.
        /// </summary>
        private const float KerbHeight = 0.4f;

        /// <summary>The rows that narrow the arrow's tip to a point.</summary>
        public const int TipRows = 3;

        public static readonly int MaxRows = GlacierSpikesPath.MaxSamples + TipRows;

        /// <summary>Where each column sits across the band, in half-widths.</summary>
        private static readonly float[] Across = { -1.0f, -0.82f, 0.0f, 0.82f, 1.0f };

        /// <summary>Which columns are the band's edge (bright) and which its outer rim (gone).</summary>
        private static readonly bool[] Outer = { true, false, false, false, true };
        private static readonly bool[] Edge = { false, true, false, true, false };

        private readonly Vector3[] _vertices = new Vector3[MaxRows * Columns];
        private readonly Color32[] _colours = new Color32[MaxRows * Columns];
        private readonly Vector2[] _uvs = new Vector2[MaxRows * Columns];
        private readonly float[] _rowDistance = new float[MaxRows];
        private readonly int[] _triangles = new int[(MaxRows - 1) * (Columns - 1) * 6];

        public Mesh Mesh { get; }

        public int Rows { get; private set; }

        public GlacierGroundStrip(string name)
        {
            Mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            Mesh.MarkDynamic();
            for (int i = 0; i < _uvs.Length; i++)
            {
                _uvs[i] = new Vector2(0.5f, 0.5f);   // the middle of the soft spot: the colour is the vertices'
            }
            int t = 0;
            for (int row = 0; row < MaxRows - 1; row++)
            {
                for (int column = 0; column < Columns - 1; column++)
                {
                    int a = row * Columns + column;
                    int b = a + Columns;
                    _triangles[t++] = a;
                    _triangles[t++] = b;
                    _triangles[t++] = a + 1;
                    _triangles[t++] = a + 1;
                    _triangles[t++] = b;
                    _triangles[t++] = b + 1;
                }
            }
        }

        /// <summary>The distance down the line of row <paramref name="row"/>, metres (past the line's length on the arrow's tip).</summary>
        public float RowDistance(int row) => _rowDistance[Mathf.Clamp(row, 0, MaxRows - 1)];

        /// <summary>
        /// Lays the strip along <paramref name="path"/>, <paramref name="lift"/> above the ground, each row level across: on the highest of
        /// the ground at its middle and at its two edges when <paramref name="overSteps"/> (the arrow, which must never be hidden under a kerb
        /// or a step beside the line), else on the lowest (the frost, which must never float). With <paramref name="tipLength"/> above 0 a
        /// pointed tip runs that far past the end.
        /// </summary>
        public void Lay(GlacierSpikesPath path, IGlacierGround ground, float lift, float tipLength, bool overSteps)
        {
            float length = path.Length;
            int lineRows = Mathf.Clamp(Mathf.CeilToInt(length / GlacierSpikesRules.GroundStep) + 1, 2, GlacierSpikesPath.MaxSamples);
            int rows = lineRows + (tipLength > 0.0f ? TipRows : 0);
            Vector3 side = Vector3.Cross(Vector3.up, path.Direction);
            float rowY = path.Start.y;
            for (int row = 0; row < rows; row++)
            {
                bool tip = row >= lineRows;
                float distance = tip ? length + tipLength * (row - lineRows + 1) / TipRows : length * row / (lineRows - 1);
                float share = length > 0.0f ? Mathf.Clamp01(distance / length) : 0.0f;
                float half = GlacierSpikesRules.HalfWidth(share) * (tip ? 1.0f - (float)(row - lineRows + 1) / TipRows : 1.0f);
                Vector3 middle = path.Start + path.Direction * distance;
                if (!tip)
                {
                    float centre = path.HeightAt(distance);
                    float left = GroundBeside(ground, middle - side * half * 0.9f, centre);
                    float right = GroundBeside(ground, middle + side * half * 0.9f, centre);
                    rowY = overSteps ? Mathf.Max(centre, Mathf.Max(left, right)) : Mathf.Min(centre, Mathf.Min(left, right));
                }
                _rowDistance[row] = distance;
                for (int column = 0; column < Columns; column++)
                {
                    Vector3 at = middle + side * (Across[column] * half);
                    _vertices[row * Columns + column] = new Vector3(at.x, rowY + lift, at.z);
                }
            }
            Rows = rows;
            Mesh.Clear();
            Mesh.SetVertices(_vertices, 0, rows * Columns);
            Mesh.SetUVs(0, _uvs, 0, rows * Columns);
            Mesh.SetColors(_colours, 0, rows * Columns);
            Mesh.SetTriangles(_triangles, 0, (rows - 1) * (Columns - 1) * 6, 0);
            Mesh.RecalculateBounds();
        }

        /// <summary>
        /// Colours the strip: <paramref name="edge"/> along its two bright edges, <paramref name="middle"/> between them, nothing at the outer
        /// rim; shown down to <paramref name="upTo"/> metres (fading over the last <paramref name="head"/>), faded in over the first <paramref name="tail"/>,
        /// all times <paramref name="strength"/>.
        /// </summary>
        public void Paint(Color edge, Color middle, float upTo, float head, float tail, float strength)
        {
            for (int row = 0; row < Rows; row++)
            {
                float shown = head > 0.0f ? Mathf.Clamp01((upTo - _rowDistance[row]) / head) : (_rowDistance[row] <= upTo ? 1.0f : 0.0f);
                shown *= tail > 0.0f ? Mathf.Clamp01(_rowDistance[row] / tail) : 1.0f;
                for (int column = 0; column < Columns; column++)
                {
                    Color colour = Edge[column] ? edge : middle;
                    colour.a *= Outer[column] ? 0.0f : shown * Mathf.Clamp01(strength);
                    _colours[row * Columns + column] = colour;
                }
            }
            Mesh.SetColors(_colours, 0, Rows * Columns);
        }

        private static float GroundBeside(IGlacierGround ground, Vector3 point, float lineY)
        {
            point.y = lineY + GlacierSpikesRules.StepUp;
            return ground != null && ground.TryGround(point, GlacierSpikesRules.StepUp + 1.0f, out float y) && Mathf.Abs(y - lineY) < KerbHeight ? y : lineY;
        }
    }
}
