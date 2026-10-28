using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the angel wings (docs/WINGS.md) to Logs/CityShots/wings-*.png -- folded on the back (from behind and the side),
    /// hovering (front quarter, at the bottom of a downstroke and at the top), climbing, gliding forward (side and front), and six moments of
    /// one hovering beat. Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.AngelWingsCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class AngelWingsCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheWings()
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
            camera.fieldOfView = 40.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var wings = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(AngelWings.PrefabPath)).GetComponent<AngelWings>();
            wings.OnEquip(player, ItemInstanceData.None);
            var locomotion = player.GetComponent<PlayerLocomotion>();

            yield return Fly(player, wings, 1.0f);
            Vector3 chest = player.transform.position + Vector3.up * 1.6f;
            yield return Shoot(camera, "wings-folded-back", chest + new Vector3(2.0f, 1.0f, -7.0f), chest);
            yield return Shoot(camera, "wings-folded-side", chest + new Vector3(7.0f, 0.5f, -0.5f), chest);

            locomotion.SetVerticalVelocity(6.0f);
            yield return Fly(player, wings, 0.1f);
            yield return Fly(player, wings, 0.6f, thrust: true);
            yield return Fly(player, wings, 1.5f);
            // Hovering: the bottom of a downstroke, then the top.
            yield return Until(player, wings, b => b.Phase > WingBeat.DownstrokeShare - 0.03f && b.Phase < WingBeat.DownstrokeShare + 0.03f);
            chest = player.transform.position + Vector3.up * 1.6f;
            yield return Shoot(camera, "wings-hover-down", chest + new Vector3(5.0f, 1.5f, 9.0f), chest);
            yield return Until(player, wings, b => b.Phase > 0.97f || b.Phase < 0.03f);
            chest = player.transform.position + Vector3.up * 1.6f;
            yield return Shoot(camera, "wings-hover-up", chest + new Vector3(5.0f, 1.5f, 9.0f), chest);
            yield return Shoot(camera, "wings-hover-behind", chest + new Vector3(0.0f, 2.5f, -9.0f), chest);

            // Six moments of one hovering beat, from the front quarter.
            yield return Until(player, wings, b => b.Phase < 0.02f);
            for (int i = 0; i < 6; i++)
            {
                chest = player.transform.position + Vector3.up * 1.6f;
                yield return Shoot(camera, $"wings-beat-{i}", chest + new Vector3(6.0f, 2.0f, 8.0f), chest);
                float target = (i + 1) / 6.0f;
                yield return Until(player, wings, b => b.Phase >= target - 0.01f && b.Phase < target + 0.1f || (target >= 1.0f && b.Phase < 0.05f));
            }

            // Climbing, mid-downstroke.
            yield return Fly(player, wings, 0.6f, thrust: true);
            yield return Until(player, wings, b => b.Phase > 0.2f && b.Phase < 0.28f, thrust: true);
            chest = player.transform.position + Vector3.up * 1.6f;
            yield return Shoot(camera, "wings-climb", chest + new Vector3(6.0f, -1.0f, 8.0f), chest);

            // Gliding forward at speed: from the side, and from ahead.
            yield return Fly(player, wings, 3.0f, move: Vector2.up);
            chest = player.transform.position + Vector3.up * 1.6f;
            yield return Shoot(camera, "wings-glide-side", chest + new Vector3(8.0f, 0.5f, 0.0f), chest);
            yield return Shoot(camera, "wings-glide-front", chest + new Vector3(0.0f, 1.0f, 9.0f), chest);
            yield return Shoot(camera, "wings-glide-above", chest + new Vector3(2.0f, 9.0f, -4.0f), chest);

            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private static IEnumerator Fly(GameObject player, AngelWings wings, float seconds, bool thrust = false, Vector2 move = default)
        {
            for (float t = 0.0f; t < seconds; t += Step)
            {
                Tick(player, wings, thrust, move);
                yield return null;
            }
        }

        private static IEnumerator Until(GameObject player, AngelWings wings, System.Func<WingBeat, bool> condition, bool thrust = false)
        {
            for (int i = 0; i < 600 && !condition(wings.Beat); i++)
            {
                Tick(player, wings, thrust, Vector2.zero);
                yield return null;
            }
        }

        private static void Tick(GameObject player, AngelWings wings, bool thrust, Vector2 move)
        {
            var locomotion = player.GetComponent<PlayerLocomotion>();
            wings.Tick(new FlightInput { ThrustHeld = thrust, Move = move, Airborne = !locomotion.IsGrounded }, Step);
            locomotion.Tick(new LocomotionInput
            {
                YawDegrees = player.transform.eulerAngles.y,
                Move = move,
                JetpackThrusting = wings.IsFlying,
                FlightSpeed = wings.HorizontalSpeed,
                FlightAcceleration = wings.HorizontalAcceleration,
            }, Step);
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
