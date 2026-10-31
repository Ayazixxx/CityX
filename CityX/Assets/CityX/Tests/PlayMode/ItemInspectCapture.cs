using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the prompt under the crosshair for a knife on a crate, the camera half-way through its glide, and the open
    /// appraisal card (docs/ITEM_INSPECT.md) to Logs/CityShots/inspect-*.png. Run with -Graphics and look at the pictures.
    /// </summary>
    public sealed class ItemInspectCapture
    {
        [UnityTest]
        public IEnumerator RenderThePromptAndTheCard()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            var made = new List<GameObject>();
            DroppedItems.Shared.Clear();
            DroppedItems.Shared.PrefabFor = Inventory.FindPrefab;
            Time.captureFramerate = 60;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);
            made.Add(sun.gameObject);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            ground.GetComponent<Renderer>().material = new Material(CityX.Core.LitShader.Find()) { color = new Color(0.55f, 0.56f, 0.58f) };
            made.Add(ground);
            GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.transform.position = new Vector3(0.0f, 0.5f, 2.2f);
            crate.transform.localScale = new Vector3(1.4f, 1.0f, 1.0f);
            crate.GetComponent<Renderer>().material = new Material(CityX.Core.LitShader.Find()) { color = new Color(0.62f, 0.44f, 0.27f) };
            made.Add(crate);

            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            made.Add(player);
            var inventory = player.AddComponent<Inventory>();
            inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.KitchenKnife, Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/KitchenKnife.prefab") },
            };
            var cameraObject = new GameObject("Camera") { tag = "MainCamera" };
            made.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.74f, 0.88f);
            cameraObject.AddComponent<ThirdPersonCamera>().Target = player.transform;
            player.GetComponent<PlayerInputReader>().YawSource = cameraObject.transform;
            var prompt = new GameObject("ItemPrompt").AddComponent<ItemPromptView>();
            made.Add(prompt.gameObject);
            var card = new GameObject("ItemCard").AddComponent<ItemCardView>();
            made.Add(card.gameObject);
            DroppedItem knife = DroppedItems.Shared.Drop(new ItemDrop
            {
                ItemId = ItemRegistry.KitchenKnife,
                Data = ItemInstanceData.None,
                Position = new Vector3(0.0f, 1.08f, 2.2f),
                Rotation = Quaternion.Euler(0.0f, 60.0f, 0.0f),
                Placed = true,
            }, false);
            for (int i = 0; i < 40; i++) yield return null;

            // The prompt is a screen overlay, which a camera rendered to a picture never sees: draw it through the camera for the shot.
            Canvas promptCanvas = prompt.GetComponentInChildren<Canvas>(true);
            promptCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            promptCanvas.worldCamera = camera;
            promptCanvas.planeDistance = 0.5f;
            yield return null;
            Shoot(camera, "inspect-prompt");

            ItemInspector inspector = player.GetComponent<ItemInspector>();
            inspector.Open(knife);
            for (int i = 0; i < 15; i++) yield return null;
            Shoot(camera, "inspect-glide");
            for (int i = 0; i < 40; i++) yield return null;
            Shoot(camera, "inspect-card");

            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            DroppedItems.Shared.Clear();
            foreach (GameObject go in made) if (go != null) Object.Destroy(go);
#else
            yield break;
#endif
        }

        /// <summary>The camera's own view, as the glide has placed it.</summary>
        private static void Shoot(Camera camera, string name)
        {
            Transform at = camera.transform;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, at.position, at.position + at.forward * 10.0f);
        }
    }
}
