using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The paint bucket: floods the same-colour region touching the click, over the whole body as one (owner, 2026-10-15, of the white sliver a
    /// torso fill left round the neck; 2026-11-10: "make the paint bucket tool work across the entire body instead separated by limb";
    /// 2026-11-11: "make the paint bucket tool work across the entire body right now head and body our separated. also there is a small drop
    /// in frames whenever i use the paint bucket tool"). Through the atlas within each part's own row, and across to wherever another part
    /// meets it on the body. Still only through the colour clicked, so an outline drawn round an area keeps the fill inside it.
    ///
    /// WHERE THE PARTS MEET IS WORKED OUT ONCE, on a worker thread as the brush is prepared (<see cref="BuildLinks"/>): texels at a seam keep
    /// a texel of each part lying within <see cref="BucketBridgeMetres"/> of them on the body, facing the same way. They were looked for during
    /// each fill, and only from the texels at the edge of a part's row: a fill took a second, and none crossed from the face to the neck (the
    /// head is a ball the neck's tube goes up into, and the edges of the head's row are nowhere near the neck).
    ///
    /// A FILL CAN BE SPREAD OVER FRAMES (<see cref="BeginBucketFill"/>, <see cref="ContinueBucketFill"/>): outward from the click, a little
    /// each frame (CharacterCreator.BucketMillisecondsPerFrame), so the paint pours over the body rather than the game stopping while all of
    /// it is done.
    /// </summary>
    public sealed partial class BodyPaint
    {
        /// <summary>How near on the body a texel of another part must be for a fill to cross to it, metres (a texel is 2 to 8 mm).</summary>
        public const float BucketBridgeMetres = 0.012f;

        /// <summary>A texel of the same part this near in the atlas (texels either way) is reached by the flood itself, so is not linked.</summary>
        private const int NearInTheAtlas = 3;

        private int[] _texelFirstPoint;
        private int[] _texelNextPoint;

        // The bucket's links, as one list per texel: texel t's are _linkTo[_linkStart[t] .. _linkStart[t + 1]).
        private int[] _linkStart;
        private int[] _linkTo;

        // The fill under way: a queue of texels painted and still to spread from, and the fills waiting to start (the mirror's).
        private int[] _fillQueue;
        private int _fillHead;
        private int _fillTail;
        private Color32 _fillFrom;
        private Color32 _fillTo;
        private readonly Queue<(int Texel, Color32 Colour)> _fillSeeds = new Queue<(int, Color32)>();

        /// <summary>A fill is still spreading (or waiting to start).</summary>
        public bool BucketFilling => _fillHead < _fillTail || _fillSeeds.Count > 0;

        /// <summary>Fills the region at a hit at once (<see cref="BeginBucketFill"/> and done).</summary>
        public void BucketFillAt(Vector3 localPoint, int triangle, Color colour)
        {
            BeginBucketFill(localPoint, triangle, colour);
            FinishBucketFill();
        }

        /// <summary>
        /// Starts a fill at a hit, to be spread by <see cref="ContinueBucketFill"/>: after any fill already under way, from the colour that is
        /// at the hit when it starts. False when the hit is off the atlas.
        /// </summary>
        public bool BeginBucketFill(Vector3 localPoint, int triangle, Color colour)
        {
            if (!Corners(triangle, out Vector3 a, out Vector3 b, out Vector3 c, out Vector2 ua, out Vector2 ub, out Vector2 uc))
            {
                return false;
            }
            Vector2 uv = Barycentric(localPoint, a, b, c, ua, ub, uc);
            uv.x = Mathf.Repeat(uv.x, 1.0f);
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * PaintAtlas.Width), 0, PaintAtlas.Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * PaintAtlas.Height), 0, PaintAtlas.Height - 1);
            EnsureBucketLinks();
            _fillSeeds.Enqueue((PixelIndex(x, y), colour));
            return true;
        }

        /// <summary>Spreads the fill by up to <paramref name="texels"/> texels. True while there is more to do.</summary>
        public bool ContinueBucketFill(int texels)
        {
            int width = PaintAtlas.Width;
            int rowHeight = PaintAtlas.RowHeight;
            for (int done = 0; done < texels; done++)
            {
                if (_fillHead == _fillTail && !StartNextFill())
                {
                    return false;
                }
                int texel = _fillQueue[_fillHead++];
                int x = texel % width;
                int y = texel / width;
                int y0 = y / rowHeight * rowHeight;
                Visit(y * width + (x + 1) % width);
                Visit(y * width + (x - 1 + width) % width);
                if (y + 1 < y0 + rowHeight)
                {
                    Visit(texel + width);   // never into another part's row: parts meet where they do on the body, not in the atlas
                }
                if (y > y0)
                {
                    Visit(texel - width);
                }
                for (int k = _linkStart[texel], end = _linkStart[texel + 1]; k < end; k++)
                {
                    Visit(_linkTo[k]);
                }
            }
            return BucketFilling;
        }

        /// <summary>Spreads whatever fill is under way to its end.</summary>
        public void FinishBucketFill()
        {
            while (BucketFilling)
            {
                ContinueBucketFill(int.MaxValue);
            }
        }

        /// <summary>The next waiting fill, from the colour at its texel now. False with none (or none that would change anything).</summary>
        private bool StartNextFill()
        {
            _fillHead = _fillTail = 0;
            while (_fillSeeds.Count > 0)
            {
                (int texel, Color32 colour) = _fillSeeds.Dequeue();
                if (Same32(_pixels[texel], colour))
                {
                    continue;
                }
                _fillQueue ??= new int[PaintAtlas.Width * PaintAtlas.Height];
                _fillFrom = _pixels[texel];
                _fillTo = colour;
                _pixels[texel] = colour;
                _fillQueue[_fillTail++] = texel;
                return true;
            }
            return false;
        }

        /// <summary>Paints a texel still the colour the fill started from, and queues it to spread from (each texel at most once a fill).</summary>
        private void Visit(int texel)
        {
            if (!Same32(_pixels[texel], _fillFrom))
            {
                return;
            }
            _pixels[texel] = _fillTo;
            _fillQueue[_fillTail++] = texel;
        }

        /// <summary>The same colour, to within one step of 255 in each channel (as <see cref="Same"/>), in whole bytes.</summary>
        private static bool Same32(Color32 a, Color32 b)
        {
            return a.r - b.r <= 1 && b.r - a.r <= 1 && a.g - b.g <= 1 && b.g - a.g <= 1
                   && a.b - b.b <= 1 && b.b - a.b <= 1 && a.a - b.a <= 1 && b.a - a.a <= 1;
        }

        /// <summary>For every texel, the places on the body it is drawn at (<see cref="EnsureTexelMap"/>'s points), as a linked list.</summary>
        private void EnsureTexelIndex()
        {
            EnsureTexelMap();
            if (_texelFirstPoint != null)
            {
                return;
            }
            if (_map != null && _map.FirstPoint != null && ReferenceEquals(_map.Points, _texelPoints))
            {
                // Another brush on this surface built it already.
                _texelFirstPoint = _map.FirstPoint;
                _texelNextPoint = _map.NextPoint;
                return;
            }
            _texelFirstPoint = new int[PaintAtlas.Width * PaintAtlas.Height];
            for (int i = 0; i < _texelFirstPoint.Length; i++)
            {
                _texelFirstPoint[i] = -1;
            }
            _texelNextPoint = new int[_texelPoints.Length];
            for (int p = _texelPoints.Length - 1; p >= 0; p--)
            {
                int texel = _texelPoints[p].Texel;
                _texelNextPoint[p] = _texelFirstPoint[texel];
                _texelFirstPoint[texel] = p;
            }
            if (_map != null && ReferenceEquals(_map.Points, _texelPoints))
            {
                // Shared with the map's other brushes, like the map itself.
                _map.FirstPoint = _texelFirstPoint;
                _map.NextPoint = _texelNextPoint;
            }
        }

        /// <summary>The bucket's links (<see cref="BuildLinks"/>), as one list per texel: texel t's are To[Start[t] .. Start[t + 1]).</summary>
        private sealed class BucketLinks
        {
            public int[] Start;
            public int[] To;
        }

        /// <summary>The links, waiting for them if the worker working them out has not finished (it starts when the brush is prepared).</summary>
        private void EnsureBucketLinks()
        {
            if (_linkStart != null)
            {
                return;
            }
            BucketLinks links = StartBuildingLinks().Result;
            _linkStart = links.Start;
            _linkTo = links.To;
        }

        /// <summary>
        /// Starts working the links out on a worker thread, once per surface (they are shared with the texel map): a second or two, which the
        /// creator opening must not wait for. Everything it reads is finished and never written again.
        /// </summary>
        private Task<BucketLinks> StartBuildingLinks()
        {
            EnsureTexelIndex();
            lock (SharedLock)
            {
                if (_map.Links == null)
                {
                    TexelPoint[] points = _texelPoints;
                    Dictionary<Vector3Int, List<int>> grid = _texelGrid;
                    int[] first = _texelFirstPoint;
                    _map.Links = Task.Run(() => BuildLinks(points, grid, first));
                }
                return _map.Links;
            }
        }

        /// <summary>
        /// Where the bucket crosses on the body that it cannot reach through the atlas: from a texel near another part (every other texel along
        /// the seam, either way), to one texel of each part lying within <see cref="BucketBridgeMetres"/> of it on the body facing the same way;
        /// and from a texel at the edge of its row's paint, to one of its own part's texels that near on the body but far off in the atlas
        /// (across a seam in the part's own layout). One a part is enough: the fill spreads along the seam through the atlas on either side.
        /// </summary>
        private static BucketLinks BuildLinks(TexelPoint[] points, Dictionary<Vector3Int, List<int>> grid, int[] first)
        {
            int width = PaintAtlas.Width;
            int rowHeight = PaintAtlas.RowHeight;

            // Which parts' rows each cell of the grid holds a point of, and then which its cell and the cells round it do.
            var rowsIn = new Dictionary<Vector3Int, int>(grid.Count);
            foreach (KeyValuePair<Vector3Int, List<int>> cell in grid)
            {
                int rows = 0;
                foreach (int p in cell.Value)
                {
                    rows |= 1 << (points[p].Texel / width / rowHeight);
                }
                rowsIn[cell.Key] = rows;
            }
            var rowsAround = new Dictionary<Vector3Int, int>(grid.Count);
            foreach (Vector3Int cell in rowsIn.Keys)
            {
                int rows = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (rowsIn.TryGetValue(new Vector3Int(cell.x + dx, cell.y + dy, cell.z + dz), out int near))
                            {
                                rows |= near;
                            }
                        }
                    }
                }
                rowsAround[cell] = rows;
            }

            var pairs = new List<long>();
            float reachSquared = BucketBridgeMetres * BucketBridgeMetres;
            Vector3 span = Vector3.one * BucketBridgeMetres;
            for (int p = 0; p < points.Length; p++)
            {
                TexelPoint here = points[p];
                int texel = here.Texel;
                int x = texel % width;
                int y = texel / width;
                int row = y / rowHeight;
                bool edge = AtTheEdgeOfItsRow(first, x, y, row * rowHeight, row * rowHeight + rowHeight - 1);
                rowsAround.TryGetValue(GridCellOf(here.Position), out int around);
                int others = around & ~(1 << row);
                if (!edge && (others == 0 || (x & 1) != 0 || (y & 1) != 0))
                {
                    continue;   // away from every seam, or between the texels the seam is linked from
                }
                bool wantOwn = edge;
                Vector3Int low = GridCellOf(here.Position - span);
                Vector3Int high = GridCellOf(here.Position + span);
                for (int cx = low.x; cx <= high.x && (others != 0 || wantOwn); cx++)
                {
                    for (int cy = low.y; cy <= high.y && (others != 0 || wantOwn); cy++)
                    {
                        for (int cz = low.z; cz <= high.z && (others != 0 || wantOwn); cz++)
                        {
                            if (!grid.TryGetValue(new Vector3Int(cx, cy, cz), out List<int> near))
                            {
                                continue;
                            }
                            for (int i = 0; i < near.Count && (others != 0 || wantOwn); i++)
                            {
                                TexelPoint there = points[near[i]];
                                int other = there.Texel;
                                int otherRow = other / width / rowHeight;
                                if (otherRow == row)
                                {
                                    if (!wantOwn || other == texel)
                                    {
                                        continue;
                                    }
                                    int across = Mathf.Abs(other % width - x);
                                    if (Mathf.Min(across, width - across) <= NearInTheAtlas && Mathf.Abs(other / width - y) <= NearInTheAtlas)
                                    {
                                        continue;   // the flood goes there through the atlas by itself
                                    }
                                }
                                else if ((others & (1 << otherRow)) == 0)
                                {
                                    continue;   // linked to that part already
                                }
                                if ((there.Position - here.Position).sqrMagnitude > reachSquared || Vector3.Dot(there.Normal, here.Normal) < 0.0f)
                                {
                                    continue;
                                }
                                if (otherRow == row)
                                {
                                    wantOwn = false;
                                }
                                else
                                {
                                    others &= ~(1 << otherRow);
                                }
                                pairs.Add(((long)texel << 32) | (uint)other);
                            }
                        }
                    }
                }
            }

            long[] sorted = pairs.ToArray();
            System.Array.Sort(sorted);
            int texels = width * PaintAtlas.Height;
            var start = new int[texels + 1];
            var to = new List<int>(sorted.Length);
            long last = -1;
            int filled = 0;
            foreach (long pair in sorted)
            {
                if (pair == last)
                {
                    continue;
                }
                last = pair;
                int from = (int)(pair >> 32);
                while (filled <= from)
                {
                    start[filled++] = to.Count;
                }
                to.Add((int)(pair & 0xffffffffL));
            }
            while (filled <= texels)
            {
                start[filled++] = to.Count;
            }
            return new BucketLinks { Start = start, To = to.ToArray() };
        }

        /// <summary>A texel on its row's first or last line, or beside a texel no triangle uses: where a part's paint ends in its row.</summary>
        private static bool AtTheEdgeOfItsRow(int[] first, int x, int y, int y0, int y1)
        {
            if (y == y0 || y == y1)
            {
                return true;
            }
            int width = PaintAtlas.Width;
            return first[PixelIndex((x + 1) % width, y)] < 0 || first[PixelIndex((x - 1 + width) % width, y)] < 0
                || first[PixelIndex(x, y + 1)] < 0 || first[PixelIndex(x, y - 1)] < 0;
        }
    }
}
