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
    /// GETTING INTO BUILDINGS WITHOUT STOPPING DEAD (owner, 2026-10-14, a screenshot of the body held at the foot of a shop's floor-length opening:
    /// "im getting stuck when running into small obstacles like this when i should just be running right over this"). On the city's own buildings
    /// near spawn: every front door with no steps built in front of it is walked and run into (the door open), and every floor-length window on the
    /// door side, shattered, is run straight into -- whenever the step up to it is one a body takes in its stride (StepUpProbe, a quarter of its
    /// height). Each step is measured and logged, the taller ones too, so a failure says how tall the thing in the way was.
    /// </summary>
    public sealed class RealCityDoorwayPlayTests
    {
        private const int Seed = 23481876;
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
        private CityStreamer _streamer;
        private string _lastTraced;

        /// <summary>The tallest step a body takes in its stride, less a little for the controller's skin and the pavement's unevenness.</summary>
        private static float Steppable => StepUpProbe.MaxRise(BodyRig.StandingHeight) - 0.03f;

        private void Streamer()
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
        }

        private IEnumerator Stream(Vector3 at)
        {
            _streamer.StreamAround(at, int.MaxValue);
            yield return null;
            yield return null;
            Physics.SyncTransforms();
        }

        /// <summary>Waits, by the clock, until the city has streamed in and every window change has landed.</summary>
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

        /// <summary>
        /// The first walkable surface straight down from <paramref name="fromY"/> at <paramref name="at"/>. Cast from just over the floor, not from
        /// high up: a canopy or a balcony over a door is not the pavement under it.
        /// </summary>
        private static bool SurfaceBelow(Vector3 at, float fromY, out float y)
        {
            y = 0.0f;
            RaycastHit[] hits = Physics.RaycastAll(new Vector3(at.x, fromY, at.z), Vector3.down, 12.0f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (RaycastHit h in hits)
            {
                if (h.normal.y > 0.7f && h.point.y > best) best = h.point.y;
            }
            y = best;
            return best > float.MinValue;
        }

        /// <summary>
        /// A fresh body set down <paramref name="from"/>, facing <paramref name="inward"/>, held on W (and Shift when <paramref name="sprint"/>) for
        /// <paramref name="seconds"/>, ticked straight through PlayerLocomotion as the game does; hands back where it ended.
        /// </summary>
        private IEnumerator RunInto(Vector3 from, Vector3 inward, bool sprint, float seconds, System.Action<Vector3> done,
            Vector3 face = default, System.Text.StringBuilder trace = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(prefab, from + Vector3.up * 0.05f, Quaternion.LookRotation(inward, Vector3.up));
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var body = player.GetComponent<PlayerLocomotion>();
            float yaw = Quaternion.LookRotation(inward, Vector3.up).eulerAngles.y;
            for (int i = 0; i < 10; i++)
            {
                body.Tick(new LocomotionInput { YawDegrees = yaw }, 1.0f / 60.0f);
                yield return null;
            }
            var forward = new LocomotionInput { Move = Vector2.up, YawDegrees = yaw, Sprint = sprint };
            for (float t = 0.0f; t < seconds; t += 1.0f / 60.0f)
            {
                body.Tick(forward, 1.0f / 60.0f);
                if (trace != null)
                {
                    Vector3 p = player.transform.position;
                    float inside = Vector3.Dot(p - face, inward);
                    string state = $"in {inside:+0.000;-0.000} y {p.y - face.y:+0.000;-0.000} step {body.IsSteppingUp} grounded {body.IsGrounded} " +
                                   $"flags {body.LastMoveCollisionFlags} wall {(body.HitWallThisFrame ? body.LastWallHitNormal.ToString("F2") : "-")} v {body.Velocity.ToString("F2")}";
                    if (inside > -1.0f && inside < 1.5f && state != _lastTraced)
                    {
                        trace.AppendLine($"    t {t:0.000} {state}");
                    }
                    _lastTraced = state;
                }
                yield return null;
            }
            done(player.transform.position);
            Object.Destroy(player);
            yield return null;
        }

        /// <summary>The free height over a window's sill, the lowest of three places across the wall's thickness (rays up from just over the sill).</summary>
        private static float OpeningOver(Vector3 face, Vector3 inward, float sillY)
        {
            float lowest = float.MaxValue;
            foreach (float depth in new[] { 0.06f, 0.15f, 0.24f })
            {
                Vector3 from = face + inward * depth;
                from.y = sillY + 0.05f;
                lowest = Mathf.Min(lowest, Physics.Raycast(from, Vector3.up, out RaycastHit hit, 6.0f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance + 0.05f : 6.0f);
            }
            return lowest;
        }

        /// <summary>Whether a standing body fits on the floor just inside the wall (nothing solid where it would stand).</summary>
        private static bool RoomInside(Vector3 face, Vector3 inward, float floorY)
        {
            const float radius = 0.3f;
            Vector3 at = face + inward * (BuildingInterior.WallThickness + radius + 0.15f);
            at.y = floorY;
            Vector3 low = at + Vector3.up * (radius + 0.08f), high = at + Vector3.up * (BodyRig.StandingHeight - radius);
            foreach (Collider c in Physics.OverlapCapsule(low, high, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            return true;
        }

        private static string Ahead(Vector3 feet, Vector3 inward)
        {
            var parts = new List<string>();
            foreach (float height in new[] { 0.1f, 0.3f, 0.5f, 0.7f, 1.0f, 1.6f, 2.4f })
            {
                if (Physics.Raycast(feet + Vector3.up * height, inward, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    parts.Add($"{height:0.0} m up: {hit.collider.name} at {hit.distance:0.00} m");
                }
            }
            return parts.Count == 0 ? "nothing" : string.Join("; ", parts);
        }

        /// <summary>What is over a body standing at <paramref name="feet"/>: rays straight up from its shoulders, across it and along <paramref name="inward"/>.</summary>
        private static string Overhead(Vector3 feet, Vector3 inward)
        {
            Vector3 across = Vector3.Cross(Vector3.up, inward);
            var parts = new List<string>();
            foreach (float alongIn in new[] { -0.3f, 0.0f, 0.3f })
            {
                foreach (float side in new[] { -0.3f, 0.0f, 0.3f })
                {
                    Vector3 from = feet + Vector3.up * 2.0f + inward * alongIn + across * side;
                    if (Physics.Raycast(from, Vector3.up, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        parts.Add($"(in {alongIn:+0.0;-0.0;0}, across {side:+0.0;-0.0;0}): {2.0f + hit.distance:0.00} m up, normal {hit.normal.ToString("F2")}");
                    }
                }
            }
            var floor = new List<string>();
            for (float alongIn = 0.0f; alongIn <= 1.0f; alongIn += 0.05f)
            {
                if (Physics.Raycast(feet + Vector3.up * 0.8f + inward * alongIn, Vector3.down, out RaycastHit under, 2.0f, ~0, QueryTriggerInteraction.Ignore))
                {
                    floor.Add($"{alongIn:0.00}:{0.8f - under.distance:+0.00;-0.00}");
                }
            }
            parts.Add("floor ahead (m in: height over the feet) " + string.Join(" ", floor));
            var widths = new List<string>();
            foreach (float alongIn in new[] { -0.15f, 0.0f, 0.1f, 0.2f })
            {
                foreach (float height in new[] { 0.3f, 1.3f, 2.3f })
                {
                    Vector3 from = feet + Vector3.up * height + inward * alongIn;
                    float left = Physics.Raycast(from, -across, out RaycastHit l, 2.0f, ~0, QueryTriggerInteraction.Ignore) ? l.distance : 9.0f;
                    float right = Physics.Raycast(from, across, out RaycastHit r, 2.0f, ~0, QueryTriggerInteraction.Ignore) ? r.distance : 9.0f;
                    widths.Add($"{alongIn:+0.00;-0.00}/{height:0.0}: {left:0.00}+{right:0.00}");
                }
            }
            parts.Add("clear either side (m in/m up: left+right) " + string.Join(" ", widths));
            foreach (float height in new[] { 2.5f, 2.6f, 2.7f })
            {
                if (Physics.Raycast(feet + Vector3.up * height, inward, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    parts.Add($"ahead at {height:0.0} m up: {hit.distance:0.00} m, normal {hit.normal.ToString("F2")}");
                }
                if (Physics.Raycast(feet + Vector3.up * height - inward * 0.6f, inward, out hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                {
                    parts.Add($"from 0.6 m back at {height:0.0} m up: {hit.distance - 0.6f:+0.00;-0.00} m, normal {hit.normal.ToString("F2")}");
                }
            }
            return parts.Count == 0 ? "nothing" : string.Join("; ", parts);
        }
#endif

        /// <summary>
        /// Every front door near spawn that has no steps built in front of it (BuildingDecor.EntranceStyleFor) is walked into and run into from
        /// three metres out, its door open, when the step at the wall's foot is one a body takes in its stride.
        /// </summary>
        [UnityTest]
        public IEnumerator FrontDoorsWithoutStepsAreWalkedAndRunInto()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            Streamer();
            Time.captureFramerate = 60;
            var log = new System.Text.StringBuilder();
            var failures = new List<string>();
            var tooTall = new List<string>();
            int buildings = 0, withStairs = 0, doors = 0, attempts = 0, entered = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 30))
            {
                if (site.OnRoof || site.Shell || site.Params.IsSpecial || site.IsOpenGround) continue;   // no door (CityStreamer.AddDoor)
                if (BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight).NeedsStairs)
                {
                    withStairs++;
                    continue;
                }
                // Fourteen doors without steps, however many buildings that takes: on the hills (seed 19077151) the first ninety buildings near the
                // spawn held one (50 held 7 on seed 74102958, before the hills).
                if (buildings++ >= 14) break;
                site.LocalToWorld(0.0, site.FrontHalfDepth, out double fx, out double fz);
                Vector3 outward = BuildingDoor.PanelFor(site).Facing.normalized;
                Vector3 face = new Vector3((float)fx, site.BaseHeight, (float)fz);
                yield return Stream(face);
                yield return Settle();
                foreach (BuildingDoor door in Object.FindObjectsByType<BuildingDoor>(FindObjectsSortMode.None))
                {
                    door.SetOpen(true);
                }
                for (int i = 0; i < 60; i++) yield return null;
                Physics.SyncTransforms();

                Vector3 start = face + outward * 3.0f;
                if (!SurfaceBelow(start, site.BaseHeight + 1.0f, out float ground) || !SurfaceBelow(face + outward * 0.3f, site.BaseHeight + 1.0f, out float atWall)) continue;
                start.y = ground;
                float step = site.BaseHeight - atWall;
                doors++;
                foreach (bool sprint in new[] { false, true })
                {
                    Vector3 end = default;
                    yield return RunInto(start, -outward, sprint, 2.5f, p => end = p);
                    float inside = Vector3.Dot(end - face, -outward);
                    string line = $"building {site.Id} ({(sprint ? "running" : "walking")}): the floor {step:0.00} m over the paving at the wall, " +
                                  $"{site.BaseHeight - ground:0.00} m over it 3 m out; ended {inside:0.00} m inside at y {end.y - site.BaseHeight:+0.00;-0.00} against the floor";
                    if (inside > 1.0f)
                    {
                        entered++;
                        attempts++;
                        log.AppendLine("  " + line);
                    }
                    else if (step <= Steppable)
                    {
                        attempts++;
                        failures.Add(line + "; ahead: " + Ahead(end, -outward));
                    }
                    else
                    {
                        tooTall.Add(line);
                    }
                }
            }
            Debug.Log($"CityX doorway survey: {buildings} buildings, {withStairs} with steps built, {doors} doors without; {entered}/{attempts} " +
                      $"steppable walks and runs entered\n{log}" +
                      (tooTall.Count > 0 ? "TOO TALL A STEP, NO STAIRS:\n  " + string.Join("\n  ", tooTall) + "\n" : "") +
                      (failures.Count > 0 ? "STUCK:\n  " + string.Join("\n  ", failures) : ""));
            Assert.Greater(doors, 8, "too few doors without steps near spawn to judge");
            Assert.IsEmpty(failures, $"{failures.Count} of {attempts} walks and runs at a door with a step a body takes stopped short:\n  " + string.Join("\n  ", failures));
#else
            yield break;
#endif
        }

        /// <summary>
        /// The ground row of a door wall is glazed floor to ceiling (a shop front). Shattered, each such window near spawn with room for a standing
        /// body is run straight into from 2.5 m out; when its sill is a step a body takes in its stride, the body must end up inside, not held at
        /// the frame. The real sill (the first thing under the opening) is measured, not the plan's.
        /// </summary>
        [UnityTest]
        public IEnumerator ShatteredFloorLengthWindowsAreRunStraightInto()
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            Streamer();
            Time.captureFramerate = 60;
            var log = new System.Text.StringBuilder();
            var failures = new List<string>();
            var tooTall = new List<string>();
            int buildings = 0, windows = 0, steppable = 0, entered = 0;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 20))
            {
                if (site.OnRoof || site.Shell || site.Params.IsSpecial || site.IsOpenGround || site.Sides >= 3) continue;
                if (buildings++ >= 70) break;   // 40 held only 5 steppable shop windows on seed 61928374 (a sparser spawn)
                var panes = new List<RealCityWindowVaultPlayTests.Candidate>(RealCityWindowVaultPlayTests.PanesOf(world, site, WindowGrid.Wall.North));
                if (panes.Count == 0) continue;
                yield return Stream(panes[0].Outside);
                yield return Settle();
                var tall = new List<RealCityWindowVaultPlayTests.Candidate>();
                foreach (RealCityWindowVaultPlayTests.Candidate c in panes)
                {
                    if (c.Opening < BodyRig.StandingHeight + 0.15f) continue;   // a body does not stand up in it: a dive (the vault) is its way in
                    tall.Add(c);
                    if (tall.Count == 2) break;   // two a building is plenty
                }
                if (tall.Count == 0) continue;
                foreach (RealCityWindowVaultPlayTests.Candidate c in tall) _streamer.Damage.Hit(c.Key, shatterOutright: true);
                yield return Settle();
                yield return Stream(panes[0].Outside);
                yield return Settle();
                for (int i = 0; i < 90; i++) yield return null;   // the shards settle
                foreach (RealCityWindowVaultPlayTests.Candidate c in tall)
                {
                    Vector3 face = c.Outside + c.Inward * 0.7f;
                    Vector3 start = c.Outside - c.Inward * 1.8f;
                    float paneBottom = site.BaseHeight + c.SillAboveGround;
                    if (!SurfaceBelow(start, site.BaseHeight + 1.0f, out float ground) ||
                        !SurfaceBelow(face - c.Inward * 0.3f, site.BaseHeight + 1.0f, out float paving) ||
                        !SurfaceBelow(face + c.Inward * 0.08f, paneBottom + 1.0f, out float sill)) continue;
                    windows++;
                    start.y = ground;
                    float rise = sill - paving;
                    Vector3 end = default;
                    var trace = new System.Text.StringBuilder();
                    yield return RunInto(start, c.Inward, true, 2.0f, p => end = p, face, trace);
                    float inside = Vector3.Dot(end - face, c.Inward);
                    string line = $"building {site.Id} column {c.Column}: the sill {rise:0.00} m over the paving at the wall (the plan's {paneBottom - paving:0.00}), " +
                                  $"opening {c.Opening:0.00} m by {c.Width:0.00} m; ended {inside:0.00} m past the wall face at y {end.y - site.BaseHeight:+0.00;-0.00} against the floor";
                    // A body steps in only where it fits standing: the real opening over the sill (the lowest across the wall's thickness -- a shop
                    // front's stall riser or a lintel on the inside face makes it lower than the plan's) and standing room just inside (a stair, a
                    // ceiling). Seed 74102958 has both kinds; they are logged, not failed.
                    float opening = OpeningOver(face, c.Inward, sill);
                    bool roomInside = RoomInside(face, c.Inward, site.BaseHeight);
                    if (rise <= Steppable && (opening < BodyRig.StandingHeight + 0.05f || !roomInside))
                    {
                        tooTall.Add(line + $" (no room to stand: opening {opening:0.00} m, room inside {roomInside})");
                        continue;
                    }
                    bool canStep = rise <= Steppable;
                    if (canStep) steppable++;
                    if (inside > BuildingInterior.WallThickness + 0.6f)
                    {
                        entered++;
                        log.AppendLine("  " + line);
                    }
                    else if (canStep)
                    {
                        failures.Add(line + "; ahead: " + Ahead(end, c.Inward) + "; overhead: " + Overhead(end, c.Inward) +
                                     (failures.Count == 0 ? "\n" + trace : ""));
                    }
                    else
                    {
                        tooTall.Add(line);
                    }
                }
            }
            Debug.Log($"CityX shop-window run-in survey: {buildings} buildings, {windows} floor-length windows shattered, {steppable} with a sill a body " +
                      $"steps; {entered} run into\n{log}" +
                      (tooTall.Count > 0 ? "TOO TALL TO STEP (a vault or a climb):\n  " + string.Join("\n  ", tooTall) + "\n" : "") +
                      (failures.Count > 0 ? "STUCK:\n  " + string.Join("\n  ", failures) : ""));
            Assert.Greater(steppable, 5, "too few floor-length windows near spawn with a sill a body steps to judge");
            Assert.IsEmpty(failures, $"{failures.Count} of {steppable} runs into a shattered floor-length window with a sill a body steps stopped short:\n  " +
                                     string.Join("\n  ", failures));
#else
            yield break;
#endif
        }
    }
}
