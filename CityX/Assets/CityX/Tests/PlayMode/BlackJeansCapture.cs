using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: the player in the Black Jeans and a T-shirt, standing (front, side, behind, three-quarters, the waist and the hems close up)
    /// and mid-walk and mid-sprint, then without the shirt, so the fit, the texture and the skinning can be looked at. Written to
    /// Logs/CityShots/blackjeans-*.png.
    /// Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BlackJeansCapture
    {
        [UnityTest]
        public IEnumerator RenderThePlayerInTheBlackJeans()
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
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            IEquippableItem shirt = null;
            foreach (string path in new[] { "Assets/CityX/Wearables/BlackJeans.prefab", "Assets/CityX/Wearables/TShirt.prefab" })
            {
                GameObject worn = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path), new Vector3(30.0f, 0.5f, 30.0f),
                    Quaternion.identity);
                var item = worn.GetComponent<IEquippableItem>();
                item.OnEquip(player, ItemInstanceData.None);
                shirt = item;   // the last one put on
            }

            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            Vector3 hips = player.transform.position + Vector3.up * 0.75f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-front", hips + new Vector3(0.0f, 0.3f, 7.0f), hips);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-side", hips + new Vector3(7.0f, 0.3f, 0.0f), hips);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-behind", hips + new Vector3(0.0f, 0.5f, -7.0f), hips);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-threequarter", hips + new Vector3(5.0f, 1.5f, 5.0f), hips);
            Vector3 waist = player.transform.position + Vector3.up * 0.95f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-waist", waist + new Vector3(1.0f, 0.4f, 2.6f), waist);
            Vector3 ankles = player.transform.position + Vector3.up * 0.15f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-hems", ankles + new Vector3(1.2f, 0.6f, 2.6f), ankles);

            // Into a sprint from standing: about half a second in the legs are at their widest apart (the crotch stretches most).
            for (int i = 0; i < 33; i++)
            {
                locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = true }, 1.0f / 60.0f);
                yield return null;
            }
            hips = player.transform.position + Vector3.up * 0.7f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-stride-crotch-front",
                hips + player.transform.forward * 2.4f + Vector3.down * 0.2f, hips);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-stride-crotch-behind",
                hips - player.transform.forward * 2.4f + Vector3.down * 0.2f, hips);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-stride-side",
                hips + player.transform.right * 3.0f, hips);

            // The walk once; the sprint at four moments of a stride (the knees and the crotch move most there).
            foreach ((string name, bool sprint, int frames) in new[] { ("walk", false, 50), ("sprint1", true, 60), ("sprint2", true, 6),
                ("sprint3", true, 6), ("sprint4", true, 6) })
            {
                for (int i = 0; i < frames; i++)
                {
                    locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = sprint }, 1.0f / 60.0f);
                    yield return null;
                }
                hips = player.transform.position + Vector3.up * 0.75f;
                Vector3 side = player.transform.right;
                Vector3 ahead = player.transform.forward;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"blackjeans-{name}-side", hips + side * 7.0f + Vector3.up * 0.3f, hips);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"blackjeans-{name}-front", hips + ahead * 7.0f + Vector3.up * 0.3f, hips);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"blackjeans-{name}-behind", hips - ahead * 7.0f + Vector3.up * 0.6f, hips);
            }

            // The jeans alone, standing: the waistband drawn in under where a shirt's hem hangs.
            shirt.OnUnequip(player);
            for (int i = 0; i < 60; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            waist = player.transform.position + Vector3.up * 0.95f;
            Vector3 front = player.transform.forward;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-alone-waist", waist + front * 2.6f + Vector3.up * 0.4f, waist);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "blackjeans-alone-back", waist - front * 2.6f + Vector3.up * 0.4f, waist);

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
