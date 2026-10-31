using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the tutorial house (docs/OLD_GUYS_HOUSE.md) to Logs/CityShots/house-*.png -- outside from the street, the back and the
    /// side, the boarded front door, and inside: the garage, the living room, the hall, the kitchen, the den, the stairs, the grandson's room,
    /// the study, the swordsman in the bedroom; the looting; then broken in: the door open, a window smashed. Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.OldGuysHouseCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class OldGuysHouseCapture
    {
        [UnityTest]
        public IEnumerator RenderTheHouse()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            var made = new List<GameObject>();
            DroppedItems.Shared.Clear();
            CityX.World.TutorialHouseDamage.Shared.Clear();
            DroppedItems.Shared.PrefabFor = id => OldGuysHousePlayTests.PrefabPaths.TryGetValue(id, out string path) ? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
            Time.captureFramerate = 60;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50.0f, -40.0f, 0.0f);
            made.Add(sun.gameObject);
            CityX.View.OldGuysHouseSite site = OldGuysHousePlayTests.MakeHouse(made, true, true);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.74f, 0.88f);
            made.Add(camera.gameObject);
            for (int i = 0; i < 30; i++) yield return null;

            float l1 = CityX.World.OldGuysHouse.LevelY(1), l2 = CityX.World.OldGuysHouse.LevelY(2);
            camera.fieldOfView = 45.0f;
            yield return Shoot(camera, "house-street", new Vector3(-18, 12, -30), new Vector3(14, 7, 8));
            yield return Shoot(camera, "house-back", new Vector3(38, 15, 44), new Vector3(14, 7, 11));
            yield return Shoot(camera, "house-side", new Vector3(46, 8, 2), new Vector3(28, 5, 6));
            camera.fieldOfView = 60.0f;
            yield return Shoot(camera, "house-front-door", new Vector3(32.6f, l1 + 2.6f, 9.7f), new Vector3(28.0f, l1 + 1.8f, 9.7f));
            camera.fieldOfView = 70.0f;
            yield return Shoot(camera, "house-garage", new Vector3(10.2f, 3.4f, 1.0f), new Vector3(3.5f, 1.0f, 12.0f));
            yield return Shoot(camera, "house-living", new Vector3(10.4f, l1 + 3.0f, 11.4f), new Vector3(2.0f, l1 + 1.5f, 4.0f));
            yield return Shoot(camera, "house-living-from-gallery", new Vector3(9.6f, l2 + 2.4f, 10.8f), new Vector3(1.0f, l1 + 0.5f, 5.0f));
            yield return Shoot(camera, "house-hall", new Vector3(12.6f, l1 + 2.8f, 9.7f), new Vector3(28.0f, l1 + 1.6f, 9.7f));
            yield return Shoot(camera, "house-kitchen", new Vector3(8.6f, l1 + 2.8f, 13.0f), new Vector3(0.8f, l1 + 1.4f, 18.4f));
            yield return Shoot(camera, "house-den", new Vector3(19.4f, l1 + 2.6f, 12.8f), new Vector3(25.0f, l1 + 0.8f, 19.5f));
            yield return Shoot(camera, "house-stairs", new Vector3(16.4f, l1 + 2.8f, 10.8f), new Vector3(12.5f, 2.0f, 2.0f));
            yield return Shoot(camera, "house-grandson", new Vector3(9.6f, l2 + 2.6f, 13.0f), new Vector3(2.6f, l2 + 1.0f, 21.0f));
            yield return Shoot(camera, "house-study", new Vector3(18.0f, l2 + 2.6f, 6.8f), new Vector3(26.4f, l2 + 1.0f, 3.6f));
            yield return Shoot(camera, "house-bedroom", new Vector3(18.0f, l2 + 2.6f, 12.8f), new Vector3(25.5f, l2 + 0.8f, 17.0f));

            // Looting (docs/LOOTING.md): the kitchen's and the bedroom's drawers and doors opened, then the loot window over the wardrobe. The
            // swordsman goes first: left in, he walks up and fights whoever is looking into the wardrobe.
            Object.Destroy(site.Swordsman);
            foreach (LootContainer container in LootContainer.All)
            {
                CityX.World.OldGuysHouse.Room room = CityX.World.OldGuysHouse.RoomAt(Mathf.FloorToInt((container.transform.position.y + 0.1f) / CityX.World.OldGuysHouse.Storey),
                    container.transform.position.x, container.transform.position.z);
                if (room != null && (room.Name == "Kitchen" || room.Name == "Old Guy's bedroom"))
                {
                    container.Open();
                }
            }
            for (int i = 0; i < 40; i++) yield return null;
            yield return Shoot(camera, "house-loot-kitchen", new Vector3(6.4f, l1 + 2.8f, 15.6f), new Vector3(0.8f, l1 + 1.0f, 16.4f));
            yield return Shoot(camera, "house-loot-bedroom", new Vector3(22.0f, l2 + 2.6f, 15.0f), new Vector3(17.6f, l2 + 1.2f, 18.0f));
            LootContainer wardrobe = null;
            foreach (LootContainer container in LootContainer.All) if (container.Items.Contains("t_shirt")) wardrobe = container;
            var player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), new Vector3(19.2f, l2 + 0.05f, 18.0f), Quaternion.Euler(0, 270, 0));
            made.Add(player);
            if (player.GetComponent<Inventory>() == null) player.AddComponent<Inventory>();
            var window = new GameObject("LootWindow").AddComponent<CityX.UI.LootWindowView>();
            made.Add(window.gameObject);
            for (int i = 0; i < 3; i++) yield return null;
            player.GetComponent<LootSearcher>().Search(wardrobe);
            for (int i = 0; i < 40; i++) yield return null;
            Canvas windowCanvas = window.GetComponentInChildren<Canvas>(true);
            windowCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            windowCanvas.worldCamera = camera;
            windowCanvas.planeDistance = 0.5f;
            camera.fieldOfView = 60.0f;
            yield return Shoot(camera, "house-loot-window", new Vector3(21.8f, l2 + 3.0f, 18.4f), new Vector3(17.8f, l2 + 1.4f, 18.0f));
            player.GetComponent<LootSearcher>().CloseWindow();

            // Broken in (docs/OLD_GUYS_HOUSE.md): the front door's planks torn off and the door open, a front window of the living room smashed.
            for (int i = 0; i < CityX.World.OldGuysHouse.FrontDoorPlanks; i++) CityX.World.TutorialHouseDamage.Shared.Break(i);
            CityX.View.HousePane smashed = site.Built.GetComponentsInChildren<CityX.View.HousePane>()[0];
            foreach (CityX.View.HousePane pane in site.Built.GetComponentsInChildren<CityX.View.HousePane>())
            {
                if (pane.transform.localPosition.z < 0.1f && pane.transform.localPosition.x < 11.0f && pane.transform.localPosition.y < l2) smashed = pane;
            }
            smashed.TakeBlow(smashed.transform.position, Vector3.forward, CityX.World.BlowStrength.Heavy);
            for (int i = 0; i < 60; i++) yield return null;
            yield return Shoot(camera, "house-front-door-open", new Vector3(32.6f, l1 + 2.6f, 9.7f), new Vector3(26.0f, l1 + 1.8f, 9.7f));
            yield return Shoot(camera, "house-window-smashed", new Vector3(5.5f, l1 + 2.6f, -6.0f), smashed.transform.position);

            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject go in made) if (go != null) Object.Destroy(go);
            foreach (CityX.View.BoardedPlank plank in Object.FindObjectsByType<CityX.View.BoardedPlank>(FindObjectsSortMode.None)) Object.Destroy(plank.gameObject);
            CityX.World.TutorialHouseDamage.Shared.Clear();
            DroppedItems.Shared.Clear();
            DroppedItems.Shared.PrefabFor = Inventory.FindPrefab;
#else
            yield break;
#endif
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
