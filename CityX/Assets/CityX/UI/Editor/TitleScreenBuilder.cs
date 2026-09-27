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
    /// Builds the title screen: a row of the game's own generated buildings at
    /// night, the logo hanging in the sky above them, rain over the top, and a
    /// menu.
    ///
    /// THE COMPOSITION IS THE GODOT TITLE'S -- a handful of towers of uneven
    /// height standing shoulder to shoulder, the logo over the tallest, rain
    /// falling across all of it, a fixed camera -- BUT THE BUILDINGS ARE THIS
    /// GAME'S. They are real buildings from the city generator (see
    /// TitleSkylineBaker), so the concrete facades, the real window openings
    /// with lit glass, the roof details and the night sky are exactly what the
    /// player will then see in the world. An earlier pass copied Godot's
    /// palette and hand-made towers to match a screenshot pixel for pixel;
    /// that was the wrong target -- what was wanted was Unity's own art style,
    /// not Godot's colours.
    ///
    /// Layers, bottom to top: the 3D scene (buildings, ground, sky, fog,
    /// moonlight, the logo quad) -> rain -> vignette -> the eyes -> prompt,
    /// menu and panels -> the lightning flash over everything.
    ///
    /// Run headlessly:
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
    ///             -executeMethod CityX.UIEditor.TitleScreenBuilder.Build
    /// </summary>
    public static class TitleScreenBuilder
    {
        private const string ScenePath = "Assets/CityX/Scenes/Title.unity";

        /// <summary>The rendering layer everything lights and renders on by default (bit 0).</summary>
        public const uint DefaultRenderingLayer = 1u << 0;

        /// <summary>The rendering layer of the title's city -- lit by the sun and moon and by nothing else.</summary>
        public const uint CityRenderingLayer = 1u << 1;
        private const string LogoPath = "Assets/CityX/UI/Textures/LogoCityX.png";

        /// <summary>Everything the title generates -- the baked row of buildings, the logo's material, the vignette -- lives here, so a rebuild rewrites one folder.</summary>
        private const string TitleAssets = "Assets/CityX/UI/Title";

        /// <summary>The one project-wide FishNet prefab collection -- see BuildNetworkRoot's own comment.</summary>
        private const string DefaultPrefabObjectsPath = "Assets/DefaultPrefabObjects.asset";

        /// <summary>
        /// Deep into the night, where the lit windows carry the picture. Not
        /// midnight exactly -- a little before keeps a trace of colour at the
        /// horizon rather than a flat black band behind the skyline.
        /// </summary>
        private static readonly float TimeOfDay =
            SkyCycle.DaySeconds + SkyCycle.NightSeconds * 0.34f;

        /// <summary>The logo's width as a fraction of the row's, and its shape (the art is 942x714).</summary>
        private const float LogoWidthFraction = 0.26f;
        private const float LogoAspect = 942.0f / 714.0f;

        /// <summary>Where the logo hangs on z: in FRONT of every building (negative), so it draws over the rooftops rather than under them.</summary>
        private const float LogoDepthBehind = -8.0f;

        /// <summary>Vertical field of view of the fixed camera.</summary>
        private const float FieldOfView = 42.0f;

        /// <summary>The vignette's edge darkness on screen (title_screen.tscn: 55% black).</summary>
        private const float VignetteStrength = 0.55f;

        /// <summary>
        /// This project blends UI in LINEAR space, where the same alpha of black
        /// darkens far less than in Godot's sRGB blending. Measured from two
        /// captures: 0.55 alpha darkened to 0.66 and 0.83 to 0.38 of the
        /// underlying colour, which fits (1 - a) ^ 1.92.
        /// </summary>
        private const float LinearBlendExponent = 1.92f;

        private static readonly Color Gold = new Color(0.75f, 0.6f, 0.2f, 1.0f);
        private static readonly Color Paper = new Color(0.82f, 0.82f, 0.84f, 1.0f);
        private static readonly Color FieldInk = new Color(0.09f, 0.10f, 0.13f, 0.88f);

        [MenuItem("CityX/Rebuild Title Screen")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/CityX/Scenes");
            Directory.CreateDirectory(TitleAssets);

            Material ground = LoadMaterial("CityGround");
            Material pavement = LoadMaterial("CityPavement");
            Material structure = LoadMaterial("CityConcrete");
            Material darkGlass = LoadMaterial("CityGlassDark");
            Material litGlass = LoadMaterial("CityGlassLit");
            if (structure == null || litGlass == null)
            {
                Debug.LogError("CityX: run CityX/Rebuild Massing Scene first -- the title screen " +
                               "uses the city's own materials.");
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The framing decides how wide the row has to be, so it is worked out first.
            Framing frame = Frame();
            TitleSkylineBaker.Baked row = TitleSkylineBaker.Bake(frame.HalfWidthToCover);
            Debug.Log($"CityX: title row -- {row.BuildingCount} buildings across {frame.HalfWidthToCover * 2.0f:0}m");

            BuildScene(row, frame, ground, pavement, structure, darkGlass, litGlass);


            NetworkBootstrap bootstrap = BuildNetworkRoot();
            BuildInterface(bootstrap);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"CityX: title screen written to {ScenePath}");
        }

        private static Material LoadMaterial(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"Assets/CityX/View/Materials/{name}.mat");
        }

        /// <summary>
        /// The 3D picture: the row of buildings, a ground, the night sky and
        /// moon (the game's own DayNightCycle, frozen), fog, the logo, and the
        /// fixed camera that frames it all.
        /// </summary>
        private struct Framing
        {
            public float Distance, LogoWidth, LogoHeight, LogoCentreY, AimY, CameraY, HalfWidthToCover;
        }

        /// <summary>
        /// Where the camera stands and how big the logo is, from the tallest tower
        /// and the fixed field of view alone -- so the row can then be made wide
        /// enough to run off BOTH edges of the screen, even a very wide one.
        /// </summary>
        private static Framing Frame()
        {
            float tallest = TitleSkylineBaker.TallestHeight;
            var f = new Framing();
            f.LogoHeight = tallest * 0.30f;
            f.LogoWidth = f.LogoHeight * LogoAspect;
            // The logo's foot dips behind the tallest roof, as in the Godot title.
            f.LogoCentreY = tallest + f.LogoHeight * 0.12f;
            float logoTop = f.LogoCentreY + f.LogoHeight * 0.5f;

            float tanHalfV = Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            float bottomY = tallest * 0.10f;
            float viewHeight = logoTop - bottomY;
            f.Distance = viewHeight * 0.5f / tanHalfV;
            f.AimY = (logoTop + bottomY) * 0.5f;
            f.CameraY = f.AimY + viewHeight * 0.06f;

            // 21:9, the widest screen worth covering, plus a margin.
            const float widestAspect = 21.0f / 9.0f;
            f.HalfWidthToCover = f.Distance * tanHalfV * widestAspect * 1.15f;
            return f;
        }

        private static void BuildScene(TitleSkylineBaker.Baked row, Framing frame, Material ground, Material pavement,
            Material structure, Material darkGlass, Material litGlass)
        {
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;

            var moonObject = new GameObject("Moon");
            Light moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.Soft;

            // THE CITY IS ON ITS OWN RENDERING LAYER, AND ONLY THE SUN AND MOON LIGHT IT. The
            // character list and the creator each stand a few directional lights around their
            // bodies -- and URP ignores a light's culling mask, so those lights lit the city
            // too, the moment the character list's camera was drawn over it (the buildings went
            // from dark night faces to washed-out grey). Light layers are how URP says "this
            // light is not for that": the city is on a layer only the sun and moon include, so
            // the stage lights (on the default layer) leave it alone.
            sun.renderingLayerMask = (int)(DefaultRenderingLayer | CityRenderingLayer);
            moon.renderingLayerMask = (int)(DefaultRenderingLayer | CityRenderingLayer);

            string meshPath = $"{TitleAssets}/TitleSkyline.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(row.Mesh, meshPath);

            var skyline = new GameObject("Skyline");
            skyline.AddComponent<MeshFilter>().sharedMesh = row.Mesh;
            MeshRenderer renderer = skyline.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { ground, pavement, structure, darkGlass, litGlass };
            renderer.renderingLayerMask = CityRenderingLayer;

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            Object.DestroyImmediate(plane.GetComponent<Collider>());
            plane.transform.position = new Vector3(0.0f, -0.05f, 0.0f);
            plane.transform.localScale = new Vector3(300.0f, 1.0f, 300.0f);
            plane.GetComponent<MeshRenderer>().sharedMaterial = ground;
            plane.GetComponent<MeshRenderer>().renderingLayerMask = CityRenderingLayer;

            float distance = frame.Distance;
            Vector3 cameraPosition = new Vector3(0.0f, frame.CameraY, -distance);

            var cameraObject = new GameObject("TitleCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 4000.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = FieldOfView;
            camera.allowMSAA = true;
            cameraObject.transform.position = cameraPosition;
            cameraObject.transform.rotation = Quaternion.LookRotation(
                new Vector3(0.0f, frame.AimY, 0.0f) - cameraPosition, Vector3.up);
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraObject.AddComponent<AudioListener>();

            // The logo: dead centre (x = 0, where the camera looks), in front of the towers.
            GameObject logo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            logo.name = "Logo";
            Object.DestroyImmediate(logo.GetComponent<Collider>());
            logo.transform.SetParent(skyline.transform, false);
            // Pulled toward the camera ALONG THE LINE OF SIGHT, so it lands on screen exactly
            // where it would at the buildings' depth, just drawn in front of them.
            Vector3 atBuildings = new Vector3(0.0f, frame.LogoCentreY, 0.0f);
            float scaleForDepth = (distance + LogoDepthBehind) / distance;
            logo.transform.position = cameraPosition + (atBuildings - cameraPosition) * scaleForDepth;
            logo.transform.localScale = new Vector3(
                frame.LogoWidth * scaleForDepth, frame.LogoHeight * scaleForDepth, 1.0f);
            var logoRenderer = logo.GetComponent<MeshRenderer>();
            logoRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            logoRenderer.sharedMaterial = MakeLogoMaterial();


            var live = skyline.AddComponent<TitleSkyline>();
            live.LogoRenderer = logoRenderer;

            // --- the night, from the game's own day/night cycle, held still.
            var cycleObject = new GameObject("DayNightCycle");
            DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
            cycle.Sun = sun;
            cycle.Moon = moon;
            cycle.Sky = camera;
            // NOT SkyDome -- this camera clears to a solid colour, not a skybox, so
            // a sky material would never actually be drawn here. LitWindows is the
            // real gap: without it nothing ever raises the glass's emission, so even
            // the "lit" windows sit flat and dark.
            cycle.LitWindows = litGlass;
            cycle.TimeOfDay = TimeOfDay;
            // FROZEN. A title screen that gets brighter while somebody reads the
            // menu is a title screen with two different looks depending on how
            // long you took to press a button.
            cycle.Running = false;
            cycle.Apply(SkyCycle.At(TimeOfDay));

            // Haze that thickens with distance, scaled to how far the camera stands
            // (the buildings are tens of metres tall, so this is a long way back).
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = distance * 0.9f;
            RenderSettings.fogEndDistance = distance * 3.2f;
        }

        /// <summary>The logo art on an unlit, alpha-blended material.</summary>
        private static Material MakeLogoMaterial()
        {
            string path = $"{TitleAssets}/TitleLogo.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            if (texture == null)
            {
                Debug.LogWarning($"CityX: no logo at {LogoPath}.");
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_Cull", 0.0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(material);
            return material;
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

            // NO MUSIC. title_screen.gd had a StarsPlayer (stars.mp3) looping under the
            // whole title flow; EXPLICIT ASK: "remove the title screen music also." The
            // rain sound below is ambience, not music, and stays. TitleScreen.MusicPlayer
            // is left null on purpose -- the settings panel's music slider guards for it.

            title.Skyline = Object.FindFirstObjectByType<TitleSkyline>();

            // title_screen.tscn's own RainPlayer: rain_noises.mp3, looping from
            // the moment the screen loads. (StarsPlayer above is the music.)
            var rainSoundObject = new GameObject("RainSound");
            rainSoundObject.transform.SetParent(root.transform, false);
            AudioSource rainSound = rainSoundObject.AddComponent<AudioSource>();
            rainSound.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/CityX/UI/Audio/rain_noises.mp3");
            rainSound.loop = true;
            rainSound.playOnAwake = true;
            rainSound.spatialBlend = 0.0f;
            if (rainSound.clip == null)
            {
                Debug.LogWarning("CityX: no rain clip at Assets/CityX/UI/Audio/rain_noises.mp3.");
            }

            // Rain, then the vignette over it -- the Godot scene's own order.
            var rain = new GameObject("RainLayer", typeof(RectTransform));
            rain.transform.SetParent(root.transform, false);
            Stretch((RectTransform)rain.transform, 0.0f);
            TitleRainLayer rainLayer = rain.AddComponent<TitleRainLayer>();
            rainLayer.raycastTarget = false;

            // The rain's sound follows the panel: full on the menu, quiet and muffled (rain heard
            // through a wall) on the character list, gone -- with the streaks -- on the creator.
            AudioLowPassFilter rainMuffle = rainSoundObject.AddComponent<AudioLowPassFilter>();
            rainMuffle.cutoffFrequency = TitleAmbience.OutdoorsCutoffHz;
            TitleAmbience ambience = rainSoundObject.AddComponent<TitleAmbience>();
            ambience.Sound = rainSound;
            ambience.Filter = rainMuffle;
            ambience.Rain = rainLayer;
            title.Ambience = ambience;

            var vignette = new GameObject("Vignette", typeof(RectTransform));
            vignette.transform.SetParent(root.transform, false);
            Stretch((RectTransform)vignette.transform, 0.0f);
            Image vignetteImage = vignette.AddComponent<Image>();
            vignetteImage.sprite = EnsureVignetteSprite();
            vignetteImage.raycastTarget = false;

            // The logo itself is a quad in the skyline scene (behind the rooftop
            // tank), so this group holds nothing to draw -- it is the switch the
            // panels and the tests flip to say "the logo is showing".
            var logoGroup = new GameObject("LogoGroup", typeof(RectTransform));
            Stretch((RectTransform)logoGroup.transform, 0.0f);
            logoGroup.transform.SetParent(root.transform, false);
            title.LogoGroup = logoGroup;

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

        /// <summary>
        /// title_screen.tscn's vignette: a radial gradient, clear out to 45% of
        /// the way to the edge then darkening to 55% black, stretched over the
        /// whole screen -- so the corners fall away and the middle is left alone.
        /// </summary>
        private static Sprite EnsureVignetteSprite()
        {
            string path = $"{TitleAssets}/TitleVignette.png";
            const int size = 256;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 0 at the centre, 1 at the middle of an edge, ~1.41 in a corner.
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy) / 0.5f;
                    float wanted = radius <= 0.45f
                        ? 0.0f
                        : Mathf.Lerp(0.0f, VignetteStrength, Mathf.Clamp01((radius - 0.45f) / 0.55f));

                    // Godot blended this in sRGB space; this project blends UI in linear
                    // space, where the same alpha of black darkens far less. This is the
                    // alpha that gives, in linear blending, the result 0.55 gave there.
                    float alpha = 1.0f - Mathf.Pow(1.0f - wanted, LinearBlendExponent);
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(alpha * 255.0f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path),
                texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
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
            // Bottom-centre, as in title_screen.tscn, so the row of buildings above
            // it stays in view rather than being sat on by three buttons.
            rect.anchorMin = new Vector2(0.5f, 0.0f);
            rect.anchorMax = new Vector2(0.5f, 0.0f);
            rect.pivot = new Vector2(0.5f, 0.0f);
            rect.anchoredPosition = new Vector2(0.0f, 30.0f);
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
                new Vector2(0.0f, 178.0f), new Vector2(440.0f, 40.0f));

            MakeLabel(panel.transform, "MasterLabel", "MASTER VOLUME", 17, Paper,
                new Vector2(0.0f, 124.0f), new Vector2(440.0f, 30.0f));
            view.MasterSlider = MakeSlider(panel.transform, "MasterSlider", new Vector2(0.0f, 98.0f));

            MakeLabel(panel.transform, "FullscreenLabel", "FULLSCREEN", 17, Paper,
                new Vector2(-110.0f, 50.0f), new Vector2(240.0f, 30.0f));
            view.FullscreenToggle = MakeToggle(panel.transform, "FullscreenToggle", new Vector2(120.0f, 50.0f));

            MakeLabel(panel.transform, "ResolutionLabel", "RESOLUTION", 17, Paper,
                new Vector2(0.0f, 0.0f), new Vector2(440.0f, 30.0f));
            view.ResolutionPrevButton = MakeArrowButton(panel.transform, "ResolutionPrev", "<",
                new Vector2(-140.0f, -30.0f));
            WireArrowButton(view.ResolutionPrevButton, view, ButtonAction.Kind.SettingsResolutionPrevious);
            view.ResolutionLabel = MakeLabel(panel.transform, "ResolutionValue", "1920x1080", 17, Paper,
                new Vector2(0.0f, -30.0f), new Vector2(200.0f, 30.0f));
            view.ResolutionNextButton = MakeArrowButton(panel.transform, "ResolutionNext", ">",
                new Vector2(140.0f, -30.0f));
            WireArrowButton(view.ResolutionNextButton, view, ButtonAction.Kind.SettingsResolutionNext);

            // The frame-rate cap: a single-line stepper like the device rows below, in the room made by moving
            // Heading..Resolution up (the panel had empty space above its heading).
            MakeLabel(panel.transform, "FpsLabel", "MAX FPS", 15, Paper,
                new Vector2(-140.0f, -74.0f), new Vector2(180.0f, 22.0f));
            view.FpsPrevButton = MakeArrowButton(panel.transform, "FpsPrev", "<",
                new Vector2(30.0f, -74.0f));
            WireArrowButton(view.FpsPrevButton, view, ButtonAction.Kind.SettingsFpsPrevious);
            view.FpsLabel = MakeLabel(panel.transform, "FpsValue", "UNLIMITED", 12, Paper,
                new Vector2(105.0f, -74.0f), new Vector2(90.0f, 22.0f));
            view.FpsNextButton = MakeArrowButton(panel.transform, "FpsNext", ">",
                new Vector2(180.0f, -74.0f));
            WireArrowButton(view.FpsNextButton, view, ButtonAction.Kind.SettingsFpsNext);

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
