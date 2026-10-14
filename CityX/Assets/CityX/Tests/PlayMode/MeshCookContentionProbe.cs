using System.Collections;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: does a collider being cooked on another thread (Physics.BakeMesh) hold up the main thread's own collider work -- switching a
    /// mesh collider on or off, a raycast, a box collider, a small mesh cooked here? Times each, with and without a big cook running.
    /// Measured 2026-10-14 (Unity 6000.3): no. With a 131 ms cook running all of them stayed under a third of a millisecond, so CityStreamer
    /// can cook chunks' colliders on its own threads while the main thread carries on (CityStreamer.BeginSwap).
    /// </summary>
    [Explicit("a measurement, not a test")]
    public sealed class MeshCookContentionProbe
    {
        private static Mesh Grid(int n, float jitter, string name)
        {
            var vertices = new Vector3[(n + 1) * (n + 1)];
            for (int z = 0; z <= n; z++)
            {
                for (int x = 0; x <= n; x++)
                {
                    vertices[z * (n + 1) + x] = new Vector3(x * 0.5f, Mathf.Sin(x * 0.37f) * Mathf.Cos(z * 0.21f) * jitter, z * 0.5f);
                }
            }
            var triangles = new int[n * n * 6];
            int t = 0;
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int a = z * (n + 1) + x;
                    triangles[t++] = a; triangles[t++] = a + n + 1; triangles[t++] = a + 1;
                    triangles[t++] = a + 1; triangles[t++] = a + n + 1; triangles[t++] = a + n + 2;
                }
            }
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject WithCollider(Mesh mesh, bool active)
        {
            var go = new GameObject(mesh.name);
            go.SetActive(active);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        [UnityTest]
        public IEnumerator MeasureMainThreadColliderWorkWhileACookRuns()
        {
            var report = new StringBuilder();
            for (int round = 0; round < 2; round++)
            {
                bool withCook = round == 1;
                Mesh big = Grid(420, 3.0f, "big");   // ~350k triangles
                Mesh onMesh = Grid(150, 1.0f, "on");
                Mesh offMesh = Grid(150, 1.0f, "off");
                Mesh small = Grid(40, 1.0f, "small");
                var reference = new GameObject("options");
                MeshColliderCookingOptions options = reference.AddComponent<MeshCollider>().cookingOptions;   // what a new chunk's collider uses
                Object.Destroy(reference);
                Physics.BakeMesh(onMesh.GetInstanceID(), false, options);
                Physics.BakeMesh(offMesh.GetInstanceID(), false, options);
                GameObject turnOff = WithCollider(offMesh, true);
                var turnOn = new GameObject("on");
                turnOn.SetActive(false);
                turnOn.AddComponent<MeshCollider>().sharedMesh = onMesh;
                yield return null;
                Physics.SyncTransforms();

                int bigId = big.GetInstanceID();
                int started = 0, finished = 0;
                double cookMs = 0.0;
                if (withCook)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        Volatile.Write(ref started, 1);
                        Physics.BakeMesh(bigId, false, options);
                        cookMs = clock.Elapsed.TotalMilliseconds;
                        Volatile.Write(ref finished, 1);
                    });
                    while (Volatile.Read(ref started) == 0) Thread.Sleep(0);
                    Thread.Sleep(5);
                }

                int gc = System.GC.CollectionCount(0);
                var t = System.Diagnostics.Stopwatch.StartNew();
                turnOn.SetActive(true);
                double on = t.Elapsed.TotalMilliseconds; bool onAfter = Volatile.Read(ref finished) == 1;
                t.Restart();
                turnOff.SetActive(false);
                double off = t.Elapsed.TotalMilliseconds; bool offAfter = Volatile.Read(ref finished) == 1;
                t.Restart();
                Physics.Raycast(new Vector3(10, 50, 10), Vector3.down, 100.0f);
                double ray = t.Elapsed.TotalMilliseconds; bool rayAfter = Volatile.Read(ref finished) == 1;
                t.Restart();
                var box = new GameObject("box");
                box.AddComponent<BoxCollider>();
                double boxMs = t.Elapsed.TotalMilliseconds; bool boxAfter = Volatile.Read(ref finished) == 1;
                t.Restart();
                GameObject cooked = WithCollider(small, true);
                double smallMs = t.Elapsed.TotalMilliseconds; bool smallAfter = Volatile.Read(ref finished) == 1;
                t.Restart();
                Object.Destroy(turnOn);
                Object.DestroyImmediate(offMesh);
                double destroyMs = t.Elapsed.TotalMilliseconds; bool destroyAfter = Volatile.Read(ref finished) == 1;
                int gcs = System.GC.CollectionCount(0) - gc;

                while (withCook && Volatile.Read(ref finished) == 0) Thread.Sleep(1);
                report.AppendLine($"{(withCook ? "with a big cook running" : "nothing cooking")}{(withCook ? $" ({cookMs:0} ms)" : "")}: " +
                                  $"switch a cooked collider on {on:0.00} ms{(withCook && onAfter ? " (after the cook)" : "")}, " +
                                  $"off {off:0.00} ms{(withCook && offAfter ? " (after the cook)" : "")}, " +
                                  $"raycast {ray:0.00} ms{(withCook && rayAfter ? " (after the cook)" : "")}, " +
                                  $"box collider {boxMs:0.00} ms{(withCook && boxAfter ? " (after the cook)" : "")}, " +
                                  $"small mesh cooked here {smallMs:0.00} ms{(withCook && smallAfter ? " (after the cook)" : "")}, " +
                                  $"destroy a cooked mesh {destroyMs:0.00} ms{(withCook && destroyAfter ? " (after the cook)" : "")}; gc {gcs}");

                Object.Destroy(cooked);
                Object.Destroy(box);
                Object.Destroy(turnOff);
                Object.Destroy(big);
                Object.Destroy(onMesh);
                Object.Destroy(small);
                yield return null;
            }
            Debug.Log("CityX cook contention:\n" + report);
            TestContext.WriteLine(report.ToString());
        }
    }
}
