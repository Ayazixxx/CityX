using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using CityX.World.Plan;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using House = CityX.World.OldGuysHouse;

namespace CityX.Tests
{
    /// <summary>
    /// Old Guy's house in the streamed city (docs/OLD_GUYS_HOUSE.md): the streamer stands it on the land the plan keeps beside the spawn, nothing the
    /// city builds stands inside it, and a body the player's size walks from where the player lands (the driveway) to the foot of its outdoor steps, up onto the
    /// porch, and back out to the street.
    /// </summary>
    public sealed class OldGuysHouseCityPlayTests
    {
        private const int Seed = 81565117;
        private readonly List<GameObject> _made = new List<GameObject>();
        private CityScale _scaleBefore;

        [SetUp]
        public void SetUp()
        {
            _scaleBefore = CityScale.Active;
            CityScale.Active = CityScale.Default;   // the game's own city: the classic one's blocks are too small for the house
        }

        [TearDown]
        public void TearDown()
        {
            CityScale.Active = _scaleBefore;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
        }

        private OldGuysHouseSite StreamTheHouse(out PlanWorld plan)
        {
            var house = new GameObject("OldGuysHouse").AddComponent<OldGuysHouseSite>();
            _made.Add(house.gameObject);
#if UNITY_EDITOR
            house.Structure = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityConcrete.mat");
            house.Glass = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/HouseGlass.mat");
#endif
            house.PlaceItems = false;
            house.SpawnSwordsman = false;

            var go = new GameObject("Streamer");
            _made.Add(go);
            go.SetActive(false);
            var streamer = go.AddComponent<CityStreamer>();
            streamer.WorldSeed = Seed;
            streamer.RadiusChunks = 4;
            streamer.GeneratorThreads = 0;
            streamer.SpawnPreStreamBudget = 0;
            streamer.TutorialHouse = house;
            go.SetActive(true);
            plan = PlanWorld.For(Seed);
            house.Build();
            plan.TryFindSpawn(out double sx, out double sz);
            TutorialLot lot = plan.TutorialHouse;
            streamer.StreamAround(new Vector3((float)((sx + lot.OriginX) * 0.5), 0.0f, (float)((sz + lot.OriginZ) * 0.5)), int.MaxValue);
            Physics.SyncTransforms();
            return house;
        }

