using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The real player goes up a stair to a roof (World/RoofStairs) as a player does (owner, 2026-11-12: "walking up this staircase is super glitchy
    /// and barely works"): walking and sprinting with the camera turned toward the walking line a little ahead at each landing, and walking with
    /// the camera kept up the flight it came up and the keys sidestepping round the landing. Measured: the posts under the upper flights stood up
    /// through the lower ones (a walk stuck on them), and the arms' remembered walls held a sprint at the first landing and threw it back down the
    /// flight (it never got up).
    /// </summary>
    public sealed class RoofStairWalkPlayTests
    {
        private const int Seed = CitySeed.Default;
        private readonly List<GameObject> _made = new List<GameObject>();

        public enum Going
        {
            Walk,
            Sprint,
            Sidestep,
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        private void City(Vector3 around)
        {
            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 3;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            go.SetActive(true);
            streamer.StreamAround(around, int.MaxValue);
            Physics.SyncTransforms();
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator ThePlayerGoesUpARoofStairWithoutStalling([Values] Going going)
        {
#if UNITY_EDITOR
            var world = new CityWorld(Seed);
            RoofStair roof = null;
            BuildingSite on = BuildingSite.None;
            foreach (BuildingSite site in TestSites.NearSpawn(world, 60))
            {
                roof = RoofStairs.For(world, site);
                if (roof != null && roof.Stair.Flights.Length >= 4)
                {
                    on = site;
                    break;
                }
            }
            Assert.IsNotNull(roof, "no block near the spawn has a stair of four flights up to its roof");
            StreetStair stair = roof.Stair;
            // The walking line from the foot up.
            var line = new Vector3[stair.Points.Length];
            for (int i = 0; i < line.Length; i++) line[i] = stair.Points[stair.Points.Length - 1 - i];
            var at = new float[line.Length];
            for (int i = 1; i < line.Length; i++) at[i] = at[i - 1] + Flat(line[i] - line[i - 1]).magnitude;
            float length = at[at.Length - 1];
            City(line[0]);
            yield return null;

            Vector3 start = line[0] - Flat(line[1] - line[0]).normalized * 2.0f + Vector3.up * 0.1f;
            float yaw = Quaternion.LookRotation(Flat(line[1] - line[0])).eulerAngles.y;
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                start, Quaternion.Euler(0.0f, yaw, 0.0f));
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            const float dt = 1.0f / 60.0f;
            for (int i = 0; i < 10; i++)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = yaw }, dt);
                yield return null;
            }

            float s = 0.0f, furthest = 0.0f, worstBack = 0.0f, seconds = 0.0f;
            int stalled = 0;
            for (; seconds < 60.0f && s < length - 0.5f; seconds += dt)
            {
                Vector3 p = player.transform.position;
                // Where along the line the body is: the nearest point near where it was (the flights stand over one another).
                float best = s, bestD = float.MaxValue;
                for (float q = s - 1.0f; q <= s + 1.5f; q += 0.05f)
                {
                    float d = Flat(PointAt(line, at, Mathf.Clamp(q, 0.0f, length)) - p).magnitude;
                    if (d < bestD)
                    {
                        bestD = d;
                        best = Mathf.Clamp(q, 0.0f, length);
                    }
                }
                s = best;
                furthest = Mathf.Max(furthest, s);
                worstBack = Mathf.Max(worstBack, furthest - s);
                Vector3 toward = Flat(PointAt(line, at, Mathf.Min(s + 1.2f, length)) - p);
                Vector2 keys = Vector2.up;
                if (toward.sqrMagnitude > 1e-4f)
                {
                    bool onAFlight = Mathf.Abs(PointAt(line, at, Mathf.Min(s + 0.3f, length)).y - PointAt(line, at, s).y) > 0.05f;
                    if (going != Going.Sidestep || onAFlight)
                    {
                        yaw = Quaternion.LookRotation(toward).eulerAngles.y;
                    }
                    else
                    {
                        Vector3 local = Quaternion.Euler(0.0f, -yaw, 0.0f) * toward.normalized;
                        keys = new Vector2(local.x, local.z);
                    }
                }
                locomotion.Tick(new LocomotionInput { Move = keys, YawDegrees = yaw, Sprint = going == Going.Sprint }, dt);
                yield return null;
                if (Flat(player.transform.position - p).magnitude < 0.3f * dt) stalled++;
            }
            Debug.Log($"CityX roof stair ({going}): building {on.Id}, {stair.Top - stair.Bottom:F1} m up in {stair.Flights.Length} flights, " +
                $"{length:F1} m of walking line: {s:F1} m in {seconds:F1} s, {stalled} frames stalled, at worst {worstBack:F1} m back down");
            Assert.That(s, Is.GreaterThan(length - 1.0f), $"the body stopped {length - s:F1} m short of the roof");
            Assert.That(player.transform.position.y, Is.EqualTo(on.Top).Within(0.4f), "the body is not on the roof");
            Assert.Less(stalled, 20, "the body stuck on the way up");
            Assert.Less(worstBack, 1.0f, "the body was pushed back down the stair");
#else
            yield break;
#endif
        }

        private static Vector3 PointAt(Vector3[] line, float[] at, float s)
        {
            for (int i = 1; i < line.Length; i++)
            {
                if (s <= at[i] || i == line.Length - 1)
                {
                    float span = at[i] - at[i - 1];
                    return Vector3.Lerp(line[i - 1], line[i], span > 1e-5f ? Mathf.Clamp01((s - at[i - 1]) / span) : 1.0f);
                }
            }
            return line[line.Length - 1];
        }
    }
}
