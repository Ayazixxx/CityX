using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A WINDOW THAT BREAKS INTO ITS OWN PIECES (2026-10-15, the owner: "improve upon the window fracturing system"). The pane that broke is cut
    /// where it stood into the pattern struck glass makes (GlassFracture: slivers round the blow, rings of bigger shards out to the frame), and
    /// every piece is a real falling body: the slivers round the blow are knocked through the way the blow went, the rings further out follow
    /// slower, and the big shards along the frame hang on in the putty for a moment before they drop. Before this a broken window vanished and
    /// the same twelve little prisms popped out of the point it was hit, whatever its size.
    ///
    /// WHAT IT COSTS, AND WHY IT IS CHEAP (GlassFracturePlayTests measures it): cutting the pattern is 2D clipping of a few dozen polygons --
    /// microseconds. The pieces are POOLED (each keeps its GameObject, Rigidbody, BoxCollider and Mesh, refilled in place: no allocation
    /// after the first break) and stay switched on while they wait, parked far below the city with their renderers off -- switching a body and
    /// its collider on is what cost a break most of its time. Their colliders are thin boxes (nothing to cook, unlike the old convex prisms), they never collide with each
    /// other (a layer of their own: pieces of one pane start edge to edge), at most <see cref="MaxLiveShards"/> exist (the oldest go first),
    /// they sleep once they settle, and each shrinks away after <see cref="LifetimeSeconds"/>. One shared material: the SRP batcher draws them
    /// cheaply.
    /// </summary>
    public sealed class GlassFractureBurst : MonoBehaviour
    {
        /// <summary>The most glass pieces alive at once, all windows together; a new break takes the oldest pieces first.</summary>
        public const int MaxLiveShards = 160;

        /// <summary>How long a piece lasts after it falls, seconds (before it shrinks away), and the shrinking.</summary>
        public const float LifetimeSeconds = 7.0f;

        public const float ShrinkSeconds = 0.4f;

        /// <summary>The glass's thickness, metres (a city pane, 1.3 times a real one's 6 mm).</summary>
        public const float Thickness = 0.008f;

        /// <summary>The physics layer the pieces are on, which does not collide with itself (an unnamed spare layer).</summary>
        public const int ShardLayer = GlassFracture.ShardLayer;

        /// <summary>How fast the slivers round the blow are knocked through, metres a second (a light blow; a heavy one is more).</summary>
        public const float InnerSpeed = 4.0f;

        /// <summary>The longest a piece on the frame hangs on before it drops, seconds.</summary>
        public const float MaxHangSeconds = 0.8f;

        private static GlassFractureBurst _instance;

        private readonly Stack<Shard> _free = new Stack<Shard>();
        private readonly List<Shard> _live = new List<Shard>();
        private readonly List<Vector3> _vertices = new List<Vector3>(64);
        private readonly List<Vector3> _normals = new List<Vector3>(64);
        private readonly List<int> _triangles = new List<int>(128);

        /// <summary>Glass pieces alive now (for tests and the bench).</summary>
        public static int LiveShards => _instance != null ? _instance._live.Count : 0;

        /// <summary>Pieces ever made (pool growth: the bench checks a second break makes none).</summary>
        public static int ShardsMade { get; private set; }

        /// <summary>What the last break cost on its frame, milliseconds, and how much of that was building the pieces' meshes (the bench reads them).</summary>
        public static double LastBreakMs { get; private set; }

        public static double LastMeshMs { get; private set; }

        /// <summary>Where a piece waits in the pool: switched on (switching a body and its collider on and off is what costs), but far below the city.</summary>
        private static readonly Vector3 Parked = new Vector3(0.0f, -5000.0f, 0.0f);

        private sealed class Shard
        {
            public GameObject Go;
            public Transform Transform;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public Rigidbody Body;
            public BoxCollider Box;
            public float ReleaseAt;
            public float DieAt;
            public bool Released;
            public Vector3 Launch;
            public Vector3 Spin;
        }

        /// <summary>
        /// Breaks a pane: <paramref name="frame"/> is the glass, <paramref name="impact"/> where it was struck (world), <paramref name="through"/>
        /// the way the pieces go (through the pane, the way the blow went). Returns how many pieces it broke into.
        /// </summary>
        public static int Shatter(Material material, PaneFrame frame, Vector3 impact, Vector3 through, BlowStrength strength, int seed)
        {
            if (material == null || frame.Width <= 0.0f || frame.Height <= 0.0f)
            {
                return 0;
            }
            return Instance().Break(material, frame, impact, through, strength, seed);
        }

        /// <summary>
        /// Makes <paramref name="count"/> pieces ahead of time (GlassBreaker does, while the city streams in), so the first window to break makes
        /// none on its own frame.
        /// </summary>
        public static void Prewarm(int count, Material material = null)
        {
            GlassFractureBurst burst = Instance();
            while (burst._free.Count + burst._live.Count < Mathf.Min(count, MaxLiveShards))
            {
                burst._free.Push(burst.Make());
            }
            // And breaks nobody sees, down where the pool waits, until every piece has been used once: a pool's first real break paid
            // 14 ms (WindowSprayFramePlayTests) for its pieces' first meshes, bodies and renderers, and the first in a session for compiling.
            // (Not cleared between them: a cleared piece goes back on top of the pool and the next break would take the same ones again.)
            if (material != null && burst._warmedPieces < burst._free.Count + burst._live.Count)
            {
                int pool = burst._free.Count + burst._live.Count;
                for (int seed = 1; burst._warmedPieces < pool && seed < 16; seed++)
                {
                    burst._warmedPieces += burst.Break(material, new PaneFrame(Parked, Vector3.right, Vector3.up, Vector3.back, 1.5f, 2.0f),
                        Parked + new Vector3(0.75f, 1.0f, 0.0f), Vector3.forward, BlowStrength.Light, seed);
                }
                Clear();
            }
        }

        /// <summary>How many of this pool's pieces have been through a hidden break (Prewarm).</summary>
        private int _warmedPieces;

        private static GlassFractureBurst Instance()
        {
            if (_instance == null)
            {
                var host = new GameObject("GlassFractureBurst");
                _instance = host.AddComponent<GlassFractureBurst>();
                Physics.IgnoreLayerCollision(ShardLayer, ShardLayer, true);
            }
            return _instance;
        }

        /// <summary>Takes every piece away at once (a test).</summary>
        public static void Clear()
        {
            if (_instance == null)
            {
                return;
            }
            for (int i = _instance._live.Count - 1; i >= 0; i--)
            {
                _instance.Recycle(i);
            }
        }

        private int Break(Material material, PaneFrame frame, Vector3 impact, Vector3 through, BlowStrength strength, int seed)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            long meshTicks = 0;
            Vector2 at = frame.Of(impact);
            List<GlassPiece> pieces = GlassFracture.Fracture(frame.Width, frame.Height, at, seed);
            // Room for this break: the oldest pieces (on the ground by now) go first.
            int over = _live.Count + pieces.Count - MaxLiveShards;
            for (int i = 0; i < over && _live.Count > 0; i++)
            {
                Recycle(0);
            }

            var random = new System.Random(seed);
            float power = strength == BlowStrength.Heavy ? 1.6f : strength == BlowStrength.Bullet ? 0.6f : 1.0f;
            Vector3 away = through.sqrMagnitude > 1e-6f ? through.normalized : frame.Normal;
            float now = Time.time;
            float glassMass = 2500.0f * Thickness;   // kilograms a square metre
            Vector3 impactPlane = frame.At(at);

            foreach (GlassPiece piece in pieces)
            {
                Shard shard = Take(material);
                float area = GlassFracture.Area(piece.Corners);
                Vector2 middle = GlassFracture.Centroid(piece.Corners);
                Vector3 centre = frame.At(middle);

                // The piece's own frame: its face along the pane's, turned to its longest edge so its box fits it closely.
                Vector2 longest = LongestEdge(piece.Corners);
                Vector3 alongWorld = (frame.Across * longest.x + frame.Up * longest.y).normalized;
                Quaternion rotation = Quaternion.LookRotation(frame.Normal, Vector3.Cross(frame.Normal, alongWorld));   // its x along the edge
                shard.Transform.SetPositionAndRotation(centre, rotation);
                shard.Transform.localScale = Vector3.one;
                long meshStart = System.Diagnostics.Stopwatch.GetTimestamp();
                Bounds bounds = BuildMesh(shard.Mesh, piece.Corners, frame, centre, Quaternion.Inverse(rotation));
                meshTicks += System.Diagnostics.Stopwatch.GetTimestamp() - meshStart;
                shard.Box.center = bounds.center;
                shard.Box.size = new Vector3(Mathf.Max(bounds.size.x, 0.01f), Mathf.Max(bounds.size.y, 0.01f), Thickness * 2.0f);
                shard.Body.mass = Mathf.Clamp(area * glassMass, 0.02f, 15.0f);
                shard.Body.isKinematic = true;
                shard.Released = false;
                shard.Box.isTrigger = true;   // not solid while it hangs in the frame: a body runs through a broken window, not onto its edge

                // Inner pieces go first and fastest, through the pane and a little out from the blow; the frame's big pieces hang on, then drop.
                float ring = piece.Ring;
                float speed = InnerSpeed * power / (1.0f + ring * ring * 0.9f) * Range(random, 0.7f, 1.3f);
                Vector3 outFromBlow = centre - impactPlane;
                outFromBlow = outFromBlow.sqrMagnitude > 1e-6f ? outFromBlow.normalized : Vector3.zero;
                shard.Launch = away * speed + outFromBlow * speed * 0.3f + Random3(random) * 0.25f * power;
                shard.Spin = Random3(random) * (Range(random, 2.0f, 9.0f) / (1.0f + ring));
                float hang = piece.OnTheFrame ? Range(random, 0.1f, MaxHangSeconds) * (1.0f / power) : ring * Range(random, 0.0f, 0.03f);
                shard.ReleaseAt = now + hang;
                shard.DieAt = now + hang + LifetimeSeconds * Range(random, 0.85f, 1.15f);
                shard.Renderer.enabled = true;
                _live.Add(shard);
            }
            LastBreakMs = (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            LastMeshMs = meshTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return pieces.Count;
        }

        private void Update()
        {
            float now = Time.time;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Shard shard = _live[i];
                if (shard.Go == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }
                if (!shard.Released && now >= shard.ReleaseAt)
                {
                    shard.Released = true;
                    shard.Box.isTrigger = false;
                    shard.Body.isKinematic = false;
                    shard.Body.linearVelocity = shard.Launch;
                    shard.Body.angularVelocity = shard.Spin;
                    shard.Body.WakeUp();
                }
                float left = shard.DieAt - now;
                if (left <= 0.0f)
                {
                    Recycle(i);
                }
                else if (left < ShrinkSeconds)
                {
                    shard.Transform.localScale = Vector3.one * Mathf.Max(0.01f, left / ShrinkSeconds);
                }
            }
        }

        private Shard Take(Material material)
        {
            Shard shard = _free.Count > 0 ? _free.Pop() : Make();
            if (shard.Renderer.sharedMaterial != material)
            {
                shard.Renderer.sharedMaterial = material;
            }
            return shard;
        }

        private Shard Make()
        {
            ShardsMade++;
            var go = new GameObject("GlassPiece") { layer = ShardLayer };
            go.transform.SetParent(transform, false);
            go.transform.position = Parked;
            var mesh = new Mesh { name = "GlassPiece" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.enabled = false;   // waiting in the pool
            var body = go.AddComponent<Rigidbody>();
            body.linearDamping = 0.05f;
            body.angularDamping = 0.4f;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;   // solid only once it falls
            go.AddComponent<IgnoredByShots>();   // bullets and the gun's aim pass through falling glass
            return new Shard { Go = go, Transform = go.transform, Mesh = mesh, Renderer = renderer, Body = body, Box = box };
        }

        private void Recycle(int index)
        {
            Shard shard = _live[index];
            _live.RemoveAt(index);
            if (shard.Go == null)
            {
                return;
            }
            shard.Body.isKinematic = true;
            shard.Box.isTrigger = true;
            shard.Renderer.enabled = false;
            shard.Transform.SetPositionAndRotation(Parked, Quaternion.identity);
            shard.Transform.localScale = Vector3.one;
            _free.Push(shard);
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
            foreach (Shard shard in _live)
            {
                if (shard.Mesh != null) Destroy(shard.Mesh);
            }
            foreach (Shard shard in _free)
            {
                if (shard.Mesh != null) Destroy(shard.Mesh);
            }
        }

        /// <summary>
        /// The piece as a thin slab: its outline front and back, and its edges. In the piece's own frame (<paramref name="toLocal"/>), round
        /// its middle. Returns its bounds.
        /// </summary>
        private Bounds BuildMesh(Mesh mesh, Vector2[] corners, PaneFrame frame, Vector3 centre, Quaternion toLocal)
        {
            _vertices.Clear();
            _normals.Clear();
            _triangles.Clear();
            int n = corners.Length;
            float half = Thickness * 0.5f;
            var outline = new Vector3[n];
            var bounds = new Bounds();
            for (int i = 0; i < n; i++)
            {
                outline[i] = toLocal * (frame.At(corners[i]) - centre);
                outline[i].z = 0.0f;
                if (i == 0) bounds = new Bounds(outline[i], Vector3.zero);
                else bounds.Encapsulate(outline[i]);
            }
            // Which way the outline turns in the local frame decides the winding of each face.
            float turn = 0.0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = outline[i];
                Vector3 b = outline[(i + 1) % n];
                turn += a.x * b.y - b.x * a.y;
            }
            bool ccw = turn > 0.0f;

            // Front (+z) and back (-z) faces, fanned from the first corner.
            for (int face = 0; face < 2; face++)
            {
                float z = face == 0 ? half : -half;
                int first = _vertices.Count;
                for (int i = 0; i < n; i++)
                {
                    _vertices.Add(new Vector3(outline[i].x, outline[i].y, z));
                    _normals.Add(face == 0 ? Vector3.forward : Vector3.back);
                }
                // A counter-clockwise outline (x right, y up) faces +z as it is; the back face is wound the other way.
                bool flip = (face == 0) != ccw;
                for (int i = 1; i + 1 < n; i++)
                {
                    if (flip)
                    {
                        _triangles.Add(first);
                        _triangles.Add(first + i + 1);
                        _triangles.Add(first + i);
                    }
                    else
                    {
                        _triangles.Add(first);
                        _triangles.Add(first + i);
                        _triangles.Add(first + i + 1);
                    }
                }
            }
            // The edges: a quad per side, facing out.
            for (int i = 0; i < n; i++)
            {
                Vector3 a = outline[i];
                Vector3 b = outline[(i + 1) % n];
                Vector3 edge = b - a;
                Vector3 outward = (ccw ? new Vector3(edge.y, -edge.x, 0.0f) : new Vector3(-edge.y, edge.x, 0.0f)).normalized;
                int first = _vertices.Count;
                _vertices.Add(new Vector3(a.x, a.y, half));
                _vertices.Add(new Vector3(b.x, b.y, half));
                _vertices.Add(new Vector3(b.x, b.y, -half));
                _vertices.Add(new Vector3(a.x, a.y, -half));
                for (int k = 0; k < 4; k++)
                {
                    _normals.Add(outward);
                }
                // Wound so the face's front is toward 'outward'.
                Vector3 faceNormal = Vector3.Cross(_vertices[first + 1] - _vertices[first], _vertices[first + 2] - _vertices[first]);
                bool facesOut = Vector3.Dot(faceNormal, outward) > 0.0f;   // Unity: cross(b - a, c - a) is a front face's normal
                if (facesOut)
                {
                    _triangles.Add(first); _triangles.Add(first + 1); _triangles.Add(first + 2);
                    _triangles.Add(first); _triangles.Add(first + 2); _triangles.Add(first + 3);
                }
                else
                {
                    _triangles.Add(first); _triangles.Add(first + 2); _triangles.Add(first + 1);
                    _triangles.Add(first); _triangles.Add(first + 3); _triangles.Add(first + 2);
                }
            }
            mesh.Clear();
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetTriangles(_triangles, 0);
            bounds.Expand(new Vector3(0.0f, 0.0f, Thickness));
            mesh.bounds = bounds;
            return bounds;
        }

        private static Vector2 LongestEdge(Vector2[] corners)
        {
            Vector2 best = Vector2.right;
            float bestLength = -1.0f;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 edge = corners[(i + 1) % corners.Length] - corners[i];
                float length = edge.sqrMagnitude;
                if (length > bestLength)
                {
                    bestLength = length;
                    best = edge;
                }
            }
            return bestLength > 1e-10f ? best.normalized : Vector2.right;
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        private static Vector3 Random3(System.Random random) =>
            new Vector3(Range(random, -1.0f, 1.0f), Range(random, -1.0f, 1.0f), Range(random, -1.0f, 1.0f));
    }
}
