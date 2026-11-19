using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player in the 3D Glasses, standing (front, three-quarters, side, above behind, close on the lenses), with the Farmer Hat
    /// over them, and in them with each hairstyle, so the fit, the lenses' film and the arms against the hair can be looked at. Written to
    /// Logs/CityShots/glasses-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class ThreeDGlassesCapture
    {
        [UnityTest]
        public IEnumerator RenderThePlayerInTheGlasses()
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
            Wear(player, "Assets/CityX/Wearables/ThreeDGlasses.prefab");
            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 head = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).position;
            BuildingShapeCapture.Shoot(camera, folder, "glasses-front", head + new Vector3(0.0f, 0.2f, 5.0f), head);
            BuildingShapeCapture.Shoot(camera, folder, "glasses-close", head + new Vector3(0.6f, 0.3f, 2.2f), head);
            BuildingShapeCapture.Shoot(camera, folder, "glasses-threequarter", head + new Vector3(3.5f, 1.0f, 3.5f), head);
            BuildingShapeCapture.Shoot(camera, folder, "glasses-side", head + new Vector3(5.0f, 0.3f, 0.0f), head);
            BuildingShapeCapture.Shoot(camera, folder, "glasses-above-behind", head + new Vector3(-2.0f, 3.0f, -4.0f), head);
            Vector3 hips = player.transform.position + Vector3.up * 1.4f;
            BuildingShapeCapture.Shoot(camera, folder, "glasses-whole", hips + new Vector3(3.0f, 1.0f, 8.0f), hips);
            // The sun above the shots' backs leaves the front in shade (grey on the cel look): once with it before the face.
            Quaternion sunWas = sun.transform.rotation;
            sun.transform.rotation = Quaternion.Euler(35.0f, 200.0f, 0.0f);
            BuildingShapeCapture.Shoot(camera, folder, "glasses-front-lit", head + new Vector3(0.8f, 0.3f, 2.6f), head);
            sun.transform.rotation = sunWas;

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
                BuildingShapeCapture.Shoot(camera, folder, $"glasses-hair-{style.ToLowerInvariant()}", head + new Vector3(2.5f, 0.6f, 4.5f), head);
            }
            hair.SetStyle("none");

            Wear(player, "Assets/CityX/Wearables/FarmerHat.prefab");
            yield return null;
            BuildingShapeCapture.Shoot(camera, folder, "glasses-with-hat", head + new Vector3(2.5f, 0.8f, 4.5f), head);

            // A pair on its own with dark bars standing behind its lenses, to see them through the film.
            var loose = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/ThreeDGlasses.prefab"),
                new Vector3(40.0f, 3.0f, 40.0f), Quaternion.identity);
            var dark = new Material(CityX.Core.LitShader.Find());
            dark.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
            foreach (float across in new[] { -0.16f, -0.09f, 0.09f, 0.16f })
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.transform.position = loose.transform.position + new Vector3(across, 0.05f, 0.15f);
                bar.transform.localScale = new Vector3(0.025f, 0.35f, 0.02f);
                bar.GetComponent<Renderer>().sharedMaterial = dark;
            }
            yield return null;
            Vector3 lenses = loose.transform.position + new Vector3(0.0f, 0.05f, 0.37f);
            BuildingShapeCapture.Shoot(camera, folder, "glasses-see-through", lenses + new Vector3(0.0f, 0.05f, 1.8f), lenses);

            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static void Wear(GameObject player, string prefabPath)
        {
            Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity)
                .GetComponent<IEquippableItem>().OnEquip(player, ItemInstanceData.None);
        }
#endif
    }
}
