using System.Collections;
using System.Collections.Generic;
using System.Text;
using CityX.View;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// "im getting serious frame drops when rapid firing the ak at windows" (owner, 2026-10-14). The real AK-47 sprayed at a row of real
    /// windows in a real streamed city block (background chunk generation, the real shard burst, as in the game) for three seconds: every
    /// frame's time is measured, with what happened in it (a shot, a window cracking or shattering, a chunk swapped), so a hitch can be
    /// pinned on its cause.
    /// </summary>
    public sealed class WindowSprayFramePlayTests
    {
        private const int Seed = 39271846;

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

        /// <summary>A building's street wall with a row of glazed windows in clear air, and where to stand to spray it.</summary>
        /// <summary>Up to <paramref name="most"/> door-side facades near spawn with at least four glazed windows in their first two upper rows.</summary>
        private static List<(List<Vector3> panes, Vector3 outward)> FindFacades(CityWorld world, int most)
        {
            var found = new List<(List<Vector3>, Vector3)>();
            foreach (BuildingSite site in TestSites.NearSpawn(world))
            {
                if (found.Count >= most)
                {
                    break;
                }
                if (FacadeOf(world, site, out List<Vector3> panes, out Vector3 outward))
                {
                    found.Add((panes, outward));
                }
            }
            return found;
        }

        /// <summary>The first such facade near spawn.</summary>
        private static bool FindFacade(CityWorld world, out List<Vector3> panes, out Vector3 outward)
        {
            List<(List<Vector3> panes, Vector3 outward)> facades = FindFacades(world, 1);
            panes = facades.Count > 0 ? facades[0].panes : null;
            outward = facades.Count > 0 ? facades[0].outward : Vector3.zero;
            return facades.Count > 0;
        }

        private static bool FacadeOf(CityWorld world, BuildingSite site, out List<Vector3> panes, out Vector3 outward)
        {
            {
                panes = null;
                outward = Vector3.zero;
                if (site.Params.Width < 12.0f)
                {
                    return false;
                }
                outward = CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North);
                WindowPlan plan = WindowPlan.For(world, site);
                CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
                CellLayout columns = plan.ColumnsFor(site.Params.Width);
                panes = new List<Vector3>();
                for (int row = 1; row < Mathf.Min(rows.Count, 3); row++)
                {
                    for (int column = 1; column < columns.Count - 1; column++)
                    {
                        if (!plan.IsGlazed(WindowGrid.Wall.North, column, row))
                        {
                            continue;
                        }
                        Vector3 centre = TestSites.At(site, site.MinX + (column + 0.5f) * columns.Size, site.BaseHeight + (row + 0.5f) * rows.Size, site.MaxZ);
                        if (GlassDamage.TryPaneAt(world, site, centre + outward * 0.02f, out _))
                        {
                            panes.Add(centre);
                        }
                    }
                }
                return panes.Count >= 4;
            }
        }

        /// <summary>
        /// THE BUG UNDER THE STALL: a hit landing while the block was already being refreshed made the NEXT refresh a whole-block one too, so
        /// once one window had cracked, every further hole -- which only changes the cracked glass -- swapped in the whole block again, for as
        /// long as the shooting went on. Here: crack a window, let it settle, then put three more holes in it in quick succession: the block
        /// on screen must be the same object afterwards (only its glass replaced), and every hole must be there.
        /// </summary>


        /// <summary>Waits, by the clock, until the city around the viewer has finished streaming in (nothing generating, nothing waiting).</summary>
        private static IEnumerator Streamed(CityStreamer streamer)
        {
            float until = Time.realtimeSinceStartup + 20.0f;
            int quiet = 0;
            while (Time.realtimeSinceStartup < until && quiet < 20)
            {
                quiet = streamer.IsStreaming ? 0 : quiet + 1;
                yield return null;
            }
            Physics.SyncTransforms();
            Assert.IsFalse(streamer.IsStreaming, "the city never finished streaming in");
        }
        /// <summary>Waits, by the clock (a batch-mode frame is under a millisecond), until every window change has reached the screen.</summary>
        private static IEnumerator Settle(CityStreamer streamer)
        {
            float until = Time.realtimeSinceStartup + 6.0f;
            int quiet = 0;
            while (Time.realtimeSinceStartup < until && quiet < 10)
            {
                quiet = streamer.WindowRefreshPending ? 0 : quiet + 1;
                yield return null;
            }
            Physics.SyncTransforms();
            Assert.IsFalse(streamer.WindowRefreshPending, "a window's change never reached the screen");
        }
        [UnityTest]
        public IEnumerator MoreHolesInACrackedWindowReplaceOnlyItsGlassNotTheWholeBlock()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            Assert.IsTrue(FindFacade(world, out List<Vector3> panes, out Vector3 outward), "no facade with a row of windows near the origin");
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.SpawnPreStreamBudget = 0;
            var breaker = go.AddComponent<GlassBreaker>();
            go.SetActive(true);
            Vector3 standing = panes[0] + outward * 8.0f + Vector3.up * 0.2f;
            var viewer = new GameObject("Viewer");
            _made.Add(viewer);
            viewer.transform.position = standing;
            streamer.Viewer = viewer.transform;
            streamer.StreamAround(standing, int.MaxValue);
            yield return Streamed(streamer);
            yield return Settle(streamer);

            Assert.IsTrue(world.TryBuildingAt(panes[0], out BuildingSite site, 0.6), "the window's building is not there");
            Assert.IsTrue(GlassDamage.TryPaneAt(world, site, panes[0] + outward * 0.02f, out PaneKey key), "the window is not a pane");
            string blockName = $"Chunk {site.OwnerChunkX},{site.OwnerChunkY}";
            Transform block = go.transform.Find(blockName);
            Assert.IsNotNull(block, "the window's block is not on screen");
            var seen = new HashSet<int> { block.gameObject.GetInstanceID() };

            // The crack, and three more holes while the crack's whole-block rebuild is still on its way (all in one frame: an AK burst).
            Vector3 across = Vector3.Cross(Vector3.up, outward);
            for (int hole = 0; hole < 4; hole++)
            {
                Vector3 at = panes[0] + outward * 0.02f + Vector3.up * (0.1f * (hole % 3 - 1)) + across * (0.12f * (hole % 2 * 2 - 1));
                Assert.IsTrue(breaker.TakeBlow(at, -outward, BlowStrength.Bullet), $"bullet {hole + 1} did not hit the window");
            }
            Assert.AreEqual(4, streamer.Damage.HoleCount(key), "a bullet did not make its hole");
            Assert.AreEqual(PaneState.Cracked, streamer.Damage.StateOf(key));

            // Watch the block until everything has reached the screen: every block object that is ever the one switched on is a swap.
            float limit = Time.realtimeSinceStartup + 6.0f;
            int quiet = 0;
            while (Time.realtimeSinceStartup < limit && quiet < 10)
            {
                foreach (Transform child in go.transform)
                {
                    if (child.name == blockName && child.gameObject.activeSelf)
                    {
                        seen.Add(child.gameObject.GetInstanceID());
                    }
                }
                quiet = streamer.WindowRefreshPending ? 0 : quiet + 1;
                yield return null;
            }
            Assert.IsFalse(streamer.WindowRefreshPending, "the window's change never reached the screen");
            Assert.AreEqual(2, seen.Count,
                $"the block was put on screen {seen.Count - 1} times: once is all a crack needs -- the three holes after it only change its glass");
            Transform after = go.transform.Find(blockName);
            Assert.IsNotNull(after);
            Assert.IsNotNull(after.Find("CrackedGlass"), "the cracked glass is not drawn");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SprayingTheAKAtWindowsDoesNotStallFrames()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            List<(List<Vector3> panes, Vector3 outward)> facades = FindFacades(world, 6);
            Assert.IsNotEmpty(facades, "no facade with a row of windows near the origin");

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 2;
            streamer.SpawnPreStreamBudget = 0;   // background generation, as in the game (GeneratorThreads left at its default)
            var breaker = go.AddComponent<GlassBreaker>();
            breaker.ShardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/View/GlassShards.prefab");
            breaker.CrackSounds = new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CityX/View/Audio/glass_crack.mp3") };
            breaker.BreakSounds = new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CityX/View/Audio/glass_break.mp3") };
            go.SetActive(true);
            Assert.IsNotNull(breaker.ShardPrefab, "the shard prefab is missing -- rebuild the scene");

            var viewer = new GameObject("Viewer");
            _made.Add(viewer);
            streamer.Viewer = viewer.transform;
            List<Vector3> panes = null;
            Vector3 outward = default, standing = default;
            foreach ((List<Vector3> facadePanes, Vector3 facadeOutward) in facades)
            {
                standing = facadePanes[0] + facadeOutward * 8.0f + Vector3.up * 0.2f;
                viewer.transform.position = standing;
                streamer.StreamAround(standing, int.MaxValue);
                yield return Streamed(streamer);   // the city settled, as it is when someone stops to shoot at a building
                // Only the windows the shooter can see: something standing out of a facade (a balcony's railing, a ledge) takes the bullets
                // first, and on seed 74102958 the first facade's first window had one 0.36 m in front of it.
                Vector3 from = standing;
                List<Vector3> clear = facadePanes.FindAll(p => !Physics.Raycast(from, (p - from).normalized, (p - from).magnitude - 0.1f, ~0, QueryTriggerInteraction.Ignore));
                if (clear.Count >= 4)
                {
                    panes = clear;
                    outward = facadeOutward;
                    break;
                }
            }
            Assert.IsNotNull(panes, "no facade near spawn with four windows in clear sight of the shooter");

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/AK47.prefab");
            GameObject akObject = Object.Instantiate(prefab, new Vector3(0.0f, -50.0f, 0.0f), Quaternion.identity);
            _made.Add(akObject);
            var ak = akObject.GetComponent<PistolWeapon>();
            ak.SetAmmo(30, 0);
            System.GC.Collect();   // what earlier tests left behind is not this test's to pay for
            yield return null;

            // Three seconds of the trigger held, the aim walking along the windows two shots at a time and a little up and down (a spray
            // with recoil): new panes crack, hit ones take more holes, some shatter (the fifth bullet) and throw their shards.
            var frames = new List<float>();
            var lines = new StringBuilder();
            float worst = 0.0f;
            int worstFrame = -1;
            double worstWork = 0.0;
            int worstWorkFrame = -1;
            int shots = 0;
            var clock = new System.Diagnostics.Stopwatch();
            Transform chunkRoot = go.transform;
            int lastChildren = chunkRoot.childCount;
            for (int frame = 0; frame < 200; frame++)
            {
                int pane = Mathf.Min(panes.Count - 1, (shots / 5) % panes.Count);
                Vector3 at = panes[pane] + Vector3.up * (0.12f * ((shots % 3) - 1)) + Vector3.Cross(Vector3.up, outward) * (0.1f * ((shots % 2) * 2 - 1));
                clock.Restart();
                bool fired = ak.Fire(standing, (at - standing).normalized, true, null);
                double fireMs = clock.Elapsed.TotalMilliseconds;
                if (fired)
                {
                    shots++;
                }
                yield return null;
                float frameMs = Time.unscaledDeltaTime * 1000.0f;
                frames.Add(frameMs);
                int children = chunkRoot.childCount;
                // The first two frames carry one-time loading (the first crack's sound, the first tracer, the first worker): judged apart.
                // What this is about: the main-thread work the windows cost -- the shot itself and the streamer's frame (rebuilt blocks going on
                // screen). The frame time also carries whatever the editor does in between (a garbage collection after hundreds of tests).
                double work = streamer.LastUpdateMs + (fired ? fireMs : 0.0);
                if (frame >= 2 && work > worstWork)
                {
                    worstWork = work;
                    worstWorkFrame = frame;
                }
                if (frame >= 2 && frameMs > worst)
                {
                    worst = frameMs;
                    worstFrame = frame;
                }
                if (fired || frameMs > 12.0f || children != lastChildren)
                {
                    lines.AppendLine($"  frame {frame,3}: {frameMs,6:0.0} ms  streamer {streamer.LastUpdateMs,5:0.0} ms  fire {(fired ? fireMs.ToString("0.0") + " ms" : "-"),-9} shots {shots,2}  pane {pane}  chunks {children}");
                }
                lastChildren = children;
                if (ak.MagazineAmmo == 0 && frame > 40)
                {
                    break;
                }
            }
            frames.Sort();
            float p95 = frames[Mathf.Clamp(Mathf.RoundToInt(frames.Count * 0.95f), 0, frames.Count - 1)];
            float median = frames[frames.Count / 2];
            Debug.Log($"CityX window spray: {shots} shots, median frame {median:0.0} ms, 95th percentile {p95:0.0} ms, worst frame after the first two {worst:0.0} ms (frame {worstFrame}), " +
                      $"worst window work {worstWork:0.0} ms (frame {worstWorkFrame})\n{lines}");
            Assert.Greater(shots, 20, "the spray did not fire");

            // Every window the spray hit has made it on screen: cracked or gone, none of them is solid glass any more (a ray at it passes
            // the wall), and nothing is left half swapped.
            yield return Settle(streamer);
            Physics.SyncTransforms();
            Assert.AreEqual(0, streamer.PendingSwapCount, "a rebuilt chunk never went on screen");
            int hitPanes = Mathf.Min(panes.Count, (shots + 4) / 5);
            for (int pane = 0; pane < hitPanes; pane++)
            {
                Vector3 toPane = panes[pane] - standing;
                // (Its own falling glass is not the window: GlassFracture.AllButShards.)
                bool solid = Physics.Raycast(standing, toPane.normalized, out RaycastHit hit, toPane.magnitude + 0.25f, GlassFracture.AllButShards,
                    QueryTriggerInteraction.Ignore);
                Assert.IsFalse(solid, $"window {pane} was shot but its glass is still solid on screen (hit {(solid ? hit.collider.name : "")} at {hit.distance:0.00} m of {toPane.magnitude:0.00})");
            }

            // THE POINT: no stall. Before the fix every hit on a block swapped in the whole rebuilt block on the main thread, 15-20 ms at a
            // time, several times a second (the tangents and the collider's cooking); now both are done off the main thread.
            Assert.Less(worstWork, 8.0, $"the windows took {worstWork:0.0} ms of one frame (frame {worstWorkFrame}): they still stall the game");
#else
            yield break;
#endif
        }
    }
}
