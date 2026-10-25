using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The folded stairs up to raised front doors are walked (owner, 2026-10-15: "make everything walkable and functionable as a pedestrian not
    /// just look good"; the stairs fold back along their own front since 2026-10-16, World/EntranceStairs.PlanFolded): the real player controller,
    /// in a real streamed city, climbs from the paving at a stair's foot up every flight and round every platform where it turns to the landing at
    /// the door, on the broad folded stair near the spawn with the most lanes and on a tight one (EntranceStairs.NarrowWidth).
    /// </summary>
    public sealed class EntranceStairWalkPlayTests
    {
        private const int Seed = 23481876;
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        [UnityTest]
        public IEnumerator TheRealPlayerClimbsAFoldedEntranceStairToItsDoor() => ClimbToTheDoor(false);

        [UnityTest]
        public IEnumerator TheRealPlayerClimbsATightFoldedEntranceStairToItsDoor() => ClimbToTheDoor(true);

        private IEnumerator ClimbToTheDoor(bool tight)
        {
            var world = new CityWorld(Seed);
            // The raised door near the spawn whose stair folds into the most lanes (then has the most steps), reaching the ground on a flight.
            BuildingSite site = BuildingSite.None;
            EntranceStairs.Folded stair = null;
            foreach (BuildingSite s in TestSites.NearSpawn(world, 14))
            {
                if (s.OnRoof || s.Shell || s.Sides >= 3 || !BuildingDecor.EntranceStyleFor(s.BaseHeight - s.DoorGroundHeight).NeedsStairs) continue;
                EntranceStairs.Folded f = EntranceStairs.ForDoor(world.Plan, s, 0.0f, s.BaseHeight, s.Params.Depth * 0.5f, s.Params.Width * 0.5f);
                if (!f.Reaches || f.Lanes < 2 || f.Turns.Count >= f.Runs.Count || (f.LaneWidth < EntranceStairs.Width) != tight) continue;
                if (stair == null || f.Lanes > stair.Lanes || f.Lanes == stair.Lanes && f.Steps > stair.Steps)
                {
                    site = s;
                    stair = f;
                }
            }
            Assert.IsNotNull(stair, $"no {(tight ? "tight " : "")}folded entrance stair near the spawn");

            // Its walking line, up from the foot, in the building's frame (x along the wall from the door, z out from it) with the surface's height.
            float wallZ = site.Params.Depth * 0.5f, w = stair.LaneWidth;
            float LaneZ(int lane) => wallZ + (lane + 0.5f) * w;
            float Paving(float x, float z)
            {
                site.LocalToWorld(x, z, out double wx, out double wz);
                return (float)(world.Plan.GroundHeight(wx, wz) + PlanBuildings.PavingRise);
            }
            var local = new List<Vector3>();
            void Add(float x, float y, float z)
            {
                var p = new Vector3(x, y, z);
                if (local.Count == 0 || (local[local.Count - 1] - p).sqrMagnitude > 1e-4f) local.Add(p);
            }
            EntranceStairs.Run last = stair.Runs[stair.Runs.Count - 1];
            float footX = last.End + last.Dir * 0.8f;
            Add(footX, Paving(footX, LaneZ(last.Lane)), LaneZ(last.Lane));
            for (int i = stair.Runs.Count - 1; i >= 0; i--)
            {
                EntranceStairs.Run run = stair.Runs[i];
                Add(run.End, run.Bottom, LaneZ(run.Lane));
                Add(run.Start, run.From, LaneZ(run.Lane));
                if (i > 0)
                {
                    // Round the platform between this flight and the one above: to its middle, across into the lane above, back to that flight's foot.
                    EntranceStairs.Turn turn = stair.Turns[i - 1];
                    float mx = (turn.X0 + turn.X1) * 0.5f;
                    Add(mx, turn.Top, LaneZ(run.Lane));
                    Add(mx, turn.Top, LaneZ(turn.Lane));
                }
            }
            Add(0.0f, stair.Runs[0].From, LaneZ(0));   // the landing at the door
            var line = new Vector3[local.Count];
            for (int i = 0; i < local.Count; i++)
            {
                site.LocalToWorld(local[i].x, local[i].z, out double wx, out double wz);
                line[i] = new Vector3((float)wx, local[i].y, (float)wz);
            }

            City(line[line.Length - 1], site.Params.Width * 0.5f + 12.0f);
            yield return null;
            var made = new PlayerLocomotion[1];
            yield return Player(line[0] + Vector3.up * 0.1f, made);
            PlayerLocomotion locomotion = made[0];
            var result = new float[3];
            yield return WalkLine(locomotion, line, result);
            Vector3 at = locomotion.transform.position;
            float door = stair.Runs[0].From;
            Debug.Log($"CityX entrance stair walk ({(tight ? "tight" : "broad")}): #{site.Id}, its door {door - Paving(0.0f, wallZ + 0.3f):F1} m over the paving, " +
                $"{stair.Lanes} lanes {w:F1} m wide, {stair.Steps} steps, {stair.Turns.Count} turns: walked {result[0]:F1} of {result[2]:F1} m, ended at y {at.y:F2} " +
                $"(the door {door:F2}), at most {result[1]:F2} m off the walking line");
            Assert.That(result[0], Is.GreaterThan(result[2] - 1.0f), $"the climb stopped {result[2] - result[0]:F1} m short of the door's landing");
            Assert.That(at.y, Is.EqualTo(door).Within(0.4f), "the body is not up on the door's landing");
            Assert.That(result[1], Is.LessThan(0.6f), "the body left the stair");
        }

        private void City(Vector3 around, double reach)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = Mathf.CeilToInt((float)(reach / PlanWorld.ChunkSize)) + 1;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            streamer.StreamAround(around, int.MaxValue);
            Physics.SyncTransforms();
        }

        private IEnumerator Player(Vector3 feet, PlayerLocomotion[] made)
        {
            var player = new GameObject("Player");
            _made.Add(player);
            player.AddComponent<CharacterController>();
            var locomotion = player.AddComponent<PlayerLocomotion>();
            yield return null;
            player.transform.position = feet;
            Physics.SyncTransforms();
            for (int i = 0; i < 20; i++)
            {
                locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
            made[0] = locomotion;
        }

        /// <summary>
        /// Walks the player along a walking line (world points, each at the surface's height), steering at a point 0.7 m ahead on it: how far
        /// along it got, the worst it stood off the line's height, and the line's length.
        /// </summary>
        private static IEnumerator WalkLine(PlayerLocomotion locomotion, Vector3[] line, float[] result)
        {
            Transform body = locomotion.transform;
            float length = 0.0f;
            var at = new float[line.Length];
            for (int i = 1; i < line.Length; i++)
            {
                length += Flat(line[i] - line[i - 1]).magnitude;
                at[i] = length;
            }
            float s = 0.0f, worst = 0.0f;
            for (int tick = 0; tick < (int)(length * 60.0f) + 900; tick++)
            {
                if (s >= length - 0.3f) break;
                Vector3 p = body.position;
                Vector3 toward = Flat(PointAt(line, at, Mathf.Min(length, s + 0.7f)) - p);
                locomotion.Tick(new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg }, Step);
                yield return null;
                // Where along the line the body is now: the nearest point of it a little behind or ahead of where it was.
                p = body.position;
                float best = s, bestD = float.MaxValue;
                for (float q = s - 0.8f; q <= s + 1.2f; q += 0.05f)
                {
                    float c = Mathf.Clamp(q, 0.0f, length);
                    float d = Flat(PointAt(line, at, c) - p).magnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = c;
                    }
                }
                s = best;
                worst = Mathf.Max(worst, Mathf.Abs(p.y - PointAt(line, at, s).y));
            }
            result[0] = s;
            result[1] = worst;
            result[2] = length;
        }

        private static Vector3 PointAt(Vector3[] line, float[] at, float s)
        {
            for (int i = 1; i < line.Length; i++)
            {
                if (s <= at[i] || i == line.Length - 1)
                {
                    float span = at[i] - at[i - 1];
                    return Vector3.Lerp(line[i - 1], line[i], span < 1e-5f ? 1.0f : Mathf.Clamp01((s - at[i - 1]) / span));
                }
            }
            return line[line.Length - 1];
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }
}
