using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders Semtex (docs/GRENADE.md) to Logs/CityShots/semtex-*.png -- stuck on a wall, stuck on a body's chest, and close up.
    /// Explicit; run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SemtexCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SemtexCapture
    {
        [UnityTest]
        public IEnumerator RenderSemtex()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(-4.0f, 2.0f, 5.0f);
            wall.transform.localScale = new Vector3(6.0f, 4.0f, 0.5f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 50.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(4.0f, 0.05f, 5.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            body.GetComponent<PlayerInputReader>().enabled = false;
            yield return null;
            Physics.SyncTransforms();
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/ThrownSemtex.prefab");

            ThrownGrenade onWall = ThrownGrenade.Spawn(prefab, new Vector3(-4.0f, 2.0f, 0.0f), new Vector3(0.0f, 1.0f, 12.0f), null, false);
            Vector3 chest = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Spine).position + Vector3.up * 0.3f;
            Vector3 from = new Vector3(4.0f, chest.y, 0.5f);
            ThrownGrenade onBody = ThrownGrenade.Spawn(prefab, from, (chest - from).normalized * 12.0f + Vector3.up * 0.5f, null, false);
            yield return Wait(1.0f);
            yield return Shoot(camera, "semtex-on-wall", onWall.transform.position + new Vector3(0.6f, 0.3f, -1.2f), onWall.transform.position);
            yield return Shoot(camera, "semtex-on-body", onBody.transform.position + new Vector3(-0.8f, 0.4f, -1.8f), onBody.transform.position);
            yield return Shoot(camera, "semtex-both", new Vector3(0.0f, 2.5f, -6.0f), new Vector3(0.0f, 1.6f, 5.0f));

            foreach (GameObject go in new[] { body, wall, camera.gameObject, ground, sunObject })
            {
                Object.Destroy(go);
            }
            foreach (ThrownGrenade g in Object.FindObjectsByType<ThrownGrenade>(FindObjectsSortMode.None)) Object.Destroy(g.gameObject);
            FireScene.Clear();
#else
            yield break;
#endif
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
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
