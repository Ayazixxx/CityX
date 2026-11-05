using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The sky hub built and walked on (SkyHubBuilder, docs/SKY_HUB.md): a body walks off the landing onto the stair and climbs it at the stair's slope, a
    /// body at the stair's top walks round the flat and onto the island, the wall keeps a body on the stair, and the whole thing stands where the plan
    /// says. The real body and a stair built on its own, so the city's streaming is not part of it.
    /// </summary>
    public sealed class SkyHubPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private readonly List<GameObject> _made = new List<GameObject>();

        /// <summary>A hub on open flat ground: its stair climbing with the angle from the +x side of an island at the origin.</summary>
        private static readonly SkyHubPlan Plan = new SkyHubPlan(0.0, 0.0, 0.0, 1, 0.0, -0.2, 0);

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        private SkyHubSite Hub()
        {
            var site = new GameObject("SkyHubSite").AddComponent<SkyHubSite>();
            _made.Add(site.gameObject);
            site.Structure = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            site.PlaceOn(Plan);
            return site;
        }

        private GameObject Body(Vector3 at, Vector3 facing)
        {
#if UNITY_EDITOR
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.LookRotation(facing));
            _made.Add(body);
            body.GetComponent<PlayerInputReader>().enabled = false;
            return body;
#else
            return null;
