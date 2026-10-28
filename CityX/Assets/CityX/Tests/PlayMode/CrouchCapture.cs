using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the crouch (docs/CROUCH.md) to Logs/CityShots/crouch-*.png -- standing and crouched still, from the side, the
    /// front and three-quarters; a crouched walk forward, sideways and back through its cycle; and a crouch in the air at the top of a jump.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.CrouchCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CrouchCapture
    {
        private const float Step = 1.0f / 60.0f;
        private const int Moments = 8;
        private const int FramesBetween = 6;

        [UnityTest]
        public IEnumerator RenderTheCrouch()
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
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");

            // Standing and crouched still.
            GameObject player = Spawn(prefab, out PlayerLocomotion locomotion);
            yield return Hold(locomotion, default, 1.0f);
            Views(camera, player, "crouch-standing");
            yield return Hold(locomotion, new LocomotionInput { Crouch = true }, 1.0f);
            Views(camera, player, "crouch-still");
            Object.Destroy(player);
            yield return null;

            // Crouched walking, through the cycle.
            foreach ((string name, Vector2 stick, bool fromSide) in new[]
                { ("walk", Vector2.up, true), ("strafe", Vector2.right, false), ("back", Vector2.down, true) })
            {
                player = Spawn(prefab, out locomotion);
                var input = new LocomotionInput { Move = stick, Crouch = true };
                yield return Hold(locomotion, input, 1.5f);
                for (int moment = 0; moment < Moments; moment++)
                {
                    yield return Hold(locomotion, input, FramesBetween * Step);
                    Vector3 hips = player.transform.position + Vector3.up * 0.9f;
                    Vector3 view = fromSide ? player.transform.right : player.transform.forward;
                    BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"crouch-{name}-{moment:00}", hips + view * 6.5f + Vector3.up * 0.4f, hips);
                }
                Object.Destroy(player);
                yield return null;
            }

            // A crouch at the top of a jump: the feet come up.
            player = Spawn(prefab, out locomotion);
            yield return Hold(locomotion, default, 0.5f);
            yield return Hold(locomotion, new LocomotionInput { Jump = true }, Step);
            yield return Hold(locomotion, default, 0.3f);
            yield return Hold(locomotion, new LocomotionInput { Crouch = true }, 0.08f);
            Vector3 centre = player.transform.position + Vector3.up * 1.2f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "crouch-air-tuck", centre + player.transform.right * 7.0f, centre);

            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private static GameObject Spawn(GameObject prefab, out PlayerLocomotion locomotion)
        {
            GameObject player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            locomotion = player.GetComponent<PlayerLocomotion>();
            return player;
        }

        private static IEnumerator Hold(PlayerLocomotion locomotion, LocomotionInput input, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Step)
            {
                locomotion.Tick(input, Step);
                yield return null;
            }
        }

        internal static void Views(Camera camera, GameObject player, string name)
        {
            Vector3 middle = player.transform.position + Vector3.up * 1.2f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name + "-side", middle + player.transform.right * 7.0f, middle);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name + "-front", middle + player.transform.forward * 7.0f, middle);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name + "-quarter",
                middle + (player.transform.forward + player.transform.right).normalized * 7.0f + Vector3.up * 1.5f, middle);
        }
    }
}
