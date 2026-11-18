using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Ten swordsmen outside a wall with the player on the other side (owner, 2026-11-18: "i spawned 10 swordsman outside and they we poking
    /// through at first with their swords and body parts and eventually 2 were able to pass through the wall"): Old Guy's workshop, the player
    /// inside by the front wall, the pack in the street pressing at it.
    /// </summary>
    public sealed class SwordsmenAtAWallPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private const int Pack = 10;

        /// <summary>The workshop's front wall stands on z = 0, 0.3 m thick: its inside face.</summary>
        private const float InsideFace = 0.15f;

        /// <summary>A bone this far short of the inside face still has flesh past it that shows (a hand's, a finger's).</summary>
        private const float Flesh = 0.05f;

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        [UnityTest]
        public IEnumerator TenSwordsmenPressingAtAWallStayOutsideItSwordsAndAll()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            OldGuysHousePlayTests.MakeHouse(_made, false, false);
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(22.0f, 0.05f, 2.5f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;
            var probe = new GameObject("WallReachProbe").AddComponent<WallReachProbe>();
            _made.Add(probe.gameObject);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab");
            for (int i = 0; i < Pack; i++)
            {
                // Two rows in the street, straight out from the wall under the workshop's windows, facing it.
                var swordsman = Object.Instantiate(prefab, new Vector3(17.0f + (i % 5) * 2.2f, 0.05f, i < 5 ? -2.5f : -5.0f), Quaternion.identity);
                _made.Add(swordsman);
                var brain = swordsman.GetComponent<SwordsmanBrain>();
                brain.Seed = 4100 + i;
                brain.Target = vitals;
                swordsman.GetComponent<PlayerVitals>().InfiniteHealth = true;
                probe.Bodies.Add(swordsman);
            }

            // A moment to settle out of the pose they are made in before anything is measured.
            for (int i = 0; i < 10; i++) yield return null;
            probe.DeepestBody = probe.DeepestBlade = float.MinValue;
            for (float t = 0.0f; t < 40.0f; t += Dt)
            {
                yield return null;
            }
            Assert.AreEqual(0, probe.Through.Count, "swordsmen got through the wall");
            Assert.Less(probe.DeepestBody, InsideFace - Flesh, $"{probe.BodyNote} reached through the wall");
            Assert.Less(probe.DeepestBlade, InsideFace, $"the {probe.BladeNote} reached through the wall");
#else
            Assert.Ignore("the prefabs can only be loaded by path inside the editor");
            yield break;
#endif
        }
    }
}
