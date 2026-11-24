using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player in the fitted cap (docs/FITTED_CAP.md), standing (front, three-quarters, side, from above behind, straight down,
    /// close), then with each hairstyle, so the fit can be looked at. Written to Logs/CityShots/fittedcap-*.png.
    /// Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class FittedCapCapture
    {
        [UnityTest]
        public IEnumerator RenderThePlayerInTheFittedCap()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
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
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(CityX.Items.FittedCapColourways.PrefabPath(CityX.Items.FittedCapColourways.All[0].ItemId)),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity).GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 head = player.transform.position + Vector3.up * 2.2f;
            BuildingShapeCapture.Shoot(camera, folder, "fittedcap-front", head + new Vector3(0.0f, 0.4f, 6.0f), head);
            BuildingShapeCapture.Shoot(camera, folder, "fittedcap-threequarter", head + new Vector3(4.0f, 1.5f, 4.0f), head);
            BuildingShapeCapture.Shoot(camera, folder, "fittedcap-side", head + new Vector3(6.0f, 0.3f, 0.0f), head);
            BuildingShapeCapture.Shoot(camera, folder, "fittedcap-above-behind", head + new Vector3(-2.0f, 3.5f, -4.0f), head);
            BuildingShapeCapture.Shoot(camera, folder, "fittedcap-top", head + new Vector3(0.0f, 5.0f, 0.3f), head);   // the owner's red circle
            BuildingShapeCapture.Shoot(camera, folder, "fittedcap-close", head + new Vector3(1.2f, 0.3f, 1.9f), head);
            Vector3 hips = player.transform.position + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, folder, "fittedcap-whole", hips + new Vector3(3.0f, 1.0f, 8.0f), hips);

            // Player.prefab wears no hair (the scene's player is given it when built): the networked body's, every style wired, onto its head.
            var styles = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab")
                .GetComponentInChildren<CharacterHair>(true);
            var hair = player.AddComponent<CharacterHair>();
            hair.Head = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            hair.StyleMeshes = styles.StyleMeshes;
            hair.HairMaterial = styles.HairMaterial;
            foreach (string style in CharacterHair.ValidStyles)
            {
                hair.SetStyle(style);
                yield return null;
                BuildingShapeCapture.Shoot(camera, folder, $"fittedcap-hair-{style.ToLowerInvariant()}", head + new Vector3(2.5f, 0.6f, 4.5f), head);
            }

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
