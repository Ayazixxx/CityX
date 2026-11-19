using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the shields (docs/SHIELD.md) to Logs/CityShots/shield-*.png and riotshield-*.png -- lowered and raised, bare-handed and with a
    /// weapon, from the side, the front and three-quarters. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.ShieldCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ShieldCapture
    {
        private const float Step = 1.0f / 60.0f;

        /// <summary>The riot shield to riotshield-*.png: bare-handed, with the pistol and with the combat sword, lowered and raised.</summary>
        [UnityTest]
        public IEnumerator RenderTheRiotShield()
        {
            yield return Render(ItemRegistry.RiotShield, "riotshield", new[] { null, ItemRegistry.Pistol, ItemRegistry.CombatSword });
        }

        [UnityTest]
        public IEnumerator RenderTheShield()
        {
            yield return Render(ItemRegistry.RoundShield, "shield", new[] { null, ItemRegistry.CombatSword });
        }

        private static IEnumerator Render(string shieldId, string prefix, string[] weapons)
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

            foreach (string weapon in weapons)
            {
                bool sword = weapon == ItemRegistry.CombatSword, pistol = weapon == ItemRegistry.Pistol;
                GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab"),
                    new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var inventory = player.GetComponent<Inventory>();
                inventory.EquipItemId(shieldId, player);
                if (weapon != null)
                {
                    inventory.EquipItemId(weapon, player);
                }
                var locomotion = player.GetComponent<PlayerLocomotion>();
                string name = sword ? prefix + "-sword" : pistol ? prefix + "-pistol" : prefix;
                yield return Hold(locomotion, new LocomotionInput { Sword1HEquipped = sword, GunEquipped = pistol }, 0.6f);
                CrouchCapture.Views(camera, player, name + "-lowered");
                yield return Hold(locomotion, new LocomotionInput { ShieldRaised = true, Sword1HEquipped = sword, GunEquipped = pistol }, 0.6f);
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
