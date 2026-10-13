using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The character's paint texture, and turning a click on the body into a
    /// brush stamp in it. Ported from character.gd's paint_at / sample_color_at
    /// / bucket_fill_at.
    /// </summary>
    public sealed class BodyPaint
    {
        public Texture2D Atlas { get; private set; }

        private Vector3[] _positions = new Vector3[0];
        private Vector2[] _uvs = new Vector2[0];
        private Color32[] _pixels;

        /// <summary>
        /// THE BRUSH IS MEASURED ON THE BODY, IN METRES (owner, 2026-10-10: "the brush sizes dont scale up naturally the first one feels very
        /// choppy ... appears as a line or ovel instead of a pixel or circle and then the 2nd brush size is way bigger"). The atlas is stretched
        /// unevenly over the body -- on an arm one texel is about 1.5 mm around and 8 mm along -- so any brush sized in atlas pixels is a sliver
        /// at size 1, and correcting that per triangle (what this was) blew size 2 up by the stretch ratio. As 3D paint tools do, every texel
        /// knows where it sits on the body (<see cref="EnsureTexelMap"/>) and a stamp paints every texel within the brush's radius of the hit:
        /// round on any part, straight across the seams between parts, and each size this much wider than the last.
        /// </summary>
        public const float BrushStepMetres = 0.007f;

        /// <summary>The brush's radius on the body for a slider size (1..15): size 1 is a dot about 1.4 cm across on the 2.6 m body.</summary>
        public static float BrushRadiusMetres(int size)
        {
            return BrushStepMetres * Mathf.Max(1, size);
        }

        /// <summary>How far apart the stamps of a stroke are: a fraction of the brush, so a line is smooth at every size.</summary>
        public static float StrokeSpacingMetres(int size)
        {
            return Mathf.Max(0.002f, BrushRadiusMetres(size) * StrokeSpacingOfRadius);
        }

        public const float StrokeSpacingOfRadius = 0.3f;

        /// <summary>
        /// A texel facing away from the brush by more than this (the dot of the two normals) is left alone, so a big brush on the front of an
        /// arm does not come out on its back. Slightly below zero so the brush still wraps a little round a curve.
        /// </summary>
        public const float FacingCutoff = -0.2f;

        /// <summary>When the brush is smaller than the texels where it lands, the nearest texel within this much of its edge is painted anyway.</summary>
        public const float NearestTexelSlackMetres = 0.02f;

        private const float GridCellMetres = 0.04f;

        /// <summary>Texels just outside every triangle borrow their neighbour's place, so filtering at a part's edge never blends in unpainted colour.</summary>
        private const int DilationPasses = 2;

        private Dictionary<Vector3Int, List<int>> _texelGrid;

        public BodyPaint(Color fill)
        {
            Atlas = new Texture2D(PaintAtlas.Width, PaintAtlas.Height, TextureFormat.RGBA32, false)
            {
                // LINEAR, not point: the atlas is low-res and point sampling
                // makes every stroke read as stair-stepped blocks.
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };

            _pixels = new Color32[PaintAtlas.Width * PaintAtlas.Height];
            Clear(fill);
        }

        /// <summary>
        /// Takes a whole atlas as it stands, for paint that came from
        /// somewhere other than this brush -- see GodotCharacterImport and
        /// OldGuyBuilder.
        ///
        /// Returns false and changes nothing if the size is wrong, for the same
        /// reason Decode does: an atlas of another shape would put an arm's
        /// paint on a leg, and a plain body beats a scrambled one.
        ///
        /// VERTICALLY FLIPPED, DELIBERATELY -- confirmed as a real bug live,
        /// not assumed. texels always arrives fresh from
        /// Texture2D.GetPixels32() on a just-decoded Godot PNG, both real
        /// callers pass it straight through. A PNG file's own row order is
        /// top-down (row 0 = the visual top -- confirmed directly against a
        /// real design's atlas_png, whose row 0 is unmistakably the drawn
        /// face), but GetPixels32() uses Unity's own BOTTOM-origin
        /// convention (confirmed directly with an EncodeToPNG/LoadImage
        /// round-trip: array index 0 comes back as whatever was set at the
        /// BOTTOM). PaintAtlas.RowOf gives the Head row index 0, and
        /// HeadMesh reads it starting from v=0 -- Unity's low v, i.e.
        /// GetPixels32 index 0 -- so without this flip the head sampled
        /// from the PNG's own bottom row (the last atlas part, a leg) the
        /// whole time. Rendered live as a flat, featureless head with no
        /// trace of the drawn face; the same face reappeared once this
        /// flip went in.
        /// </summary>
        public bool Adopt(Color32[] texels)
        {
            if (texels == null)
            {
                return false;
            }
            if (texels.Length == _pixels.Length)
            {
                Take(texels, PaintAtlas.Width, PaintAtlas.Height, flip: true);
            }
            else if (texels.Length == PaintAtlas.LegacyWidth * PaintAtlas.LegacyHeight)
            {
                // Every Godot design is Godot's size: scaled up to this one.
                Take(texels, PaintAtlas.LegacyWidth, PaintAtlas.LegacyHeight, flip: true);
            }
            else
            {
                return false;
            }
            Apply();
            return true;
        }

        public void Clear(Color fill)
        {
            Color32 texel = fill;
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = texel;
            }
            Apply();
        }

        public void Apply()
        {
            Atlas.SetPixels32(_pixels);
            Atlas.Apply(updateMipmaps: false);
        }

        /// <summary>
        /// The body's triangle soup, in the same order as the mesh -- so a
        /// raycast's triangleIndex indexes straight into it.
        /// </summary>
        public void Bind(Mesh mesh)
        {
            Soup(mesh, out _positions, out _uvs);
            _bodyTriangles = _positions.Length / 3;
            _texelGrid = null;
        }

        /// <summary>
        /// A mesh as triangle soup, corner by corner in triangle order, whether or not it shares vertices: since 2026-10-13 the body mesh is
        /// welded wherever position and UV agree (FusedBodyMesh.Weld), and reading its vertex array as soup made every triangle after the
        /// first few a scramble of unrelated corners -- a click painted nothing.
        /// </summary>
        private static void Soup(Mesh mesh, out Vector3[] positions, out Vector2[] uvs)
        {
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            positions = new Vector3[triangles.Length];
            uvs = new Vector2[triangles.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                positions[i] = vertices[triangles[i]];
                uvs[i] = uv.Length > 0 ? uv[triangles[i]] : Vector2.zero;
            }
        }

        /// <summary>
        /// The body AND the head, as one list of triangles.
        ///
        /// THE HEAD IS ITS OWN MESH ON ITS OWN COLLIDER -- see HeadMesh -- so a
        /// ray that hits it comes back with a triangle index counted from that
        /// mesh's own zero. Appending the head's triangles after the body's and
        /// offsetting by <see cref="BodyTriangleCount"/> keeps one flat index
        /// space, which is all the rest of this class ever wanted.
        ///
        /// The head's vertices are stored in the BODY's space, because that is
        /// the space every caller hands in a hit point in. It is a fixed
        /// transform: the head is unposed while it is being painted.
        /// </summary>
        public void Bind(Mesh body, Mesh head, Matrix4x4 headToBody)
        {
            Bind(body);
            if (head == null)
            {
                return;
            }

            Soup(head, out Vector3[] headPositions, out Vector2[] headUvs);

            var positions = new Vector3[_positions.Length + headPositions.Length];
            var uvs = new Vector2[_uvs.Length + headUvs.Length];
            _positions.CopyTo(positions, 0);
            _uvs.CopyTo(uvs, 0);

            for (int i = 0; i < headPositions.Length; i++)
            {
                positions[_positions.Length + i] = headToBody.MultiplyPoint3x4(headPositions[i]);
                uvs[_uvs.Length + i] = headUvs[i];
            }

            _positions = positions;
            _uvs = uvs;
            _texelGrid = null;
        }

        /// <summary>
        /// How many triangles belong to the body. A head hit's own triangle
        /// index is added to this.
        /// </summary>
        public int BodyTriangleCount => _bodyTriangles;

        private int _bodyTriangles;

        public int TriangleCount => _positions.Length / 3;

        /// <summary>Reads back whatever is painted where a ray hit.</summary>
        public Color SampleAt(Vector3 localPoint, int triangle)
        {
            if (!Corners(triangle, out Vector3 a, out Vector3 b, out Vector3 c,
                out Vector2 ua, out Vector2 ub, out Vector2 uc))
            {
                return Color.white;
            }

            Vector2 uv = Barycentric(localPoint, a, b, c, ua, ub, uc);
            uv.x = Mathf.Repeat(uv.x, 1.0f);
            return _pixels[PixelIndex(
                Mathf.Clamp(Mathf.FloorToInt(uv.x * PaintAtlas.Width), 0, PaintAtlas.Width - 1),
                Mathf.Clamp(Mathf.FloorToInt(uv.y * PaintAtlas.Height), 0, PaintAtlas.Height - 1))];
        }

        /// <summary>
        /// Stamps the brush where a ray hit the body: every texel within the brush's radius on the body (see <see cref="BrushStepMetres"/>).
        /// <paramref name="brushSize"/> is the slider's own 1..15.
        /// </summary>
        public void PaintAt(Vector3 localPoint, int triangle, Color colour, int brushSize)
        {
            if (TryNormalOf(triangle, out Vector3 normal))
            {
                StampSphere(localPoint, normal, BrushRadiusMetres(brushSize), colour);
            }
        }

        /// <summary>The same stamp on the other side of the body (the rig is symmetric about x = 0), for the mirror tool.</summary>
        public void PaintMirroredAt(Vector3 localPoint, int triangle, Color colour, int brushSize)
        {
            if (TryNormalOf(triangle, out Vector3 normal))
            {
                StampSphere(Mirror(localPoint), Mirror(normal), BrushRadiusMetres(brushSize), colour);
            }
        }

        /// <summary>
        /// Draws a stroke between two hits rather than two separate dots: stamps every <see cref="StrokeSpacingMetres"/> along the way, so the
        /// line is equally smooth at every brush size.
        /// </summary>
        public void PaintStroke(Vector3 fromLocal, int fromTriangle, Vector3 toLocal,
            int toTriangle, Color colour, int brushSize)
        {
            if (!TryNormalOf(toTriangle, out Vector3 toNormal))
            {
                return;
            }
            Vector3 fromNormal = TryNormalOf(fromTriangle, out Vector3 n) ? n : toNormal;
            PaintStroke(fromLocal, fromNormal, toLocal, toNormal, colour, brushSize);
        }

        /// <summary>The same stroke between two points whose facing is already known (the mirror tool's, which have no ray behind them).</summary>
        public void PaintStroke(Vector3 fromLocal, Vector3 fromNormal, Vector3 toLocal, Vector3 toNormal, Color colour, int brushSize)
        {
            float radius = BrushRadiusMetres(brushSize);
            int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(fromLocal, toLocal) / StrokeSpacingMetres(brushSize)), 1, 256);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                StampSphere(Vector3.Lerp(fromLocal, toLocal, t), Vector3.Slerp(fromNormal, toNormal, t), radius, colour);
            }
        }

        /// <summary>The outward direction of a triangle of the bound surface, in its own space.</summary>
        public bool TryNormalOf(int triangle, out Vector3 normal)
        {
            normal = Vector3.up;
            if (!Corners(triangle, out Vector3 a, out Vector3 b, out Vector3 c, out _, out _, out _))
            {
                return false;
            }
            Vector3 cross = Vector3.Cross(b - a, c - a);
            if (cross.sqrMagnitude < 1e-14f)
            {
                return false;
            }
            normal = cross.normalized;
            return true;
        }

        /// <summary>
        /// Paints every texel within <paramref name="radius"/> of <paramref name="centre"/> on the body that faces the brush. When none is that
        /// close (a brush smaller than the texels it lands on), the nearest one is painted, so a tap always leaves a mark.
        /// </summary>
        public void StampSphere(Vector3 centre, Vector3 normal, float radius, Color colour)
        {
            EnsureTexelMap();
            float radiusSquared = radius * radius;
            float search = radius + NearestTexelSlackMetres;
            Vector3Int low = GridCellOf(centre - Vector3.one * search);
            Vector3Int high = GridCellOf(centre + Vector3.one * search);

            int nearest = -1;
            float nearestSquared = search * search;
            bool painted = false;
            Color32 texel = colour;
            for (int x = low.x; x <= high.x; x++)
            {
                for (int y = low.y; y <= high.y; y++)
                {
                    for (int z = low.z; z <= high.z; z++)
                    {
                        if (!_texelGrid.TryGetValue(new Vector3Int(x, y, z), out List<int> points))
                        {
                            continue;
                        }
                        foreach (int p in points)
                        {
                            TexelPoint point = _texelPoints[p];
                            if (Vector3.Dot(point.Normal, normal) < FacingCutoff)
                            {
                                continue;
                            }
                            float distanceSquared = (point.Position - centre).sqrMagnitude;
                            if (distanceSquared <= radiusSquared)
                            {
                                _pixels[point.Texel] = texel;
                                painted = true;
                            }
                            else if (distanceSquared < nearestSquared)
                            {
                                nearestSquared = distanceSquared;
                                nearest = point.Texel;
                            }
                        }
                    }
                }
            }
            if (!painted && nearest >= 0)
            {
                _pixels[nearest] = texel;
            }
        }

        /// <summary>Builds the texel map ahead of the first stroke, so the first click does not stall on it.</summary>
        public void PrepareBrush()
        {
            EnsureTexelMap();
        }

        /// <summary>One place on the surface that a texel of the atlas is drawn at.</summary>
        private struct TexelPoint
        {
            public int Texel;
            public Vector3 Position;
            public Vector3 Normal;
        }

        private TexelPoint[] _texelPoints;

        /// <summary>
        /// Where every texel of the atlas is drawn on the bound surface, and which way it faces there: each triangle is rasterised into the
        /// atlas through its own UVs (wrapping round in u, as a limb's row does), each covered texel taking the point of the triangle under its
        /// centre. Then a couple of passes let the texels just outside the triangles borrow their neighbours' places. Built once per Bind.
        ///
        /// A TEXEL CAN BE IN SEVERAL PLACES, and keeps all of them. The body is separate tubes whose rounded ends tuck inside each other at the
        /// joints, and a part's row maps by position along its limb, so the hidden end of the upper arm uses the same texels as the visible
        /// forearm just past the elbow. Keeping only the last triangle's point left some of the visible ones at the hidden point, out of the
        /// brush's reach: a stroke down the arm broke off at the elbow.
        /// </summary>
        private void EnsureTexelMap()
        {
            if (_texelGrid != null)
            {
                return;
            }
            int width = PaintAtlas.Width;
            int height = PaintAtlas.Height;
            var points = new List<TexelPoint>(width * height);
            var first = new int[width * height];
            for (int i = 0; i < first.Length; i++)
            {
                first[i] = -1;
            }
            var atlas = new Vector2(width, height);

            for (int t = 0; t + 2 < _positions.Length && t + 2 < _uvs.Length; t += 3)
            {
                Vector3 a = _positions[t];
                Vector3 b = _positions[t + 1];
                Vector3 c = _positions[t + 2];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude < 1e-14f)
                {
                    continue;
                }
                Vector3 normal = cross.normalized;
                Vector2 pa = Vector2.Scale(_uvs[t], atlas);
                Vector2 pb = Vector2.Scale(_uvs[t + 1], atlas);
                Vector2 pc = Vector2.Scale(_uvs[t + 2], atlas);
                float area = Cross(pb - pa, pc - pa);
                if (Mathf.Abs(area) < 1e-9f)
                {
                    continue;
                }
                int fromX = Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x)));
                int toX = Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x)));
                int fromY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))));
                int toY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))));
                for (int y = fromY; y <= toY; y++)
                {
                    for (int x = fromX; x <= toX; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float wa = Cross(pb - p, pc - p) / area;
                        float wb = Cross(pc - p, pa - p) / area;
                        float wc = 1.0f - wa - wb;
                        if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f)
                        {
                            continue;
                        }
                        int index = PixelIndex(((x % width) + width) % width, y);
                        if (first[index] < 0)
                        {
                            first[index] = points.Count;
                        }
                        points.Add(new TexelPoint { Texel = index, Position = a * wa + b * wb + c * wc, Normal = normal });
                    }
                }
            }

            for (int pass = 0; pass < DilationPasses; pass++)
            {
                var before = (int[])first.Clone();
                for (int y = 0; y < height; y++)
                {
                    int row = y / PaintAtlas.RowHeight;
                    for (int x = 0; x < width; x++)
                    {
                        int index = PixelIndex(x, y);
                        if (before[index] >= 0)
                        {
                            continue;
                        }
                        for (int k = 0; k < 8 && first[index] < 0; k++)
                        {
                            int ny = y + NeighbourY[k];
                            if (ny < 0 || ny >= height || ny / PaintAtlas.RowHeight != row)
                            {
                                continue;   // never borrow from another part's row
                            }
                            int from = before[PixelIndex(((x + NeighbourX[k]) % width + width) % width, ny)];
                            if (from >= 0)
                            {
                                first[index] = points.Count;
                                points.Add(new TexelPoint { Texel = index, Position = points[from].Position, Normal = points[from].Normal });
                            }
                        }
                    }
                }
            }

            var grid = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector3Int cell = GridCellOf(points[i].Position);
                if (!grid.TryGetValue(cell, out List<int> list))
                {
                    list = new List<int>();
                    grid[cell] = list;
                }
                list.Add(i);
            }
            _texelPoints = points.ToArray();
            _texelGrid = grid;
        }

        private static readonly int[] NeighbourX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] NeighbourY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private static Vector3Int GridCellOf(Vector3 p)
        {
            return new Vector3Int(Mathf.FloorToInt(p.x / GridCellMetres), Mathf.FloorToInt(p.y / GridCellMetres),
                Mathf.FloorToInt(p.z / GridCellMetres));
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        /// <summary>
        /// Paint-bucket: floods the contiguous same-colour region touching the
        /// hit, never leaving that part's own atlas row.
        /// </summary>
        public void BucketFillAt(Vector3 localPoint, int triangle, Color colour)
        {
            if (!Corners(triangle, out Vector3 a, out Vector3 b, out Vector3 c,
                out Vector2 ua, out Vector2 ub, out Vector2 uc))
            {
                return;
            }

            Vector2 uv = Barycentric(localPoint, a, b, c, ua, ub, uc);
            uv.x = Mathf.Repeat(uv.x, 1.0f);

            int startX = Mathf.Clamp(
                Mathf.FloorToInt(uv.x * PaintAtlas.Width), 0, PaintAtlas.Width - 1);
            int startY = Mathf.Clamp(
                Mathf.FloorToInt(uv.y * PaintAtlas.Height), 0, PaintAtlas.Height - 1);

            int row = RowOfV(uv.y);
            int y0 = row * PaintAtlas.RowHeight;
            int y1 = (row + 1) * PaintAtlas.RowHeight - 1;

            Color target = _pixels[PixelIndex(startX, startY)];
            if (Same(target, colour))
            {
                return;
            }

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startX, startY));
            _pixels[PixelIndex(startX, startY)] = colour;

            while (queue.Count > 0)
            {
                Vector2Int at = queue.Dequeue();
                TryFlood(queue, at.x + 1, at.y, y0, y1, target, colour);
                TryFlood(queue, at.x - 1, at.y, y0, y1, target, colour);
                TryFlood(queue, at.x, at.y + 1, y0, y1, target, colour);
                TryFlood(queue, at.x, at.y - 1, y0, y1, target, colour);
            }
        }

        private void TryFlood(Queue<Vector2Int> queue, int x, int y, int y0, int y1,
            Color target, Color colour)
        {
            if (y < y0 || y > y1)
            {
                return;   // never leave this part's row
            }
            // Wraps in x for the same reason the stamp does.
            x = ((x % PaintAtlas.Width) + PaintAtlas.Width) % PaintAtlas.Width;

            int index = PixelIndex(x, y);
            if (!Same(_pixels[index], target))
            {
                return;
            }
            _pixels[index] = colour;
            queue.Enqueue(new Vector2Int(x, y));
        }

        // --- undo, mirroring and relocating a point on the mesh ---

        private readonly List<Color32[]> _undo = new List<Color32[]>();
        private readonly List<Color32[]> _redo = new List<Color32[]>();

        /// <summary>
        /// How many strokes back you can go. Each snapshot is the whole atlas,
        /// which at the finer atlas (PaintAtlas.Width by Height) is about 5.5 MB --
        /// 24 of them is affordable, and a shallow history is what people complain about.
        /// </summary>
        public const int UndoDepth = 24;

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        /// <summary>
        /// Taken BEFORE an edit, once per stroke rather than per stamp -- a
        /// drag is one thing the player did, so one press of undo should take
        /// all of it back rather than a hundred presses taking back a hundred
        /// dots.
        /// </summary>
        public void PushUndoSnapshot()
        {
            _undo.Add((Color32[])_pixels.Clone());
            if (_undo.Count > UndoDepth)
            {
                _undo.RemoveAt(0);
            }
            // A new edit makes any redone-away future unreachable, which is
            // what every other editor does.
            _redo.Clear();
        }

        public void Undo()
        {
            if (_undo.Count == 0)
            {
                return;
            }
            _redo.Add((Color32[])_pixels.Clone());
            _pixels = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            Apply();
        }

        public void Redo()
        {
            if (_redo.Count == 0)
            {
                return;
            }
            _undo.Add((Color32[])_pixels.Clone());
            _pixels = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            Apply();
        }

        /// <summary>The same point on the other side. The rig is symmetric about x=0.</summary>
        public static Vector3 Mirror(Vector3 localPoint)
        {
            return new Vector3(-localPoint.x, localPoint.y, localPoint.z);
        }

        /// <summary>
        /// Which triangle a point sits on, for mirroring -- the mirrored point
        /// is a position in space with no ray behind it, so it has to be
        /// relocated on the surface.
        ///
        /// Returns -1 when nothing is near enough, which is CORRECT rather than
        /// a failure right on the centreline: a brush dead-centre on the spine
        /// mirrors onto itself, and there is nothing different to paint.
        /// </summary>
        public int FindTriangle(Vector3 localPoint, float maximumDistance = 0.05f)
        {
            int best = -1;
            float bestDistance = maximumDistance * maximumDistance;

            for (int i = 0; i + 2 < _positions.Length; i += 3)
            {
                Vector3 centroid = (_positions[i] + _positions[i + 1] + _positions[i + 2]) / 3.0f;
                float distance = (centroid - localPoint).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i / 3;
                }
            }
            return best;
        }

        /// <summary>
        /// The paint, for saving with a character: raw RGBA, gzipped.
        ///
        /// NOT PNG, which would be the obvious choice. EncodeToPNG lives in
        /// Unity's ImageConversion module, which is not referenced here and
        /// can be stripped from a build entirely -- a save format that stops
        /// working depending on build settings is a bad trade for a smaller
        /// file. Raw RGBA is about 5.5 MB, and a paint texture is mostly flat colour,
        /// so gzip takes it to a few KB.
        /// </summary>
        public byte[] Encode()
        {
            // The bytes the texture shows, exactly: the pixels are kept as bytes (they were floats, and rounding a float channel to a byte a
            // second time on the way out -- 0.7 is 178.4999... -- brought every saved character back a step darker).
            Color32[] texels = _pixels;

            var raw = new byte[texels.Length * 4];
            for (int i = 0; i < texels.Length; i++)
            {
                Color32 c = texels[i];
                raw[i * 4] = c.r;
                raw[i * 4 + 1] = c.g;
                raw[i * 4 + 2] = c.b;
                raw[i * 4 + 3] = c.a;
            }

            using (var output = new System.IO.MemoryStream())
            {
                using (var zip = new System.IO.Compression.GZipStream(
                    output, System.IO.Compression.CompressionLevel.Optimal))
                {
                    zip.Write(raw, 0, raw.Length);
                }
                return output.ToArray();
            }
        }

        /// <summary>
        /// Loads previously saved paint. Returns false and changes nothing if
        /// it will not load -- a save from a different atlas size would put an
        /// arm's paint on a leg, so starting clean is better than loading it
        /// wrong.
        /// </summary>
        public bool Decode(byte[] compressed)
        {
            if (compressed == null || compressed.Length == 0)
            {
                return false;
            }

            byte[] raw;
            try
            {
                using (var input = new System.IO.MemoryStream(compressed))
                using (var zip = new System.IO.Compression.GZipStream(
                    input, System.IO.Compression.CompressionMode.Decompress))
                using (var output = new System.IO.MemoryStream())
                {
                    zip.CopyTo(output);
                    raw = output.ToArray();
                }
            }
            catch (System.Exception error)
            {
                Debug.LogError($"CityX: the saved paint would not decompress -- {error.Message}");
                return false;
            }

            int width;
            int height;
            if (raw.Length == _pixels.Length * 4)
            {
                width = PaintAtlas.Width;
                height = PaintAtlas.Height;
            }
            else if (raw.Length == PaintAtlas.LegacyWidth * PaintAtlas.LegacyHeight * 4)
            {
                // Saved before the atlas got finer: scaled up, texel for block.
                width = PaintAtlas.LegacyWidth;
                height = PaintAtlas.LegacyHeight;
            }
            else
            {
                return false;
            }

            var texels = new Color32[width * height];
            for (int i = 0; i < texels.Length; i++)
            {
                texels[i] = new Color32(raw[i * 4], raw[i * 4 + 1], raw[i * 4 + 2], raw[i * 4 + 3]);
            }
            Take(texels, width, height, flip: false);
            Apply();
            return true;
        }

        /// <summary>
        /// Copies a whole atlas in, of this size or of Godot's (<see cref="PaintAtlas.LegacyWidth"/>), scaling the latter up -- each old texel
        /// becomes a block -- and, for a picture read from a PNG, turning it the right way up.
        /// </summary>
        private void Take(Color32[] texels, int width, int height, bool flip)
        {
            int toWidth = PaintAtlas.Width;
            int toHeight = PaintAtlas.Height;
            for (int y = 0; y < toHeight; y++)
            {
                int fromY = y * height / toHeight;
                if (flip)
                {
                    fromY = height - 1 - fromY;
                }
                int fromRow = fromY * width;
                int toRow = y * toWidth;
                for (int x = 0; x < toWidth; x++)
                {
                    _pixels[toRow + x] = texels[fromRow + x * width / toWidth];
                }
            }
        }

        /// <summary>A Godot-sized atlas read from a PNG, turned the right way up (see <see cref="Adopt"/>), at its own size.</summary>
        public static Color32[] FlippedRows(Color32[] texels, int width, int height)
        {
            var flipped = new Color32[texels.Length];
            for (int y = 0; y < height; y++)
            {
                System.Array.Copy(texels, y * width, flipped, (height - 1 - y) * width, width);
            }
            return flipped;
        }

        /// <summary>
        /// For a body that is only worn, never painted (another player's, a character on the select screen): compresses the atlas on the
        /// graphics card to a quarter of its size and lets go of the copy in memory. Nothing can be painted on it afterwards.
        /// </summary>
        public void ShrinkForWearing()
        {
            Apply();
            Atlas.Compress(highQuality: false);
            Atlas.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            _pixels = new Color32[0];
            _undo.Clear();
            _redo.Clear();
        }

        private static bool Same(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f
                   && Mathf.Abs(a.g - b.g) < 0.004f
                   && Mathf.Abs(a.b - b.b) < 0.004f
                   && Mathf.Abs(a.a - b.a) < 0.004f;
        }

        private static Vector2 Barycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c,
            Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 v0 = b - a;
            Vector3 v1 = c - a;
            Vector3 v2 = p - a;

            float d00 = Vector3.Dot(v0, v0);
            float d01 = Vector3.Dot(v0, v1);
            float d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0);
            float d21 = Vector3.Dot(v2, v1);

            float denominator = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denominator) < 1e-12f)
            {
                return ua;
            }

            float v = (d11 * d20 - d01 * d21) / denominator;
            float w = (d00 * d21 - d01 * d20) / denominator;
            float u = 1.0f - v - w;
            return ua * u + ub * v + uc * w;
        }

        private bool Corners(int triangle, out Vector3 a, out Vector3 b, out Vector3 c,
            out Vector2 ua, out Vector2 ub, out Vector2 uc)
        {
            a = b = c = Vector3.zero;
            ua = ub = uc = Vector2.zero;

            int i = triangle * 3;
            if (triangle < 0 || i + 2 >= _positions.Length || i + 2 >= _uvs.Length)
            {
                return false;
            }

            a = _positions[i];
            b = _positions[i + 1];
            c = _positions[i + 2];
            ua = _uvs[i];
            ub = _uvs[i + 1];
            uc = _uvs[i + 2];
            return true;
        }

        private static int RowOfV(float v)
        {
            return Mathf.Clamp(
                Mathf.FloorToInt(v * PaintAtlas.PartCount), 0, PaintAtlas.PartCount - 1);
        }

        private static int PixelIndex(int x, int y)
        {
            return y * PaintAtlas.Width + x;
        }
    }
}
