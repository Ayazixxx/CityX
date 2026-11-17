using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using House = CityX.World.OldGuysHouse;

namespace CityX.Tests
{
    /// <summary>
    /// Old Guy's swordsman waits upstairs until he lays eyes on the player (owner, 2026-11-16: "make the swordsman upstairs only hostile once he
    /// lays eyes on you i shouldnt hear him dashing around upstairs before i even go up there"): the floor and the walls hide the player
    /// from him, someone right beside him or a blow wakes him, and a swordsman put down anywhere else notices as before.
    /// </summary>
    public sealed class SwordsmanWaitsToSeePlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        [UnityTest]
        public IEnumerator HeSeesOnlyWhatIsInFrontOfHimWithNothingBetween()
        {
            OldGuysHousePlayTests.MakeHouse(_made, false, false);
            yield return null;
            var him = new GameObject("Him").transform;
            var someone = new GameObject("Someone").transform;
            _made.Add(him.gameObject);
            _made.Add(someone.gameObject);
            him.SetPositionAndRotation(House.SwordsmanSpot, Quaternion.Euler(0.0f, House.SwordsmanYaw, 0.0f));
            Vector3 ahead = him.forward, spot = House.SwordsmanSpot;

            someone.position = spot + ahead * 3.5f;
            Assert.IsTrue(SwordsmanBrain.Notices(him, someone), "he does not see someone standing in front of him in his room");
            someone.position = spot - ahead * 4.0f;
            Assert.IsFalse(SwordsmanBrain.Notices(him, someone), "he sees someone behind his back");
            someone.position = spot - ahead * 2.0f;
            Assert.IsTrue(SwordsmanBrain.Notices(him, someone), "he does not notice someone right behind him");
            someone.position = new Vector3(spot.x + ahead.x * 3.0f, House.LevelY(1), spot.z + ahead.z * 3.0f);
            Assert.IsFalse(SwordsmanBrain.Notices(him, someone), "he sees through the floor into the den below");
            someone.position = new Vector3(20.0f, House.LevelY(2), 9.5f);
            Assert.IsFalse(SwordsmanBrain.Notices(him, someone), "he sees through his bedroom wall into the corridor");
        }

        [UnityTest]
        public IEnumerator UpstairsHeLeavesThePlayerBelowAloneUntilHurt()
        {
#if UNITY_EDITOR
            OldGuysHouseSite site = OldGuysHousePlayTests.MakeHouse(_made, false, true);
            var brain = site.Swordsman.GetComponent<SwordsmanBrain>();
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(25.5f, House.LevelY(1) + 0.1f, 15.0f), Quaternion.identity);
            _made.Add(player);
            yield return new WaitForSeconds(3.0f);
            Assert.IsTrue(brain.WaitsToSee, "the swordsman in the house was not told to wait until he sees someone");
            Assert.IsNull(brain.Target, "he went after the player on the floor below him");
            Assert.AreEqual(0, brain.Dashes, "he dashed about with nobody to fight");

            site.Swordsman.GetComponent<PlayerVitals>().TakeDamage(1.0f);
            float until = Time.time + 2.5f;
            while (brain.Target == null && Time.time < until)
            {
                yield return null;
            }
            Assert.IsNotNull(brain.Target, "a blow did not wake him");
            Assert.AreEqual(player, brain.Target.gameObject);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ASwordsmanPutDownElsewhereNoticesAsBefore()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(80.0f, 1.0f, 80.0f);
            _made.Add(ground);
            var swordsman = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab"),
                new Vector3(0.0f, 0.1f, 0.0f), Quaternion.identity);
            _made.Add(swordsman);
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.1f, -15.0f), Quaternion.identity);
            _made.Add(player);
            var brain = swordsman.GetComponent<SwordsmanBrain>();
            float until = Time.time + 2.5f;
            while (brain.Target == null && Time.time < until)
            {
                yield return null;
            }
            Assert.IsFalse(brain.WaitsToSee);
            Assert.IsNotNull(brain.Target, "a swordsman on open ground did not go for the player behind him");
#else
            yield break;
#endif
        }
    }
}
