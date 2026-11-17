using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.UI;
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
    /// The tutorial played through the director in the real house (docs/TUTORIAL.md): Old Guy steps into his den out of sight once the house
    /// is clear, the level-up comes before the stat points, and the ending: the den's windows shot out, the player and Old Guy down, black,
    /// and the player on their feet in the middle of the hub with Old Guy gone.
    /// </summary>
    public sealed class TutorialEndingPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private CityScale _scaleBefore;
        private CityWorld _worldBefore;

        [SetUp]
        public void SetUp()
        {
            _scaleBefore = CityScale.Active;
            _worldBefore = CurrentWorld.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            CurrentWorld.Instance = _worldBefore;
            CityScale.Active = _scaleBefore;
            ScreenBlack.FadeTo(0.0f, 0.0f);
            TutorialHouseDamage.Shared.Clear();
        }

#if UNITY_EDITOR
        private (TutorialDirector Director, OldGuy OldGuy, PlayerVitals Player) MakeTutorial(Vector3 playerAt)
        {
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), playerAt, Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var oldGuy = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/OldGuy.prefab"),
                House.OldGuyAtTheSteps + Vector3.up * 0.1f, Quaternion.identity);
            _made.Add(oldGuy);
            var director = new GameObject("TutorialDirector").AddComponent<TutorialDirector>();
            _made.Add(director.gameObject);
            director.Player = player.GetComponent<PlayerVitals>();
            return (director, oldGuy.GetComponent<OldGuy>(), director.Player);
        }
#endif

        [UnityTest]
        public IEnumerator OnceTheHouseIsClearHeWaitsInHisDenAndTheLevelUpComesBeforeTheStatPoints()
        {
#if UNITY_EDITOR
            OldGuysHousePlayTests.MakeHouse(_made, false, false);
            (TutorialDirector director, OldGuy oldGuy, PlayerVitals player) = MakeTutorial(new Vector3(House.OldGuyInTheDen.x + 2.0f, House.LevelY(1) + 0.1f, 15.0f));
            yield return null;
            Assert.AreSame(oldGuy, director.OldGuy, "the director did not find Old Guy");

            Assert.IsNull(director.JumpTo(TutorialStep.ReportBack));
            Assert.Less(Vector3.Distance(oldGuy.transform.position, House.OldGuyInTheDen), 0.3f, "he is not in his den");
            Assert.AreEqual("Old Guy's den", House.RoomAt(1, oldGuy.transform.position.x, oldGuy.transform.position.z)?.Name);

            int level = player.Level;
            player.StatPoints = 0;
            Assert.IsNull(director.JumpTo(TutorialStep.Stats));
            Assert.AreEqual(level + 1, player.Level, "no level-up before the stat points");
            Assert.Greater(player.StatPoints, 0);
            yield return null;
            Assert.AreEqual(TutorialStep.Stats, oldGuy.Progress.Step, "the stat step passed with the points unspent");
            player.StatPoints = 0;
            yield return null;
            Assert.AreEqual(TutorialStep.Skill, oldGuy.Progress.Step, "spending the points did not pass the step");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheShotsComeThroughTheDenWindowsAndThePlayerWakesAtTheHub()
        {
#if UNITY_EDITOR
            CityScale.Active = CityScale.Default;   // the game's own city: it has a hub
            CurrentWorld.Instance = new CityWorld(CitySeed.Default);
            Assert.IsTrue(TutorialDirector.TryHubSpot(out Vector3 hub), "the game's city has no hub");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = hub + Vector3.down * (0.5f + TutorialDirector.ArriveAbove);
            floor.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            _made.Add(floor);

            OldGuysHouseSite site = OldGuysHousePlayTests.MakeHouse(_made, false, false);
            (TutorialDirector director, OldGuy oldGuy, PlayerVitals player) = MakeTutorial(new Vector3(House.OldGuyInTheDen.x + 2.0f, House.LevelY(1) + 0.1f, 15.0f));
            yield return null;
            TutorialProgress progress = oldGuy.Progress;
            Assert.IsNull(director.JumpTo(TutorialStep.Ambush));
            Assert.IsTrue(director.EndingPlaying);

            float until = Time.time + 2.0f;
            while ((!player.IsDead || !oldGuy.Collapsed) && Time.time < until)
            {
                yield return null;
            }
            Assert.IsTrue(player.IsDead, "the shots did not kill the player");
            Assert.IsTrue(oldGuy.Collapsed, "the shots did not bring Old Guy down");
            int shattered = 0;
            foreach (HousePane pane in site.Built.GetComponentsInChildren<HousePane>())
            {
                Vector3 local = site.transform.InverseTransformPoint(pane.transform.position);
                if (pane.Shattered && House.RoomAt(1, local.x, local.z)?.Name == "Old Guy's den")
                {
                    shattered++;
                }
            }
            Assert.Greater(shattered, 0, "none of the den's windows was shot out");

            until = Time.realtimeSinceStartup + 15.0f;
            while (director.EndingPlaying && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
            Assert.IsFalse(director.EndingPlaying, "the ending never finished");
            Assert.IsFalse(player.IsDead, "the player was not brought back");
            Assert.Less(Vector3.Distance(player.transform.position, hub), 1.5f, $"the player is at {player.transform.position}, not at the hub {hub}");
            Assert.IsTrue(oldGuy == null, "Old Guy was left behind");
            Assert.AreEqual(TutorialStep.Done, progress.Step);
#else
            yield break;
#endif
        }
    }
}