#endif
        }

        private static Vector3 World(in SkyHubPlan hub, double s, double across, double y)
        {
            Vec2 p = hub.PlaceAt(s, across);
            return new Vector3((float)p.X, (float)y, (float)p.Y);
        }

        private static float YawOf(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        /// <summary>Walks a body round the island, the way the stair climbs (at <paramref name="radius"/>'s tangent), for <paramref name="seconds"/>.</summary>
        private static IEnumerator WalkAlong(GameObject body, float seconds)
        {
            var locomotion = body.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Vector3 p = body.transform.position;
                float angle = Mathf.Atan2(p.z, p.x);
                var along = new Vector3(-Mathf.Sin(angle), 0.0f, Mathf.Cos(angle));
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = YawOf(along) }, Step);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ABodyWalksOffTheLandingAndClimbsTheStairAtItsSlope()
        {
            Hub();
            Physics.SyncTransforms();
            Vector3 tangent = new Vector3((float)Plan.FootTangent.X, 0.0f, (float)Plan.FootTangent.Y);
            GameObject body = Body(World(Plan, -8.0, 0.0, 0.1), tangent);
            yield return WalkAlong(body, 0.5f);
            Assert.AreEqual(0.0f, body.transform.position.y, 0.3f, "not standing on the landing");

            yield return WalkAlong(body, 8.0f);
            Vector3 p = body.transform.position;
            float radius = new Vector2(p.x, p.z).magnitude;
            Assert.Greater(radius, SkyHubPlan.StairInnerRadius, "the body left the stair inward");
            Assert.Less(radius, SkyHubPlan.StairOuterRadius, "the body left the stair outward");
            double s = (float)(Mathf.Atan2(p.z, p.x) * SkyHubPlan.StairMidRadius);
            Assert.Greater(s, 6.0, "the body did not get far up the stair: it is stuck");   // at a stair's pace (StairGait)
            Assert.AreEqual(s * SkyHubPlan.Rise, p.y, 0.8, $"at {s:F1} m along the body is not on the stair");
        }

        [UnityTest]
        public IEnumerator AtTheTopABodyWalksFromTheStairOntoTheIsland()
        {
            Hub();
            Physics.SyncTransforms();
            double top = Plan.HeightAt(SkyHubPlan.StairLength);
            GameObject body = Body(World(Plan, SkyHubPlan.StairLength - 12.0, 0.0, Plan.HeightAt(SkyHubPlan.StairLength - 12.0) + 0.1), Vector3.forward);
            yield return WalkAlong(body, 14.0f);   // 12 m at a stair's pace (StairGait)
            Assert.AreEqual((float)top, body.transform.position.y, 0.3f, "the stair's top is not level with its flat");

            // Off the stair's flat, inward, onto the island.
            var locomotion = body.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f, walk = (float)(SkyHubPlan.StairMidRadius - SkyHubPlan.IslandRadius + 12.0) / locomotion.WalkSpeed + 1.0f; t < walk; t += Step)
            {
                Vector3 p = body.transform.position;
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = YawOf(new Vector3(-p.x, 0.0f, -p.z)) }, Step);
                yield return null;
            }
            Vector3 end = body.transform.position;
            Assert.Less(new Vector2(end.x, end.z).magnitude, SkyHubPlan.IslandRadius - 10.0, "the wall round the island is shut where the stair comes up");
            Assert.AreEqual((float)top, end.y, 0.2f, "the body is not on the island's top");
        }

        [UnityTest]
        public IEnumerator TheWallsKeepABodyOnTheStairAndTheIslandsEdgeKeepsItOn()
        {
            Hub();
            Physics.SyncTransforms();
            GameObject body = Body(World(Plan, 200.0, 0.0, Plan.HeightAt(200.0) + 0.2), Vector3.right);
            var locomotion = body.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < 3.0f; t += Step)
            {
                Vector3 p = body.transform.position;
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = YawOf(new Vector3(p.x, 0.0f, p.z)) }, Step);
                yield return null;
            }
            Assert.Less(new Vector2(body.transform.position.x, body.transform.position.z).magnitude, SkyHubPlan.StairOuterRadius, "the outer wall let a walking body off the stair");

            // Across the island to its edge, away from where the stair comes up.
            float middleOfGround = (float)Plan.IslandTop + 0.2f;
            GameObject second = Body(new Vector3(0.0f, middleOfGround, -(float)SkyHubPlan.IslandRadius + 12.0f), Vector3.back);
            var walker = second.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < 4.0f; t += Step)
            {
                walker.Tick(new LocomotionInput { Move = Vector2.up, YawDegrees = 180.0f }, Step);
                yield return null;
            }
            Assert.Less(new Vector2(second.transform.position.x, second.transform.position.z).magnitude, SkyHubPlan.IslandRadius, "the wall round the island let a walking body off");
        }

        /// <summary>The rock under the island has no holes: its faces look outward (one wound the wrong way lets the sky through; it was seen from the street).</summary>
        [UnityTest]
        public IEnumerator TheRocksFacesLookOutward()
        {
            SkyHubSite site = Hub();
            Physics.SyncTransforms();
            Mesh rock = null;
            foreach (MeshFilter filter in site.Built.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name == "Rock") rock = filter.sharedMesh;
            }
            Assert.IsNotNull(rock, "no rock");
            Vector3[] v = rock.vertices;
            int[] t = rock.triangles;
            int against = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 facing = Vector3.Cross(b - a, c - a);
                Vector3 middle = (a + b + c) / 3.0f;
                // Seen from outside and below, a drawn face looks out and down from the axis.
                Vector3 outward = new Vector3(middle.x, 0.0f, middle.z);
                if (Vector3.Dot(facing, outward + Vector3.down * outward.magnitude) <= 0.0f) against++;
            }
            Assert.Less(against, t.Length / 3 / 20, $"{against} of the rock's {t.Length / 3} faces look into it: the sky shows through there");
            yield return null;
        }

        [UnityTest]
        public IEnumerator EverySolidPieceIsThereAndTheStairIsInLengthsOfItsOwn()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            SkyHubSite site = Hub();
            Debug.Log($"CityX sky hub built in {clock.ElapsedMilliseconds} ms");
            MeshRenderer[] parts = site.Built.GetComponentsInChildren<MeshRenderer>();
            int stairs = 0, triangles = 0;
            foreach (MeshRenderer part in parts)
            {
                Assert.IsNotNull(part.GetComponent<MeshCollider>(), $"{part.name} has no collider: a body would fall through it");
                if (part.name.StartsWith("Stair")) stairs++;
                triangles += part.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
            }
            Assert.Greater(stairs, 20, "the stair is one great mesh: it cannot be culled length by length");
            Assert.Less(triangles, 90000, "the hub has grown past what it costs to draw now");
            Debug.Log($"CityX sky hub: {parts.Length} parts, {stairs} stair lengths, {triangles} triangles");
            yield return null;
        }
    }
}
