using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using House = CityX.World.OldGuysHouse;

namespace CityX.Tests
{
    /// <summary>
    /// The tutorial house built for real (docs/OLD_GUYS_HOUSE.md): the real body walks up its stair core from the garage level to the upper
    /// floor, the five weapons lie exactly where the plan puts them and stay there, and the swordsman waits upstairs.
    /// </summary>
    public sealed class OldGuysHousePlayTests
    {
        private const float Step = 1.0f / 60.0f;

        public static readonly Dictionary<string, string> PrefabPaths = new Dictionary<string, string>
        {
            [ItemRegistry.KitchenKnife] = "Assets/CityX/Weapons/KitchenKnife.prefab",
            [ItemRegistry.ButterKnife] = "Assets/CityX/Weapons/ButterKnife.prefab",
            [ItemRegistry.Slingshot] = "Assets/CityX/Weapons/Slingshot.prefab",
            [ItemRegistry.MagicWand] = "Assets/CityX/Weapons/MagicWand.prefab",
            [ItemRegistry.BaseballBat] = "Assets/CityX/Weapons/BaseballBat.prefab",
        };

        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            DroppedItems.Shared.Clear();
#if UNITY_EDITOR
            DroppedItems.Shared.PrefabFor = id => PrefabPaths.TryGetValue(id, out string path) ? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
#endif
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            DroppedItems.Shared.Clear();
            DroppedItems.Shared.PrefabFor = Inventory.FindPrefab;
        }

        /// <summary>The house at the origin on a ground plane, built now.</summary>
        public static OldGuysHouseSite MakeHouse(List<GameObject> made, bool items, bool swordsman)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(10, -0.5f, 10);
            ground.transform.localScale = new Vector3(120, 1, 120);
            made.Add(ground);
            var site = new GameObject("OldGuysHouseSite").AddComponent<OldGuysHouseSite>();
            made.Add(site.gameObject);
#if UNITY_EDITOR
            site.Structure = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/CityConcrete.mat");
            site.Glass = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/HouseGlass.mat");
            site.SwordsmanPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab");
#endif
            site.PlaceItems = items;
            site.SpawnSwordsman = swordsman;
            site.Build();
            if (swordsman)
            {
                site.PutDownSwordsman();
                made.Add(site.Swordsman);
            }
            return site;
        }

        [UnityTest]
        public IEnumerator TheRealBodyWalksUpTheStairCoreFromTheGarageLevelToTheUpperFloor()
        {
#if UNITY_EDITOR
            MakeHouse(_made, false, false);
            float west = (House.FlightWestX0 + House.FlightWestX1) * 0.5f, east = (House.FlightEastX0 + House.FlightEastX1) * 0.5f;
            float landing = (House.LandingZ0 + House.FlightTurnZ) * 0.5f;
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(west, 0.1f, House.FlightTopZ + 2.0f), Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 20; i++)
            {
                locomotion.Tick(default, Step);
                yield return null;
            }
            // Up the west flight to the landing, round, up the east flight onto the main floor; again onto the upper floor.
            Vector2[] route =
            {
                new Vector2(west, House.FlightTopZ + 0.6f), new Vector2(west, landing), new Vector2(east, landing), new Vector2(east, House.FlightTopZ + 1.2f),
                new Vector2(west, House.FlightTopZ + 0.8f), new Vector2(west, landing), new Vector2(east, landing), new Vector2(east, House.FlightTopZ + 1.2f),
                new Vector2((west + east) * 0.5f, House.FlightTopZ + 2.6f),
            };
            int next = 0;
            for (int tick = 0; tick < 3600 && next < route.Length; tick++)
            {
                Vector3 here = player.transform.position;
                Vector3 toward = new Vector3(route[next].x - here.x, 0, route[next].y - here.z);
                if (toward.magnitude < 0.45f)
                {
                    next++;
                    continue;
                }
                locomotion.Tick(new LocomotionInput { Move = new Vector2(0, 1), YawDegrees = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg }, Step);
                yield return null;
            }
            Assert.AreEqual(route.Length, next, $"the body stopped at waypoint {next} of {route.Length}, at {player.transform.position}");
            Assert.AreEqual(House.LevelY(2), player.transform.position.y, 0.35f, "the body did not end up standing on the upper floor");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheWeaponsLieWhereThePlanPutsThemAndStayThere()
        {
            OldGuysHouseSite site = MakeHouse(_made, true, false);
            for (int i = 0; i < 90; i++) yield return null;
            foreach (House.ItemSpot spot in House.Items)
            {
                Assert.IsTrue(site.Items.TryGetValue(spot.ItemId, out DroppedItem item) && item != null, $"no {spot.ItemId} in the house");
                Assert.Less(Vector3.Distance(item.transform.position, site.transform.TransformPoint(spot.Position)), 0.02f, $"the {spot.ItemId} moved from where it was put");
            }
        }

        [UnityTest]
        public IEnumerator TheSwordsmanWaitsUpstairs()
        {
            OldGuysHouseSite site = MakeHouse(_made, false, true);
            Assert.IsNotNull(site.Swordsman, "no swordsman in the house");
            for (int i = 0; i < 60; i++) yield return null;
            Assert.Greater(site.Swordsman.transform.position.y, House.LevelY(2) - 0.3f, "the swordsman fell through the upper floor");
            Assert.AreEqual("Old Guy's bedroom", House.RoomAt(2, site.Swordsman.transform.position.x, site.Swordsman.transform.position.z)?.Name);
        }

        [UnityTest]
        public IEnumerator TheSwordsmanIsPutDownOnceThePlayerGetsIn()
        {
#if UNITY_EDITOR
            OldGuysHouseSite site = MakeHouse(_made, false, false);
            site.SpawnSwordsman = true;
            site.SwordsmanPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab");
            site.PlaceOn(new CityX.World.Plan.TutorialLot(0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0));
            site.Build();
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(10.0f, 0.1f, -60.0f), Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsNull(site.Swordsman, "the swordsman was put down with the player far off");
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = House.Landing + new Vector3(0, 0.1f, 0);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsNull(site.Swordsman, "the swordsman was put down with the player still on the driveway");
            player.transform.position = new Vector3(House.Width - 3.0f, House.LevelY(1) + 0.1f, 10.0f);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsNotNull(site.Swordsman, "no swordsman once the player got into the house");
            _made.Add(site.Swordsman);
#else
            yield break;
#endif
        }
    }
}
