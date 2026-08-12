using System.Collections.Generic;
using System.IO;
using CityX.UI;
using CityX.View;
using CityX.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the title screen: the real city at night, drifting, with the logo
    /// over it and a menu.
    ///
    /// THE BACKDROP IS THE ACTUAL GAME WORLD. The Godot original hand-listed a
    /// skyline just for this screen, because generating its city was expensive.
    /// This one is arithmetic and streams itself, so the menu can fly over the
    /// real thing -- which means the title screen can never drift out of sync
    /// with how the game looks, and there is no second skyline to maintain.
    ///
    /// Run headlessly:
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
    ///             -executeMethod CityX.UIEditor.TitleScreenBuilder.Build
    /// </summary>
    public static class TitleScreenBuilder
    {
        private const string ScenePath = "Assets/CityX/Scenes/Title.unity";
        private const string LogoPath = "Assets/CityX/UI/Textures/LogoCityX.png";

        private const int WorldSeed = 20260810;

        /// <summary>
        /// Deep into the night, where the lit windows carry the picture. Not
        /// midnight exactly -- a little before keeps a trace of colour at the
        /// horizon rather than a flat black band behind the skyline.
        /// </summary>
        private static readonly float TimeOfDay =
            SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.34f;

        private static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);

        [MenuItem("CityX/Rebuild Title Screen")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/CityX/Scenes");

            Material ground = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityGround.mat");
            Material pavement = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityPavement.mat");
            Material structure = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityConcrete.mat");
            if (structure == null)
            {
                Debug.LogError("CityX: run CityX/Rebuild Massing Scene first -- the title screen " +
                               "uses the city's own materials.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;

            var moonObject = new GameObject("Moon");
            Light moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.Soft;

            // --- the camera, high enough to see over the near roofs and looking
            // --- slightly down a canyon rather than at a single wall.
            var cameraObject = new GameObject("TitleCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 2000.0f;
            camera.nearClipPlane = 0.3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = 34.0f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<TitleBackdrop>();

            var terrain = new TerrainField(WorldSeed);
            float chunk = CityLayout.ChunkSize;
            float groundHeight = terrain.PadHeightFor(0, 0);
            cameraObject.transform.position = new Vector3(
                chunk * 0.5f, groundHeight + 46.0f, -chunk * 5.0f);
            cameraObject.transform.rotation = Quaternion.Euler(6.0f, 12.0f, 0.0f);

            var cycleObject = new GameObject("DayNightCycle");
            DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
            cycle.Sun = sun;
            cycle.Moon = moon;
            cycle.Sky = camera;
            cycle.Facades = structure;
            cycle.TimeOfDay = TimeOfDay;
            // FROZEN. A title screen that gets brighter while somebody reads the
            // menu is a title screen with two different looks depending on how
            // long you took to press a button.
            cycle.Running = false;
            cycle.Apply(SkyCycle.At(TimeOfDay));

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90.0f;
            RenderSettings.fogEndDistance = 420.0f;

            var streamerObject = new GameObject("CityStreamer");
            CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
            streamer.WorldSeed = WorldSeed;
            streamer.RadiusChunks = 16;
            streamer.GroundMaterial = ground;
            streamer.PavementMaterial = pavement;
            streamer.StructureMaterial = structure;
            streamer.Viewer = cameraObject.transform;

            BuildInterface();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: title screen written to {ScenePath}");
        }

        /// <summary>
        /// Puts the title and the world into the build's scene list, title
        /// first.
        ///
        /// NOT COSMETIC. SceneManager.LoadScene can only reach scenes that are
        /// in this list, so without it the PLAY button on the character select
        /// throws and reads as the button being broken. The project was still
        /// carrying Unity's default SampleScene entry, which does not exist
        /// here -- so the list was wrong in both directions.
        ///
        /// Index 0 is what a built game starts on, which has to be the title.
        /// </summary>
        private static void EnsureBuildScenes()
        {
            string[] wanted =
            {
                ScenePath,
                "Assets/CityX/Scenes/CityMassing.unity",
            };

            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in wanted)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    Debug.LogWarning($"CityX: no scene at {path} -- it will not be buildable " +
                                     "or loadable until it is built.");
                    continue;
                }
                scenes.Add(new EditorBuildSettingsScene(path, enabled: true));
            }

            // Anything else somebody added stays, after ours, so this does not
            // quietly delete a scene that was put there on purpose.
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                bool alreadyWanted = System.Array.IndexOf(wanted, existing.path) >= 0;
                bool missing = AssetDatabase.LoadAssetAtPath<SceneAsset>(existing.path) == null;
                if (!alreadyWanted && !missing)
                {
                    scenes.Add(existing);
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void BuildInterface()
        {
            var root = new GameObject("TitleInterface", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1152.0f, 648.0f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            TitleScreen title = root.AddComponent<TitleScreen>();

            // A dark wash over the city so the menu stays readable against
            // whatever the camera happens to be flying past.
            var vignette = MakeRect("Vignette", root.transform, new Color(0.0f, 0.0f, 0.0f, 0.35f));
            Stretch((RectTransform)vignette.transform, 0.0f);
            vignette.GetComponent<Image>().raycastTarget = false;

            // Grouped so the character list can hide the logo without hiding
            // the vignette under it.
            var logoGroup = new GameObject("LogoGroup", typeof(RectTransform));
            Stretch((RectTransform)logoGroup.transform, 0.0f);
            logoGroup.transform.SetParent(root.transform, false);
            title.LogoGroup = logoGroup;

            title.Logo = BuildLogo(logoGroup.transform);
            title.MenuPanel = BuildMenu(root.transform, title);
            title.CharacterSelectPanel = CharacterSelectBuilder.Build(root.transform, title);

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            if (playerPrefab != null)
            {
                title.CreatorPanel = CharacterCreatorBuilder.Build(
                    root.transform, title, playerPrefab);
            }
            else
            {
                Debug.LogWarning("CityX: no player prefab -- the creator will not be built. " +
                                 "Run CityX/Rebuild Player Rig first.");
            }

            title.MultiplayerPanel = BuildStub(root.transform, "MultiplayerPanel",
                "MULTIPLAYER", "Host and join are not ported yet.", title);
            title.SettingsPanel = BuildStub(root.transform, "SettingsPanel",
                "SETTINGS", "Display and audio options are not ported yet.", title);

            title.Show(TitleScreen.Panel.Menu);
            EnsureEventSystem();
        }

        private static Image BuildLogo(Transform parent)
        {
            // A PNG dropped into the project imports as a plain Texture, and a
            // UI Image can only take a Sprite -- so the logo renders as nothing
            // until the importer is told. Doing it here rather than by clicking
            // means copying in a new logo and rebuilding just works.
            var importer = AssetImporter.GetAtPath(LogoPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);

            var logoObject = new GameObject("Logo", typeof(RectTransform));
            var rect = (RectTransform)logoObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1.0f);
            rect.anchorMax = new Vector2(0.5f, 1.0f);
            rect.pivot = new Vector2(0.5f, 1.0f);
            rect.anchoredPosition = new Vector2(0.0f, -46.0f);

            Image image = logoObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;

            if (sprite != null)
            {
                image.sprite = sprite;
                // Sized from the art's own aspect, so a redrawn logo does not
                // need this number changed too.
                float aspect = (float)sprite.rect.width / sprite.rect.height;
                rect.sizeDelta = new Vector2(520.0f, 520.0f / Mathf.Max(aspect, 0.01f));
            }
            else
            {
                Debug.LogWarning($"CityX: no logo sprite at {LogoPath} -- is its texture type " +
                                 "set to Sprite? The title will show a plain block.");
                rect.sizeDelta = new Vector2(520.0f, 200.0f);
            }

            return image;
        }

        private static GameObject BuildMenu(Transform parent, TitleScreen title)
        {
            var panel = new GameObject("MenuPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0.0f, -70.0f);
            rect.sizeDelta = new Vector2(280.0f, 190.0f);

            MakeButton(panel.transform, "SingleplayerButton", "SINGLEPLAYER", 0,
                () => title.Show(TitleScreen.Panel.CharacterSelect));
            MakeButton(panel.transform, "MultiplayerButton", "MULTIPLAYER", 1,
                () => title.Show(TitleScreen.Panel.Multiplayer));
            MakeButton(panel.transform, "SettingsButton", "SETTINGS", 2,
                () => title.Show(TitleScreen.Panel.Settings));

            return panel;
        }

        /// <summary>
        /// A panel that exists, is reachable, and says what it will be.
        ///
        /// A button that does nothing is worse than one that admits it: the
        /// first reads as broken, the second as unfinished. The panel structure
        /// and the back navigation are real, so filling these in later is
        /// adding contents rather than rebuilding the screen.
        /// </summary>
        private static GameObject BuildStub(Transform parent, string name, string heading,
            string body, TitleScreen title)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0.0f, -70.0f);
            rect.sizeDelta = new Vector2(460.0f, 190.0f);

            MakeLabel(panel.transform, "Heading", heading, 30, Gold,
                new Vector2(0.0f, 60.0f), new Vector2(440.0f, 40.0f));
            MakeLabel(panel.transform, "Body", body, 17, new Color(0.82f, 0.82f, 0.84f),
                new Vector2(0.0f, 14.0f), new Vector2(440.0f, 46.0f));

            MakeButton(panel.transform, "BackButton", "BACK", 2, title.Back);
            return panel;
        }

        private static void MakeButton(Transform parent, string name, string label, int row,
            UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0.0f, 46.0f - row * 58.0f);
            rect.sizeDelta = new Vector2(260.0f, 46.0f);

            Image background = buttonObject.AddComponent<Image>();
            background.color = new Color(0.09f, 0.10f, 0.13f, 0.88f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(onClick);

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = Gold;
            button.colors = colours;

            MakeLabel(buttonObject.transform, "Label", label, 20, Gold,
                Vector2.zero, new Vector2(250.0f, 40.0f));
        }

        private static Text MakeLabel(Transform parent, string name, string content, int size,
            Color colour, Vector2 position, Vector2 dimensions)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            Text text = labelObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private static GameObject MakeRect(string name, Transform parent, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = colour;
            return go;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }
            var events = new GameObject("EventSystem");
            events.AddComponent<UnityEngine.EventSystems.EventSystem>();
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }
    }
}
