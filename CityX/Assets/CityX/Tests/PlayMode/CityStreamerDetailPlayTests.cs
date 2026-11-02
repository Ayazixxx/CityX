using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Distance detail: chunks near the viewer are full detail, chunks further out are the far version (the
    /// building's outside only -- no rooms, stairs or window cut-outs), and walking swaps them both ways. Frame
    /// time in the real player followed the amount of geometry loaded, and about half of a downtown chunk's
    /// triangles were rooms nobody could see from a few chunks away.
    /// </summary>
    public sealed class CityStreamerDetailPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        private CityStreamer Streamer(string name, int radius, int fullRadius, Transform viewer)
        {
            var go = new GameObject(name);
            _made.Add(go);
            go.SetActive(false);   // set the fields before Awake runs
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = 95998845;
            streamer.RadiusChunks = radius;
            streamer.FullDetailRadiusChunks = fullRadius;
            streamer.DetailHysteresisChunks = 1;
            streamer.GeneratorThreads = 3;
            streamer.SpawnPreStreamBudget = 0;
            streamer.Viewer = viewer;
            go.SetActive(true);
            return streamer;
        }

        private Transform Viewer(Vector3 position)
        {
            var go = new GameObject("Viewer");
            _made.Add(go);
            go.transform.position = position;
            return go.transform;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float seconds)
        {
            float elapsed = 0.0f;
            while (!condition() && elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static int Triangles(CityStreamer streamer)
        {
            int total = 0;
            foreach (Transform child in streamer.transform)
            {
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                {
                    total += (int)filter.sharedMesh.GetIndexCount(i) / 3;
                }
            }
            return total;
        }

        private static Vector3 CentreOfChunk(int x, int y)
        {
            return new Vector3((x + 0.5f) * CityLayout.ChunkSize, 0.0f, (y + 0.5f) * CityLayout.ChunkSize);
        }

        [UnityTest]
        public IEnumerator ChunksBeyondTheFullDetailRadiusAreTheFarVersion()
        {
            CityStreamer streamer = Streamer("Detail", radius: 6, fullRadius: 2, Viewer(CentreOfChunk(0, 0)));
            int expected = 13 * 13;
            yield return WaitUntil(() => streamer.LoadedChunkCount >= expected, 30.0f);
            Assert.AreEqual(expected, streamer.LoadedChunkCount);

            Assert.IsFalse(streamer.IsChunkFar(new Vector2Int(0, 0)), "the chunk under the viewer is not full detail");
            Assert.IsFalse(streamer.IsChunkFar(new Vector2Int(2, -2)), "a chunk inside the full-detail radius is far");
            Assert.IsTrue(streamer.IsChunkFar(new Vector2Int(4, 0)), "a chunk outside the full-detail radius is full detail");
            Assert.IsTrue(streamer.IsChunkFar(new Vector2Int(-6, 5)), "the edge of the ring is full detail");

            // A far chunk is one object: a mesh, its collider, and nothing hung off it.
            Transform far = streamer.transform.Find("Chunk 4,0");
            Assert.IsNotNull(far);
            Assert.AreEqual(0, far.childCount, "a far chunk carries extra objects (doors, cracked glass, benches...)");
            Assert.IsNotNull(far.GetComponent<MeshCollider>(), "a far chunk has no collider: a fast flyer would go through it");
            Assert.IsNotNull(far.GetComponent<MeshCollider>().sharedMesh);
        }

        [UnityTest]
        public IEnumerator TheFarVersionIsMuchLighterThanTheFullOne()
        {
            Vector3 at = CentreOfChunk(0, 0);
            CityStreamer full = Streamer("Full", radius: 5, fullRadius: 0, Viewer(at));   // 0: no far version at all
            CityStreamer far = Streamer("Far", radius: 5, fullRadius: 1, Viewer(at));
            int expected = 11 * 11;
            yield return WaitUntil(() => full.LoadedChunkCount >= expected && far.LoadedChunkCount >= expected, 40.0f);
            Assert.AreEqual(expected, full.LoadedChunkCount);
            Assert.AreEqual(expected, far.LoadedChunkCount);

            int fullTriangles = Triangles(full);
            int farTriangles = Triangles(far);
            Assert.Less(farTriangles, fullTriangles * 0.6f,
                $"far detail is not much lighter: {farTriangles} triangles vs {fullTriangles} at full detail");
        }

        [UnityTest]
        public IEnumerator WalkingSwapsDetailBothWays()
        {
            Transform viewer = Viewer(CentreOfChunk(0, 0));
            CityStreamer streamer = Streamer("Detail", radius: 6, fullRadius: 2, viewer);
            yield return WaitUntil(() => streamer.LoadedChunkCount >= 13 * 13, 30.0f);
            Assert.IsTrue(streamer.IsChunkFar(new Vector2Int(5, 0)));
            Assert.IsFalse(streamer.IsChunkFar(new Vector2Int(0, 0)));

            viewer.position = CentreOfChunk(5, 0);
            yield return WaitUntil(() => !streamer.IsChunkFar(new Vector2Int(5, 0)) && streamer.IsChunkFar(new Vector2Int(0, 0))
                && !streamer.IsChunkFar(new Vector2Int(7, 0)) && streamer.transform.Find("Chunk 11,0") != null, 40.0f);

            Assert.IsFalse(streamer.IsChunkFar(new Vector2Int(5, 0)), "the chunk the viewer walked up to never became full detail");
            Assert.IsFalse(streamer.IsChunkFar(new Vector2Int(7, 0)), "the chunk two ahead never became full detail");
            Assert.IsTrue(streamer.IsChunkFar(new Vector2Int(0, 0)), "the chunk left far behind stayed full detail forever");

            yield return null;
            yield return null;   // a replaced chunk's old object goes at the end of the frame
            int copies = 0;
            foreach (Transform child in streamer.transform)
            {
                if (child.name == "Chunk 5,0") copies++;
            }
            Assert.AreEqual(1, copies, "swapping detail left two objects for one chunk");
        }

        [UnityTest]
        public IEnumerator ABrokenWindowInAFarChunkKeepsItFar()
        {
            // Round the spawn, which is built up, not the origin (open ground on seed 23481876; the east side alone on 58203917).
            var world = new CityWorld(95998845);
            Assert.IsTrue(world.Plan.TryFindSpawn(out double sx, out double sz), "the world has no spawn point");
            int cx = CityX.World.Plan.PlanWorld.ChunkOf(sx), cy = CityX.World.Plan.PlanWorld.ChunkOf(sz);
            CityStreamer streamer = Streamer("Detail", radius: 5, fullRadius: 1, Viewer(CentreOfChunk(cx, cy)));
            yield return WaitUntil(() => streamer.LoadedChunkCount >= 11 * 11, 30.0f);

            // A building built by one of the far chunks: anywhere in the ring three to five chunks out.
            BuildingSite site = BuildingSite.None;
            for (int dx = -5; dx <= 5 && !site.Exists; dx++)
            {
                for (int dy = -5; dy <= 5 && !site.Exists; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) < 3)
                    {
                        continue;
                    }
                    site = TestSites.FirstOwnedBy(world, cx + dx, cy + dy);
                }
            }
            Assert.IsTrue(site.Exists, "no building in the far ring to break a window of");
            var chunk = new Vector2Int(site.OwnerChunkX, site.OwnerChunkY);
            Assert.IsTrue(streamer.IsChunkFar(chunk));

            streamer.Damage.Hit(new PaneKey(site.Id, WindowGrid.Wall.North, 1, 1));
            yield return null;
            yield return null;

            Assert.IsTrue(streamer.IsChunkFar(chunk), "the rebuilt chunk came back full detail");
            Assert.IsNotNull(streamer.transform.Find($"Chunk {chunk.x},{chunk.y}"));
        }
    }
}
