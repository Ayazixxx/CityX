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
    /// Not a test: renders the cast pose (PlayerLocomotion.Casting.cs; owner, 2026-11-15: the free hand up, its palm to the target, the spell
    /// out of it) to Logs/CityShots/cast-*.png: unarmed (the right hand) and with the combat sword in the right hand (the left), each from the
    /// front quarter and from behind as the game's camera sees it, a fireball just out of the palm. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.CastPoseCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class CastPoseCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheCastPose()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 40.0f;
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);
            GameObject fireball = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FireballProjectile.prefab");

            foreach (bool sword in new[] { false, true })
            {
                GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                    new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                GameObject held = null;
                if (sword)
                {
                    held = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab"));
                    held.GetComponent<SwordWeapon>().OnEquip(player, ItemInstanceData.None);
                }
                var input = new LocomotionInput { Sword1HEquipped = sword };
                for (int i = 0; i < 40; i++)
                {
                    locomotion.Tick(input, Step);
                    yield return null;
                }
                Vector3 aim = (Vector3.forward + Vector3.up * 0.08f).normalized;
                FireballProjectile ball = null;
                for (int i = 0; i < 14; i++)
                {
                    locomotion.SetCastPose(1.0f, aim);
                    locomotion.Tick(input, Step);
                    if (i == 10 && fireball != null)
                    {
                        ball = FireballProjectile.Spawn(fireball, locomotion.CastPalmPoint(aim), aim * 6.0f, 0.0f, player.GetComponentsInChildren<Collider>(), cosmetic: true);
                    }
                    yield return null;
                }
                string name = sword ? "cast-sword" : "cast-unarmed";
                Vector3 chest = player.transform.position + Vector3.up * 2.0f;
                yield return Shoot(camera, name + "-front", chest + new Vector3(sword ? -2.6f : 2.6f, 0.3f, 3.6f), chest + Vector3.forward * 0.6f);
                yield return Shoot(camera, name + "-behind", chest + new Vector3(0.9f, 0.6f, -5.0f), chest + Vector3.forward * 2.0f);
                yield return Shoot(camera, name + "-side", chest + new Vector3(sword ? -3.2f : 3.2f, 0.2f, 0.6f), chest + Vector3.forward * 0.5f);
                foreach (GameObject go in new[] { player, held, ball != null ? ball.gameObject : null })
                {
                    if (go != null) Object.Destroy(go);
                }
                yield return null;
            }
            foreach (GameObject go in new[] { camera.gameObject, ground, sun.gameObject })
            {
                Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 from, Vector3 at)
        {
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, from, at);
            yield return null;
        }
    }
}
