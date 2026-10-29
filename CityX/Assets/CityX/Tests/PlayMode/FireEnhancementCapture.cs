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
    /// Not a test: renders the Fire Enhancement (docs/FIRE_ENHANCEMENT.md) to Logs/CityShots/fire-enhance-*.png -- the sword ablaze in the
    /// hand by day and at dusk, its burning cut mid-swing, the body it cut alight, and fists ablaze. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.FireEnhancementCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FireEnhancementCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheFireEnhancement()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 45.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            GameObject body = Body(new Vector3(0.0f, 0.1f, 0.0f));
            GameObject sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SpikeSword.prefab"));
            sword.GetComponent<SwordWeapon>().OnEquip(body, ItemInstanceData.None);
            var combat = body.GetComponent<SoulsSwordCombat>();
            var fire = body.GetComponent<FireEnhancementAbility>();
            if (fire == null)
            {
                fire = body.AddComponent<FireEnhancementAbility>();
            }
            fire.Bind();
            yield return Swing(body, 0.5f);
            fire.TryCast();
            yield return Swing(body, 1.0f);
            Vector3 chest = body.transform.position + Vector3.up * 1.6f;
            yield return Shoot(camera, "fire-enhance-sword", chest + new Vector3(2.6f, 0.4f, 2.2f), chest + Vector3.up * 0.2f);
            sun.intensity = 0.08f;
            camera.backgroundColor = new Color(0.04f, 0.04f, 0.08f);
            RenderSettings.ambientIntensity = 0.2f;
            yield return Shoot(camera, "fire-enhance-sword-dusk", chest + new Vector3(2.6f, 0.4f, 2.2f), chest + Vector3.up * 0.2f);

            // The cut: a trail of fire behind the blade, and the dummy it lands on alight.
            var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            dummy.transform.position = new Vector3(0.0f, 1.0f, 1.6f);
            dummy.AddComponent<SparringDummy>().Reset();
            Physics.SyncTransforms();
            combat.RequestLight(0.0f);
            yield return Swing(body, combat.Light1.Startup + combat.Light1.Active * 0.5f);
            yield return Shoot(camera, "fire-enhance-cut", chest + new Vector3(3.4f, 0.6f, 0.6f), chest + Vector3.forward * 0.8f);
            yield return Swing(body, 0.6f);
            yield return Shoot(camera, "fire-enhance-burning", chest + new Vector3(3.4f, 0.6f, 3.4f), dummy.transform.position);

            // Fists: the hand that will throw the burning punch.
            sword.GetComponent<SwordWeapon>().OnUnequip(body);
            yield return Swing(body, 1.6f);
            fire.TryCast();
            yield return Swing(body, 0.8f);
            yield return Shoot(camera, "fire-enhance-fist", chest + new Vector3(2.0f, 0.2f, 1.8f), chest - Vector3.up * 0.3f);
            RenderSettings.ambientIntensity = 1.0f;

            foreach (GameObject go in new[] { body, dummy, camera.gameObject, ground, sunObject })
            {
                Object.Destroy(go);
            }
            FireScene.Clear();
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static GameObject Body(Vector3 at)
        {
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                at, Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            return body;
        }
#endif

        private static IEnumerator Swing(GameObject body, float seconds)
        {
            var combat = body.GetComponent<SoulsSwordCombat>();
            var vitals = body.GetComponent<PlayerVitals>();
            var locomotion = body.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                if (combat != null && combat.IsEquipped)
                {
                    combat.Tick(Step);
                }
                vitals.TickStamina(false, Step);
                locomotion.Tick(new LocomotionInput { SwordEquipped = combat != null && combat.IsEquipped }, Step);
                yield return null;
            }
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
