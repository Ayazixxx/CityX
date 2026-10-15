using System.Collections;
using System.Collections.Generic;
using System.IO;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the expressways (ExpresswayPlanner) to Logs/CityShots/xw-*.png -- on the deck at a driver's height, under the viaduct at
    /// a walker's, an exit from the air, from the deck where it leaves and from the street where it lands, each kind of ramp there is, a cloverleaf
    /// from above and from the street under it, the ring, and a route's closed end. In the game's own city (CityScale.Default: the tests pin the
    /// classic one), every spot built afresh at full detail. Explicit; needs -Graphics. Look at the pictures.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ExpresswayCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        private CityScale _scaleBefore;

        [TearDown]
        public void TearDown()
        {
            CityScale.Active = _scaleBefore;
        }

        [UnityTest]
        public IEnumerator RenderTheExpressways()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;   // what the player sees
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var streamer = Object.FindFirstObjectByType<CityStreamer>();
            streamer.Rebuild();
            var world = new CityWorld(streamer.WorldSeed);
            PlanWorld plan = world.Plan;
            ExpresswayNetwork xw = plan.Data.Expressways;
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            BuildingShapeCapture.UseTheScenesAir();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                DynamicGI.UpdateEnvironment();
            }

            var cameraObject = new GameObject("ExpresswayCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.farClipPlane = 3000.0f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;
            streamer.Viewer = cameraObject.transform;

            plan.TryFindSpawn(out double sx, out double sz);
            var spawn = new Vec2(sx, sz);
            Debug.Log($"CityX expressway capture: scale {CityScale.Active}, spawn {sx:F0},{sz:F0}");

            // ---- the main deck: the highest sample over dry land nearest the spawn ----
            ViaductPath mainLine = null;
            int mainIndex = -1;
            double mainScore = double.MaxValue;
            foreach (ExpresswayRoute route in xw.Routes)
            {
                ViaductPath m = xw.Paths[route.Path];
                for (int i = 10; i < m.Count - 10; i += 4)
                {
                    double under = m.Top[i] - m.Slab - plan.GroundHeight(m.P[i].X, m.P[i].Y);
                    if (under < ExpresswayPlanner.StreetClearance || plan.Fields.IsWater(m.P[i])) continue;
                    double score = (m.P[i] - spawn).Length;
                    if (score < mainScore) { mainScore = score; mainLine = m; mainIndex = i; }
                }
            }
            if (mainLine != null)
            {
                Vector3 centre = Point(mainLine, mainIndex, 0.0);
                Vector3 along = Along(mainLine, mainIndex);
                Vector3 left = new Vector3(-along.z, 0.0f, along.x);
                float ground = (float)plan.GroundHeight(centre.x, centre.z);
                yield return Stream(streamer, centre);
                Shoot(camera, directory, "xw-deck-drive", centre - along * 40.0f + left * (float)(mainLine.HalfLeft[mainIndex] * 0.5) + Vector3.up * 2.4f,
                    centre + along * 80.0f + left * (float)(mainLine.HalfLeft[mainIndex] * 0.5) + Vector3.up * 1.5f);
                Shoot(camera, directory, "xw-deck-air", ClearView(centre,
                    centre + left * 90.0f + Vector3.up * 55.0f - along * 40.0f,
                    centre - left * 90.0f + Vector3.up * 55.0f - along * 40.0f,
                    centre + left * 120.0f + Vector3.up * 110.0f - along * 60.0f,
                    centre - left * 120.0f + Vector3.up * 110.0f - along * 60.0f,
                    centre + Vector3.up * 170.0f - along * 90.0f), centre);
                var under = new Vector3(centre.x, ground + 1.8f, centre.z) - left * ((float)mainLine.HalfLeft[mainIndex] + 14.0f) - along * 20.0f;
                Shoot(camera, directory, "xw-under", under, new Vector3(centre.x, ground + 6.0f, centre.z) + along * 40.0f);
                Debug.Log($"CityX expressway capture: deck of route {mainLine.Route} at {centre.x:F0},{centre.z:F0}, {centre.y - ground:F1} m up");
            }

            // ---- an exit of each shape: from the air, from the deck where it leaves, from the street where it lands ----
            var shown = new HashSet<string>();
            var ramps = new List<ViaductPath>();
            foreach (ViaductPath r in xw.Paths) if (r.Kind == ViaductKind.Ramp) ramps.Add(r);
            ramps.Sort((a, b) => (a.P[0] - spawn).Length.CompareTo((b.P[0] - spawn).Length));
            foreach (ViaductPath r in ramps)
            {
                string key = r.Shape + (r.Leaving ? "-off" : "-on");
                if (!shown.Add(key)) continue;
                int highIndex = r.StreetAtEnd ? 0 : r.Count - 1;
                int lowIndex = r.StreetAtEnd ? r.Count - 1 : 0;
                Vector3 high = Point(r, highIndex, 0.0), low = Point(r, lowIndex, 0.0), mid = Point(r, r.Count / 2, 0.0);
                Vector3 look = (low - high);
                look.y = 0.0f;
                look.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, look);
                yield return Stream(streamer, mid);
                Shoot(camera, directory, $"xw-ramp-{key}-air", ClearView(mid,
                    mid + side * 110.0f + Vector3.up * 70.0f - look * 30.0f,
                    mid - side * 110.0f + Vector3.up * 70.0f - look * 30.0f,
                    mid + side * 140.0f + Vector3.up * 130.0f - look * 60.0f,
                    mid - side * 140.0f + Vector3.up * 130.0f - look * 60.0f,
                    mid + Vector3.up * 200.0f - look * 80.0f), mid);
                Vector3 onDeck = Along(r, highIndex) * (r.StreetAtEnd ? -1.0f : 1.0f);
                Shoot(camera, directory, $"xw-ramp-{key}-deck", high + onDeck * 45.0f + Vector3.up * 2.6f - side * 2.0f, mid + Vector3.up * 1.0f);
                Vector3 inward = Along(r, lowIndex) * (r.StreetAtEnd ? 1.0f : -1.0f);   // across the kerb into the street
                Vector3 streetEye = low + inward * 22.0f + Vector3.Cross(Vector3.up, inward) * 10.0f + Vector3.up * 1.8f;
                Shoot(camera, directory, $"xw-ramp-{key}-street", streetEye, low - inward * 25.0f + Vector3.up * 2.5f);
                ShootTip(camera, directory, $"xw-ramp-{key}-tip", r, highIndex, r.StreetAtEnd ? 1.0f : -1.0f);
                Debug.Log($"CityX expressway capture: {key} ramp {r.Id} from {high.x:F0},{high.z:F0} ({high.y:F1}) to {low.x:F0},{low.z:F0} ({low.y:F1}), {r.Length:F0} m");
                if (shown.Count >= 6) break;
            }

            // ---- the cloverleaf nearest the spawn ----
            ExpresswayInterchange clover = null;
            foreach (ExpresswayInterchange ic in xw.Interchanges)
            {
                if (ic.System && (clover == null || (ic.At - spawn).Length < (clover.At - spawn).Length)) clover = ic;
            }
            if (clover != null)
            {
                float ground = (float)plan.GroundHeight(clover.At.X, clover.At.Y);
                var centre = new Vector3((float)clover.At.X, ground, (float)clover.At.Y);
                yield return Stream(streamer, centre);
                Shoot(camera, directory, "xw-cloverleaf-air", centre + new Vector3(260.0f, 330.0f, -260.0f), centre);
                Shoot(camera, directory, "xw-cloverleaf-high", centre + new Vector3(-40.0f, 620.0f, -60.0f), centre);
                Shoot(camera, directory, "xw-cloverleaf-eye", centre + new Vector3(70.0f, 1.8f, -95.0f), centre + Vector3.up * 14.0f);
                Debug.Log($"CityX expressway capture: cloverleaf {clover.Id} at {centre.x:F0},{centre.z:F0}, {clover.Paths.Count} connectors");
                // Where one of its connectors leaves its main line, and where one joins the other.
                if (clover.Paths.Count > 0)
                {
                    ViaductPath c = xw.Paths[clover.Paths[0]];
                    yield return Stream(streamer, Point(c, 0, 0.0));
                    ShootTip(camera, directory, "xw-connector-leaves", c, 0, 1.0f);
                    yield return Stream(streamer, Point(c, c.Count - 1, 0.0));
                    ShootTip(camera, directory, "xw-connector-joins", c, c.Count - 1, -1.0f);
                }
            }

            // ---- the ring, a stretch of it from high up ----
            foreach (ExpresswayRoute route in xw.Routes)
            {
                if (!route.Ring) continue;
                ViaductPath ring = xw.Paths[route.Path];
                int nearest = 0;
                for (int i = 0; i < ring.Count; i++) if ((ring.P[i] - spawn).Length < (ring.P[nearest] - spawn).Length) nearest = i;
                Vector3 at = Point(ring, nearest, 0.0);
                Vector3 outward = new Vector3(at.x, 0.0f, at.z).normalized;
                yield return Stream(streamer, at);
                Shoot(camera, directory, "xw-ring-air", ClearView(at, at + outward * 220.0f + Vector3.up * 180.0f, at - outward * 220.0f + Vector3.up * 180.0f,
                    at + Vector3.up * 320.0f + outward * 120.0f), at - outward * 120.0f);
            }

            // ---- a route's closed end at the region's edge ----
            foreach (ExpresswayBarrier end in xw.Barriers)
            {
                var at = new Vector3((float)end.P.X, (float)end.Top, (float)end.P.Y);
                var across = new Vector3((float)end.Across.X, 0.0f, (float)end.Across.Y);
                Vector3 along = Vector3.Cross(across, Vector3.up);
                Vector3 inward = Vector3.Dot(along, -at) > 0.0f ? along : -along;   // back into the region
                yield return Stream(streamer, at + inward * 60.0f);
                Shoot(camera, directory, "xw-end", at + inward * 45.0f + Vector3.up * 3.0f, at + Vector3.up * 0.8f);
                break;
            }

            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }

        /// <summary>A point on a deck: a sample, moved across it (positive: to the left), at its top.</summary>
        private static Vector3 Point(ViaductPath path, int i, double across)
        {
            Vec2 p = path.P[i] + path.N[i] * across;
            return new Vector3((float)p.X, (float)path.Top[i], (float)p.Y);
        }

        /// <summary>The direction of a deck at a sample, flat.</summary>
        private static Vector3 Along(ViaductPath path, int i)
        {
            Vec2 t = -path.N[i].Perp;
            return new Vector3((float)t.X, 0.0f, (float)t.Y);
        }

        /// <summary>
        /// The first camera position that sees the target with nothing in the way, either way along the line (a camera inside a building sees
        /// out through its back faces, so the line is also cast from the target's end). The last if none does.
        /// </summary>
        private static Vector3 ClearView(Vector3 target, params Vector3[] candidates)
        {
            Physics.SyncTransforms();
            foreach (Vector3 c in candidates)
            {
                Vector3 near = target + (c - target).normalized * 4.0f;   // short of the target: the deck itself is not in the way
                if (!Physics.Linecast(c, near) && !Physics.Linecast(near, c)) return c;
            }
            return candidates[candidates.Length - 1];
        }

        /// <summary>Builds the city round a spot afresh: every chunk at the detail a player there would see (StreamAround never re-details a far one).</summary>
        private static IEnumerator Stream(CityStreamer streamer, Vector3 at)
        {
            if (streamer.Viewer != null)
            {
                streamer.Viewer.position = at;
            }
            streamer.Rebuild();
            streamer.StreamAround(at, int.MaxValue);
            yield return null;
            yield return null;
        }

        private static void Shoot(Camera camera, string directory, string name, Vector3 position, Vector3 lookAt)
        {
            BuildingShapeCapture.Shoot(camera, directory, name, position, lookAt);
        }

        /// <summary>
        /// Where a ramp or connector meets its main line, from above the main line as a player stands there: the tip of its taper at sample
        /// <paramref name="tip"/>, looking along the taper the way it widens (<paramref name="intoRamp"/>: +1 along the samples, -1 against them).
        /// </summary>
        private static void ShootTip(Camera camera, string directory, string name, ViaductPath path, int tip, float intoRamp)
        {
            Vector3 at = Point(path, tip, 0.0);
            Vector3 along = Along(path, tip) * intoRamp;
            var towardMain = new Vector3((float)path.N[tip].X, 0.0f, (float)path.N[tip].Y);   // a ramp's main line is on its left
            Shoot(camera, directory, name, at - along * 22.0f + towardMain * 6.0f + Vector3.up * 9.0f, at + along * 25.0f);
        }
    }
}
