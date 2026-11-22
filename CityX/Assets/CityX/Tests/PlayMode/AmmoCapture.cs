using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the four boxes of ammunition on the ground (owner, 2026-11-21), side by side beside a player for scale and each close, written to
    /// Logs/CityShots/ammo-*.png. Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class AmmoCapture
    {
        private static readonly string[] Boxes = { "PistolAmmo", "RifleAmmo", "ShotgunShells", "RpgRockets" };
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        [UnityTest]
        public IEnumerator RenderTheAmmunition()
        {
#if UNITY_EDITOR
            CaptureGuard.NeedsGraphics();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(20.0f, 1.0f, 20.0f);
            _made.Add(ground);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45.0f, 150.0f, 0.0f);
            _made.Add(sun.gameObject);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(-1.0f, 0.02f, 0.6f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            player.GetComponent<CityX.Character.PlayerInputReader>().enabled = false;
            _made.Add(player);
            var placed = new List<Transform>();
            for (int i = 0; i < Boxes.Length; i++)
            {
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Weapons/Ammo/{Boxes[i]}.prefab");
                Assert.IsNotNull(prefab, Boxes[i] + " was not built");
                GameObject box = Object.Instantiate(prefab, new Vector3(i * 0.55f + (i == Boxes.Length - 1 ? 0.6f : 0.0f), 0.0f, i == Boxes.Length - 1 ? 0.5f : 0.0f), Quaternion.Euler(0.0f, -20.0f, 0.0f));
                _made.Add(box);
                placed.Add(box.transform);
            }
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
            var eye = new GameObject("Eye").AddComponent<Camera>();
            _made.Add(eye.gameObject);
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = new Color(0.55f, 0.65f, 0.75f);
            eye.fieldOfView = 35.0f;
            string folder = BuildingShapeCapture.ShotsFolder;
            BuildingShapeCapture.Shoot(eye, folder, "ammo-all", new Vector3(0.6f, 1.6f, -2.6f), new Vector3(0.6f, 0.3f, 0.2f));
            for (int i = 0; i < placed.Count; i++)
            {
                Vector3 at = placed[i].position + Vector3.up * 0.08f;
                BuildingShapeCapture.Shoot(eye, folder, "ammo-" + Boxes[i], at + new Vector3(0.25f, 0.35f, -0.6f), at);
            }
#else
            yield break;
#endif
        }
    }
}
