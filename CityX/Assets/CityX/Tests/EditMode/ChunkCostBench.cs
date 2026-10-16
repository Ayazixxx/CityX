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
    /// Not a test: how long each chunk round the spawn takes to generate, full detail and far, in the game's own districts -- to find the
    /// chunks that hold the streamer up (StreamingPopInBench). Lists the slowest with what they build. Writes Logs/chunk-cost-bench.txt.
    /// </summary>
    [Explicit("a measurement, not a test")]
    public sealed class ChunkCostBench
    {
        [Test]
        public void MeasureChunkCosts()
        {
            CityScale before = CityScale.Active;
            CityScale.Active = CityScale.Default;
            try
            {
                var world = new CityWorld(63428175);
                world.Plan.TryFindSpawn(out double sx, out double sz);
                int cx0 = PlanWorld.ChunkOf(sx), cy0 = PlanWorld.ChunkOf(sz);
                var report = new StringBuilder();
                foreach (bool far in new[] { false, true })
                {
                    var costs = new List<(double ms, int cx, int cy)>();
                    double total = 0.0;
                    const int radius = 10;
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int cx = cx0 + dx, cy = cy0 + dy;
                            var buffers = new MeshBuffer[5];
                            for (int i = 0; i < 5; i++) buffers[i] = new MeshBuffer();
                            var origin = new Vector3(cx * CityLayout.ChunkSize, 0.0f, cy * CityLayout.ChunkSize);
                            var timer = System.Diagnostics.Stopwatch.StartNew();
                            CityMeshBuilder.BuildChunk(world, cx, cy, origin, buffers[0], buffers[1], buffers[2], buffers[4], buffers[3],
                                new MeshBuffer(), null, new List<CityMeshBuilder.CrackedOpening>(),
                                new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), new MeshBuffer(), far: far);
                            double ms = timer.Elapsed.TotalMilliseconds;
                            total += ms;
                            costs.Add((ms, cx, cy));
                        }
                    }
                    costs.Sort((a, b) => b.ms.CompareTo(a.ms));
                    costs.Reverse();
                    double median = costs[costs.Count / 2].ms;
                    costs.Reverse();
                    report.AppendLine($"{(far ? "far " : "full")}: {costs.Count} chunks, mean {total / costs.Count:0.0} ms, median {median:0.0} ms, slowest:");
                    for (int i = 0; i < 8 && i < costs.Count; i++)
                    {
                        (double ms, int cx, int cy) = costs[i];
                        var what = new StringBuilder();
                        foreach (int id in world.BuildingsOwnedBy(cx, cy))
                        {
                            BuildingSite b = world.Building(id);
                            what.Append($" #{id} {b.Params.Width:0}x{b.Params.Depth:0}x{b.Params.Height:0}{(b.Params.IsSpecial ? " " + b.Params.SpecialType : "")}{(b.OnRoof ? " roof" : "")}{(b.Shell ? " shell" : "")};");
                        }
                        report.AppendLine($"  {ms,8:0.0} ms  chunk ({cx},{cy}):{what}");
                    }
                }
                string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "chunk-cost-bench.txt");
                File.WriteAllText(path, report.ToString());
                TestContext.WriteLine(report.ToString());
            }
            finally
            {
                CityScale.Active = before;
            }
        }
    }
}
