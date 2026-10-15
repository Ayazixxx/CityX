using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the body standing empty-handed and standing with the spiked greatsword, from the side and three-quarters on, so the head's
    /// carriage can be compared (owner, 2026-10-14: "why is he looking upward. make him looking straight like the idle position"). Written to
    /// Logs/CityShots/sword-head-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SwordIdleHeadCapture
    {
        [UnityTest]
        public IEnumerator RenderTheHeadWithAndWithoutTheGreatsword()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.65f, 0.78f);

            for (int i = 0; i < 120; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Shoot(camera, directory, "sword-head-idle-side", new Vector3(6.5f, 1.6f, 0.0f), new Vector3(0.0f, 1.6f, 0.0f));
            Shoot(camera, directory, "sword-head-idle-front", new Vector3(3.5f, 1.8f, 5.5f), new Vector3(0.0f, 1.6f, 0.0f));

            var swordObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/SpikeSword.prefab"),
                new Vector3(20.0f, 0.5f, 20.0f), Quaternion.identity);
            swordObject.GetComponent<SwordWeapon>().OnEquip(player, CityX.Items.ItemInstanceData.None);
            for (int i = 0; i < 150; i++)
            {
                locomotion.Tick(new LocomotionInput { SwordEquipped = true }, 1.0f / 60.0f);
                yield return null;
            }
            Shoot(camera, directory, "sword-head-greatsword-side", new Vector3(6.5f, 1.6f, 0.0f), new Vector3(0.0f, 1.6f, 0.0f));
            Shoot(camera, directory, "sword-head-greatsword-front", new Vector3(3.5f, 1.8f, 5.5f), new Vector3(0.0f, 1.6f, 0.0f));

            Object.Destroy(swordObject);
            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

        private static void Shoot(Camera camera, string directory, string name, Vector3 position, Vector3 lookAt)
        {
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position, Vector3.up);
            const int w = 512, h = 640;
            var target = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            System.Type conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conversion.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
            Object.Destroy(image);
        }
    }
}
