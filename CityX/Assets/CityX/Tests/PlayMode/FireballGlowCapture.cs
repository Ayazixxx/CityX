using System.Collections;
using System.IO;
using CityX.Character;
using CityX.View;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: a fireball hanging over the street at night, behind the player, to Logs/CityShots/fireball-glow-*.png (with and without its
    /// glow). Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FireballGlowCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheFireballAtNight()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++) yield return null;
            yield return new WaitForSeconds(0.5f);
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            cycle.Running = false;
            BuildingShapeCapture.UseTheScenesAir();
            float night = SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.5f;
            cycle.TimeOfDay = night;
            cycle.Apply(SkyCycle.At(night));
            DynamicGI.UpdateEnvironment();

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/FireballProjectile.prefab");
            Vector3 body = vitals.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(vitals.transform.forward, Vector3.up).normalized;
            GameObject ball = Object.Instantiate(prefab, body + forward * 2.5f + Vector3.up * 1.8f, Quaternion.LookRotation(forward));
            ball.GetComponent<Rigidbody>().isKinematic = true;
            yield return new WaitForSeconds(0.4f);

            var cameraObject = new GameObject("FireballCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60.0f;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            Vector3 from = body - forward * 4.0f + Vector3.up * 3.0f;
            Light glow = ball.GetComponentInChildren<Light>();
            foreach (bool lit in new[] { true, false })
            {
                if (glow != null) glow.enabled = lit;
                BuildingShapeCapture.Shoot(camera, directory, lit ? "fireball-glow-on" : "fireball-glow-off", from, ball.transform.position);
                yield return null;
            }
            Object.Destroy(ball);
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }
    }
}
