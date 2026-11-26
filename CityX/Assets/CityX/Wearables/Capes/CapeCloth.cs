using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// THE CAPE'S CLOTH (docs/CAPE.md; owner, 2026-11-24: "research how to make good cape physics in video games"), as games do it:
    /// <list type="bullet">
    /// <item>Points joined by links that keep their length, moved by Verlet steps and the links put right a few times a step (position-based
    /// dynamics: Jakobsen's character physics, Müller et al. 2007), so it never blows up at a game's frame rate.</item>
    /// <item>Long-range tethers (Kim, Chentanez and Müller, 2012, made for a cape): no point gets further from its own column's top than the
    /// cloth's length between them, so the cape hangs without stretching however few times the links are put right.</item>
    /// <item>A gentle pull back towards its shape at rest on the body, strongest at the shoulders (the Arkham games drive their cape from a
    /// posed shape and let the simulation add the motion): it hangs like a cape, not a sheet.</item>
    /// <item>Air pushing on the cloth's face as it moves through it (and the wind): the cape streams out behind a runner.</item>
    /// <item>The body as capsules on its bones (and the head a ball), which the cloth is pushed out of.</item>
    /// </list>
    /// Fixed steps of <see cref="StepSeconds"/>, the same at any frame rate. Pure, so it is tested without a scene: the component
    /// (<see cref="Cape"/>) hands it the pinned points, the body and the shape to keep each frame.
    ///
    /// Cheap enough for a crowd (owner, 2026-11-25: "would that be a risk for performance in an mmo"): the links and tethers are worked
    /// through as plain arrays in plain numbers (Unity's vector operators cost a call each in the editor), and the body pushes the cloth
    /// out twice a step, halfway through the putting right and at its end (every time round, it was most of the cost), so each step still ends
    /// with the cloth outside the body.
    /// </summary>
    public sealed class CapeCloth
    {
        public struct Capsule
        {
            public Vector3 A;
            public Vector3 B;
            public float Radius;

            public Capsule(Vector3 a, Vector3 b, float radius)
            {
                A = a;
                B = b;
                Radius = radius;
            }
        }

        private struct Link
        {
            public int A, B;
            public float Rest, Stiffness;
        }

        private struct Tether
        {
            public int Point, Anchor;
            public float Length;
        }

        public const float StepSeconds = 1.0f / 90.0f;
        public const int Iterations = 8;
        public static readonly Vector3 Gravity = new Vector3(0.0f, -9.81f, 0.0f);

        /// <summary>The share of its speed a point keeps each step (what air and the cloth's own stiffness take).</summary>
        public const float Keep = 0.985f;

        /// <summary>How hard air pushes on the cloth's face, for each metre a second it moves through it.</summary>
        public const float AirPush = 2.2f;

        /// <summary>How much more than its length along the cloth a point may be from its column's top (a little give).</summary>
        public const float TetherGive = 1.02f;

        public readonly Vector3[] Position;
        public readonly Vector3[] Previous;

        /// <summary>Whether a point is held where it is put (<see cref="Hold"/>), not moved by the cloth.</summary>
        public readonly bool[] Pinned;

        /// <summary>How hard each point is drawn back to its shape at rest each step (0: not at all).</summary>
        public readonly float[] Drive;

        /// <summary>The points of the woven grid (the rest are cords): their faces catch the air.</summary>
        public readonly int GridColumns, GridRows;

        private readonly List<Link> _links = new List<Link>();
        private readonly List<Tether> _tethers = new List<Tether>();
        private Link[] _linkArray = System.Array.Empty<Link>();
        private Tether[] _tetherArray = System.Array.Empty<Tether>();
        private float[] _free = System.Array.Empty<float>();
        private readonly Vector3[] _normals;

        public CapeCloth(Vector3[] rest, int gridColumns, int gridRows)
        {
            Position = (Vector3[])rest.Clone();
            Previous = (Vector3[])rest.Clone();
            Pinned = new bool[rest.Length];
            Drive = new float[rest.Length];
            GridColumns = gridColumns;
            GridRows = gridRows;
            _normals = new Vector3[gridColumns * gridRows];
        }

        public int LinkCount => _links.Count;

        /// <summary>Joins two points at their distance now, as stiff as asked (1: put right fully each time).</summary>
        public void Join(int a, int b, float stiffness, float slack = 1.0f)
        {
            _links.Add(new Link { A = a, B = b, Rest = Vector3.Distance(Position[a], Position[b]) * slack, Stiffness = stiffness });
        }

        /// <summary>Ties a point to an anchor point no further than <paramref name="length"/> (a long-range tether).</summary>
        public void TetherTo(int point, int anchor, float length)
        {
            _tethers.Add(new Tether { Point = point, Anchor = anchor, Length = length });
        }

        /// <summary>Holds a point where it is put from now on.</summary>
        public void Hold(int point, Vector3 at)
        {
            Pinned[point] = true;
            Position[point] = at;
            Previous[point] = at;
        }

        /// <summary>Moves every point (and its last place, so its speed is kept) by <paramref name="move"/>: the body carried part of the way.</summary>
        public void Carry(Matrix4x4 move)
        {
            for (int i = 0; i < Position.Length; i++)
            {
                if (Pinned[i]) continue;
                Position[i] = move.MultiplyPoint3x4(Position[i]);
                Previous[i] = move.MultiplyPoint3x4(Previous[i]);
            }
        }

        /// <summary>Puts every point back at <paramref name="at"/>, standing still (a body put somewhere new, not walked there).</summary>
        public void Reset(Vector3[] at)
        {
            for (int i = 0; i < Position.Length; i++)
            {
                Position[i] = at[i];
                Previous[i] = at[i];
            }
        }

        /// <summary>
        /// One step: <paramref name="pins"/> where the held points are now, <paramref name="shape"/> where every point is at rest on the body now
        /// (drawn towards by <see cref="Drive"/>), the body's <paramref name="capsules"/>, the <paramref name="wind"/>, the floor's height, and how
        /// many times the links are put right (fewer for a cape far off, CapeDetail).
        /// </summary>
        public void Step(IReadOnlyList<(int point, Vector3 at)> pins, Vector3[] shape, IReadOnlyList<Capsule> capsules, Vector3 wind, float floor,
            int iterations = Iterations)
        {
            const float dt = StepSeconds;
            foreach ((int point, Vector3 at) in pins)
            {
                Previous[point] = Position[point];
                Position[point] = at;
            }
            Normals();
            for (int i = 0; i < Position.Length; i++)
            {
                if (Pinned[i]) continue;
                Vector3 velocity = (Position[i] - Previous[i]) / dt;
                Vector3 push = Vector3.zero;
                if (i < _normals.Length)
                {
                    Vector3 n = _normals[i];
                    push = -AirPush * Vector3.Dot(velocity - wind, n) * n;
                }
                Vector3 next = Position[i] + (Position[i] - Previous[i]) * Keep + (Gravity + push) * (dt * dt);
                Previous[i] = Position[i];
                Position[i] = next;
                if (shape != null && Drive[i] > 0.0f)
                {
                    Position[i] += (shape[i] - Position[i]) * Drive[i];
                }
            }
            Compile();
            int halfway = iterations / 2 - 1;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                SolveLinks();
                SolveTethers();
                if (iteration == halfway || iteration == iterations - 1)
                {
                    Collide(capsules, floor);
                }
            }
        }

        /// <summary>The links and tethers as arrays, and which points move (1) or are held (0), for the putting right.</summary>
        private void Compile()
        {
            if (_linkArray.Length != _links.Count) _linkArray = _links.ToArray();
            if (_tetherArray.Length != _tethers.Count) _tetherArray = _tethers.ToArray();
            if (_free.Length != Position.Length) _free = new float[Position.Length];
            for (int i = 0; i < Position.Length; i++)
            {
                _free[i] = Pinned[i] ? 0.0f : 1.0f;
            }
        }

        private void SolveLinks()
        {
            Vector3[] p = Position;
            for (int l = 0; l < _linkArray.Length; l++)
            {
                Link link = _linkArray[l];
                float wA = _free[link.A], wB = _free[link.B];
                float w = wA + wB;
                if (w <= 0.0f) continue;
                Vector3 a = p[link.A], b = p[link.B];
                float dx = b.x - a.x, dy = b.y - a.y, dz = b.z - a.z;
                float distance = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                if (distance < 1e-6f) continue;
                float s = (distance - link.Rest) / distance * link.Stiffness / w;
                dx *= s;
                dy *= s;
                dz *= s;
                p[link.A] = new Vector3(a.x + dx * wA, a.y + dy * wA, a.z + dz * wA);
                p[link.B] = new Vector3(b.x - dx * wB, b.y - dy * wB, b.z - dz * wB);
            }
        }

        private void SolveTethers()
        {
            Vector3[] p = Position;
            for (int k = 0; k < _tetherArray.Length; k++)
            {
                Tether tether = _tetherArray[k];
                Vector3 point = p[tether.Point], anchor = p[tether.Anchor];
                float dx = point.x - anchor.x, dy = point.y - anchor.y, dz = point.z - anchor.z;
                float squared = dx * dx + dy * dy + dz * dz;
                if (squared <= tether.Length * tether.Length) continue;
                float distance = Mathf.Sqrt(squared);
                float s = (distance - tether.Length) / distance;
                p[tether.Point] = new Vector3(point.x - dx * s, point.y - dy * s, point.z - dz * s);
            }
        }

        private void Collide(IReadOnlyList<Capsule> capsules, float floor)
        {
            int count = capsules != null ? capsules.Count : 0;
            for (int i = 0; i < Position.Length; i++)
            {
                if (Pinned[i]) continue;
                Vector3 p = Position[i];
                for (int c = 0; c < count; c++)
                {
                    p = PushOut(p, capsules[c]);
                }
                if (p.y < floor) p.y = floor;
                Position[i] = p;
            }
        }

        /// <summary>A point pushed out of a capsule to its surface (left where it is when outside).</summary>
        public static Vector3 PushOut(Vector3 p, Capsule capsule)
        {
            float ax = capsule.B.x - capsule.A.x, ay = capsule.B.y - capsule.A.y, az = capsule.B.z - capsule.A.z;
            float px = p.x - capsule.A.x, py = p.y - capsule.A.y, pz = p.z - capsule.A.z;
            float length = ax * ax + ay * ay + az * az;
            float t = length > 1e-8f ? Mathf.Clamp01((px * ax + py * ay + pz * az) / length) : 0.0f;
            float ox = px - ax * t, oy = py - ay * t, oz = pz - az * t;
            float squared = ox * ox + oy * oy + oz * oz;
            float radius = capsule.Radius;
            if (squared >= radius * radius)
            {
                return p;
            }
            float nx = capsule.A.x + ax * t, ny = capsule.A.y + ay * t, nz = capsule.A.z + az * t;
            float distance = Mathf.Sqrt(squared);
            if (distance < 1e-5f)
            {
                return new Vector3(nx, ny, nz - radius);   // dead centre: out behind (where a cape belongs)
            }
            float s = radius / distance;
            return new Vector3(nx + ox * s, ny + oy * s, nz + oz * s);
        }

        /// <summary>Each grid point's facing, from its neighbours across and down.</summary>
        private void Normals()
        {
            for (int row = 0; row < GridRows; row++)
            {
                for (int column = 0; column < GridColumns; column++)
                {
                    int left = row * GridColumns + Mathf.Max(0, column - 1), right = row * GridColumns + Mathf.Min(GridColumns - 1, column + 1);
                    int up = Mathf.Max(0, row - 1) * GridColumns + column, down = Mathf.Min(GridRows - 1, row + 1) * GridColumns + column;
                    Vector3 n = Vector3.Cross(Position[right] - Position[left], Position[down] - Position[up]);
                    float m = n.magnitude;
                    _normals[row * GridColumns + column] = m > 1e-8f ? n / m : Vector3.zero;
                }
            }
        }

        /// <summary>The cloth's facing at a grid point (from the last step).</summary>
        public Vector3 NormalAt(int index) => _normals[index];
    }
}
