using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Getting in through a broken window on a REAL facade (owner, 2026-10-13: "right now you cant vault into a window which you should be able to if
    /// its been shattered or broken completely"). On the city's own buildings near spawn: a ground-floor window with a sill at vault height is refused
    /// while its glass is whole and vaulted (a dive under its lintel) once the pane is shattered; a window whose sill is only a step up is simply walked
    /// into once its glass is gone.
    /// </summary>
    public sealed class RealCityWindowVaultPlayTests
    {
        private const int Seed = 61928374;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

#if UNITY_EDITOR
        internal sealed class Candidate
        {
            public BuildingSite Site;
            public WindowGrid.Wall Wall;
            public int Column;
            public PaneKey Key;
            public Vector3 Outside;    // where a body stands outside the pane, on the ground
            public Vector3 Inward;
            public float SillAboveGround;
            public float Opening;
            public float Width;          // the pane, metres
            public float SillOverGround; // the sill over the real ground outside, metres
        }

        private CityStreamer _streamer;

        private CityStreamer Streamer()
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            _streamer = go.AddComponent<CityStreamer>();
            _streamer.WorldSeed = Seed;
            _streamer.RadiusChunks = 2;
            _streamer.GeneratorThreads = 0;
            _streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            return _streamer;
        }

        internal static bool GroundAt(Vector3 near, out float y)
        {
            y = 0.0f;
            RaycastHit[] hits = Physics.RaycastAll(near + Vector3.up * 6.0f, Vector3.down, 30.0f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (RaycastHit h in hits)
            {
                if (h.normal.y > 0.7f && h.point.y > best) best = h.point.y;
            }
            y = best;
            return best > float.MinValue;
        }

        /// <summary>The ground-floor panes of one wall of one building: where they are and how big, in the world.</summary>
        internal static IEnumerable<Candidate> PanesOf(CityWorld world, BuildingSite site, WindowGrid.Wall wall)
        {
            WindowPlan plan = WindowPlan.For(world, site);
            if (plan.IsBlankWall(wall)) yield break;
            CellLayout rows = WindowGrid.RowsFor(site.Params.Height);
            bool alongX = wall == WindowGrid.Wall.North || wall == WindowGrid.Wall.South;
            float u0 = (float)(alongX ? site.MinX : site.MinZ);
            float u1 = (float)(alongX ? site.MaxX : site.MaxZ);
            CellLayout columns = plan.ColumnsFor(u1 - u0);
            plan.Insets(WindowGrid.TopEdgeHasSlab(0, rows.Size, site.Params.Height), out float insetBottom, out float insetTop);
            if (wall == WindowGrid.Wall.North)
            {
                insetBottom = WindowGrid.PaneInsetVerticalClear;   // the door wall's ground row is cut clear (CityMeshBuilder.AddShellGlass)
                insetTop = WindowGrid.PaneInsetVerticalClear;
            }
            float floorY = site.BaseHeight;
            float paneY0 = floorY + rows.Size * insetBottom;
            float paneY1 = floorY + rows.Size - rows.Size * insetTop;
            Vector3 outward = CityMeshBuilder.WallNormal(site, wall);
            double doorU0 = site.CentreX - BuildingInterior.DoorWidth * 0.5, doorU1 = site.CentreX + BuildingInterior.DoorWidth * 0.5;
            for (int column = 1; column < columns.Count - 1; column++)   // not the end columns: the corner piers stand in front of them
            {
                if (!plan.IsGlazed(wall, column, 0)) continue;
                float cellU0 = u0 + column * columns.Size;
                if (wall == WindowGrid.Wall.North && cellU0 + columns.Size > doorU0 - 2.5 && cellU0 < doorU1 + 2.5) continue;   // the doorway and its entrance either side
                float u = cellU0 + columns.Size * 0.5f;
                double fx = alongX ? u : (wall == WindowGrid.Wall.East ? site.MaxX : site.MinX);
                double fz = alongX ? (wall == WindowGrid.Wall.North ? site.MaxZ : site.MinZ) : u;
                Vector3 onWall = TestSites.At(site, fx, floorY, fz);
                yield return new Candidate
                {
                    Site = site, Wall = wall, Column = column, Key = new PaneKey(site.Id, wall, column, 0),
                    Outside = onWall + outward * 0.7f, Inward = -outward, SillAboveGround = paneY0 - floorY, Opening = paneY1 - paneY0, Width = columns.PaneSize,
                };
            }
        }

        private IEnumerator Stream(Vector3 at)
        {
            _streamer.StreamAround(at, int.MaxValue);
            yield return null;
            yield return null;
            Physics.SyncTransforms();
        }

        private IEnumerator Shatter(PaneKey key, Vector3 near)
        {
            _streamer.Damage.Hit(key, shatterOutright: true);
            yield return null;
            yield return null;
            yield return null;
            yield return Stream(near);
        }

        private static bool Vaultable(Candidate c, out VaultProbe.Vault vault)
        {
            return VaultProbe.TryFind(c.Outside, c.Inward, BodyRig.StandingHeight, 0.3f, null, out vault);
        }

        /// <summary>True when a body can walk from 1.3 m outside the pane to right in front of it without hitting anything (the glass is further in).</summary>
        internal static bool ApproachClear(Candidate c)
        {
            Vector3 start = c.Outside - c.Inward * 0.6f;
            Vector3 bottom = start + Vector3.up * 0.45f;
            Vector3 top = start + Vector3.up * (BodyRig.StandingHeight - 0.35f);
            return !Physics.CapsuleCast(bottom, top, 0.28f, c.Inward, 0.9f, ~0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Everything solid between a body and the wall, for a failure message.</summary>
        private static string Blockers(Vector3 from, Vector3 inward)
        {
            var parts = new List<string>();
            foreach (float height in new[] { 0.15f, 0.5f, 1.0f, 1.6f, 2.3f })
            {
                if (Physics.Raycast(from + Vector3.up * height, inward, out RaycastHit hit, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    parts.Add($"{height:0.0} m up: {hit.collider.name} at {hit.distance:0.00} m");
                }
            }
            return parts.Count == 0 ? "nothing" : string.Join("; ", parts);
        }
#endif

        [UnityTest]
        public IEnumerator AShatteredSillHighWindowOnARealFacadeIsVaultedAndAWholeOneIsNot()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            Streamer();
            int buildingsTried = 0;
            int sillHighFound = 0;
            Candidate vaulted = null;
            foreach (BuildingSite site in TestSites.NearSpawn(world))
            {
                if (site.Params.Width < 10.0f || site.Params.Height < 8.0f || site.OnRoof || site.Params.IsSpecial) continue;
                if (buildingsTried++ > 24) break;
                foreach (WindowGrid.Wall wall in new[] { WindowGrid.Wall.East, WindowGrid.Wall.West, WindowGrid.Wall.South, WindowGrid.Wall.North })
                {
                    var panes = new List<Candidate>(PanesOf(world, site, wall));
                    if (panes.Count == 0) continue;
                    yield return Stream(panes[0].Outside);
                    // Only panes whose sill is at vault height ABOVE THE REAL GROUND outside (the lobby floor can be a podium above the street).
                    var sillHigh = new List<Candidate>();
                    foreach (Candidate c in panes)
                    {
                        if (!GroundAt(c.Outside, out float ground)) continue;
                        float sill = c.SillAboveGround + c.Site.BaseHeight - ground;
                        if (sill < BodyRig.StandingHeight * VaultProbe.BandLowFraction + 0.05f || sill > BodyRig.StandingHeight * VaultProbe.BandHighFraction - 0.05f) continue;
                        if (c.Opening < VaultProbe.MinHeadroomMetres + 0.1f) continue;
                        c.Outside.y = ground;
                        sillHigh.Add(c);
                    }
                    if (sillHigh.Count == 0) continue;
                    sillHighFound += sillHigh.Count;
                    foreach (Candidate c in sillHigh)
                    {
                        Assert.IsFalse(Vaultable(c, out _), $"building {c.Site.Id} {c.Wall} column {c.Column}: a WHOLE window was vaultable");
                    }
                    foreach (Candidate c in sillHigh)
                    {
                        yield return Shatter(c.Key, c.Outside);
                        if (Vaultable(c, out VaultProbe.Vault vault))
                        {
                            vaulted = c;
                            Assert.Less(vault.Headroom, BodyRig.StandingHeight, "the lintel over a real window was not seen");
                            Assert.Greater(vault.Headroom, VaultProbe.MinHeadroomMetres);
                            Debug.Log($"CityX window vault: building {c.Site.Id} {c.Wall} column {c.Column}: sill {vault.Height - c.Outside.y:0.00} m up, {vault.Headroom:0.00} m of opening, dive {PlayerVaulting.DiveFor(vault.Headroom, BodyRig.StandingHeight, out _):0.00}");
                            break;
                        }
                        Debug.Log($"CityX window vault: building {c.Site.Id} {c.Wall} column {c.Column} shattered but not vaultable (sill {c.SillAboveGround:0.00}, opening {c.Opening:0.00})");
                    }
                    if (vaulted != null) break;
                }
                if (vaulted != null) break;
            }
            Assert.Greater(sillHighFound, 0, "no building near spawn has a ground-floor window with its sill at vault height: nothing to test against");
            Assert.IsNotNull(vaulted, "no shattered sill-high window on a real facade could be vaulted into");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AShatteredLowSillWindowOnARealFacadeIsWalkedInto()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            Streamer();
            Candidate low = null;
            int buildingsTried = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world))
            {
                if (site.Params.Width < 10.0f || site.Params.Height < 8.0f || site.OnRoof || site.Params.IsSpecial) continue;
                if (buildingsTried++ > 24) break;
                // Not the door wall: its ground row is the entrance's own (AddDoorRow), not plain panes.
                foreach (WindowGrid.Wall wall in new[] { WindowGrid.Wall.East, WindowGrid.Wall.West, WindowGrid.Wall.South })
                {
                    var panes = new List<Candidate>(PanesOf(world, site, wall));
                    if (panes.Count == 0) continue;
                    yield return Stream(panes[0].Outside);
                    foreach (Candidate c in panes)
                    {
                        // The ground right at the wall's foot (a pavement step or plinth there is what the sill is really measured from).
                        if (!GroundAt(c.Outside, out float ground) || !GroundAt(c.Outside + c.Inward * 0.5f, out float groundAtWall)) continue;
                        float sill = c.SillAboveGround + c.Site.BaseHeight - Mathf.Max(ground, groundAtWall);
                        if (sill < 0.0f || sill > 0.35f || c.Opening < 2.2f) continue;   // a step, with a body's worth of opening
                        c.Outside.y = ground;
                        if (!ApproachClear(c)) continue;   // a planter, a bin, a ramp between the body and the wall: not what this test is about
                        low = c;
                        break;
                    }
                    if (low != null) break;
                }
                if (low != null) break;
            }
            if (low == null)
            {
                Assert.Inconclusive("no building near spawn has a ground-floor window with a step-high sill");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, low.Outside + low.Inward * -0.6f + Vector3.up * 0.05f, Quaternion.LookRotation(low.Inward, Vector3.up));
            _made.Add(player);
            player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            Time.captureFramerate = 60;
            yield return null; yield return null;
            float yaw = Quaternion.LookRotation(low.Inward, Vector3.up).eulerAngles.y;
            Vector3 wallFace = low.Outside + low.Inward * 0.7f;   // Outside is 0.7 m out from the face
            float Inside(Vector3 p) => Vector3.Dot(p - wallFace, low.Inward);   // metres past the wall's outside face

            // What stands in the pane's place, seen from outside at chest height (for the message when it is not glass): nearest first.
            string inTheWay = "nothing";
            RaycastHit[] ahead = Physics.RaycastAll(low.Outside + Vector3.up * 1.3f, low.Inward, 2.0f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(ahead, (a, b) => a.distance.CompareTo(b.distance));
            if (ahead.Length > 0)
            {
                inTheWay = string.Join(", ", System.Array.ConvertAll(ahead, h => $"{h.collider.name} ({(h.collider.isTrigger ? "trigger" : "solid")}) at {h.distance:0.00} m"));
            }
            Debug.Log($"CityX window walk-in: building {low.Site.Id} {low.Wall} column {low.Column}, sill {low.SillAboveGround:0.00} m, opening {low.Opening:0.00} m, in the way: {inTheWay}");

            for (int i = 0; i < 120; i++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = yaw }, 1.0f / 60.0f);
                yield return null;
            }
            Assert.Less(Inside(player.transform.position), 0.3f, $"walked through a WHOLE pane of glass (building {low.Site.Id} {low.Wall} column {low.Column}; in the way from outside: {inTheWay})");

            // The whole ground row of this wall goes (the builder's and the plan's column numbering need not agree on a door wall; the pane in front is what matters).
            foreach (Candidate c in PanesOf(world, low.Site, low.Wall))
            {
                _streamer.Damage.Hit(c.Key, shatterOutright: true);
            }
            yield return Shatter(low.Key, low.Outside);
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false; player.transform.position = low.Outside + low.Inward * -0.6f + Vector3.up * 0.05f; controller.enabled = true;
            Physics.SyncTransforms();
            for (int i = 0; i < 150; i++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = yaw }, 1.0f / 60.0f);
                yield return null;
            }
            string afterShatter = "nothing";
            RaycastHit[] after = Physics.RaycastAll(low.Outside + Vector3.up * 1.3f, low.Inward, 2.0f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(after, (a, b) => a.distance.CompareTo(b.distance));
            if (after.Length > 0)
            {
                afterShatter = string.Join(", ", System.Array.ConvertAll(after, h => $"{h.collider.name} ({(h.collider.isTrigger ? "trigger" : "solid")}) at {h.distance:0.00} m"));
            }
            // The room's floor just inside, which may stand higher than the sill (a raised lobby): getting over THAT is the interior's business, not the window's.
            string innerFloor = "none found";
            if (GroundAt(wallFace + low.Inward * 1.2f, out float floorInside))
            {
                innerFloor = $"{floorInside - player.transform.position.y:+0.00;-0.00} m against the body's feet";
            }
            float inside = Inside(player.transform.position);
            Debug.Log($"CityX window walk-in: after shattering the body is {inside:0.00} m past the wall face at y {player.transform.position.y:0.00}; floor inside {innerFloor}; ahead: {Blockers(player.transform.position, low.Inward)}");
            // With the glass gone the window no longer stops the body: it reaches the face (it was held 0.3 m short of it by the pane).
            Assert.Greater(inside, -0.12f, $"the shattered window still stops the body (building {low.Site.Id} {low.Wall} column {low.Column}, sill {low.SillAboveGround:0.00} m): " +
                $"it is {inside:0.00} m past the wall face, at y {player.transform.position.y:0.00} (ground {low.Outside.y:0.00}); ahead of it: {Blockers(player.transform.position, low.Inward)}; " +
                $"from outside at chest height after shattering: {afterShatter}");
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        /// <summary>Waits, by the clock (a batch-mode frame is under a millisecond), until the city has streamed in and every window change has landed.</summary>
        private IEnumerator Settle()
        {
            float until = Time.realtimeSinceStartup + 20.0f;
            int quiet = 0;
            while (Time.realtimeSinceStartup < until && quiet < 10)
            {
                quiet = _streamer.IsStreaming || _streamer.WindowRefreshPending ? 0 : quiet + 1;
                yield return null;
            }
            Physics.SyncTransforms();
        }

        /// <summary>Where a player runs at a window from: off its middle by these fractions of its width ...</summary>
        private static readonly float[] ApproachSides = { 0.0f, -0.25f, 0.25f };

        /// <summary>... this far out from the wall's face, metres (a stride out, and pressed against it) ...</summary>
        private static readonly float[] ApproachDistances = { 0.7f, 0.35f };

        /// <summary>... and this many degrees off square.</summary>
        private static readonly float[] ApproachTurns = { 0.0f, -25.0f, 25.0f };

        /// <summary>
        /// The vault probe from every approach to one window: how many of them vault, whether the straight-on one from the middle does, and (when asked)
        /// each refusal and a note of it.
        /// </summary>
        private static int VaultsFrom(Candidate c, out int tried, out bool headOn, Dictionary<VaultProbe.Refusal, int> refusals, List<string> notes)
        {
            Vector3 across = Vector3.Cross(Vector3.up, c.Inward).normalized;
            tried = 0;
            headOn = false;
            int passed = 0;
            foreach (float side in ApproachSides)
            foreach (float outFromWall in ApproachDistances)
            foreach (float turn in ApproachTurns)
            {
                Vector3 feet = c.Outside + c.Inward * (0.7f - outFromWall) + across * (side * c.Width);
                if (!GroundAt(feet, out float y)) continue;
                feet.y = y;
                Vector3 run = Quaternion.AngleAxis(turn, Vector3.up) * c.Inward;
                tried++;
                if (VaultProbe.TryFind(feet, run, BodyRig.StandingHeight, 0.3f, null, out _, out VaultProbe.Refusal refusal))
                {
                    passed++;
                    headOn |= side == 0.0f && turn == 0.0f && outFromWall > 0.5f;
                    continue;
                }
                if (refusals != null)
                {
                    refusals.TryGetValue(refusal, out int n);
                    refusals[refusal] = n + 1;
                }
                notes?.Add($"{side:+0.00;-0.00;0}w {outFromWall:0.00}m {turn:+0;-0;0}deg {refusal}");
            }
            return passed;
        }

        /// <summary>
        /// The dive a climber makes into a window too high to vault from the street (PlayerClimbing): hanging on the wall under it, 0.35 m out, as the
        /// sill comes into the hands' reach, the vault looked for as PlayerClimbing looks for it (lifted to that reach).
        /// </summary>
        private static bool ClimbsIn(Candidate c, out VaultProbe.Refusal why)
        {
            float sillY = c.Site.BaseHeight + c.SillAboveGround;
            // The real sill, as the climber's hands find it: a shop front's stall riser stands under some door-side windows, up to 0.35 m over
            // the plan's pane bottom (seed 74102958), and a climber placed by the plan's number had it out of reach. In the game the climb asks
            // the vault probe every frame as it rises, so it meets the sill wherever it really is.
            Vector3 over = c.Outside + c.Inward * 0.78f;
            over.y = sillY + 1.2f;
            if (Physics.Raycast(over, Vector3.down, out RaycastHit realSill, 1.6f, ~0, QueryTriggerInteraction.Ignore) && realSill.normal.y > 0.7f)
            {
                sillY = realSill.point.y;
            }
            Vector3 feet = c.Outside + c.Inward * 0.35f;
            feet.y = sillY - BodyRig.StandingHeight * PlayerClimbing.MantleReachFraction + 0.1f;
            return VaultProbe.TryFind(feet + Vector3.up * (BodyRig.StandingHeight * PlayerClimbing.WindowReachLiftFraction), c.Inward,
                BodyRig.StandingHeight, 0.3f, null, out VaultProbe.Vault vault, out why) && VaultProbe.IsThroughAnOpening(vault, BodyRig.StandingHeight);
        }
#endif

        /// <summary>
        /// MOST WINDOWS, NOT ONE (owner, 2026-10-14: "the vaulting through window function isnt working on most windows"; it vaulted 46 of 315).
        /// Every ground-floor window near spawn whose sill is at vault height over the street is run at the way a player does: at its middle and a
        /// quarter of its width to either side, from a stride out and pressed against the wall, straight on and 25 degrees off. While whole, none
        /// may be vaulted from anywhere; shattered, nearly all must be from straight on and most from everywhere. Each refusal is logged with the
        /// stage of VaultProbe that refused it. The windows too high over the street to vault are climbed to instead: shattered, most must be dived
        /// into from the climb (PlayerClimbing), and none while whole.
        /// </summary>
        [UnityTest]
        public IEnumerator MostShatteredSillHighWindowsOnRealFacadesAreVaulted()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            Streamer();
            var refusals = new Dictionary<VaultProbe.Refusal, int>();
            var log = new System.Text.StringBuilder();
            var highLog = new System.Text.StringBuilder();
            var wholeVaulted = new List<string>();
            int windows = 0, windowsVaulted = 0, attempts = 0, attemptsVaulted = 0, buildingsTried = 0, stepHigh = 0, highWindows = 0, highClimbedIn = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world))
            {
                if (site.OnRoof || site.Params.IsSpecial || site.IsOpenGround || site.Sides >= 3) continue;   // an open car park has no windows; a round tower is not four walls
                if (buildingsTried++ >= 30) break;
                foreach (WindowGrid.Wall wall in new[] { WindowGrid.Wall.East, WindowGrid.Wall.West, WindowGrid.Wall.South, WindowGrid.Wall.North })
                {
                    var panes = new List<Candidate>(PanesOf(world, site, wall));
                    if (panes.Count == 0) continue;
                    yield return Stream(panes[0].Outside);
                    yield return Settle();
                    var sillHigh = new List<Candidate>();
                    var high = new List<Candidate>();
                    foreach (Candidate c in panes)
                    {
                        if (!GroundAt(c.Outside, out float ground)) continue;
                        c.SillOverGround = c.SillAboveGround + c.Site.BaseHeight - ground;
                        if (c.Opening < VaultProbe.MinHeadroomMetres + 0.1f) continue;
                        if (c.SillOverGround > BodyRig.StandingHeight * VaultProbe.BandHighFraction - 0.05f) { high.Add(c); continue; }
                        if (c.SillOverGround < BodyRig.StandingHeight * VaultProbe.BandLowFraction + 0.05f) { stepHigh++; continue; }
                        c.Outside.y = ground;
                        sillHigh.Add(c);
                    }
                    if (sillHigh.Count == 0 && high.Count == 0) continue;
                    foreach (Candidate c in sillHigh)
                    {
                        int whole = VaultsFrom(c, out int wholeTried, out _, null, null);
                        if (whole > 0) wholeVaulted.Add($"building {c.Site.Id} {c.Wall} column {c.Column} ({whole}/{wholeTried})");
                    }
                    foreach (Candidate c in high)
                    {
                        if (ClimbsIn(c, out _)) wholeVaulted.Add($"building {c.Site.Id} {c.Wall} column {c.Column} (from a climb)");
                    }
                    foreach (Candidate c in sillHigh) _streamer.Damage.Hit(c.Key, shatterOutright: true);
                    foreach (Candidate c in high) _streamer.Damage.Hit(c.Key, shatterOutright: true);
                    yield return Settle();
                    yield return Stream(panes[0].Outside);
                    yield return Settle();
                    foreach (Candidate c in sillHigh)
                    {
                        windows++;
                        var notes = new List<string>();
                        int passed = VaultsFrom(c, out int tried, out bool headOn, refusals, notes);
                        attempts += tried;
                        attemptsVaulted += passed;
                        if (headOn) windowsVaulted++;
                        log.AppendLine($"  building {c.Site.Id} {c.Wall} column {c.Column}: sill {c.SillOverGround:0.00} m over the street, opening {c.Opening:0.00} m, pane {c.Width:0.00} m wide, " +
                            $"{passed}/{tried} vaulted" + (notes.Count > 0 ? ": " + string.Join(", ", notes) : ""));
                    }
                    foreach (Candidate c in high)
                    {
                        highWindows++;
                        if (ClimbsIn(c, out VaultProbe.Refusal why))
                        {
                            highClimbedIn++;
                            continue;
                        }
                        highLog.AppendLine($"  building {c.Site.Id} {c.Wall} column {c.Column}: sill {c.SillOverGround:0.00} m over the street, opening {c.Opening:0.00} m, not dived into from a climb: {why}");
                    }
                }
            }
            var summary = new System.Text.StringBuilder();
            foreach (KeyValuePair<VaultProbe.Refusal, int> pair in refusals) summary.Append($" {pair.Key} {pair.Value}");
            Debug.Log($"CityX window vault survey: {buildingsTried} buildings; ground-floor windows: {windows} sill-high, {stepHigh} a step up, {highWindows} too high to vault; " +
                $"shattered, {windowsVaulted} vaulted straight on, {attemptsVaulted}/{attempts} approaches vaulted, {highClimbedIn}/{highWindows} high ones dived into from a climb; " +
                $"refusals:{summary}; whole ones gone through: {wholeVaulted.Count}\n{log}{highLog}");
            Assert.IsEmpty(wholeVaulted, "a WHOLE window was gone through: " + string.Join("; ", wholeVaulted));
            Assert.Greater(windows, 20, "too few sill-high ground-floor windows near spawn to judge");
            Assert.GreaterOrEqual(windowsVaulted, windows * 0.9f, $"only {windowsVaulted} of {windows} shattered sill-high windows vault straight on; refusals:{summary}");
            Assert.GreaterOrEqual(attemptsVaulted, attempts * 0.8f, $"only {attemptsVaulted} of {attempts} approaches to shattered windows vault; refusals:{summary}");
            Assert.Greater(highWindows, 5, "too few windows too high to vault near spawn to judge the climb-in");
            Assert.GreaterOrEqual(highClimbedIn, highWindows * 0.8f, $"only {highClimbedIn} of {highWindows} shattered windows too high to vault are dived into from a climb");
#else
            yield break;
#endif
        }
    }
}
