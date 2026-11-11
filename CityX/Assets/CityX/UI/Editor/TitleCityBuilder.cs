using CityX.UI;
using CityX.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.UIEditor
{
    /// <summary>
    /// The title's backdrop is the game's own city (owner, 2026-11-11: "make the city in the background look like the ingame cities that
    /// generate ingame. instead of the crappy title screen buildings, those were meant to be temporary"; "just make it run better on the title
    /// screen like theres no need for shatterable windows or stuff like that just the look of the ingame buildings"): the game's streamer and
    /// skyline, in the game's own materials, sky and grade, built round a real rooftop (TitleStage). None of the game's interactors (glass
    /// breaking, doors, light switches, the networked world) is added, and the city is full of rooms only close in.
    ///
    /// Everything is loaded from the city's own assets (CityMassingSceneBuilder makes them): rebuild the city scene first.
    /// </summary>
    public static class TitleCityBuilder
    {
        private const string MaterialFolder = "Assets/CityX/View/Materials";

        /// <summary>
        /// How far the city streams, chunks: to where the game's skyline of boxes takes over (it leaves out the chunks this near the viewer).
        /// The rooms and the fittings only this near: past it the buildings are their outsides, as the game's are further off.
        /// </summary>
        public const int RadiusChunks = SkylineStreamer.ExcludeRadiusChunks;

        public const int FullDetailRadiusChunks = 4;

        /// <summary>The game's clear-weather fog: clear to here, gone into the sky at the skyline's edge (CityMassingSceneBuilder.ClearFogStartMetres).</summary>
        private const float FogStartMetres = 600.0f;

        /// <summary>Builds the city, the skyline, the sky and the grade, and the stage that puts the camera and the rooftop on a real roof.</summary>
        public static TitleStage Build(Camera camera, DayNightCycle cycle, Transform rooftop)
        {
            var cityObject = new GameObject("City");
            CityStreamer city = cityObject.AddComponent<CityStreamer>();
            city.RadiusChunks = RadiusChunks;
            city.FullDetailRadiusChunks = FullDetailRadiusChunks;
            city.PlaceAtSpawn = false;
            city.GroundMaterial = Load("CityGround");
            city.PavementMaterial = Load("CityPavement");
            city.StructureMaterial = Load("CityConcrete");
            city.DarkGlassMaterial = Load("CityGlassDark");
            city.LitGlassMaterial = Load("CityGlassLit");
            city.CrackedGlassMaterial = Load("CityGlassCracked");
            city.CrackDecalMaterial = Load("BulletHoleDecal");
            city.RoomGlowMaterial = Load("RoomGlow");
            city.WindowLightMaterial = Load("WindowLight");
            city.DoorMaterial = Load("BuildingDoor");
            city.DoorHandleMaterial = Load("BuildingDoorHandle");
            city.RoadLineMaterial = Load("RoadLine");
            city.CrosswalkMaterial = Load("Crosswalk");
            city.GuardRailMaterial = Load("CityGuardRail");
            city.SignalLampMaterial = Load("SignalLamp");
            city.BenchMaterial = Load("CityBenchWood");
            city.BookMaterial = Load("CityBookStand");
            city.GrassMaterial = Load("CityGrass");
            city.BarkMaterial = city.BenchMaterial;
            city.Viewer = camera.transform;

            var skylineObject = new GameObject("Skyline");
            SkylineStreamer skyline = skylineObject.AddComponent<SkylineStreamer>();
            skyline.GroundMaterial = city.GroundMaterial;
            skyline.StructureMaterial = city.StructureMaterial;
            skyline.Viewer = camera.transform;

            // The game's sky, and its grade (with the night's over it, weighted by the dark: DayNightCycle.NightGrade).
            Material sky = Load("CityXSky");
            RenderSettings.skybox = sky;
            cycle.SkyDome = sky;
            camera.clearFlags = CameraClearFlags.Skybox;
            Grade("PostProcessing", "CityGrade", 0.0f, 1.0f);
            cycle.NightGrade = Grade("NightGrade", "CityNightGrade", 1.0f, 0.0f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = FogStartMetres;
            RenderSettings.fogEndDistance = SkylineStreamer.RadiusMetres;

            TitleStage stage = new GameObject("TitleStage").AddComponent<TitleStage>();
            stage.City = city;
            stage.Skyline = skyline;
            stage.View = camera;
            stage.Rooftop = rooftop;
            return stage;
        }

        private static Volume Grade(string name, string profile, float priority, float weight)
        {
            var volume = new GameObject(name).AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = weight;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{MaterialFolder}/{profile}.asset");
            if (volume.sharedProfile == null)
            {
                Debug.LogError($"CityX: no {profile} at {MaterialFolder} -- rebuild the city scene first.");
            }
            return volume;
        }

        private static Material Load(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
            if (material == null)
            {
                Debug.LogError($"CityX: no {name}.mat in {MaterialFolder} -- rebuild the city scene first.");
            }
            return material;
        }
    }
}
