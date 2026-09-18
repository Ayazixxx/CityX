using System.Collections.Generic;
using System.IO;
using CityX.Core;
using CityX.Net;
using CityX.UI;
using CityX.View;
using CityX.World;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Builds the title screen: the real city at night, held still, with the
    /// logo over it and a menu.
    ///
    /// THE BACKDROP IS THE ACTUAL GAME WORLD. The Godot original hand-listed a
    /// small fixed skyline just for this screen, because generating its city
    /// was expensive -- but its own camera never moved either way. This one is
    /// arithmetic and streams itself, so the menu can look out over the real
    /// thing instead of a second, separately-maintained skyline -- but
    /// EXPLICIT ASK, after an earlier pass added a slow drifting/turning/
    /// bobbing camera here: "recreate the godot title screen with the still
    /// city." A moving establishing shot was this project's own addition, not
    /// something Godot's title screen ever had; the camera is now placed once,
    /// at build time, and never touched again, matching the original's actual
    /// stillness while keeping the real-world-not-a-duplicate architecture.
    ///
    /// Run headlessly:
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
    ///             -executeMethod CityX.UIEditor.TitleScreenBuilder.Build
    /// </summary>
    public static class TitleScreenBuilder
    {
        private const string ScenePath = "Assets/CityX/Scenes/Title.unity";
        private const string LogoPath = "Assets/CityX/UI/Textures/LogoCityX.png";

        /// <summary>The one project-wide FishNet prefab collection -- see BuildNetworkRoot's own comment.</summary>
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

        // Must match CityMassingSceneBuilder.WorldSeed exactly -- this is
        // the SAME real, played game world, just viewed from the title
        // screen's own drifting camera instead of the player's. EXPLICIT
        // ASK: "generate a new world every time you make a change to the
        // city generation" -- change this alongside that constant (and
        // CityMassingCapture's own copy), never alone.
        private const int WorldSeed = 29481073;

        /// <summary>
        /// Deep into the night, where the lit windows carry the picture. Not
        /// midnight exactly -- a little before keeps a trace of colour at the
        /// horizon rather than a flat black band behind the skyline.
        /// </summary>
        private static readonly float TimeOfDay =
            SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.34f;

        private static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);
        private static readonly Color Paper = new Color(0.82f, 0.82f, 0.84f, 1.0f);
        private static readonly Color FieldInk = new Color(0.09f, 0.10f, 0.13f, 0.88f);

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
            // NEVER LOADED HERE BEFORE -- CityStreamer rendered every window
            // submesh with a null material slot, which Unity shows as its
            // bright magenta "missing material" fallback rather than
            // anything resembling glass. Ground/pavement/structure were
            // wired below; these three were simply forgotten alongside them.
            Material darkGlass = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityGlassDark.mat");
            Material litGlass = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityGlassLit.mat");
            Material crackedGlass = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/CityGlassCracked.mat");

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
            camera.allowMSAA = true;
            // Same reasoning as CityMassingSceneBuilder's own player camera --
            // this is the very first thing anyone sees, so it gets the same
            // SMAA pass rather than relying on MSAA alone.
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraObject.AddComponent<AudioListener>();

            // A RAINY CITY AT NIGHT -- EXPLICIT ASK: "recreate the godot
            // title screen instead of the one you have now." title_screen.gd
            // played rain_noises.mp3 the instant the screen loaded and never
            // stopped it until Singleplayer was actually pressed; the real
            // in-game weather system (RainEffect, normally toggled by
            // /weather rain/clear) is reused here rather than rebuilding a
            // second rain effect from scratch, hanging off this still
            // camera in place of a player. Grade/
            // WetGroundMaterials are deliberately left unset: those are the
            // SAME shared material assets the real playable city uses, and
            // mutating them here (RainEffect wets/darkens them in place)
            // would leave the game world looking rain-wetted the moment a
            // player left this screen, before any real in-game weather ever
            // ran -- Sun alone is enough for the rain itself plus its own
            // lightning (RainEffect's sun-intensity flash, automatic while
            // active, needs nothing further built here).
            var rainObject = new GameObject("Rain");
            rainObject.transform.SetParent(cameraObject.transform, false);
            RainEffect rain = rainObject.AddComponent<RainEffect>();
            rain.RainSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/CityX/UI/Audio/rain_noises.mp3");
            rain.DropMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/Raindrop.mat");
            rain.SplashMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/CityX/View/Materials/Splash.mat");
            rain.Sun = sun;
            if (rain.DropMaterial == null || rain.SplashMaterial == null)
            {
                Debug.LogWarning("CityX: rain materials missing -- run CityX/Rebuild Massing Scene " +
                                 "first so Raindrop.mat/Splash.mat exist.");
            }
            rain.SetActive(true);

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
            // NOT SkyDome -- this camera clears to a solid colour, not a
            // skybox, so a sky material would never actually be drawn here.
            // LitWindows is the real gap: without it nothing ever raises the
            // glass's emission, so even the "lit" windows sit flat and dark
            // -- exactly wrong for a screen whose whole point is the lit
            // windows carrying the picture (see TimeOfDay's own comment).
            cycle.LitWindows = litGlass;
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
            streamer.DarkGlassMaterial = darkGlass;
            streamer.LitGlassMaterial = litGlass;
            streamer.CrackedGlassMaterial = crackedGlass;
            streamer.Viewer = cameraObject.transform;

            NetworkBootstrap bootstrap = BuildNetworkRoot();
            BuildInterface(bootstrap);

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

        /// <summary>
        /// NetworkManager + Transport + NetworkBootstrap, for the Multiplayer
        /// panel's own Host/Join buttons to drive. AutoStartOnPlay is OFF --
        /// EXPLICIT ASK: "get the multiplayer part of the menu working" --
        /// a menu decides when to connect, not Start() on its own.
        ///
        /// DOES NOT CALL DontDestroyOnLoad HERE -- and just as importantly,
        /// NEITHER DOES THE NetworkManager COMPONENT ITSELF (see the
        /// _dontDestroyOnLoad override just below). Tried "don't call it
        /// ourselves" alone first (moving a PersistAcrossSceneLoad-style
        /// call into TitleMultiplayerView.Host()/Join(), gated on an actual
        /// button press) and it did NOT fix the ~30 unrelated PlayMode test
        /// failures (NetworkBootstrapPlayTests, SpikeWorldPlayTests,
        /// InterestManagementPlayTests, and every real-scene test that loads
        /// CityMassing.unity) -- because FishNet's own NetworkManager.Awake()
        /// calls DontDestroyOnLoad(this) UNCONDITIONALLY whenever its own
        /// private _dontDestroyOnLoad field is true, which is its default,
        /// completely independent of anything this project's own code does.
        /// So the Title screen's NetworkRoot was persisting itself the
        /// instant ANY test merely OPENED Title.unity (most of
        /// TitleFlowPlayTests does, just to click around the menu) --
        /// nobody had to press Host or Join at all. Unity's PlayMode Test
        /// Runner does NOT reset the DontDestroyOnLoad scene between
        /// separate test methods within one batch run, so that leftover
        /// NetworkManager survived into every LATER test in the same run,
        /// and FishNet's own NetworkManager persistence (_persistence
        /// defaults to DestroyNewest) then silently destroyed each of those
        /// tests' own, completely unrelated NetworkManager on the spot.
        /// The real fix is here: force this NetworkManager's own
        /// _dontDestroyOnLoad off, so it behaves like an ordinary scene
        /// object (destroyed with Title.unity, same as every other test
        /// fixture) unless TitleMultiplayerView.Host()/Join() explicitly
        /// calls DontDestroyOnLoad on it themselves, right before actually
        /// starting a connection -- the one thing no pre-existing test ever
        /// does, so ordinary navigation through the title screen (including
        /// every existing test that does exactly that) never triggers it.
        /// </summary>
        private static NetworkBootstrap BuildNetworkRoot()
        {
            var networkObject = new GameObject("NetworkRoot");
            NetworkManager manager = networkObject.AddComponent<NetworkManager>();
            networkObject.AddComponent<Tugboat>();

            {
                var so = new SerializedObject(manager);
                SerializedProperty dontDestroy = so.FindProperty("_dontDestroyOnLoad");
                if (dontDestroy != null)
                {
                    dontDestroy.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Same collection CityX.Spike's own generated NetworkManager
            // prefab points at -- there is exactly one of these per project,
            // and FishNet's own tooling keeps it populated with every
            // NetworkObject prefab it finds (the Player prefab, once
            // PlayerRigBuilder gives it one).
            PrefabObjects defaultPrefabs = AssetDatabase.LoadAssetAtPath<PrefabObjects>(DefaultPrefabObjectsPath);
            if (defaultPrefabs != null)
            {
                var so = new SerializedObject(manager);
                SerializedProperty prefabs = so.FindProperty("_spawnablePrefabs");
                if (prefabs != null)
                {
                    prefabs.objectReferenceValue = defaultPrefabs;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else
            {
                Debug.LogWarning($"CityX: no prefab collection at {DefaultPrefabObjectsPath} -- " +
                                  "networked player spawns will fail until one exists.");
            }

            NetworkBootstrap bootstrap = networkObject.AddComponent<NetworkBootstrap>();
            bootstrap.AutoStartOnPlay = false;
            return bootstrap;
        }

        private static void BuildInterface(NetworkBootstrap bootstrap)
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

            // title_screen.gd's own StarsPlayer -- background music that
            // starts the instant the title screen loads and just keeps
            // playing underneath Menu/CharacterSelect/Creator exactly the
            // way that node does (see its own class comment: "the
            // persistent background music... keep playing regardless").
            // Nothing here has to stop or restart it across those panels --
            // Show() only ever toggles the UI panels, never touches this,
            // so playOnAwake alone is enough to cover the whole title flow.
            // It stops the moment CharacterSelect.Play() loads the world
            // scene -- that unloads this whole scene outright, which is a
            // hard cut rather than title_screen.gd's own tweened
            // fade_out_stars(): that fade is only possible there because
            // Godot's own title screen node survives the transition and can
            // still be tweened afterwards, and re-architecting scene
            // loading to keep this scene alive across that boundary is a
            // bigger change than "add the music" asked for.
            var musicObject = new GameObject("MusicPlayer");
            musicObject.transform.SetParent(root.transform, false);
            AudioSource music = musicObject.AddComponent<AudioSource>();
            music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/CityX/UI/Audio/stars.mp3");
            music.loop = true;
            music.playOnAwake = true;
            if (music.clip == null)
            {
                Debug.LogWarning("CityX: no title music clip at Assets/CityX/UI/Audio/stars.mp3.");
            }
            title.MusicPlayer = music;

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
            title.PromptLabel = BuildPromptLabel(root.transform);
            title.MenuPanel = BuildMenu(root.transform, title);

            // Loaded once, up here, because both the select carousel and the
            // creator need their own clone of the same body -- CityX/Rebuild
            // Player Rig has to have run first for either to have anything
            // to show.
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Debug.LogWarning("CityX: no player prefab -- the select carousel and the " +
                                 "creator will not be built. Run CityX/Rebuild Player Rig first.");
            }

            title.CharacterSelectPanel = CharacterSelectBuilder.Build(root.transform, title, playerPrefab);

            if (playerPrefab != null)
            {
                title.CreatorPanel = CharacterCreatorBuilder.Build(
                    root.transform, title, playerPrefab);
            }

            title.MultiplayerPanel = BuildMultiplayerPanel(root.transform, title, bootstrap);
            title.SettingsPanel = BuildSettings(root.transform, title);

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

        /// <summary>
        /// title_screen.gd's own PromptLabel: "PRESS ANY KEY TO START",
        /// bottom-centre, pulsing (TitleScreen.TickPrompt drives the alpha).
        /// A soft blue Shadow behind the text matches the Godot original's
        /// own theme_override_colors/font_shadow_color -- legacy UI.Text has
        /// no shadow colour of its own, so it needs a real Shadow component
        /// rather than a Text property.
        /// </summary>
        private static Text BuildPromptLabel(Transform parent)
        {
            var labelObject = new GameObject("PromptLabel", typeof(RectTransform));
            var rect = (RectTransform)labelObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.0f);
            rect.anchorMax = new Vector2(0.5f, 0.0f);
            rect.pivot = new Vector2(0.5f, 0.0f);
            rect.anchoredPosition = new Vector2(0.0f, 40.0f);
            rect.sizeDelta = new Vector2(400.0f, 40.0f);

            Text text = labelObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.color = new Color(0.812f, 0.839f, 1.0f, 0.15f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = "PRESS ANY KEY TO START";

            Shadow shadow = labelObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.55f, 0.63f, 1.0f, 0.6f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            return text;
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
                title, ButtonAction.Kind.ShowCharacterSelect);
            MakeButton(panel.transform, "MultiplayerButton", "MULTIPLAYER", 1,
                title, ButtonAction.Kind.ShowMultiplayer);
            MakeButton(panel.transform, "SettingsButton", "SETTINGS", 2,
                title, ButtonAction.Kind.ShowSettings);

            return panel;
        }

        /// <summary>
        /// Host/Join, direct-IP -- ported from title_screen.gd's own
        /// identical panel (see that file's own comments on why a blank
        /// port field means NetworkBootstrap's default, why the two fields
        /// are separate ports rather than one shared one, and why a pasted
        /// "1.2.3.4:port" address is accepted directly). EXPLICIT ASK: "get
        /// the multiplayer part of the menu working" -- this replaces the
        /// former placeholder ("Host and join are not ported yet.").
        /// </summary>
        private static GameObject BuildMultiplayerPanel(Transform parent, TitleScreen title, NetworkBootstrap bootstrap)
        {
            var panel = new GameObject("MultiplayerPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0.0f, -30.0f);
            rect.sizeDelta = new Vector2(460.0f, 340.0f);

            TitleMultiplayerView view = panel.AddComponent<TitleMultiplayerView>();
            view.Title = title;
            view.Bootstrap = bootstrap;

            MakeLabel(panel.transform, "Heading", "MULTIPLAYER", 30, Gold,
                new Vector2(0.0f, 138.0f), new Vector2(440.0f, 40.0f));

            MakeLabel(panel.transform, "HostLabel", "HOST A GAME", 17, Paper,
                new Vector2(0.0f, 88.0f), new Vector2(440.0f, 26.0f));
            MakeLabel(panel.transform, "HostPortLabel", "PORT", 14, Paper,
                new Vector2(-150.0f, 48.0f), new Vector2(80.0f, 26.0f));
            view.HostPortField = MakeInputField(panel.transform, "HostPortField",
                new Vector2(-50.0f, 48.0f), new Vector2(110.0f, 36.0f), string.Empty,
                LaunchArgs.DefaultPort.ToString());
            MakeMultiplayerButton(panel.transform, "HostButton", "HOST",
                new Vector2(100.0f, 48.0f), new Vector2(140.0f, 40.0f), view, ButtonAction.Kind.MultiplayerHost);

            MakeLabel(panel.transform, "JoinLabel", "JOIN A GAME", 17, Paper,
                new Vector2(0.0f, -8.0f), new Vector2(440.0f, 26.0f));
            view.JoinAddressField = MakeInputField(panel.transform, "JoinAddressField",
                new Vector2(-70.0f, -48.0f), new Vector2(230.0f, 36.0f), string.Empty, "Address");
            view.JoinPortField = MakeInputField(panel.transform, "JoinPortField",
                new Vector2(115.0f, -48.0f), new Vector2(100.0f, 36.0f), string.Empty, "Port");
            MakeMultiplayerButton(panel.transform, "JoinButton", "JOIN",
                new Vector2(0.0f, -96.0f), new Vector2(160.0f, 40.0f), view, ButtonAction.Kind.MultiplayerJoin);

            view.StatusLabel = MakeLabel(panel.transform, "StatusLabel", string.Empty, 15,
                new Color(1.0f, 0.55f, 0.55f), new Vector2(0.0f, -136.0f), new Vector2(440.0f, 46.0f));

            MakeMultiplayerButton(panel.transform, "BackButton", "BACK",
                new Vector2(0.0f, -162.0f), new Vector2(160.0f, 40.0f), view, ButtonAction.Kind.MultiplayerBack);

            return panel;
        }

        /// <summary>
        /// A single-line text field -- background, placeholder, live text,
        /// tied together by an InputField -- matching CharacterSelectBuilder's
        /// own MakeNameField exactly, just parameterised by position/size/
        /// placeholder instead of hardcoded for a character name.
        /// </summary>
        private static InputField MakeInputField(Transform parent, string name,
            Vector2 position, Vector2 size, string initialText, string placeholderText)
        {
            var fieldObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)fieldObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = fieldObject.AddComponent<Image>();
            background.color = FieldInk;

            Text text = MakeLabel(fieldObject.transform, "Text", initialText, 16, Paper,
                Vector2.zero, size - new Vector2(16.0f, 6.0f));

            Text placeholder = MakeLabel(fieldObject.transform, "Placeholder", placeholderText, 16,
                new Color(0.45f, 0.45f, 0.48f), Vector2.zero, size - new Vector2(16.0f, 6.0f));
            placeholder.fontStyle = FontStyle.Italic;

            InputField field = fieldObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        /// <summary>
        /// MakeButton's own styling at an explicit position/size instead of
        /// its fixed row formula -- Host/Join have to sit beside a field
        /// rather than centred in their own row, and carry a
        /// TitleMultiplayerView instead of a TitleScreen.
        /// </summary>
        private static void MakeMultiplayerButton(Transform parent, string name, string label,
            Vector2 position, Vector2 size, TitleMultiplayerView view, ButtonAction.Kind action)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = buttonObject.AddComponent<Image>();
            background.color = FieldInk;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;

            var wiring = buttonObject.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Multiplayer = view;

            ColorBlock colours = button.colors;
            colours.highlightedColor = new Color(0.22f, 0.20f, 0.12f, 1.0f);
            colours.pressedColor = Gold;
            button.colors = colours;

            MakeLabel(buttonObject.transform, "Label", label, 18, Gold, Vector2.zero, size);
        }

        /// <summary>
        /// Master volume, fullscreen and a resolution stepper -- ported from
        /// ui/settings_panel.gd, minus the voice chat device pickers (see
        /// TitleSettingsView's own doc comment for why). All the load/save/
        /// apply logic lives on TitleSettingsView itself, reusing the exact
        /// GameSettings/DisplaySettings layer the in-game pause menu already
        /// does -- this method only builds the hierarchy and hands the
        /// component its control references, same division as every other
        /// panel in this file.
        /// </summary>
        private static GameObject BuildSettings(Transform parent, TitleScreen title)
        {
            var panel = new GameObject("SettingsPanel", typeof(RectTransform));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            // Grown from (0,-70)/460x340 to fit the five new Voice Chat rows
            // below Resolution -- Heading/Master/Fullscreen/Resolution keep
            // their own original positions relative to the panel's centre
            // unchanged. Confirmed by rendering CityX/Capture Settings
            // Screenshot: this screen's own 1152x648 reference resolution
            // gives only +-324 design units of real vertical room (the
            // capture is a real 1280x720 render at the same aspect ratio,
            // so what does not fit there does not fit on a real screen
            // either), which the first version of this panel's five new
            // rows -- two-line device pickers, a full "VOICE CHAT" section
            // heading, an unshortened Push-to-Talk label -- ran well past.
            // Output/Input Device and Push-to-Talk are single-line rows
            // here (label left, control right, matching Fullscreen's own
            // shape) rather than label-above-stepper like Resolution, and
            // the panel's own centre moved up slightly (-30 instead of -70)
            // to balance the extra rows underneath.
            rect.anchoredPosition = new Vector2(0.0f, -15.0f);
            rect.sizeDelta = new Vector2(460.0f, 640.0f);

            TitleSettingsView view = panel.AddComponent<TitleSettingsView>();
            view.MusicPlayer = title.MusicPlayer;

            MakeLabel(panel.transform, "Heading", "SETTINGS", 30, Gold,
                new Vector2(0.0f, 138.0f), new Vector2(440.0f, 40.0f));

            MakeLabel(panel.transform, "MasterLabel", "MASTER VOLUME", 17, Paper,
                new Vector2(0.0f, 84.0f), new Vector2(440.0f, 30.0f));
            view.MasterSlider = MakeSlider(panel.transform, "MasterSlider", new Vector2(0.0f, 58.0f));

            MakeLabel(panel.transform, "FullscreenLabel", "FULLSCREEN", 17, Paper,
                new Vector2(-110.0f, 10.0f), new Vector2(240.0f, 30.0f));
            view.FullscreenToggle = MakeToggle(panel.transform, "FullscreenToggle", new Vector2(120.0f, 10.0f));

            MakeLabel(panel.transform, "ResolutionLabel", "RESOLUTION", 17, Paper,
                new Vector2(0.0f, -40.0f), new Vector2(440.0f, 30.0f));
            view.ResolutionPrevButton = MakeArrowButton(panel.transform, "ResolutionPrev", "<",
                new Vector2(-140.0f, -70.0f));
            WireArrowButton(view.ResolutionPrevButton, view, ButtonAction.Kind.SettingsResolutionPrevious);
            view.ResolutionLabel = MakeLabel(panel.transform, "ResolutionValue", "1920x1080", 17, Paper,
                new Vector2(0.0f, -70.0f), new Vector2(200.0f, 30.0f));
            view.ResolutionNextButton = MakeArrowButton(panel.transform, "ResolutionNext", ">",
                new Vector2(140.0f, -70.0f));
            WireArrowButton(view.ResolutionNextButton, view, ButtonAction.Kind.SettingsResolutionNext);

            MakeLabel(panel.transform, "MusicLabel", "MUSIC VOLUME", 15, Paper,
                new Vector2(0.0f, -108.0f), new Vector2(440.0f, 24.0f));
            view.MusicSlider = MakeSlider(panel.transform, "MusicSlider", new Vector2(0.0f, -132.0f));

            MakeLabel(panel.transform, "OutputDeviceLabel", "OUTPUT DEVICE", 12, Paper,
                new Vector2(-140.0f, -168.0f), new Vector2(180.0f, 22.0f));
            view.OutputDevicePrevButton = MakeArrowButton(panel.transform, "OutputDevicePrev", "<",
                new Vector2(30.0f, -168.0f));
            WireArrowButton(view.OutputDevicePrevButton, view, ButtonAction.Kind.SettingsOutputDevicePrevious);
            view.OutputDeviceLabel = MakeLabel(panel.transform, "OutputDeviceValue", "Default", 12, Paper,
                new Vector2(105.0f, -168.0f), new Vector2(90.0f, 22.0f));
            view.OutputDeviceNextButton = MakeArrowButton(panel.transform, "OutputDeviceNext", ">",
                new Vector2(180.0f, -168.0f));
            WireArrowButton(view.OutputDeviceNextButton, view, ButtonAction.Kind.SettingsOutputDeviceNext);

            // 40 below Output Device, not 36 -- MakeArrowButton is 36 tall,
            // so a 36-unit gap between two of them (18+18 half-heights)
            // touches with zero clearance, confirmed as a visibly merged
            // seam once PauseMenuBuilder's own copy of this same layout was
            // rendered and checked pixel-by-pixel.
            MakeLabel(panel.transform, "InputDeviceLabel", "INPUT DEVICE", 12, Paper,
                new Vector2(-140.0f, -208.0f), new Vector2(180.0f, 22.0f));
            view.InputDevicePrevButton = MakeArrowButton(panel.transform, "InputDevicePrev", "<",
                new Vector2(30.0f, -208.0f));
            WireArrowButton(view.InputDevicePrevButton, view, ButtonAction.Kind.SettingsInputDevicePrevious);
            view.InputDeviceLabel = MakeLabel(panel.transform, "InputDeviceValue", "Default", 12, Paper,
                new Vector2(105.0f, -208.0f), new Vector2(90.0f, 22.0f));
            view.InputDeviceNextButton = MakeArrowButton(panel.transform, "InputDeviceNext", ">",
                new Vector2(180.0f, -208.0f));
            WireArrowButton(view.InputDeviceNextButton, view, ButtonAction.Kind.SettingsInputDeviceNext);

            MakeLabel(panel.transform, "PushToTalkLabel", "PUSH TO TALK (HOLD V)", 13, Paper,
                new Vector2(-90.0f, -244.0f), new Vector2(280.0f, 22.0f));
            view.PushToTalkToggle = MakeToggle(panel.transform, "PushToTalkToggle", new Vector2(140.0f, -244.0f));

            MakeButton(panel.transform, "BackButton", "BACK", 6, title, ButtonAction.Kind.TitleBack);

            return panel;
        }

        /// <summary>Hand-built rather than from DefaultControls, matching PauseMenuBuilder's own copy of this.</summary>
        private static Slider MakeSlider(Transform parent, string name, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(280.0f, 16.0f);

            Image background = go.AddComponent<Image>();
            background.color = FieldInk;

            var fillArea = new GameObject("Fill", typeof(RectTransform));
            var fillRect = (RectTransform)fillArea.transform;
            fillRect.SetParent(go.transform, false);
            Stretch(fillRect, 0.0f);
            Image fill = fillArea.AddComponent<Image>();
            fill.color = Gold;

            var handle = new GameObject("Handle", typeof(RectTransform));
            var handleRect = (RectTransform)handle.transform;
            handleRect.SetParent(go.transform, false);
            handleRect.sizeDelta = new Vector2(10.0f, 22.0f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Paper;

            Slider slider = go.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0.0f;
            slider.maxValue = 1.0f;
            slider.wholeNumbers = false;
            slider.value = 1.0f;
            return slider;
        }

        /// <summary>A checkbox: background, a checkmark shown only while on, and the Toggle tying them together.</summary>
        private static Toggle MakeToggle(Transform parent, string name, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(28.0f, 28.0f);

            Image background = go.AddComponent<Image>();
            background.color = FieldInk;

            var checkmark = new GameObject("Checkmark", typeof(RectTransform));
            var checkRect = (RectTransform)checkmark.transform;
            checkRect.SetParent(go.transform, false);
            checkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkRect.offsetMin = checkRect.offsetMax = Vector2.zero;
            Image checkImage = checkmark.AddComponent<Image>();
            checkImage.color = Gold;

            Toggle toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkImage;
            toggle.isOn = true;
            return toggle;
        }

        private static Button MakeArrowButton(Transform parent, string name, string glyph, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(36.0f, 36.0f);

            Image background = go.AddComponent<Image>();
            background.color = FieldInk;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;

            MakeLabel(go.transform, "Glyph", glyph, 18, Paper, Vector2.zero, new Vector2(36.0f, 36.0f));
            return button;
        }

        /// <summary>
        /// A COMPONENT, NOT A LISTENER -- same reasoning as MakeButton's own
        /// copy of this note. MakeArrowButton only builds the visual, so
        /// the resolution stepper's two buttons get wired here instead.
        /// </summary>
        private static void WireArrowButton(Button button, TitleSettingsView settings, ButtonAction.Kind action)
        {
            var wiring = button.gameObject.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Settings = settings;
        }

        private static void MakeButton(Transform parent, string name, string label, int row,
            TitleScreen title, ButtonAction.Kind action)
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

            // A COMPONENT, NOT A LISTENER. See ButtonAction: a listener added
            // here does not survive the scene being saved, so every button on
            // this screen was dead in play mode.
            var wiring = buttonObject.AddComponent<ButtonAction>();
            wiring.Action = action;
            wiring.Title = title;

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
