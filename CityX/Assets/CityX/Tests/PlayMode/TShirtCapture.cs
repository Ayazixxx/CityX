using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player in the T-Shirt, plain and then wearing the test design drawn on its template (SourceArt/TShirt/tshirt_test_design.png:
    /// a dark 10 cm grid and a red disc on the chest and the back), standing and mid-sprint, so the fit and where a design lands can be looked
    /// at. Written to Logs/CityShots/tshirt-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class TShirtCapture
    {
        private const string TestDesignPath = "../SourceArt/TShirt/tshirt_test_design.png";

        [UnityTest]
        public IEnumerator RenderThePlayerInTheTShirt()
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
            var design = new Texture2D(2, 2);
            var conversion = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            conversion.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) }).Invoke(null,
                new object[] { design, System.IO.File.ReadAllBytes(System.IO.Path.Combine(Application.dataPath, TestDesignPath)) });

            foreach (string look in new[] { "plain", "design", "bloodraven", "redcamo", "black", "longsleeve", "longsleeveblack" })
            {
                GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                    new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                string prefab = look == "bloodraven" ? "Assets/CityX/Wearables/BloodRavenTShirt.prefab"
                    : look == "redcamo" ? "Assets/CityX/Wearables/RedCamoTShirt.prefab"
                    : look == "black" ? "Assets/CityX/Wearables/BlackTShirt.prefab"
                    : look == "longsleeve" ? "Assets/CityX/Wearables/LongSleeveTShirt.prefab"
                    : look == "longsleeveblack" ? "Assets/CityX/Wearables/BlackLongSleeveTShirt.prefab" : "Assets/CityX/Wearables/TShirt.prefab";
                GameObject shirtObject = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefab),
                    new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
                var shirt = shirtObject.GetComponent<TShirt>();
                if (look == "design")
                {
                    shirt.GarmentMaterial = new Material(shirt.GarmentMaterial);
                    shirt.GarmentMaterial.SetTexture("_BaseMap", design);
                    shirt.GarmentMaterial.SetColor("_BaseColor", Color.white);
                    shirt.GroundPreview.GetComponent<MeshRenderer>().sharedMaterial = shirt.GarmentMaterial;
                }
                // Folded on the ground first, as it lies before it is picked up; then held up to see its underside (the worn back).
                shirtObject.transform.position = new Vector3(-3.0f, 0.0f, 3.0f);
                yield return null;
                Vector3 folded = shirtObject.transform.position + Vector3.up * 0.02f;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-folded", folded + new Vector3(0.0f, 0.75f, -0.35f), folded);
                shirtObject.transform.position = new Vector3(-3.0f, 2.0f, 3.0f);
                yield return null;
                folded = shirtObject.transform.position;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-folded-under", folded + new Vector3(0.0f, -0.75f, -0.35f), folded);
                shirt.OnEquip(player, ItemInstanceData.None);
                for (int i = 0; i < 90; i++)
                {
                    locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                }
                Vector3 chest = player.transform.position + Vector3.up * 1.5f;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-front", chest + new Vector3(0.0f, 0.3f, 7.0f), chest);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-chest", chest + new Vector3(0.0f, 0.2f, 3.2f), chest + Vector3.up * 0.1f);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-side", chest + new Vector3(7.0f, 0.3f, 0.0f), chest);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-behind", chest + new Vector3(0.0f, 0.6f, -7.0f), chest);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-threequarter", chest + new Vector3(5.0f, 1.2f, 5.0f), chest);
                Vector3 sleeve = player.transform.position + new Vector3(0.45f, 1.6f, 0.0f);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-sleeve", sleeve + new Vector3(2.2f, 0.3f, 1.6f), sleeve);
                Vector3 neck = player.transform.position + new Vector3(0.0f, 1.95f, 0.0f);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-collar", neck + new Vector3(0.6f, 0.15f, 2.2f), neck);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-collar-behind", neck + new Vector3(0.6f, 1.2f, -2.0f), neck);
                Vector3 armhole = player.transform.position + new Vector3(0.35f, 1.65f, 0.0f);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-armhole-behind", armhole + new Vector3(0.9f, 0.2f, -2.0f), armhole);
                for (int i = 0; i < 60; i++)
                {
                    locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = true }, 1.0f / 60.0f);
                    yield return null;
                }
                chest = player.transform.position + Vector3.up * 1.5f;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-sprint-side",
                    chest + player.transform.right * 7.0f + Vector3.up * 0.3f, chest);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"tshirt-{look}-sprint-front",
                    chest + player.transform.forward * 7.0f + Vector3.up * 0.3f, chest);
                Object.Destroy(player);
                Object.Destroy(shirtObject);
                yield return null;
            }

            Object.Destroy(design);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }
    }
}
