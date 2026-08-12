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
        private Color[] _pixels;

        /// <summary>
        /// World distance covered by one atlas pixel across the TORSO, which
        /// every other region is sized against. character.gd's own reference.
        /// </summary>
        private readonly float _referenceTangent;

        private readonly float _referenceBitangent;

        /// <summary>
        /// ONE shared calibration for both axes, the geometric mean.
        ///
        /// Comparing each axis against its own reference was the first attempt
        /// and is wrong: at an ordinary point local and reference match on both
        /// axes, giving an isotropic pixel-space stamp -- and an isotropic
        /// pixel stamp is exactly what reads as an OVAL on the body, because u
        /// and v do not cover equal world distance per pixel to begin with.
        /// One shared reference makes the two radii come out different, which
        /// is what cancels the atlas's baked-in anisotropy.
        /// </summary>
        private readonly float _referenceBrush;

        public BodyPaint(Color fill)
        {
            float rowSpan = 1.0f / PaintAtlas.PartCount - 2.0f * PaintAtlas.RowMargin;
            _referenceTangent =
                (Mathf.PI * 2.0f * FusedBodyShape.TorsoRadius) / PaintAtlas.Width;
            _referenceBitangent =
                (BodyRig.ShoulderY - BodyRig.HipY) / (rowSpan * PaintAtlas.Height);
            _referenceBrush = Mathf.Sqrt(_referenceTangent * _referenceBitangent);

            Atlas = new Texture2D(PaintAtlas.Width, PaintAtlas.Height, TextureFormat.RGBA32, false)
            {
                // LINEAR, not point: the atlas is low-res and point sampling
                // makes every stroke read as stair-stepped blocks.
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
            };

            _pixels = new Color[PaintAtlas.Width * PaintAtlas.Height];
            Clear(fill);
        }

        public void Clear(Color fill)
        {
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = fill;
            }
            Apply();
        }

        public void Apply()
        {
            Atlas.SetPixels(_pixels);
            Atlas.Apply(updateMipmaps: false);
        }

        /// <summary>
        /// The body's triangle soup, in the same order as the mesh -- so a
        /// raycast's triangleIndex indexes straight into it.
        /// </summary>
        public void Bind(Mesh mesh)
        {
            _positions = mesh.vertices;
            _uvs = mesh.uv;
            _bodyTriangles = _positions.Length / 3;
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

            Vector3[] headPositions = head.vertices;
            Vector2[] headUvs = head.uv;

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
        /// Stamps the brush where a ray hit the body.
        ///
        /// <paramref name="brushRadiusPixels"/> is the slider's own 1..15.
        /// </summary>
        public void PaintAt(Vector3 localPoint, int triangle, Color colour, int brushRadiusPixels)
        {
            if (!Corners(triangle, out Vector3 a, out Vector3 b, out Vector3 c,
                out Vector2 ua, out Vector2 ub, out Vector2 uc))
            {
                return;
            }

            Vector2 uv = Barycentric(localPoint, a, b, c, ua, ub, uc);
            // A triangle straddling the wrap stores u past [0,1) on purpose, so
            // interpolation stays continuous. Wrap it back before turning it
            // into a pixel column, or u=1.02 computes a column past the right
            // edge and simply fails to paint.
            uv.x = Mathf.Repeat(uv.x, 1.0f);

            // Floors at 0.5, not 0, so brush size 1 still gets a real
            // anisotropic correction. Hardcoding size 1 to isotropic meant it
            // never compensated for local distortion at all, and on the sphere
            // head near the pole -- where the wrap is most compressed -- single
            // dots rendered as visible diamonds.
            float baseR = Mathf.Max(brushRadiusPixels - 1.0f, 0.5f);

            TangentLengths(a, b, c, ua, ub, uc, out float tangent, out float bitangent);
            float rawU = baseR * _referenceBrush / tangent;
            float rawV = baseR * _referenceBrush / bitangent;

            // How far the axes may diverge. Measured on the real body: torso
            // ~3.9:1 naturally, the neck/shoulder junction ~6:1, the head near
            // its pole ~8.4:1, an arm ~12.6:1. A cap below the torso's own
            // ratio flattens torso paint into an oval; no cap at all lets the
            // neck and head stretch into slivers that stop reading as a brush.
            //
            // Loosened as the brush shrinks. The correction is a local linear
            // approximation of a non-linear compression, so it only diverges
            // once the footprint is big enough to reach past the triangle it
            // was measured on. At size 1-2 the whole stamp is under one atlas
            // pixel and the raw correction is essentially exact.
            float maxAspect = Mathf.Lerp(30.0f, 3.0f, Mathf.Clamp01((baseR - 0.5f) / 2.5f));
            float rU = rawU;
            float rV = rawV;
            if (rawU > rawV * maxAspect)
            {
                rU = rawV * maxAspect;
            }
            else if (rawV > rawU * maxAspect)
            {
                rV = rawU * maxAspect;
            }

            // A ratio cap alone still lets the axes drift far apart in ABSOLUTE
            // pixels once the brush is big -- at size 15, inside the ratio cap,
            // one axis came out about nine pixels bigger than the other and
            // read as an elongated blob rather than a big circle.
            const float maxAxisDifference = 4.0f;
            if (rU - rV > maxAxisDifference)
            {
                rU = rV + maxAxisDifference;
            }
            else if (rV - rU > maxAxisDifference)
            {
                rV = rU + maxAxisDifference;
            }

            rU = Mathf.Clamp(rU, 0.5f, baseR * 6.0f);
            rV = Mathf.Clamp(rV, 0.5f, baseR * 6.0f);

            Stamp(uv, colour, rU, rV, RowOfV(uv.y));
        }

        /// <summary>
        /// Stamps one ellipse, clamped to its own atlas row.
        ///
        /// CLAMPED, because a brush near the top or bottom of a row would
        /// otherwise bleed into the neighbouring part -- paint the top of a
        /// thigh and find it on the torso.
        /// </summary>
        private void Stamp(Vector2 uv, Color colour, float radiusU, float radiusV, int row)
        {
            float centreX = uv.x * PaintAtlas.Width;
            float centreY = uv.y * PaintAtlas.Height;

            int y0 = row * PaintAtlas.Cell;
            int y1 = (row + 1) * PaintAtlas.Cell - 1;

            int fromX = Mathf.FloorToInt(centreX - radiusU);
            int toX = Mathf.CeilToInt(centreX + radiusU);
            int fromY = Mathf.Max(y0, Mathf.FloorToInt(centreY - radiusV));
            int toY = Mathf.Min(y1, Mathf.CeilToInt(centreY + radiusV));

            for (int y = fromY; y <= toY; y++)
            {
                for (int x = fromX; x <= toX; x++)
                {
                    float dx = (x + 0.5f - centreX) / Mathf.Max(radiusU, 0.0001f);
                    float dy = (y + 0.5f - centreY) / Mathf.Max(radiusV, 0.0001f);
                    if (dx * dx + dy * dy > 1.0f)
                    {
                        continue;
                    }

                    // WRAPPED IN X, not clamped. A limb's row is a cylinder cut
                    // open: a brush over the seam has to continue round the
                    // other side, not pile up against the edge.
                    int wrapped = ((x % PaintAtlas.Width) + PaintAtlas.Width) % PaintAtlas.Width;
                    _pixels[PixelIndex(wrapped, y)] = colour;
                }
            }
        }

        /// <summary>
        /// Draws a stroke between two hits rather than two separate dots.
        ///
        /// Without this a fast drag lands as a dotted line, because the mouse
        /// only reports a handful of positions per second and each one is a
        /// single stamp.
        /// </summary>
        public void PaintStroke(Vector3 fromLocal, int fromTriangle, Vector3 toLocal,
            int toTriangle, Color colour, int brushRadiusPixels)
        {
            PaintAt(toLocal, toTriangle, colour, brushRadiusPixels);

            float distance = Vector3.Distance(fromLocal, toLocal);
            // One step per third of the brush's world size, so the bridge is
            // solid without stamping hundreds of times for a long drag.
            int steps = Mathf.Clamp(
                Mathf.CeilToInt(distance / Mathf.Max(_referenceBrush * brushRadiusPixels * 8.0f,
                    0.002f)), 1, 64);

            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                // Stepped along the LATER triangle: the two hits are usually on
                // the same one, and when they are not, the destination is the
                // one the stroke is arriving at.
                PaintAt(Vector3.Lerp(fromLocal, toLocal, t), toTriangle, colour,
                    brushRadiusPixels);
            }
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
            int y0 = row * PaintAtlas.Cell;
            int y1 = (row + 1) * PaintAtlas.Cell - 1;

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

        private readonly List<Color[]> _undo = new List<Color[]>();
        private readonly List<Color[]> _redo = new List<Color[]>();

        /// <summary>
        /// How many strokes back you can go. Each snapshot is the whole atlas,
        /// which at 384x448 is under 700KB, so a deep history is affordable and
        /// a shallow one is the thing people actually complain about.
        /// </summary>
        public const int UndoDepth = 32;

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
            _undo.Add((Color[])_pixels.Clone());
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
            _redo.Add((Color[])_pixels.Clone());
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
            _undo.Add((Color[])_pixels.Clone());
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
        /// file. Raw RGBA is 688KB, and a paint texture is mostly flat colour,
        /// so gzip takes it to a few KB.
        /// </summary>
        public byte[] Encode()
        {
            // STRAIGHT OFF THE TEXTURE, not converted again from the float
            // pixels behind it. Both are the same picture, but only one of
            // them is the picture that was on screen, and rounding a float
            // channel to a byte a second time does not reliably land on the
            // same byte the texture chose: a channel at 0.7 is 178.4999... in
            // single precision, sitting on the knife edge between 178 and 179,
            // and every plausible way of writing that conversion by hand
            // picked the other side of it. So every saved character came back
            // a step darker in every channel.
            Apply();
            Color32[] texels = Atlas.GetPixels32();

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

            if (raw.Length != _pixels.Length * 4)
            {
                return false;
            }

            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = new Color32(
                    raw[i * 4], raw[i * 4 + 1], raw[i * 4 + 2], raw[i * 4 + 3]);
            }
            Apply();
            return true;
        }

        private static bool Same(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f
                   && Mathf.Abs(a.g - b.g) < 0.004f
                   && Mathf.Abs(a.b - b.b) < 0.004f
                   && Mathf.Abs(a.a - b.a) < 0.004f;
        }

        /// <summary>
        /// World distance per atlas pixel of u and of v, for this triangle --
        /// the standard tangent/bitangent derivation. Falls back to the torso
        /// reference on a degenerate triangle rather than dividing by nothing.
        /// </summary>
        private void TangentLengths(Vector3 a, Vector3 b, Vector3 c,
            Vector2 ua, Vector2 ub, Vector2 uc, out float tangent, out float bitangent)
        {
            Vector3 edge1 = b - a;
            Vector3 edge2 = c - a;
            var atlas = new Vector2(PaintAtlas.Width, PaintAtlas.Height);
            Vector2 uvEdge1 = Vector2.Scale(ub - ua, atlas);
            Vector2 uvEdge2 = Vector2.Scale(uc - ua, atlas);

            float determinant = uvEdge1.x * uvEdge2.y - uvEdge2.x * uvEdge1.y;
            if (Mathf.Abs(determinant) < 1e-6f)
            {
                tangent = _referenceTangent;
                bitangent = _referenceBitangent;
                return;
            }

            Vector3 t = (edge1 * uvEdge2.y - edge2 * uvEdge1.y) / determinant;
            Vector3 b2 = (edge2 * uvEdge1.x - edge1 * uvEdge2.x) / determinant;

            tangent = t.magnitude;
            bitangent = b2.magnitude;
            if (tangent < 1e-6f) tangent = _referenceTangent;
            if (bitangent < 1e-6f) bitangent = _referenceBitangent;
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
