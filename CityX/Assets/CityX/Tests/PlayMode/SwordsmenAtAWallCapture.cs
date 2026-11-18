using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: ten swordsmen pressing at Old Guy's workshop wall with the player inside, as the owner's screenshots were (owner, 2026-11-18:
    /// "they we poking through at first with their swords and body parts"), filmed from inside the room behind the player and from the street
    /// (Logs/CityShots/wallpack-in-NN.png, wallpack-out-NN.png). Needs a graphics device.
    /// </summary>
    [Explicit("capture")]
    public sealed class SwordsmenAtAWallCapture
    {
        private const int Shots = 6;
        private const int FramesBetweenShots = 150;

        [UnityTest]
        public IEnumerator FilmThePackAtTheWall()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var made = new List<GameObject>();
            TutorialHousePower.Shared.Reset();
            OldGuysHousePlayTests.MakeHouse(made, false, false);
            TutorialHousePower.Shared.TurnOn();
            var sun = new GameObject("Sun").AddComponent<Light>();
            made.Add(sun.gameObject);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40.0f, 20.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            made.Add(camera.gameObject);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            camera.fieldOfView = 50.0f;
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(22.0f, 0.05f, 2.5f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var vitals = player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab");
            for (int i = 0; i < 10; i++)
            {
                var swordsman = Object.Instantiate(prefab, new Vector3(17.0f + (i % 5) * 2.2f, 0.05f, i < 5 ? -2.5f : -5.0f), Quaternion.identity);
                made.Add(swordsman);
                var brain = swordsman.GetComponent<SwordsmanBrain>();
                brain.Seed = 4100 + i;
                brain.Target = vitals;
                swordsman.GetComponent<PlayerVitals>().InfiniteHealth = true;
            }
            for (int shot = 0; shot < Shots; shot++)
            {
                for (int frame = 0; frame < FramesBetweenShots; frame++)
                {
                    yield return null;
                }
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"wallpack-in-{shot:00}",
                    new Vector3(22.0f, 3.6f, 6.6f), new Vector3(22.0f, 1.5f, 0.0f));
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"wallpack-out-{shot:00}",
                    new Vector3(23.5f, 4.0f, -8.0f), new Vector3(22.0f, 1.5f, 0.0f));
            }
            foreach (GameObject go in made) Object.Destroy(go);
            TutorialHousePower.Shared.Reset();
#else
            Assert.Ignore("the prefabs can only be loaded by path inside the editor");
            yield break;
#endif
        }
    }
}
