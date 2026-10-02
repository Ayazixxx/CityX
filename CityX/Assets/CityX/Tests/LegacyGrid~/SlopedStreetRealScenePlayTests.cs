using System.Collections;
using System.Collections.Generic;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EXPLICIT BUG REPORT, repeated many times despite extensive batch-mode
    /// verification: "still no slopes." Every previous check of this feature
    /// -- the EditMode geometry sweeps, and every "CityX/Diagnose Sloped
    /// Street" render -- ran through `-executeMethod` batch scripts, NOT
    /// real Play Mode. `grapple_swing_deprioritized`'s own memory already
    /// documents that batch mode is not a reliable stand-in for it (uncapped
    /// Time.deltaTime, an Animator that never warms up the way it does
    /// live) -- this is the first time that same caution has actually been
    /// applied to the sloped-street feature specifically: the REAL saved
    /// scene, loaded into REAL Play Mode via LoadSceneInPlayMode (the same
    /// technique GrappleSwingRealInputPlayTests already established), its
    /// REAL CityStreamer streaming a REAL chunk over REAL frames, and the
    /// REAL Mesh that came out of it -- inspected directly, not re-derived.
    /// </summary>
    public sealed class SlopedStreetRealScenePlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// Finds the nearest real sloped-street edge to the origin, mirroring
        /// AddStreetEdgeFeatures's own roll exactly (CityMeshBuilder.cs).
        ///
        /// EXPLICIT BUG, found live after rerolling the world seed (EXPLICIT
        /// ASK: "generate a new world every time you make a change to the
        /// city generation"): this helper was missing the
        /// `!CityLayout.IsStreetChunk(nx, ny)` condition BuildGround's own
        /// real gating always applies (`!eastToStreet` etc.) -- a sloped
        /// ramp only ever exists on a street-vs-BUILDING edge; two
        /// neighbouring STREETS meet via the continuous-tilt system
        /// instead, with no ramp at all regardless of the pad-drop/roll
        /// numbers. Never caught before because the old seed's own nearest
        /// qualifying (by this incomplete check) chunk never happened to be
        /// a real junction; the new seed's did -- chunk (0,0) itself, whose
        /// neighbours are ALL streets (it sits on both arterial lines
        /// crossing at the origin), so production code never builds a ramp
        /// there no matter what this helper's own roll says.
        /// </summary>
        private static bool TryFindNearestSlope(CityWorld world,
            out int foundX, out int foundY, out int foundNx, out int foundNy,
            out float foundPad, out float foundNeighbourPad)
        {
            const int SearchRadius = 40;
            double bestDistance = double.MaxValue;
            foundX = foundY = foundNx = foundNy = 0;
            foundPad = foundNeighbourPad = 0.0f;
            bool found = false;

            for (int x = -SearchRadius; x <= SearchRadius; x++)
            {
                for (int y = -SearchRadius; y <= SearchRadius; y++)
                {
                    if (!CityLayout.IsStreetChunk(x, y)) continue;
                    float pad = world.Terrain.PadHeightFor(x, y);
                    foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        if (CityLayout.IsStreetChunk(nx, ny)) continue;
                        float neighbourPad = world.Terrain.PadHeightFor(nx, ny);
                        if (neighbourPad >= pad) continue;
                        bool sloped = new DeterministicRandom(world.ChunkSeed(nx, ny)).NextDouble() <
                            CityMeshBuilder.SlopedStreetChance;
                        if (!sloped) continue;

                        double distance = (double)x * x + (double)y * y;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            foundX = x; foundY = y; foundNx = nx; foundNy = ny;
                            foundPad = pad; foundNeighbourPad = neighbourPad;
                            found = true;
                        }
                    }
                }
            }
            return found;
        }

        [UnityTest]
        public IEnumerator ARealSlopedStreetActuallyRendersInTheRealSavedScene()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            CityStreamer streamer = Object.FindFirstObjectByType<CityStreamer>();
            Assert.IsNotNull(streamer, "no CityStreamer in the real saved scene");

            var world = new CityWorld(streamer.WorldSeed);
            bool found = TryFindNearestSlope(world, out int x, out int y, out int nx, out int ny,
                out float pad, out float neighbourPad);
            Assert.IsTrue(found, "no sloped street found near the origin at all -- the roll itself is broken");

            Vector3 centre = new Vector3(
                (float)CityLayout.ChunkCentreX(x), pad, (float)CityLayout.ChunkCentreZ(y));

            // A temporary Viewer standing at the target, so the streamer's
            // own real Update() -- the exact method real gameplay calls
            // every frame, not a manual StreamAround stand-in for it --
            // does the actual streaming, over real frames.
            var viewerObject = new GameObject("TestViewer");
            viewerObject.transform.position = centre;
            streamer.Viewer = viewerObject.transform;

            // NOT "any chunk is loaded" -- the scene's own real camera,
            // wherever the player spawns, was already streaming its own
            // nearby chunks during the settle frames above, well before
            // this override had a single frame to take effect. Waiting on
            // the SPECIFIC target chunk is the only condition that actually
            // proves this override reached the real streamer at all.
            Transform chunkTransform = null;
            for (int i = 0; i < 180 && chunkTransform == null; i++)
            {
                yield return null;
                chunkTransform = streamer.transform.Find($"Chunk {x},{y}");
            }
            Object.Destroy(viewerObject);
            Assert.IsNotNull(chunkTransform,
                $"chunk ({x},{y}) was never built as a real GameObject under the streamer " +
                $"(streamer built {streamer.LoadedChunkCount} chunks total, just never this one)");

            MeshFilter filter = chunkTransform.GetComponent<MeshFilter>();
            Assert.IsNotNull(filter, $"chunk ({x},{y})'s GameObject has no MeshFilter");
            Assert.IsNotNull(filter.sharedMesh, $"chunk ({x},{y})'s MeshFilter has no mesh");

            Vector3[] vertices = filter.sharedMesh.vertices;
            Assert.Greater(vertices.Length, 0, $"chunk ({x},{y})'s real mesh has no vertices at all");

            // OWN SIDE: real continuous terrain at (x,y)'s own 4 corners,
            // NOT PadHeightFor -- EXPLICIT BUG, found live after rerolling
            // the world seed. "Sloped streets = continuous tilt, not
            // ramps": a street chunk's own ground (AddCurvingStreetGround)
            // always follows ContinuousHeightAt at its real corners, never
            // the quantised/terraced pad -- PadHeightFor can legitimately
            // differ from the real continuous height there by up to half a
            // TerraceStep, so asserting the mesh reaches within 5cm of it
            // was checking something that was only ever true by luck for
            // whichever chunk the OLD seed happened to land on. The
            // NEIGHBOUR side is correctly left as PadHeightFor: the
            // neighbour is a BUILDING (guaranteed by TryFindNearestSlope's
            // own IsStreetChunk exclusion below), and buildings genuinely
            // do sit flat at their own quantised pad.
            double chunkX0 = x * (double)CityLayout.ChunkSize;
            double chunkX1 = chunkX0 + CityLayout.ChunkSize;
            double chunkZ0 = y * (double)CityLayout.ChunkSize;
            double chunkZ1 = chunkZ0 + CityLayout.ChunkSize;
            float[] ownCorners =
            {
                (float)world.Terrain.ContinuousHeightAt(chunkX0, chunkZ0),
                (float)world.Terrain.ContinuousHeightAt(chunkX1, chunkZ0),
                (float)world.Terrain.ContinuousHeightAt(chunkX1, chunkZ1),
                (float)world.Terrain.ContinuousHeightAt(chunkX0, chunkZ1),
            };

            bool reachesOwnPad = false;
            bool reachesNeighbourPad = false;
            foreach (Vector3 vertex in vertices)
            {
                foreach (float corner in ownCorners)
                {
                    if (Mathf.Abs(vertex.y - corner) <= 0.1f)
                    {
                        reachesOwnPad = true;
                        break;
                    }
                }
                if (Mathf.Abs(vertex.y - neighbourPad) <= 0.6f) reachesNeighbourPad = true;
            }

            TestContext.WriteLine($"chunk ({x},{y}) drops to ({nx},{ny}): pad={pad:F2} " +
                $"neighbourPad={neighbourPad:F2}, {vertices.Length} real vertices in the live mesh");
            Assert.IsTrue(reachesOwnPad,
                $"the REAL streamed mesh for chunk ({x},{y}) has no vertex at its own real terrain corner height");
            Assert.IsTrue(reachesNeighbourPad,
                $"the REAL streamed mesh for chunk ({x},{y}) never reaches down toward the neighbour's " +
                $"pad height ({neighbourPad:F2}m) -- the ramp is missing from what real gameplay actually builds");
#else
            yield break;
#endif
        }
    }
}
