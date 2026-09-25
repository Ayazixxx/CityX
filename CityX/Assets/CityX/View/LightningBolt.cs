using System.Collections.Generic;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The SHAPE of a lightning bolt: a jagged, branching path from a cloud to
    /// a strike point, by midpoint displacement -- the classic that reads as
    /// lightning at a glance. Pure and deterministic for a seed (so it is
    /// testable), and separate from how it is drawn (RainEffect puts it on a
    /// LineRenderer for a tenth of a second).
    /// </summary>
    public static class LightningBolt
    {
        /// <summary>How far a bolt's kinks stray from the straight line, as a fraction of a segment's length.</summary>
        public const float Roughness = 0.32f;

        private struct Rng
        {
            private uint _state;

            public Rng(int seed)
            {
                _state = (uint)seed * 2654435761u + 0x85EBCA6Bu;
                if (_state == 0)
                {
                    _state = 1;
                }
                Next();
            }

            private uint Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return _state;
            }

            public float Signed()
            {
                return ((Next() >> 8) / 16777216.0f) * 2.0f - 1.0f;
            }

            public float Unit()
            {
                return (Next() >> 8) / 16777216.0f;
            }
        }

        /// <summary>
        /// The main channel from <paramref name="from"/> (in the cloud) to
        /// <paramref name="to"/> (the strike), <c>2^subdivisions + 1</c> points,
        /// both ends exactly where asked. Every kink strays sideways by at most
        /// <see cref="Roughness"/> of its own segment length, so a bolt can
        /// never wander off the way it is going.
        /// </summary>
        public static Vector3[] Path(Vector3 from, Vector3 to, int seed, int subdivisions)
        {
            subdivisions = Mathf.Clamp(subdivisions, 1, 8);
            var rng = new Rng(seed);
            var points = new List<Vector3> { from, to };

            for (int level = 0; level < subdivisions; level++)
            {
                var next = new List<Vector3>(points.Count * 2);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Vector3 a = points[i];
                    Vector3 b = points[i + 1];
                    Vector3 segment = b - a;
                    Vector3 mid = (a + b) * 0.5f;

                    // Sideways, in the plane across the segment.
                    Vector3 side = Vector3.Cross(segment, Vector3.up);
                    if (side.sqrMagnitude < 1e-6f)
                    {
                        side = Vector3.Cross(segment, Vector3.right);
                    }
                    side.Normalize();
                    Vector3 across = Vector3.Cross(segment, side).normalized;
                    mid += (side * rng.Signed() + across * rng.Signed()) * (segment.magnitude * Roughness * 0.5f);

                    next.Add(a);
                    next.Add(mid);
                }
                next.Add(points[points.Count - 1]);
                points = next;
            }
            return points.ToArray();
        }

        /// <summary>
        /// A fork off the main channel: starts partway down it and runs off
        /// toward the side and down, shorter and thinner than the trunk.
        /// Returns null if the trunk is too short to branch.
        /// </summary>
        public static Vector3[] Branch(Vector3[] trunk, int seed, int subdivisions)
        {
            if (trunk == null || trunk.Length < 8)
            {
                return null;
            }
            var rng = new Rng(seed);
            int start = 2 + (int)(rng.Unit() * (trunk.Length * 0.5f));
            start = Mathf.Clamp(start, 1, trunk.Length - 4);
            Vector3 origin = trunk[start];
            Vector3 heading = (trunk[trunk.Length - 1] - origin);
            float length = heading.magnitude * (0.25f + 0.25f * rng.Unit());
            Vector3 sideways = Vector3.Cross(heading, Vector3.up).normalized * rng.Signed();
            Vector3 end = origin + heading.normalized * length + sideways * length * 0.9f;
            return Path(origin, end, seed + 101, subdivisions);
        }
    }
}
