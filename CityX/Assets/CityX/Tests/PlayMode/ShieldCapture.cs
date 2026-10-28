using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the round shield (docs/SHIELD.md) to Logs/CityShots/shield-*.png -- lowered and raised, bare-handed and with the
    /// combat sword, from the side, the front and three-quarters. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.ShieldCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ShieldCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheShield()
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

            foreach (bool sword in new[] { false, true })
            {
                GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab"),
                    new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var inventory = player.GetComponent<Inventory>();
                inventory.EquipItemId(ItemRegistry.RoundShield, player);
                if (sword)
                {
                    inventory.EquipItemId(ItemRegistry.CombatSword, player);
                }
                var locomotion = player.GetComponent<PlayerLocomotion>();
                string name = sword ? "shield-sword" : "shield";
                yield return Hold(locomotion, new LocomotionInput { Sword1HEquipped = sword }, 0.6f);
                CrouchCapture.Views(camera, player, name + "-lowered");
                yield return Hold(locomotion, new LocomotionInput { ShieldRaised = true, Sword1HEquipped = sword }, 0.6f);
                CrouchCapture.Views(camera, player, name + "-raised");
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

        private static IEnumerator Hold(PlayerLocomotion locomotion, LocomotionInput input, float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Step)
            {
                locomotion.Tick(input, Step);
                yield return null;
            }
        }
    }
}
