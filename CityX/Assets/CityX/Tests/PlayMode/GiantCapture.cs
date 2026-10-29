using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the giant (docs/GIANT.md) to Logs/CityShots/giant-*.png -- standing over a player, crouched for its leap, high in
    /// the air, and its slam's dust racing out where it lands. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.GiantCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class GiantCapture
    {
        [UnityTest]
        public IEnumerator RenderTheGiant()
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
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            camera.farClipPlane = 2000.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Vector3.zero, Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = true;
            GameObject giant = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Giant.prefab"),
                new Vector3(0.0f, 0.0f, 34.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            var brain = giant.GetComponent<GiantBrain>();
            var locomotion = giant.GetComponent<PlayerLocomotion>();

            // Over a player: from behind the player, looking up at it.
            yield return Until(() => brain.Mind.Current == GiantMind.Phase.Approach, 2.0f);
            yield return Shoot(camera, "giant-standing", new Vector3(4.0f, 3.0f, -12.0f), giant.transform.position + Vector3.up * 12.0f);
            yield return Until(() => brain.Mind.Current == GiantMind.Phase.Crouch, 10.0f);
            yield return Until(() => false, GiantRules.CrouchSeconds * 0.7f);
            Vector3 side = new Vector3(55.0f, 14.0f, giant.transform.position.z * 0.5f);
            yield return Shoot(camera, "giant-crouch", side, giant.transform.position + Vector3.up * 8.0f);
            yield return Until(() => brain.Mind.Current == GiantMind.Phase.Airborne && !locomotion.IsGrounded, 3.0f);
            yield return Until(() => false, 0.35f);
            yield return Shoot(camera, "giant-leap", side + Vector3.up * 6.0f, giant.transform.position + Vector3.up * 6.0f);
            yield return Until(() => brain.Slams > 0, 8.0f);
            yield return Until(() => false, 0.25f);
            Vector3 landed = giant.transform.position;
            yield return Shoot(camera, "giant-slam", landed + new Vector3(38.0f, 22.0f, -38.0f), landed + Vector3.up * 4.0f);
            yield return Until(() => false, 0.4f);
            yield return Shoot(camera, "giant-slam-dust", landed + new Vector3(38.0f, 22.0f, -38.0f), landed + Vector3.up * 4.0f);

            foreach (GameObject go in new[] { player, giant, camera.gameObject, ground, sunObject })
            {
                Object.Destroy(go);
            }
            foreach (GiantSlam slam in Object.FindObjectsByType<GiantSlam>(FindObjectsSortMode.None))
            {
                Object.Destroy(slam.gameObject);
            }
#else
            yield break;
#endif
        }

        private static IEnumerator Until(System.Func<bool> done, float seconds)
        {
            for (float t = 0.0f; t < seconds && !done(); t += Time.deltaTime)
            {
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
