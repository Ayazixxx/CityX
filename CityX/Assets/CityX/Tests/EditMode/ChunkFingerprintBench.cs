using System.Collections.Generic;
using System.IO;
using System.Text;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the fingerprint of the city's geometry round the spawn (Logs/chunk-fingerprint.txt), every chunk built in full and in
    /// its far version and every number of every buffer hashed bit for bit. For a change that must not move a single vertex (a faster or
    /// leaner way of building the same thing: owner, 2026-10-17, "dont break the game"): run it before and after and compare the two lines.
    /// </summary>
    [Explicit("a measurement, not a test")]
    public sealed class ChunkFingerprintBench
    {
        private const int Seed = 48398181;

        private const int Radius = 6;

        [Test]
        public void FingerprintTheChunksRoundTheSpawn()
        {
            CityScale before = CityScale.Active;
            CityScale.Active = CityScale.Default;
            try
            {
                var world = new CityWorld(Seed);
                world.Plan.TryFindSpawn(out double sx, out double sz);
                int cx0 = PlanWorld.ChunkOf(sx), cy0 = PlanWorld.ChunkOf(sz);
                var report = new StringBuilder();
                foreach (bool far in new[] { false, true })
                {
                    ulong hash = 14695981039346656037UL;
                    long vertices = 0, triangles = 0;
                    int chunks = 0;
                    for (int dy = -Radius; dy <= Radius; dy++)
                    {
                        for (int dx = -Radius; dx <= Radius; dx++)
                        {
                            int cx = cx0 + dx, cy = cy0 + dy;
                            var buffers = new MeshBuffer[11];
                            for (int i = 0; i < buffers.Length; i++) buffers[i] = new MeshBuffer();
                            var origin = new Vector3(cx * CityLayout.ChunkSize, 0.0f, cy * CityLayout.ChunkSize);
                            CityMeshBuilder.BuildChunk(world, cx, cy, origin, buffers[0], buffers[1], buffers[2], buffers[4], buffers[3],
                                buffers[5], null, new List<CityMeshBuilder.CrackedOpening>(),
                                buffers[6], buffers[7], buffers[8], buffers[9], far: far);
                            foreach (MeshBuffer buffer in buffers)
                            {
                                vertices += buffer.Vertices.Count;
                                triangles += buffer.Triangles.Count / 3;
                                hash = Mix(hash, (uint)buffer.Vertices.Count);
                                foreach (Vector3 v in buffer.Vertices) hash = Mix(Mix(Mix(hash, Bits(v.x)), Bits(v.y)), Bits(v.z));
                                foreach (Vector3 n in buffer.Normals) hash = Mix(Mix(Mix(hash, Bits(n.x)), Bits(n.y)), Bits(n.z));
                                foreach (Vector2 uv in buffer.Uvs) hash = Mix(Mix(hash, Bits(uv.x)), Bits(uv.y));
                                foreach (Color32 c in buffer.Colors) hash = Mix(hash, (uint)(c.r | (c.g << 8) | (c.b << 16) | (c.a << 24)));
                                foreach (int t in buffer.Triangles) hash = Mix(hash, (uint)t);
                            }
                            chunks++;
                        }
                    }
                    report.AppendLine($"{(far ? "far " : "full")}: {chunks} chunks, {vertices} vertices, {triangles} triangles, fingerprint {hash:X16}");
                }
                string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "chunk-fingerprint.txt");
                File.WriteAllText(path, report.ToString());
                TestContext.WriteLine(report.ToString());
            }
            finally
            {
                CityScale.Active = before;
            }
        }

        private static uint Bits(float value) => unchecked((uint)System.BitConverter.SingleToInt32Bits(value));

        /// <summary>FNV-1a over the four bytes of <paramref name="value"/>.</summary>
        private static ulong Mix(ulong hash, uint value)
        {
            for (int i = 0; i < 4; i++)
            {
                hash ^= (value >> (i * 8)) & 0xFF;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
