using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player in each beard, in the game's cel look (lit from the front; from the front, three-quarters, the side, below and the
    /// game camera's distance), then the full beards over the fur parka's collar, so their shape, their edge and any line drawn on them can be
    /// looked at. Written to Logs/CityShots/beard-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BeardCapture
    {
        [UnityTest]
        public IEnumerator RenderThePlayerInEachBeard()
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
            sun.transform.rotation = Quaternion.Euler(35.0f, 200.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            string folder = BuildingShapeCapture.ShotsFolder;

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            // Player.prefab wears no hair (the scene's player is given it when built): the networked body's, every style wired, onto its head.
            var styles = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab")
                .GetComponentInChildren<CharacterHair>(true);
            var hair = player.AddComponent<CharacterHair>();
            hair.Head = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head);
            hair.StyleMeshes = styles.StyleMeshes;
            hair.HairMaterial = styles.HairMaterial;
            hair.SetStyle(CharacterHair.IdkNeedsWork);
            hair.SetColour(new Color(0.36f, 0.22f, 0.13f));
            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 head = hair.Head.position;
            foreach (string beard in CharacterHair.ValidBeards)
            {
                if (beard == CharacterHair.None)
                {
                    continue;
                }
                hair.SetBeard(beard);
                yield return null;
                Vector3 chin = head + new Vector3(0.0f, -0.1f, 0.0f);
                BuildingShapeCapture.Shoot(camera, folder, $"beard-{beard}-front", chin + new Vector3(0.0f, 0.1f, 2.6f), chin);
                BuildingShapeCapture.Shoot(camera, folder, $"beard-{beard}-zoomed", chin + new Vector3(0.15f, -0.05f, 0.95f), chin);
                BuildingShapeCapture.Shoot(camera, folder, $"beard-{beard}-threequarter", chin + new Vector3(1.8f, 0.2f, 1.9f), chin);
                BuildingShapeCapture.Shoot(camera, folder, $"beard-{beard}-side", chin + new Vector3(2.6f, 0.0f, 0.2f), chin);
                BuildingShapeCapture.Shoot(camera, folder, $"beard-{beard}-below", chin + new Vector3(1.0f, -1.3f, 2.1f), chin);
                Vector3 chest = player.transform.position + Vector3.up * 1.6f;
                BuildingShapeCapture.Shoot(camera, folder, $"beard-{beard}-far", chest + new Vector3(1.5f, 0.6f, 7.5f), chest);
            }

            Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/GreenFurHoodedParka.prefab"),
                    new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity)
                .GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
            foreach (string beard in new[] { CharacterHair.FullBeard, CharacterHair.SpikyBeard })
            {
                hair.SetBeard(beard);
                for (int i = 0; i < 5; i++)
                {
                    locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                }
                Vector3 chin = head + new Vector3(0.0f, -0.2f, 0.0f);
                BuildingShapeCapture.Shoot(camera, folder, $"beard-{beard}-parka-front", chin + new Vector3(0.3f, 0.1f, 3.0f), chin);
                BuildingShapeCapture.Shoot(camera, folder, $"beard-{beard}-parka-side", chin + new Vector3(2.8f, 0.0f, 1.0f), chin);
                BuildingShapeCapture.Shoot(camera, folder, $"beard-{beard}-parka-below", chin + new Vector3(1.2f, -1.2f, 2.4f), chin);
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
