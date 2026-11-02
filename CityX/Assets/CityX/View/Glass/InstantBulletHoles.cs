using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityX.View
{
    /// <summary>
    /// A BULLET HOLE ON THE FRAME THE BULLET LANDS (docs/DOORS_AND_WINDOWS.md; owner, 2026-11-02: "bullet holes are appearing delayed"). The real hole
    /// is cut when the block round the window is built again on a worker, 140 to 360 ms later (measured in the real scene, a hundred frames),
    /// so until it lands the same crack picture is drawn here, at the point the bullet hit. One mesh of world-space quads, written only when a hole
    /// comes or goes; each hole is kept under its block and let go when that block's change has reached the screen (CityStreamer).
    /// </summary>
    public sealed class InstantBulletHoles
    {
        private struct Hole
        {
            public Vector2Int Chunk;
            public Vector3 Point;
            public Vector3 Outward;
        }

        private readonly List<Hole> _holes = new List<Hole>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();

        /// <summary>The holes waiting for their blocks, as a mesh in world space.</summary>
        public Mesh Mesh { get; private set; }

        public int Count => _holes.Count;

        /// <summary>Draws a hole at <paramref name="point"/> on glass facing <paramref name="outward"/>, until <see cref="Forget"/> is told its block has landed.</summary>
        public void Add(Vector2Int chunk, Vector3 point, Vector3 outward)
        {
            _holes.Add(new Hole { Chunk = chunk, Point = point, Outward = outward.normalized });
            WriteMesh();
        }

        /// <summary>Lets go of the holes whose block <paramref name="landed"/> says has reached the screen, with the real hole in it.</summary>
        public void Forget(Func<Vector2Int, bool> landed)
        {
            bool any = false;
            for (int i = _holes.Count - 1; i >= 0; i--)
            {
                if (landed(_holes[i].Chunk))
                {
                    _holes.RemoveAt(i);
                    any = true;
                }
            }
            if (any)
            {
                WriteMesh();
            }
        }

        public void Clear()
        {
            _holes.Clear();
            WriteMesh();
        }

        /// <summary>Frees the mesh, for when the streamer goes away.</summary>
        public void Dispose()
        {
            if (Mesh != null)
            {
                if (Application.isPlaying) Object.Destroy(Mesh); else Object.DestroyImmediate(Mesh);
                Mesh = null;
            }
        }

        private void WriteMesh()
        {
            _vertices.Clear();
            _normals.Clear();
            _uvs.Clear();
            _triangles.Clear();
            foreach (Hole hole in _holes)
            {
                AddQuad(hole);
            }
            if (Mesh == null)
            {
                Mesh = new Mesh { name = "instant-bullet-holes" };
                Mesh.MarkDynamic();
            }
            Mesh.Clear();
            Mesh.SetVertices(_vertices);
            Mesh.SetNormals(_normals);
            Mesh.SetUVs(0, _uvs);
            Mesh.SetTriangles(_triangles, 0);
            Mesh.RecalculateBounds();
        }

        /// <summary>The whole crack picture across one quad, a hair off the glass, wound to face the way the glass does (as CityMeshBuilder.AddCrackDecal draws it).</summary>
        private void AddQuad(in Hole hole)
        {
            Vector3 across = Vector3.Cross(Vector3.up, hole.Outward).normalized * (CityMeshBuilder.BulletHoleDecalWidth * 0.5f);
            Vector3 up = Vector3.up * (CityMeshBuilder.BulletHoleDecalHeight * 0.5f);
            Vector3 centre = hole.Point + hole.Outward * CityMeshBuilder.BulletHoleDecalOffset;
            int first = _vertices.Count;
            _vertices.Add(centre + across - up);
            _vertices.Add(centre + across + up);
            _vertices.Add(centre - across + up);
            _vertices.Add(centre - across - up);
            for (int i = 0; i < 4; i++)
            {
                _normals.Add(hole.Outward);
            }
            _uvs.Add(new Vector2(0.0f, 0.0f));
            _uvs.Add(new Vector2(0.0f, 1.0f));
            _uvs.Add(new Vector2(1.0f, 1.0f));
            _uvs.Add(new Vector2(1.0f, 0.0f));
            _triangles.Add(first);
            _triangles.Add(first + 1);
            _triangles.Add(first + 2);
            _triangles.Add(first);
            _triangles.Add(first + 2);
            _triangles.Add(first + 3);
        }
    }
}
