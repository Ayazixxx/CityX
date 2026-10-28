using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders Arcane Orb (docs/ARCANE_ORB.md) to Logs/CityShots/arcane-*.png -- the ball in the air, three cursed orbs circling a
    /// body (by day and on a dark ground), and the orbs diving in to strike. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.ArcaneOrbCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ArcaneOrbCapture
    {
        [UnityTest]
        public IEnumerator RenderArcaneOrb()
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

            GameObject target = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.Euler(0.0f, 200.0f, 0.0f));
            target.GetComponent<PlayerInputReader>().enabled = false;
            var caster = new GameObject("Caster").AddComponent<ArcaneOrbCaster>();
            Vector3 middle = target.transform.position + Vector3.up * 1.4f;

            ArcaneOrbProjectile ball = ArcaneOrbProjectile.Spawn(middle + new Vector3(3.0f, 0.4f, 0.0f), Vector3.left * 0.01f, 3, 0.0f, 0.0f, 0.0f,
                caster.gameObject, null, cosmetic: true);
            ball.GetComponent<Rigidbody>().isKinematic = true;
            yield return Wait(0.3f);
            yield return ShootFacing(camera, "arcane-ball", ball.transform.position + new Vector3(2.0f, 1.0f, 8.0f), ball.transform.position);
            Object.Destroy(ball.gameObject);

            CursedOrbs.Show(target.GetComponent<IDamageable>(), caster.gameObject, 3, 10.0f, middle + Vector3.right);
            yield return Wait(1.0f);
            yield return ShootFacing(camera, "arcane-orbs-quarter", middle + new Vector3(5.0f, 2.0f, 5.0f), middle);
            yield return ShootFacing(camera, "arcane-orbs-side", middle + new Vector3(8.0f, 0.5f, 0.0f), middle);
            sun.intensity = 0.05f;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.04f);
            yield return ShootFacing(camera, "arcane-orbs-night", middle + new Vector3(5.0f, 2.0f, 5.0f), middle);
            sun.intensity = 1.3f;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            CursedOrbs curse = CursedOrbs.Find(target.transform, caster.gameObject);
            curse.Curse.Match(3, 0.62f);
            yield return Wait(0.62f + CursedOrbs.DiveSeconds + 0.05f);
            yield return ShootFacing(camera, "arcane-strike", middle + new Vector3(7.0f, 1.0f, 0.0f), middle);

            Object.Destroy(target);
            Object.Destroy(caster.gameObject);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private static IEnumerator Wait(float seconds)
        {
            for (int i = 0; i < Mathf.RoundToInt(seconds * 60.0f); i++)
            {
                yield return null;
            }
        }

        /// <summary>Puts the camera in place for a frame first, so the glows have turned to it, then renders.</summary>
        internal static IEnumerator ShootFacing(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
