using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>The skyline tier streaming round a viewer (SkylineStreamer, 2026-10-15): groups come within the radius, go when left behind, and the ones near the viewer are built again as it moves.</summary>
    public sealed class SkylinePlayTests
    {
        private const int Seed = 61256619;
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

        private SkylineStreamer Streamer(Transform viewer, bool allAtStart)
        {
            var go = new GameObject("Skyline");
            _made.Add(go);
            go.SetActive(false);
            var skyline = go.AddComponent<SkylineStreamer>();
            skyline.WorldSeed = Seed;
            skyline.Viewer = viewer;
            skyline.BuildAllAtStart = allAtStart;
            skyline.GroundMaterial = new Material(CityX.Core.LitShader.Find());
            skyline.StructureMaterial = new Material(CityX.Core.LitShader.Find());
            go.SetActive(true);
            return skyline;
        }

        private Transform Viewer(Vector3 at)
        {
            var go = new GameObject("Viewer");
            _made.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        private static Vector3 Spawn()
        {
            var world = new CityWorld(Seed);
            world.Plan.TryFindSpawn(out double sx, out double sz);
            return new Vector3((float)sx, (float)world.Plan.GroundHeight(sx, sz) + 2.0f, (float)sz);
        }

        /// <summary>How many groups lie within the radius of a point.</summary>
        private static int GroupsWithin(Vector3 at, float radius)
        {
            Vector2Int centre = SkylineStreamer.GroupOf(at);
            int r = Mathf.CeilToInt(radius / SkylineStreamer.GroupSize);
            int count = 0;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (SkylineStreamer.Distance(new Vector2Int(centre.x + dx, centre.y + dy), at) <= radius) count++;
                }
            }
            return count;
        }

        private static IEnumerator WaitUntil(System.Func<bool> done, float seconds)
        {
            float waited = 0.0f;
            while (!done() && waited < seconds)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
        }

        [UnityTest]
        public IEnumerator GroupsComeWithinTheRadiusAndGoWhenTheViewerLeaves()
        {
            Vector3 spawn = Spawn();
            Transform viewer = Viewer(spawn);
            SkylineStreamer skyline = Streamer(viewer, false);
            int expected = GroupsWithin(spawn, SkylineStreamer.RadiusMetres);
            yield return WaitUntil(() => skyline.LoadedGroupCount >= expected, 120.0f);
            Assert.AreEqual(expected, skyline.LoadedGroupCount, "every group within the radius loaded");
            Assert.IsTrue(skyline.IsGroupLoaded(SkylineStreamer.GroupOf(spawn)));

            // Far beyond the drop margin: everything goes, and the new place fills in.
            Vector3 elsewhere = spawn + new Vector3(6000.0f, 0.0f, 0.0f);
            viewer.position = elsewhere;
            Vector2Int oldGroup = SkylineStreamer.GroupOf(spawn);
            yield return WaitUntil(() => !skyline.IsGroupLoaded(oldGroup) && skyline.IsGroupLoaded(SkylineStreamer.GroupOf(elsewhere)), 120.0f);
            Assert.IsFalse(skyline.IsGroupLoaded(oldGroup), "the group left 6 km behind is still loaded");
            Assert.IsTrue(skyline.IsGroupLoaded(SkylineStreamer.GroupOf(elsewhere)));
        }

        [UnityTest]
        public IEnumerator StreamAroundBuildsEverythingAtOnceAndAGroupNearTheViewerIsRebuiltAsItMoves()
        {
            Vector3 spawn = Spawn();
            Transform viewer = Viewer(spawn);
            SkylineStreamer skyline = Streamer(viewer, false);
            skyline.StreamAround(spawn);
            int expected = GroupsWithin(spawn, SkylineStreamer.RadiusMetres);
            Assert.AreEqual(expected, skyline.LoadedGroupCount, "StreamAround builds every group within the radius on the spot");
            Vector2Int here = SkylineStreamer.GroupOf(spawn);
            Vector2Int chunk = SkylineStreamer.ChunkOf(spawn);
            Assert.AreEqual(chunk, skyline.BuiltAround(here));
            foreach (Transform child in skyline.transform)
            {
                var renderer = child.GetComponent<MeshRenderer>();
                if (renderer == null) continue;
                foreach (Material m in renderer.sharedMaterials) Assert.IsNotNull(m, $"{child.name} has a submesh with no material");
            }

            // A step of five chunks east: the group here is built again for the new chunk; a group three groups away is not.
            Vector2Int far = new Vector2Int(here.x + 3, here.y);
            Vector2Int farBuiltFor = skyline.BuiltAround(far);
            viewer.position = spawn + new Vector3(5.0f * CityLayout.ChunkSize, 0.0f, 0.0f);
            Vector2Int moved = SkylineStreamer.ChunkOf(viewer.position);
            yield return WaitUntil(() => skyline.BuiltAround(here) == moved, 60.0f);
            Assert.AreEqual(moved, skyline.BuiltAround(here), "the group round the viewer was not rebuilt for its new chunk");
            Assert.AreEqual(farBuiltFor, skyline.BuiltAround(far), "a group far from the viewer was rebuilt for nothing");
        }
    }
}