        [UnityTest]
        public IEnumerator TheStreamerStandsTheHouseOnItsLandAndNothingGeneratedIsInsideIt()
        {
            OldGuysHouseSite house = StreamTheHouse(out PlanWorld plan);
            yield return null;
            TutorialLot lot = plan.TutorialHouse;
            Assert.IsNotNull(house.Built, "the house was not built");
            Assert.Less(Vector3.Distance(house.transform.position, new Vector3((float)lot.OriginX, (float)lot.FloorHeight, (float)lot.OriginZ)), 0.01f, "the house is not on its land");
            Vector3 front = house.transform.TransformDirection(Vector3.back);
            lot.ToWorld(0.0, -1.0, out double fx, out double fz);
            Assert.Greater(Vector3.Dot(front, new Vector3((float)(fx - lot.OriginX), 0.0f, (float)(fz - lot.OriginZ))), 0.99f, "the house's front does not face its street");

            // Over the whole lot, from just above the floor to the eaves: every collider found is the house's own.
            Vector3 centre = house.transform.TransformPoint(new Vector3((float)(TutorialLot.LotX0 + TutorialLot.LotX1) * 0.5f, 6.3f, (float)(TutorialLot.LotZ0 + TutorialLot.LotZ1) * 0.5f));
            var half = new Vector3((float)(TutorialLot.LotX1 - TutorialLot.LotX0) * 0.5f - 0.1f, 6.0f, (float)(TutorialLot.LotZ1 - TutorialLot.LotZ0) * 0.5f - 0.1f);
            foreach (Collider c in Physics.OverlapBox(centre, half, house.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                Assert.IsTrue(c.transform.IsChildOf(house.transform), $"{c.name} ({c.transform.root.name}) stands inside Old Guy's lot");
            }

            // The footing closes the gap under the lot's edge down into the ground.
            Assert.IsTrue(Physics.Raycast(house.transform.TransformPoint(new Vector3((float)TutorialLot.LotX1 - 0.5f, 3.0f, -3.0f)), Vector3.down, out RaycastHit hit, 6.0f));
            Assert.IsTrue(hit.collider.transform.IsChildOf(house.transform), $"the ground at the lot's edge is {hit.collider.name}, not the house's footing");
        }

        [UnityTest]
        public IEnumerator ABodyWalksFromWhereThePlayerLandsUpTheHousesStepsOntoThePorchAndOutToTheStreet()
        {
            OldGuysHouseSite house = StreamTheHouse(out PlanWorld plan);
            yield return null;
            Vector3 At(float x, float z) => house.transform.TransformPoint(new Vector3(x, 0.0f, z));
            CharacterController body = Body(At(House.Landing.x, House.Landing.z) + Vector3.up * 0.1f);
            float steps = (House.OutdoorStepsX0 + House.OutdoorStepsX1) * 0.5f, foot = House.OutdoorStepsFootZ - 0.7f, top = House.OutdoorStepsTopZ + 1.6f;
            Vector3 porch = At((House.Width + House.PorchX1) * 0.5f, (House.PorchZ0 + House.PorchZ1) * 0.5f);
            // Along the driveway to Old Guy's spot at the foot of the steps, round the end of their rail, up onto the porch, back down, and out across the
            // pavement to the kerb.
            Vector3[] route =
            {
                At(House.OldGuyAtTheSteps.x, House.OldGuyAtTheSteps.z), At(House.OldGuyAtTheSteps.x, foot), At(steps, foot), At(steps, top), porch,
            };
            for (int i = 0; i < route.Length; i++)
            {
                yield return WalkTo(body, route[i]);
                Vector3 p = body.transform.position, to = route[i];
                float off = new Vector2(p.x - to.x, p.z - to.z).magnitude;
                Assert.Less(off, 0.8f, $"the body stopped {off:F1} m short of waypoint {i} (house frame {house.transform.InverseTransformPoint(to)}), at {house.transform.InverseTransformPoint(p)}");
            }
            Assert.AreEqual(house.transform.position.y + House.Storey, body.transform.position.y, 0.4f, "the body is not standing on the porch");

            // Down again and off the lot: the step from the driveway to the pavement is walked, not climbed.
            Vector3[] back = { At(steps, top), At(steps, foot), At(House.Landing.x, foot + 0.2f), At(House.Landing.x, (float)TutorialLot.LotZ0 - 2.5f) };
            for (int i = 0; i < back.Length; i++)
            {
                yield return WalkTo(body, back[i]);
                Vector3 p = body.transform.position, to = back[i];
                float off = new Vector2(p.x - to.x, p.z - to.z).magnitude;
                Assert.Less(off, 0.8f, $"on the way out the body stopped {off:F1} m short of waypoint {i} (house frame {house.transform.InverseTransformPoint(to)}), at {house.transform.InverseTransformPoint(p)}");
            }
            Assert.IsFalse(plan.OnTutorialLot(body.transform.position.x, body.transform.position.z), "the body never left the lot");
        }

        private CharacterController Body(Vector3 feet)
        {
            var go = new GameObject("Body");
            _made.Add(go);
            go.transform.position = feet;
            var controller = go.AddComponent<CharacterController>();
            controller.height = BodyRig.StandingHeight;
            controller.radius = FusedBodyShape.TorsoRadius;
            controller.skinWidth = 0.03f;
            controller.center = new Vector3(0.0f, BodyRig.StandingHeight * 0.5f, 0.0f);
            controller.stepOffset = 0.3f;
            controller.minMoveDistance = 0.0f;
            controller.slopeLimit = 50.0f;
            Physics.SyncTransforms();
            return controller;
        }

        /// <summary>Walks the body toward a point at 2 m/s until it is within half a metre or the time a straight walk would take, plus three seconds, is up.</summary>
        private static IEnumerator WalkTo(CharacterController body, Vector3 target)
        {
            const float dt = 1.0f / 60.0f;
            float budget = Vector3.Distance(body.transform.position, target) / 2.0f + 3.0f;
            for (float t = 0.0f; t < budget; t += dt)
            {
                Vector3 to = target - body.transform.position;
                to.y = 0.0f;
                if (to.magnitude < 0.5f) yield break;
                body.Move((to.normalized * 2.0f + Vector3.down * 3.0f) * dt);
                yield return null;
            }
        }
    }
}
