using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the Wraith rendered (owner, 2026-10-26: "make this wraith enemy looks good"), by day beside the player for its size, from the
    /// front, three-quarters and the side; a strip of eight moments over four seconds of floating (the sway of its arms, the writhe of its
    /// wisps); and by night, when its glow shows. Written to Logs/CityShots/wraith-*.png.
    /// Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class WraithCapture
    {
        [UnityTest]
        public IEnumerator RenderTheWraith()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            ground.GetComponent<Renderer>().material.color = new Color(0.32f, 0.33f, 0.36f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(-1.6f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            GameObject wraithObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Wraith.prefab"),
                new Vector3(1.2f, WraithDrift.HoverHeight, 0.0f), Quaternion.identity);
            var wraith = wraithObject.GetComponent<Wraith>();
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Vector3 chest = wraithObject.transform.position + Vector3.up * 1.5f;
            Vector3 between = (player.transform.position + wraithObject.transform.position) * 0.5f + Vector3.up * 1.3f;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-day-with-player", between + new Vector3(0.0f, 0.5f, 9.0f), between);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-day-front", chest + wraithObject.transform.forward * 6.0f + Vector3.up * 0.3f, chest);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-day-threequarter",
                chest + (wraithObject.transform.forward + wraithObject.transform.right).normalized * 6.0f + Vector3.up * 1.2f, chest);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-day-side", chest + wraithObject.transform.right * 6.0f + Vector3.up * 0.3f, chest);
            // The face, close, straight on and from a little aside, as the owner's picture shows it.
            Vector3 skull = wraith.Head.position;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-face-front", skull + wraith.Head.forward * 2.0f, skull);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-face-threequarter",
                skull + (wraith.Head.forward * 1.7f + wraith.Head.right * 1.0f) + Vector3.up * 0.3f, skull);
            // Its tail wearing away into gas, close.
            Vector3 tail = wraithObject.transform.position + Vector3.up * 0.7f;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-tail-front", tail + wraithObject.transform.forward * 3.4f + Vector3.up * 0.2f, tail);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-tail-side", tail + wraithObject.transform.right * 3.4f + Vector3.up * 0.2f, tail);

            // Floating, with no one to hunt: eight moments over four seconds, the camera following it.
            player.SetActive(false);
            for (int shot = 0; shot < 8; shot++)
            {
                for (int i = 0; i < 30; i++)
                {
                    yield return null;
                }
                chest = wraithObject.transform.position + Vector3.up * 1.4f;
                BuildingShapeCapture.Shoot(camera, folder, $"wraith-drift-{shot}", chest + new Vector3(4.5f, 1.0f, 4.5f), chest);
            }

            // The bite: a player with their back to it, 6 m on.
            Vector3 wraithAt = wraithObject.transform.position;
            player.transform.SetPositionAndRotation(new Vector3(wraithAt.x, 0.05f, wraithAt.z + 6.0f), Quaternion.identity);
            player.SetActive(true);
            player.GetComponent<PlayerInputReader>().enabled = true;   // (a body with its reader off and no session is nobody's: nothing hunts it)
            for (int i = 0; i < 900 && wraith.Phase != WraithPhase.Lunging; i++)
            {
                yield return null;
            }
            for (int i = 0; i < 16; i++)
            {
                yield return null;
            }
            Vector3 middle = (player.transform.position + wraithObject.transform.position) * 0.5f + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-lunge-side", middle + player.transform.right * 7.0f + Vector3.up * 0.6f, middle);
            for (int i = 0; i < 900 && wraith.Phase != WraithPhase.Biting; i++)
            {
                yield return null;
            }
            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }
            middle = (player.transform.position + wraithObject.transform.position) * 0.5f + Vector3.up * 1.6f;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-bite-side", middle + player.transform.right * 6.0f + Vector3.up * 0.4f, middle);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-bite-behind", middle - player.transform.forward * 6.0f + Vector3.up * 1.0f, middle);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-bite-front", middle + player.transform.forward * 5.0f + player.transform.right * 2.0f + Vector3.up * 0.8f, middle);
            Debug.Log($"CityX wraith bite: player health {player.GetComponent<PlayerVitals>().Health:0}, teeth to the neck {Vector3.Distance(wraith.Jaw.position, player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Neck).position):0.00} m");
            for (int i = 0; i < 900 && wraith.Phase != WraithPhase.Thrown; i++)
            {
                yield return null;
            }
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }
            middle = (player.transform.position + wraithObject.transform.position) * 0.5f + Vector3.up * 1.6f;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-thrown-side", middle + player.transform.right * 7.0f + Vector3.up * 0.6f, middle);
            player.SetActive(false);

            // Night: no sun, a near-black world; its own glow is the light.
            sun.intensity = 0.0f;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            ground.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.09f);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
            chest = wraithObject.transform.position + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-night-threequarter", chest + new Vector3(4.5f, 1.0f, 4.5f), chest);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-night-close", chest + new Vector3(2.0f, 0.6f, 2.4f), chest);

            // Its eyes as the game draws them: through the game's own grade (the bloom that takes their glow, the tonemap), by night and by day.
            var grade = new GameObject("Grade").AddComponent<UnityEngine.Rendering.Volume>();
            grade.isGlobal = true;
            grade.sharedProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/CityX/View/Materials/CityGrade.asset");
            camera.allowHDR = true;
            var cameraData = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            skull = wraith.Head.position;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-eyes-night", skull + wraith.Head.forward * 2.6f + wraith.Head.right * 0.9f, skull);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-eyes-night-far", skull + wraith.Head.forward * 9.0f + Vector3.up * 0.4f, skull);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-eyes-night-side", skull + wraith.Head.right * 2.6f, skull);
            BuildingShapeCapture.Shoot(camera, folder, "wraith-eyes-night-behind", skull - wraith.Head.forward * 2.6f + wraith.Head.right * 0.9f, skull);
            sun.intensity = 1.3f;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            ground.GetComponent<Renderer>().material.color = new Color(0.32f, 0.33f, 0.36f);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
            skull = wraith.Head.position;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-eyes-day", skull + wraith.Head.forward * 2.6f + wraith.Head.right * 0.9f, skull);
            cameraData.renderPostProcessing = false;
            Object.Destroy(grade.gameObject);
            sun.intensity = 0.0f;
            camera.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            ground.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.09f);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            // Hit: the flash and the flinch, a few frames in.
            ((IDamageable)wraith).ApplyHit(CombatTuning.PunchDamage, chest + Vector3.forward, CombatTuning.HitstunSeconds);
            for (int i = 0; i < 4; i++)
            {
                yield return null;
            }
            chest = wraithObject.transform.position + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, folder, "wraith-night-hit", chest + new Vector3(4.5f, 1.0f, 4.5f), chest);

            Object.Destroy(wraithObject);
            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }
    }
}
