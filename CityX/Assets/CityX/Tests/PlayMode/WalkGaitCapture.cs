using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the walks at walking speed (forward from the side, a strafe from the front, backward from the side), each at twelve moments of a
    /// stride, so the paced gaits (PacedGait) can be looked at: the step length, the knees, whether a planted foot stays put. Written to
    /// Logs/CityShots/gait-{walk,strafe,back}-NN.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class WalkGaitCapture
    {
        private const int Moments = 12;
        private const int FramesBetween = 4;

        [UnityTest]
        public IEnumerator RenderTheWalks()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            foreach ((string name, Vector2 stick, bool fromSide) in new[]
                { ("walk", Vector2.up, true), ("strafe", Vector2.right, false), ("back", Vector2.down, true) })
            {
                GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                    new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                for (int i = 0; i < 90; i++)
                {
                    locomotion.Tick(new LocomotionInput { Move = stick }, 1.0f / 60.0f);
                    yield return null;
                }
                for (int moment = 0; moment < Moments; moment++)
                {
                    for (int i = 0; i < FramesBetween; i++)
                    {
                        locomotion.Tick(new LocomotionInput { Move = stick }, 1.0f / 60.0f);
                        yield return null;
                    }
                    Vector3 hips = player.transform.position + Vector3.up * 0.8f;
                    Vector3 view = fromSide ? player.transform.right : player.transform.forward;
                    BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"gait-{name}-{moment:00}", hips + view * 5.5f + Vector3.up * 0.2f, hips);
                }
                Object.Destroy(player);
                yield return null;
            }
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }
    }
}
