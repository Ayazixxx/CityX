using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the bare-handed guard (FistGuard) on the real body, from the front-left and from behind as the game's camera sees it: coming
    /// up, held, walked with, a blow soaked, a blow parried (Logs/CityShots/fistguard-{moment}-{view}.png). Needs a graphics device.
    /// </summary>
    [Explicit("capture")]
    public sealed class FistGuardCapture
    {
        private const float Dt = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator FilmTheGuard()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            camera.fieldOfView = 40.0f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var melee = player.GetComponent<MeleeAttacker>();
            var vitals = player.GetComponent<PlayerVitals>();
            vitals.Roll = () => 1.0f;
            var still = new LocomotionInput { YawDegrees = 0.0f };

            IEnumerator Steps(int count, bool held, LocomotionInput input)
            {
                for (int i = 0; i < count; i++)
                {
                    melee.SetGuardHeld(held);
                    melee.Tick(Dt);
                    locomotion.Tick(input, Dt);
                    yield return null;
                }
            }

            void Shoot(string moment)
            {
                Vector3 at = player.transform.position + Vector3.up * 1.5f;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"fistguard-{moment}-front", at + new Vector3(-2.6f, 0.6f, 5.2f), at);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"fistguard-{moment}-behind", at + new Vector3(0.9f, 1.2f, -5.0f),
                    at + Vector3.forward * 2.0f);
            }

            yield return Steps(30, false, still);
            Shoot("0-standing");
            yield return Steps(5, true, still);
            Shoot("1-coming-up");
            yield return Steps(30, true, still);
            Shoot("2-held");
            ((IDamageable)vitals).ApplyHit(1.0f, player.transform.position + Vector3.forward, 0.3f);   // in the parry's window: swatted
            yield return Steps(8, true, still);
            Shoot("3-parry");
            yield return Steps(40, true, still);
            ((IDamageable)vitals).ApplyHit(10.0f, player.transform.position + Vector3.forward, 0.3f);
            yield return Steps(5, true, still);
            Shoot("4-blow-soaked");
            var walk = new LocomotionInput { Move = new Vector2(0.0f, 1.0f), YawDegrees = 0.0f };
            for (int i = 0; i < 4; i++)
            {
                yield return Steps(12, true, walk);
                Shoot($"5-walking-{i}");
            }
            Object.Destroy(player);
            Object.Destroy(ground);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sun.gameObject);
#else
            yield break;
#endif
        }
    }
}
