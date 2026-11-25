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
        /// (drawn towards by <see cref="Drive"/>), the body's <paramref name="capsules"/>, the <paramref name="wind"/>, and the floor's height.
        /// </summary>
        public void Step(IReadOnlyList<(int point, Vector3 at)> pins, Vector3[] shape, IReadOnlyList<Capsule> capsules, Vector3 wind, float floor)
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
            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                foreach (Link link in _links)
                {
                    Solve(link);
                }
                foreach (Tether tether in _tethers)
                {
                    Vector3 apart = Position[tether.Point] - Position[tether.Anchor];
                    float distance = apart.magnitude;
                    if (distance > tether.Length)
                    {
                        Position[tether.Point] -= apart * ((distance - tether.Length) / distance);
                    }
                }
                Collide(capsules, floor);
            }
        }

        private void Solve(Link link)
        {
            Vector3 apart = Position[link.B] - Position[link.A];
            float distance = apart.magnitude;
            if (distance < 1e-6f)
            {
                return;
            }
            float wA = Pinned[link.A] ? 0.0f : 1.0f, wB = Pinned[link.B] ? 0.0f : 1.0f;
            float w = wA + wB;
            if (w <= 0.0f)
            {
                return;
            }
            Vector3 correction = apart * ((distance - link.Rest) / distance * link.Stiffness / w);
            Position[link.A] += correction * wA;
            Position[link.B] -= correction * wB;
        }

        private void Collide(IReadOnlyList<Capsule> capsules, float floor)
        {
            for (int i = 0; i < Position.Length; i++)
            {
                if (Pinned[i]) continue;
                Vector3 p = Position[i];
                if (capsules != null)
                {
                    foreach (Capsule capsule in capsules)
                    {
                        p = PushOut(p, capsule);
                    }
                }
                if (p.y < floor) p.y = floor;
                Position[i] = p;
            }
        }

        /// <summary>A point pushed out of a capsule to its surface (left where it is when outside).</summary>
        public static Vector3 PushOut(Vector3 p, Capsule capsule)
        {
            Vector3 axis = capsule.B - capsule.A;
            float length = axis.sqrMagnitude;
            float t = length > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - capsule.A, axis) / length) : 0.0f;
            Vector3 nearest = capsule.A + axis * t;
            Vector3 out1 = p - nearest;
            float distance = out1.magnitude;
            if (distance >= capsule.Radius)
            {
                return p;
            }
            if (distance < 1e-5f)
            {
                return nearest + Vector3.back * capsule.Radius;   // dead centre: out behind (where a cape belongs)
            }
            return nearest + out1 * (capsule.Radius / distance);
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
