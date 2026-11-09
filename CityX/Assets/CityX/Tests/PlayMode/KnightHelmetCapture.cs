using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player standing in a helmet (the Knight Helmet, the Claw Skull), from the front, the side, three-quarters on and behind,
    /// with the default hair and with the biggest hairstyle (Hyde), to judge the fit (owner, 2026-10-18: "try to fit it the best you can").
    /// Written to Logs/CityShots/knighthelmet-*.png and clawskull-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class KnightHelmetCapture
    {
        [UnityTest]
        public IEnumerator RenderThePlayerInTheKnightHelmet() => Render("Assets/CityX/Wearables/KnightHelmet.prefab", "knighthelmet");

        [UnityTest]
        public IEnumerator RenderThePlayerInTheClawSkull() => Render("Assets/CityX/Wearables/ClawSkull.prefab", "clawskull");

        private static IEnumerator Render(string prefabPath, string name)
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.65f, 0.78f);

            GameObject helmet = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),
                new Vector3(20.0f, 0.5f, 20.0f), Quaternion.identity);
            helmet.GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            var hair = player.GetComponentInChildren<CharacterHair>();

            foreach (string style in new[] { "default", CharacterHair.Hyde })
            {
                if (style != "default" && hair != null)
                {
                    hair.SetStyle(style);
                }
                for (int i = 0; i < 90; i++)
                {
                    locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                }
                Vector3 head = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).position;
                string prefix = $"{name}-{style}";
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-front", head + new Vector3(0.0f, 0.3f, 6.0f), head);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-side", head + new Vector3(6.0f, 0.3f, 0.0f), head);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-threequarter", head + new Vector3(4.2f, 1.0f, 4.2f), head);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-behind", head + new Vector3(-2.0f, 1.0f, -5.6f), head);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-below", head + new Vector3(1.5f, -1.8f, 3.2f), head);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-body", head + new Vector3(4.0f, -0.6f, 9.0f), head + Vector3.down * 1.0f);
            }

            Object.Destroy(helmet);
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
