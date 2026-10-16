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
    /// Not a test: how much memory each chunk round the spawn leaves for the garbage collector when the streamer builds it, in the game's
    /// own districts, chunk by chunk (Logs/chunk-garbage-bench.txt) and stage by stage (Logs/chunk-garbage-stages.txt). A collection stops
    /// every thread, so what streaming throws away is what the collector's pauses follow (PERFORMANCE.md, garbage collection).
    /// </summary>
    [Explicit("a measurement, not a test")]
    public sealed class ChunkGarbageBench
    {
        private const int Seed = 45678679;

        [Test]
        public void MeasureChunkGarbage()
        {
            CityScale before = CityScale.Active;
            CityScale.Active = CityScale.Default;
            var go = new GameObject("Streamer");
            try
            {
                var world = new CityWorld(Seed);
                world.Plan.TryFindSpawn(out double sx, out double sz);
                int cx0 = PlanWorld.ChunkOf(sx), cy0 = PlanWorld.ChunkOf(sz);

                var streamer = go.AddComponent<CityStreamer>();
                streamer.WorldSeed = Seed;
                streamer.RadiusChunks = 0;   // one chunk at a time; the one before is dropped when the next is 3 or more chunks away
                streamer.FullDetailRadiusChunks = 0;
                var report = new StringBuilder();

                // Twice over the same chunks: the first pass also fills the world's own caches (kept, not garbage); the second is what a
                // chunk costs streaming in again (the detailed version replacing the far one and back, a window rebuild).
                for (int pass = 0; pass < 2; pass++)
                {
                    var samples = new List<(long bytes, int vertices, int cx, int cy)>();
                    int skipped = 0;
                    long total = 0;
                    const int Reach = 7;
                    for (int dy = -Reach; dy <= Reach; dy++)
                    {
                        for (int dx = -Reach; dx <= Reach; dx++)
                        {
                            int cx = cx0 + dx * 3, cy = cy0 + dy * 3;   // three apart: building one drops the last
                            var at = new Vector3((cx + 0.5f) * CityLayout.ChunkSize, 0.0f, (cy + 0.5f) * CityLayout.ChunkSize);
                            int collections = System.GC.CollectionCount(0);
                            long used = System.GC.GetTotalMemory(false);
                            streamer.StreamAround(at, 1);
                            long allocated = System.GC.GetTotalMemory(false) - used;
                            if (System.GC.CollectionCount(0) != collections)
                            {
                                skipped++;   // a collection in the middle: the difference says nothing
                                continue;
                            }
                            int vertices = 0;
                            foreach (Transform child in streamer.transform)
                            {
                                MeshFilter filter = child.GetComponent<MeshFilter>();
                                if (filter != null && filter.sharedMesh != null) vertices += filter.sharedMesh.vertexCount;
                            }
                            samples.Add((allocated, vertices, cx, cy));
                            total += allocated;
                        }
                    }
                    samples.Sort((a, b) => b.bytes.CompareTo(a.bytes));
                    long median = samples.Count > 0 ? samples[samples.Count / 2].bytes : 0;
                    report.AppendLine($"pass {pass + 1}: {samples.Count} chunks ({skipped} skipped: a collection ran), garbage+kept per chunk: mean " +
                                      $"{total / Mathf.Max(1, samples.Count) / 1024.0 / 1024.0:0.00} MB, median {median / 1024.0 / 1024.0:0.00} MB, " +
                                      $"all {total / 1024.0 / 1024.0:0} MB; the most:");
                    for (int i = 0; i < 6 && i < samples.Count; i++)
                    {
                        (long bytes, int vertices, int cx, int cy) = samples[i];
                        report.AppendLine($"  {bytes / 1024.0 / 1024.0,7:0.0} MB  {vertices,7} vertices  chunk ({cx},{cy})");
                    }
                }
                report.AppendLine($"chunk mesh arrays: {ChunkArrayPool.MadeBytes / 1048576.0:0} MB made new in all, {ChunkArrayPool.PooledBytes / 1048576.0:0} MB held for reuse now");
                string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "chunk-garbage-bench.txt");
                File.WriteAllText(path, report.ToString());
                TestContext.WriteLine(report.ToString());
                Debug.Log("CityX chunk garbage:\n" + report);
            }
            finally
            {
                Object.DestroyImmediate(go);
                CityScale.Active = before;
            }
        }

        /// <summary>
        /// A streamed chunk's garbage stage by stage, on the streamer's own (private) steps: generating it (the worker's part), making its
        /// objects (the main thread's), giving its storage back, and destroying it -- each chunk built twice and the second time measured.
        /// Writes Logs/chunk-garbage-stages.txt.
        /// </summary>
        [Test]
        public void WhichStageOfAChunkMakesGarbage()
        {
            CityScale before = CityScale.Active;
            CityScale.Active = CityScale.Default;
            var go = new GameObject("Streamer");
            try
            {
                var streamer = go.AddComponent<CityStreamer>();
                streamer.WorldSeed = Seed;
                streamer.RadiusChunks = 0;
                streamer.Rebuild();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
                System.Reflection.MethodInfo generate = typeof(CityStreamer).GetMethod("GenerateChunk", flags);
                System.Reflection.MethodInfo assemble = typeof(CityStreamer).GetMethod("AssembleChunk", flags);
                System.Reflection.MethodInfo recycle = typeof(CityStreamer).GetMethod("Recycle", flags);
                System.Reflection.MethodInfo destroy = typeof(CityStreamer).GetMethod("DestroyChunk", flags);

                var world = new CityWorld(Seed);
                world.Plan.TryFindSpawn(out double sx, out double sz);
                int cx0 = PlanWorld.ChunkOf(sx), cy0 = PlanWorld.ChunkOf(sz);
                long[] totals = new long[4];
                int measured = 0;
                var heaviest = new List<(long bytes, string stage, Vector2Int chunk)>();
                string[] stages = { "generate", "assemble", "give back", "destroy" };
                for (int dy = -4; dy <= 4; dy++)
                {
                    for (int dx = -4; dx <= 4; dx++)
                    {
                        var key = new Vector2Int(cx0 + dx, cy0 + dy);
                        foreach (bool far in new[] { false, true })
                        {
                            for (int pass = 0; pass < 2; pass++)
                            {
                                var bytes = new long[4];
                                int collections = System.GC.CollectionCount(0);
                                long used = System.GC.GetTotalMemory(false);
                                object payload = generate.Invoke(streamer, new object[] { key, false, far });
                                long t1 = System.GC.GetTotalMemory(false);
                                var chunk = (GameObject)assemble.Invoke(streamer, new object[] { payload, false });
                                long t2 = System.GC.GetTotalMemory(false);
                                recycle.Invoke(streamer, new[] { payload });
                                long t3 = System.GC.GetTotalMemory(false);
                                destroy.Invoke(null, new object[] { chunk });
                                long t4 = System.GC.GetTotalMemory(false);
                                if (pass == 0 || System.GC.CollectionCount(0) != collections)
                                {
                                    continue;
                                }
                                bytes[0] = t1 - used;
                                bytes[1] = t2 - t1;
                                bytes[2] = t3 - t2;
                                bytes[3] = t4 - t3;
                                measured++;
                                for (int s = 0; s < 4; s++)
                                {
                                    totals[s] += bytes[s];
                                    heaviest.Add((bytes[s], stages[s] + (far ? " (far)" : ""), key));
                                }
                            }
                        }
                    }
                }
                heaviest.Sort((a, b) => b.bytes.CompareTo(a.bytes));
                var report = new StringBuilder();
                report.AppendLine($"{measured} chunk builds measured (each the second time; full and far), mean per chunk:");
                for (int s = 0; s < 4; s++)
                {
                    report.AppendLine($"  {stages[s],-10} {totals[s] / (double)Mathf.Max(1, measured) / 1024.0,8:0.0} KB");
                }
                report.AppendLine("the heaviest:");
                for (int i = 0; i < 10 && i < heaviest.Count; i++)
                {
                    report.AppendLine($"  {heaviest[i].bytes / 1024.0,8:0.0} KB  {heaviest[i].stage}  chunk {heaviest[i].chunk}");
                }
                string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "chunk-garbage-stages.txt");
                File.WriteAllText(path, report.ToString());
                TestContext.WriteLine(report.ToString());
            }
            finally
            {
                Object.DestroyImmediate(go);
                CityScale.Active = before;
            }
        }
    }
}
