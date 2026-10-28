using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders Arcane Barrier (docs/ARCANE_BARRIER.md) to Logs/CityShots/barrier-*.png -- from the caster's own third-person view with
    /// someone standing beyond the wall (seen through it), from the front as an attacker sees it, from the side, the hands up close, at night,
    /// struck to half health with rings spreading, and fading away after it broke. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.ArcaneBarrierCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ArcaneBarrierCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheBarrier()
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
            camera.fieldOfView = 50.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            GameObject caster = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            caster.GetComponent<PlayerInputReader>().enabled = false;
            GameObject beyond = Object.Instantiate(prefab, new Vector3(0.6f, 0.05f, 5.0f), Quaternion.Euler(0.0f, 200.0f, 0.0f));
            beyond.GetComponent<PlayerInputReader>().enabled = false;
            var barrier = caster.GetComponent<ArcaneBarrierCaster>();
            var vitals = caster.GetComponent<PlayerVitals>();
            yield return Hold(caster, false, 0.4f);
            barrier.TryRaise(Vector3.forward);
            yield return Hold(caster, true, 0.6f);

            Vector3 chest = caster.transform.position + Vector3.up * 1.6f;
            // The game's own view (ThirdPersonCamera: about 5 m back, up over the head, a little right).
            camera.fieldOfView = 50.0f;
            yield return ShootFacing(camera, "barrier-own-view", chest + new Vector3(0.87f, 1.3f, -5.0f), chest + Vector3.forward * 6.0f, caster, true);
            camera.fieldOfView = 35.0f;
            yield return ShootFacing(camera, "barrier-front", chest + new Vector3(2.5f, 0.6f, 9.0f), chest, caster, true);
            yield return ShootFacing(camera, "barrier-side", chest + new Vector3(7.0f, 0.4f, 0.5f), chest + Vector3.forward * 0.6f, caster, true);
            // The hands up close, through the wall and from the side: pushed out flat against it, fingers up.
            Animator animator = caster.GetComponentInChildren<Animator>();
            Vector3 hands = (animator.GetBoneTransform(HumanBodyBones.LeftHand).position + animator.GetBoneTransform(HumanBodyBones.RightHand).position) * 0.5f;
            camera.fieldOfView = 25.0f;
            yield return ShootFacing(camera, "barrier-hands-front", hands + Vector3.forward * 2.2f + Vector3.up * 0.2f, hands, caster, true);
            yield return ShootFacing(camera, "barrier-hands-side", hands + new Vector3(1.6f, 0.1f, 0.3f), hands, caster, true);
            camera.fieldOfView = 35.0f;
            sun.intensity = 0.05f;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            yield return ShootFacing(camera, "barrier-night", chest + new Vector3(2.5f, 0.6f, 9.0f), chest, caster, true);
            sun.intensity = 1.3f;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            for (int i = 0; i < 5; i++)
            {
                DamageContext.Strike(vitals, barrier.MaxHealth * 0.1f, chest + new Vector3(-1.5f + 0.75f * i, (i % 2) * 0.8f - 0.4f, 12.0f), 0.0f,
                    DamageKind.Bullet, 0.0f);
                yield return Hold(caster, true, 0.06f);
            }
            yield return ShootFacing(camera, "barrier-struck", chest + new Vector3(2.5f, 0.6f, 9.0f), chest, caster, true);

            DamageContext.Strike(vitals, barrier.MaxHealth, chest + Vector3.forward * 12.0f, 0.0f, DamageKind.Bullet, 0.0f);
            yield return Hold(caster, false, ArcaneBarrierRules.BreakFadeSeconds * 0.4f);
            yield return ShootFacing(camera, "barrier-fading", chest + new Vector3(2.5f, 0.6f, 9.0f), chest, caster, false);

            Object.Destroy(caster);
            Object.Destroy(beyond);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private static IEnumerator Hold(GameObject player, bool held, float seconds)
        {
            var caster = player.GetComponent<ArcaneBarrierCaster>();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (float t = 0.0f; t < seconds; t += Step)
            {
                if (held)
                {
                    caster.Hold(true);
                }
                locomotion.Tick(new LocomotionInput { YawDegrees = player.transform.eulerAngles.y }, Step);
                yield return null;
            }
        }

        private static IEnumerator ShootFacing(Camera camera, string name, Vector3 position, Vector3 lookAt, GameObject caster, bool held)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            yield return Hold(caster, held, Step);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
