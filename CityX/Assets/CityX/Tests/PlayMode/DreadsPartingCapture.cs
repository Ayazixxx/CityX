using System.Collections;
using System.IO;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the dreads from in front and a little below, against white, as on the character creator (owner, 2026-11-21: "hide the visible
    /// white bit that is a result of being able to see underneath the hairstyle", circled at the parting on top). Written to
    /// Logs/CityShots/dreads-parting-*.png. Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class DreadsPartingCapture
    {
        private GameObject _player;
        private GameObject _light;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_light != null) Object.Destroy(_light);
        }

        [UnityTest]
        public IEnumerator RenderTheDreadsFromBelowTheirParting()
        {
#if UNITY_EDITOR
            CaptureGuard.NeedsGraphics();
            Time.captureFramerate = 60;
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), Vector3.zero, Quaternion.identity);
            CharacterHair hair = HairPushPlayTests.PutHairOn(_player);
            var meshes = hair.StyleMeshes;
            meshes[System.Array.IndexOf(CharacterHair.ValidStyles, CharacterHair.Dreads) - 1] =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/DreadsMesh.asset");
            hair.StyleMeshes = meshes;
            hair.SetStyle(CharacterHair.Dreads);
            hair.SetColour(new Color(0.12f, 0.12f, 0.12f));
            _light = new GameObject("Sun");
            var sun = _light.AddComponent<Light>();
            sun.type = LightType.Directional;
            _light.transform.rotation = Quaternion.Euler(40.0f, 160.0f, 0.0f);
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Transform head = _player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            var eye = new GameObject("Eye").AddComponent<Camera>();
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = Color.white;
            eye.fieldOfView = 30.0f;
            Vector3 forward = _player.transform.forward;
            try
            {
                foreach ((string name, float turn, float below) in new[] { ("front-low", 0.0f, 0.6f), ("front-low-left", -20.0f, 0.6f), ("front-low-right", 20.0f, 0.6f), ("front", 0.0f, 0.0f), ("under", 0.0f, 1.3f), ("under-left", -15.0f, 1.3f), ("under-right", 15.0f, 1.3f), ("close-low", 0.0f, 0.45f), ("close-low-left", -12.0f, 0.45f) })
                {
                    Vector3 from = head.position + Quaternion.AngleAxis(turn, Vector3.up) * forward * (name.StartsWith("close") ? 1.1f : 2.4f) - Vector3.up * below;
                    eye.transform.SetPositionAndRotation(from, Quaternion.LookRotation(head.position + Vector3.up * 0.15f - from));
                    Shoot(eye, "dreads-parting-" + name);
                }
            }
            finally
            {
                Object.Destroy(eye.gameObject);
            }
#else
            yield break;
#endif
        }

        private static void Shoot(Camera camera, string name)
        {
            var target = RenderTexture.GetTemporary(900, 900, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
            image.Apply();
            Directory.CreateDirectory(BuildingShapeCapture.ShotsFolder);
            CapturePng.Write(image, Path.Combine(BuildingShapeCapture.ShotsFolder, name + ".png"));
            Object.Destroy(image);
            RenderTexture.active = null;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
