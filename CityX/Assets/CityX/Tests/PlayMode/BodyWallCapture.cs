using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the body walking along a wall on its right, from behind and above as the owner's screenshots were (owner, 2026-11-10: "make
    /// it so body parts cant clip through walls"), a picture every quarter second (Logs/CityShots/bodywall-NN.png). Needs a graphics device.
    /// </summary>
    [Explicit("capture")]
    public sealed class BodyWallCapture
    {
        private const int Shots = 8;
        private const int FramesBetweenShots = 15;

        [UnityTest]
        public IEnumerator FilmTheBodyAlongAWall()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45.0f, 30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            camera.fieldOfView = 50.0f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(1.0f, 6.0f, 40.0f);
            wall.transform.position = new Vector3(1.02f, 3.0f, 0.0f);
            wall.GetComponent<Renderer>().material.color = new Color(0.55f, 0.22f, 0.18f);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, -6.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var input = new LocomotionInput { Move = new Vector2(0.25f, 1.0f).normalized, YawDegrees = 0.0f };
            for (int i = 0; i < 60; i++)
            {
                locomotion.Tick(input, 1.0f / 60.0f);
                yield return null;
            }
            for (int shot = 0; shot < Shots; shot++)
            {
                Vector3 at = player.transform.position;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"bodywall-{shot:00}",
                    at + new Vector3(-1.2f, 4.2f, -4.2f), at + new Vector3(0.3f, 1.3f, 0.0f));
                for (int frame = 0; frame < FramesBetweenShots; frame++)
                {
                    locomotion.Tick(input, 1.0f / 60.0f);
                    yield return null;
                }
            }
            Object.Destroy(player);
            Object.Destroy(wall);
            Object.Destroy(ground);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sun.gameObject);
#else
            yield break;
#endif
        }
    }
}
