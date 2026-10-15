using System.Collections;
using System.IO;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: World Gacha phase 3 in the real city -- a home's window canopies, and a shopping street with shutters down and its band of shop
    /// signs by day and at night (the lit ones glow) -- to Logs/CityShots/street-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class StreetDetailCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        [UnityTest]
        public IEnumerator RenderTheStreetDetail()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            var streamer = Object.FindFirstObjectByType<CityStreamer>();
            var world = new CityWorld(streamer.WorldSeed);
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            BuildingShapeCapture.UseTheScenesAir();

            var cameraObject = new GameObject("StreetCam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 55.0f;
            camera.farClipPlane = 1500.0f;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.renderShadows = true;

            // A home with canopies, standing free, its west wall whole; and a shopfront at street level with shutters down, facing the sun (so
            // its signs' colours show).
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            Vector3 toSun = RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : Vector3.up;
            BuildingSite home = default, shop = default;
            bool foundHome = false, foundShop = false;
            // The shop: with power (its signs glow at night) and a shutter down, at street level, wide; best facing the sun. Each pass asks less.
            for (int pass = 0; pass < 3 && !foundShop || pass == 0; pass++)
            {
                foreach (BuildingSite site in TestSites.NearSpawn(world, 50))
                {
                    if (!site.Exists || site.Params.IsSpecial || site.OnRoof || site.Sides >= 3 || site.Contacts != null && site.Contacts.Length > 0)
                    {
                        continue;
                    }
                    WindowPlan plan = WindowPlan.For(world, site);
                    FacadeOrnament o = FacadeOrnament.For(site, plan.Style, BuildingLook.For(site, plan.Style).Wall);
                    int rows = WindowGrid.RowsFor(site.Params.Height).Count;
                    if (!foundHome && o.HasCanopies && rows >= 3 && rows <= 8 && !plan.IsBlankWall(WindowGrid.Wall.North) && !ApartmentFront.For(site, plan).Any)
                    {
                        home = site;
                        foundHome = true;
                    }
                    bool shutter = false;
                    for (int c = 1; c < plan.ColumnsFor(site.Params.Width).Count - 1 && !shutter; c++)
                    {
                        shutter = Shopfronts.Shuttered(site, plan, c);
                    }
                    bool sunny = Vector3.Dot(CityMeshBuilder.WallNormal(site, WindowGrid.Wall.North), new Vector3(toSun.x, 0.0f, toSun.z).normalized) > 0.4f;
                    if (!foundShop && plan.Storefront && PowerGrid.IsOn(site) && !ApartmentFront.For(site, plan).Walkway && (shutter || pass >= 2)
                        && !BuildingDecor.EntranceStyleFor(site.BaseHeight - site.DoorGroundHeight).NeedsStairs && site.Params.Width > 12.0f && (sunny || pass >= 1))
                    {
                        shop = site;
                        foundShop = true;
                    }
                    if (foundHome && foundShop)
                    {
                        break;
                    }
                }
            }

            foreach (bool night in new[] { false, true })
            {
                if (cycle != null)
                {
                    cycle.Running = false;
                    cycle.TimeOfDay = night ? SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.4f : SkyCycle.DaySeconds * 0.42f;
                    cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
                    DynamicGI.UpdateEnvironment();
                }
                string when = night ? "night" : "day";
                if (foundHome && !night)
                {
                    Vector3 front = CityMeshBuilder.WallNormal(home, WindowGrid.Wall.North);
                    Vector3 side = Vector3.Cross(Vector3.up, front);
                    Vector3 face = TestSites.At(home, home.CentreX, home.BaseHeight, home.MaxZ);
                    streamer.Rebuild();
                    streamer.StreamAround(face, int.MaxValue);
                    yield return null;
                    yield return null;
                    float storey = WindowGrid.RowsFor(home.Params.Height).Size;
                    BuildingShapeCapture.Shoot(camera, directory, "street-canopies", face + front * 9.0f + side * 4.0f + Vector3.up * (storey * 1.7f),
                        face + Vector3.up * (storey * 1.6f) - side * 1.0f);
                    BuildingShapeCapture.Shoot(camera, directory, "street-canopies-wide", face + front * 18.0f + side * 9.0f + Vector3.up * 4.0f,
                        face + Vector3.up * (home.Params.Height * 0.4f));
                    Debug.Log($"CityX street capture: canopies = building {home.Id} {home.Zone} {home.Params.Width:F0} m");
                }
                if (foundShop)
                {
                    Vector3 front = CityMeshBuilder.WallNormal(shop, WindowGrid.Wall.North);
                    Vector3 side = Vector3.Cross(Vector3.up, front);
                    Vector3 face = TestSites.At(shop, shop.CentreX, shop.BaseHeight, shop.MaxZ);
                    streamer.Rebuild();
                    streamer.StreamAround(face, int.MaxValue);
                    yield return null;
                    yield return null;
                    BuildingShapeCapture.Shoot(camera, directory, $"street-shops-{when}", face + front * 9.0f + side * 7.0f + Vector3.up * 2.0f,
                        face + Vector3.up * 2.4f - side * 2.0f);
                    if (!night)
                    {
                        // Close on a shuttered bay: the slats, the recess, the box it rolls into, and the signs over it.
                        WindowPlan plan = WindowPlan.For(world, shop);
                        CellLayout columns = plan.ColumnsFor(shop.Params.Width);
                        int bay = 1;
                        while (bay < columns.Count - 2 && !Shopfronts.Shuttered(shop, plan, bay)) bay++;
                        Vector3 at = TestSites.At(shop, shop.MinX + (bay + 0.5f) * columns.Size, shop.BaseHeight, shop.MaxZ);
                        BuildingShapeCapture.Shoot(camera, directory, "street-shutter-close", at + front * 4.0f + side * 1.5f + Vector3.up * 1.8f,
                            at + Vector3.up * 1.9f);
                    }
                    Debug.Log($"CityX street capture: shops = building {shop.Id} {shop.Zone} {shop.Params.Width:F0} m");
                }
            }
            Object.Destroy(cameraObject);
            Assert.IsTrue(foundHome && foundShop, $"found a home with canopies {foundHome}, a shuttered shop {foundShop}");
#else
            yield break;
#endif
        }
    }
}
