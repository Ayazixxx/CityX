using System.IO;
using CityX.Character;
using CityX.View;
using CityX.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CityX.ViewEditor
{
    /// <summary>
    /// Builds the massing scene from scratch, including its materials.
    ///
    /// WHY THIS IS A SCRIPT AND NOT A HAND-AUTHORED SCENE. A .unity file is a
    /// large opaque YAML blob that cannot be reviewed in a diff and cannot be
    /// regenerated if it breaks. Everything here is a dozen lines of readable
    /// setup instead, and re-running it repairs the scene rather than requiring
    /// somebody to remember what was in it.
    ///
    /// Runs headlessly:
    ///   Unity.exe -batchmode -quit -projectPath &lt;proj&gt;
    ///             -executeMethod CityX.ViewEditor.CityMassingSceneBuilder.Build
    /// </summary>
    public static class CityMassingSceneBuilder
    {
        private const string SceneDirectory = "Assets/CityX/Scenes";
        private const string ScenePath = SceneDirectory + "/CityMassing.unity";
        private const string MaterialDirectory = "Assets/CityX/View/Materials";

        [MenuItem("CityX/Rebuild Massing Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(SceneDirectory);
            Directory.CreateDirectory(MaterialDirectory);

            CityFacadeTextures.Build();

            // The mesh builder writes UVs in METRES, so tiling is simply the
            // reciprocal of how many metres of world each texture covers. That
            // is what makes the facade line up with real floors and window
            // columns on every face of every building with no per-building UV
            // work -- and it is why these numbers are derived from the texture
            // rather than typed in.
            Material ground = EnsureMaterial("CityGround", "CityRoad", 8.0f, 0.10f);
            // Also carries roofs and walkway decks -- same material in the real
            // world, and the buffer is named flatConcrete for that reason.
            Material pavement = EnsureMaterial("CityPavement", "CityPavement", 4.0f, 0.05f);
            Material structure = EnsureMaterial("CityConcrete", "CityFacade",
                CityFacadeTextures.FacadeTileWidth, 0.04f);
            Material darkGlass = EnsureGlassMaterial("CityGlassDark", false);
            Material litGlass = EnsureGlassMaterial("CityGlassLit", true);
            Material crackedGlass = EnsureGlassMaterial("CityGlassCracked", false, cracked: true);
            GameObject shards = GlassShardsBuilder.Build();

            // building_door.gd's own DOOR_COLOR / HANDLE_COLOR. Lit, unlike the
            // lane paint -- a door is a real object catching real light, and an
            // unshaded one would read as a sticker on the wall.
            Material door = EnsureLitMaterial("BuildingDoor", new Color(0.42f, 0.28f, 0.16f));
            Material doorHandle = EnsureLitMaterial("BuildingDoorHandle",
                new Color(0.72f, 0.68f, 0.55f));

            // city_chunk.gd's own LINE_COLOR_YELLOW / LINE_COLOR_WHITE.
            Material roadLine = EnsureFlatMaterial("RoadLine", new Color(0.95f, 0.75f, 0.10f));
            Material crosswalk = EnsureFlatMaterial("Crosswalk", new Color(0.92f, 0.92f, 0.90f));

            // A cool dark steel, deliberately distinct from both the door
            // handle's warm brass and the pavement's own light concrete --
            // a guardrail has to read as a different material at a glance,
            // not just a different shape.
            Material guardRail = EnsureLitMaterial("CityGuardRail", new Color(0.28f, 0.30f, 0.33f));


            Material skyDome = EnsureSkyMaterial();
            VolumeProfile grade = EnsureGradeProfile();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // An empty scene has no environment lighting, so every surface
            // facing away from the sun goes almost black -- and in a city of
            // narrow canyons that is most surfaces. DayNightCycle overwrites
            // this every frame; it is set here so the scene still looks right
            // when opened without pressing play.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = SkyCycle.At(SkyCycle.StartSeconds).AmbientColour;

            // THE SKY IS ALSO WHAT THE CITY REFLECTS. Godot's environment sets
            // reflected_light_source to the sky, and until this scene had a sky
            // at all there was nothing for a window to reflect -- which is a
            // large part of why glass at 0.86 smoothness still read as flat
            // dark paint rather than as glass.
            RenderSettings.skybox = skyDome;
            RenderSettings.defaultReflectionMode =
                UnityEngine.Rendering.DefaultReflectionMode.Skybox;

            // --- light. A sun and a moon on one arc, driven by DayNightCycle.
            // --- Their angle, colour, intensity and which of them casts a
            // --- shadow are all decided by SkyCycle, so nothing about the
            // --- lighting is set here beyond what kind of lights they are.
            GameObject sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;

            GameObject moonObject = new GameObject("Moon");
            Light moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.Soft;

            // --- camera, standing in a street rather than above the city, since
            // --- street level is where the reference lives.
            GameObject cameraObject = new GameObject("FlyCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.farClipPlane = 2000.0f;
            camera.nearClipPlane = 0.15f;
            camera.backgroundColor = new Color(0.55f, 0.60f, 0.66f);
            camera.allowHDR = true;
            camera.allowMSAA = true;
            // MSAA alone (the URP asset's own m_MSAA) only smooths true
            // geometric edges. This city's window/rib/facade detail is a lot
            // of small hard-edged geometry viewed at street level, and the
            // difference an added SMAA pass makes there is exactly what
            // reads as "pixelated" without it -- so both run together
            // rather than relying on MSAA alone.
            UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraObject.AddComponent<FlyCamera>();
            cameraObject.AddComponent<AudioListener>();

            TerrainField terrain = new TerrainField(20260810);
            // Chunk (0,0) is a street on both axes, so this is a road junction
            // with buildings on every side -- the view worth opening on.
            float groundHeight = terrain.PadHeightFor(0, 0);
            cameraObject.transform.position = new Vector3(
                CityLayout.ChunkSize * 0.5f, groundHeight + 1.7f, CityLayout.ChunkSize * 0.5f);
            cameraObject.transform.rotation = Quaternion.Euler(4.0f, 35.0f, 0.0f);

            // --- weapons/wearables. Built here, once, so both the player's
            // --- own Inventory.ItemPrefabs and the dev console's /spawn +
            // --- /equip can point at the very same prefabs.
            GameObject bottlePrefab = CityX.WeaponsEditor.BottleBuilder.Build();
            GameObject skelliJeansPrefab = CityX.WearablesEditor.SkinnedGarmentBuilder.BuildJeans();
            GameObject hoodedCloakPrefab = CityX.WearablesEditor.SkinnedGarmentBuilder.BuildCloak();
            GameObject midnightPatchworkCoatPrefab = CityX.WearablesEditor.MidnightPatchworkCoatBuilder.Build();
            GameObject grapplingHookPrefab = CityX.WeaponsEditor.GrapplingHookBuilder.Build();
            GameObject pistolPrefab = CityX.WeaponsEditor.PistolBuilder.Build();
            GameObject jetpackPrefab = CityX.WeaponsEditor.JetpackBuilder.Build();
            GameObject swordPrefab = CityX.WeaponsEditor.SwordBuilder.Build();
            GameObject flamethrowerPrefab = CityX.WeaponsEditor.FlamethrowerBuilder.Build();
            GameObject topHatPrefab = CityX.WearablesEditor.TopHatBuilder.Build();
            GameObject farmerHatPrefab = CityX.WearablesEditor.FarmerHatBuilder.Build();
            GameObject mustangPrefab = CityX.VehiclesEditor.CarBuilder.Build();

            // --- the player. The flycam stays on the same object but disabled,
            // --- so switching between walking the city and flying over it is a
            // --- checkbox rather than a scene edit.
            GameObject playerPrefab = CityX.CharacterEditor.PlayerRigBuilder.Build();
            if (playerPrefab != null)
            {
                GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.name = "Player";
                // A little above the road so the controller settles onto it
                // rather than starting intersecting it.
                player.transform.position = new Vector3(
                    CityLayout.ChunkSize * 0.5f, groundHeight + 0.5f, CityLayout.ChunkSize * 0.5f);

                cameraObject.GetComponent<FlyCamera>().enabled = false;
                var follow = cameraObject.AddComponent<ThirdPersonCamera>();
                follow.Target = player.transform;

                CityX.UIEditor.PlayerHudBuilder.AddToScene(player.GetComponent<PlayerVitals>());

                // Rain hangs off the PLAYER, not the camera -- see RainEffect
                // for why. It starts off; /weather turns it on.
                var rainObject = new GameObject("Rain");
                rainObject.transform.SetParent(player.transform, false);
                RainEffect rain = rainObject.AddComponent<RainEffect>();
                rain.RainSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/CityX/UI/Audio/rain_noises.mp3");
                rain.DropMaterial = EnsureRainMaterial();

                // NO AMBIENT CROWD. There was one -- a pooled dozen recycled
                // around the viewer as they walked -- and it went at the user's
                // request, matching the Godot build, which retired its own
                // civilian_population.gd to archive/civilian_population/ for
                // the same reason. Nothing places a pedestrian automatically
                // now; Civilian is still built and still placed deliberately by
                // the scale screenshots.
                AddSparringDummies(groundHeight, structure);

                // --- /claim. Reuses PlayerAudio.CurrentBuilding for "which
                // --- building is this," and orbits the same single camera
                // --- FlyCamera/ThirdPersonCamera already share -- see
                // --- ClaimCutscene's own doc comment for why that is not a
                // --- second Camera.
                var claim = player.AddComponent<CityX.Character.ClaimSystem>();
                claim.Audio = player.GetComponent<PlayerAudio>();
                claim.Vitals = player.GetComponent<PlayerVitals>();
                claim.InputReader = player.GetComponent<PlayerInputReader>();
                claim.OrbitCamera = follow;
                claim.CameraTransform = cameraObject.transform;

                var claimSoundObject = new GameObject("ClaimSound");
                claimSoundObject.transform.SetParent(player.transform, false);
                var claimSource = claimSoundObject.AddComponent<AudioSource>();
                claimSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/CityX/UI/Audio/building_acquired.mp3");
                claimSource.playOnAwake = false;
                claimSource.spatialBlend = 0.0f;
                claim.AcquiredSoundSource = claimSource;

                // --- the intro cutscene. Only actually starts for a
                // --- genuinely new character (see IntroCutsceneStarter's
                // --- own PlaySession check) -- pressing Play directly on
                // --- this scene, which is how the world is normally worked
                // --- on and tested, never sets PlaySession.Character at all
                // --- and so never triggers it.
                var introStarter = player.AddComponent<CityX.Character.IntroCutsceneStarter>();
                introStarter.Controller = player.GetComponent<CharacterController>();
                introStarter.OrbitCamera = follow;
                introStarter.InputReader = player.GetComponent<PlayerInputReader>();
                introStarter.Animator = player.GetComponentInChildren<Animator>();
                introStarter.CameraTransform = cameraObject.transform;
                introStarter.Locomotion = player.GetComponent<PlayerLocomotion>();
                introStarter.RuinsMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/CityX/UI/Audio/ruins1.mp3");
                introStarter.GroundImpactSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/CityX/UI/Audio/cutscene_ground_impact.mp3");

                // --- Old Guy. Stands a few metres off, facing the spawn
                // --- point -- "he watches you land" is the doc comment
                // --- old_guy.gd itself opens with. NOT instantiated
                // --- directly -- OldGuySpawner defers that until the intro
                // --- cutscene above has actually handed control back for a
                // --- new character, matching main.gd's own
                // --- _spawn_old_guy_when_playable (see that class's own
                // --- doc comment): he must never be standing there, fully
                // --- visible, for the whole of the player's own fall.
                GameObject dialogueBoxPrefab = CityX.UIEditor.DialogueBoxBuilder.Build();
                GameObject oldGuyPrefab = CityX.CharacterEditor.OldGuyBuilder.Build(dialogueBoxPrefab);
                if (oldGuyPrefab != null)
                {
                    Vector3 oldGuySpawnPosition = player.transform.position + new Vector3(3.0f, 0.0f, 3.0f);
                    Vector3 facing = player.transform.position - oldGuySpawnPosition;
                    Quaternion oldGuySpawnRotation = facing.sqrMagnitude > 0.0001f
                        ? Quaternion.LookRotation(new Vector3(facing.x, 0.0f, facing.z), Vector3.up)
                        : Quaternion.identity;

                    var oldGuySpawnerObject = new GameObject("OldGuySpawner");
                    var oldGuySpawner = oldGuySpawnerObject.AddComponent<CityX.Character.OldGuySpawner>();
                    oldGuySpawner.OldGuyPrefab = oldGuyPrefab;
                    oldGuySpawner.SpawnPosition = oldGuySpawnPosition;
                    oldGuySpawner.SpawnRotation = oldGuySpawnRotation;
                    oldGuySpawner.IntroStarter = introStarter;
                }

                var bubbleObject = new GameObject("SpeechBubble");
                bubbleObject.transform.SetParent(player.transform, false);
                var bubble = bubbleObject.AddComponent<CityX.Character.SpeechBubble>();
                bubble.Anchor = player.transform;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                if (locomotion != null)
                {
                    bubble.AnchorHeadHeight = locomotion.StandingHeight;
                }

                // --- grab/carry. Hold E on a Grabbable rigidbody (a limp
                // --- ragdoll bone, or the crate below) to lift it; a tap
                // --- while carrying drops it. Movement/looking stay free --
                // --- see GrabCarry's own doc comment for why it never
                // --- touches PlayerInputReader/ThirdPersonCamera.enabled.
                var grabCarry = player.AddComponent<CityX.Character.GrabCarry>();
                grabCarry.Vitals = player.GetComponent<PlayerVitals>();
                grabCarry.Eye = cameraObject.transform;

                AddGrabbableCrate(groundHeight, structure);

                // --- inventory. /equip and /unequip in the dev console still
                // --- work too -- both go through the same Inventory calls.
                var inventory = player.AddComponent<CityX.Character.Inventory>();
                inventory.ItemPrefabs = new[]
                {
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.GlassBottle, Prefab = bottlePrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.SkelliJeans, Prefab = skelliJeansPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.HoodedCloak, Prefab = hoodedCloakPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.MidnightPatchworkCoat, Prefab = midnightPatchworkCoatPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.GrapplingHook, Prefab = grapplingHookPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.Pistol, Prefab = pistolPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.Jetpack, Prefab = jetpackPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.Sword, Prefab = swordPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.TopHat, Prefab = topHatPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.FarmerHat, Prefab = farmerHatPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.Flamethrower, Prefab = flamethrowerPrefab },
                };

                // --- the grapple swing. Aim (left-click while a grappling
                // --- hook is equipped), fire, and it owns the whole
                // --- movement frame for as long as the rope is attached --
                // --- see GrappleSwing's own doc comment.
                var grappleSwing = player.AddComponent<CityX.Character.GrappleSwing>();
                // See GrappleSwing.SwingCameraDistance's own doc comment --
                // pulls the camera back for the duration of a swing so the
                // reach pose never has to fight for room this close to a
                // wall-pulled-in camera.
                grappleSwing.OrbitCamera = follow;

                // --- the fireball spell. Innate, not equipped -- see
                // --- FireballCaster's own doc comment for why this lives
                // --- directly on the player rather than behind a pickup
                // --- like the sword/jetpack above.
                var fireballCaster = player.AddComponent<CityX.Weapons.FireballCaster>();
                fireballCaster.FireballPrefab = CityX.WeaponsEditor.FireballProjectileBuilder.Build();

                // No separate builder for FireballCaster itself (it's added
                // directly here, not behind a pickup item) -- so the cast
                // sound is wired inline too, the same non-positional way
                // PlayerAudio's own footstep/ambience loops are (this is
                // the sound of THIS BODY casting, heard from inside it).
                var castAudio = player.AddComponent<AudioSource>();
                castAudio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/CityX/Weapons/Audio/fireball_cast.mp3");
                castAudio.playOnAwake = false;
                castAudio.spatialBlend = 0.0f;
                fireballCaster.CastAudio = castAudio;
            }
            else
            {
                Debug.LogWarning("CityX: no player prefab -- leaving the flycam enabled.");
            }

            // --- the clock. Added after the camera because it drives the
            // --- camera's background colour, which has to keep matching the
            // --- fog or the streamed world's edge becomes a visible wall.
            var cycleObject = new GameObject("DayNightCycle");
            DayNightCycle cycle = cycleObject.AddComponent<DayNightCycle>();
            cycle.Sun = sun;
            cycle.Moon = moon;
            cycle.Sky = camera;
            cycle.SkyDome = skyDome;
            cycle.LitWindows = litGlass;
            cycle.TimeOfDay = SkyCycle.StartSeconds;
            cycle.Apply(SkyCycle.At(SkyCycle.StartSeconds));

            camera.clearFlags = CameraClearFlags.Skybox;

            // --- the grade. A global volume, which is how a URP scene gets one
            // --- at all: without it the pipeline does no tonemapping and every
            // --- highlight clips flat to white. See EnsureGradeProfile.
            var gradeObject = new GameObject("PostProcessing");
            var volume = gradeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0.0f;
            volume.sharedProfile = grade;

            // Distance fog -- pushed back out again, an explicit ask to move
            // AWAY from city.tscn's own hazy/overcast reference look this
            // time ("increase the render distance, get rid of the fog...
            // as real as possible"), the opposite direction from the fog
            // history above this comment used to explain. 35m/88m (city.tscn's
            // own fog_depth_begin/fog_depth_end) fogged out a city block
            // before you could see across it; not zeroed out entirely,
            // since a true zero-fog horizon reads flatter and LESS real
            // than a AAA reference like this was asked to match, not
            // more -- a light, distant haze is itself part of what makes a
            // skyline read as atmospheric depth rather than a cardboard
            // cutout. Timed to the new RadiusChunks below: fog is
            // essentially invisible at ordinary street-level sightlines and
            // only softens the skyline right around where the streamed
            // world actually ends, so raising the render distance doesn't
            // just trade a fog wall for a pop-in wall at a farther radius.
            // A GENUINE COMPROMISE, not a value chosen with total confidence.
            // DayNightCycle.Apply's own comment on Sky.backgroundColor names
            // the reason fog existed at all: "the streamed world ends at a
            // hard edge, and the only thing hiding it is fog the same colour
            // as what is behind it." Confirmed directly by rendering from an
            // elevated vantage with fog pushed out to 720m -- the edge is
            // NOT primarily a distance problem (extending fogEndDistance
            // made no visible difference), it's an ANGLE problem: the sky
            // dome's own ground-facing hemisphere (GroundBottomColour, also
            // used as rooftop reflection ambient -- see DayNightCycle's own
            // comment -- so not a colour safe to just flatten to match the
            // horizon without risking every roof's own lighting) shows
            // through at a shallow downward look from height at distances
            // well under any fog end distance still worth having for a
            // clear street-level view. Linear distance fog cannot solve an
            // angle-shaped problem. 150m/380m is short enough to still mask
            // that seam at ordinary jetpack-rooftop heights while being a
            // real, clearly-visible improvement over the original 35m/88m
            // at street level (confirmed by render) -- a proper fix would
            // need something angle-aware or a ground plane that actually
            // extends to the horizon, both bigger jobs than this pass.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 150.0f;
            RenderSettings.fogEndDistance = 380.0f;

            // --- the dev console. Backtick opens it. Wired here rather than
            // --- shipped as a prefab because half of what it drives only
            // --- exists in a scene, and a prefab of empty slots is the exact
            // --- class of mistake the capture rig has already been caught by.
            CityX.UI.DevConsoleView console = CityX.UIEditor.DevConsoleBuilder.AddToScene(
                Object.FindFirstObjectByType<PlayerVitals>(),
                Object.FindFirstObjectByType<RainEffect>(),
                cycle,
                cameraObject.GetComponent<FlyCamera>(),
                RadioBuilder.Build(),
                bottlePrefab,
                skelliJeansPrefab,
                hoodedCloakPrefab,
                Object.FindFirstObjectByType<PlayerInputReader>(),
                Object.FindFirstObjectByType<ThirdPersonCamera>(),
                grapplingHookPrefab,
                pistolPrefab,
                midnightPatchworkCoatPrefab,
                jetpackPrefab,
                swordPrefab,
                topHatPrefab,
                mustangPrefab,
                farmerHatPrefab,
                flamethrowerPrefab);

            // --- the pause menu. Escape opens it; it guards itself against
            // --- opening on top of the console above.
            CityX.UI.PauseMenuView pause = CityX.UIEditor.PauseMenuBuilder.AddToScene(
                console, Object.FindFirstObjectByType<CityX.Character.PlayerVoiceCapture>());

            GameObject streamerObject = new GameObject("CityStreamer");
            CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
            streamer.WorldSeed = 20260810;
            // 24, not the original 14 (336m -> 576m) -- an explicit "increase
            // the render distance" ask. ChunkBuildBudgetPerFrame already caps
            // new chunks built per frame regardless of radius, so this raises
            // steady-state chunk count/memory, not a one-frame hitch when the
            // streamed edge moves. See the fog comment above for how its own
            // distances were retimed to this new edge.
            streamer.RadiusChunks = 24;
            streamer.GroundMaterial = ground;
            streamer.PavementMaterial = pavement;
            streamer.StructureMaterial = structure;
            streamer.DarkGlassMaterial = darkGlass;
            streamer.LitGlassMaterial = litGlass;
            streamer.CrackedGlassMaterial = crackedGlass;
            streamer.RoadLineMaterial = roadLine;
            streamer.CrosswalkMaterial = crosswalk;
            streamer.DoorMaterial = door;
            streamer.DoorHandleMaterial = doorHandle;
            streamer.GuardRailMaterial = guardRail;

            // Punching a window is the streamer's business because the damage
            // is: chunks come and go, the record of what is broken does not.
            GlassBreaker breaker = streamerObject.AddComponent<GlassBreaker>();
            breaker.CrackSounds = new[] { LoadClip("glass_crack") };
            breaker.BreakSounds = new[] { LoadClip("glass_break") };
            breaker.ShardPrefab = shards;
            streamer.Viewer = cameraObject.transform;

            // --- the map. M opens it; it guards itself against opening on
            // --- top of the console or while the game is paused.
            var mapPlayer = Object.FindFirstObjectByType<PlayerVitals>();
            CityX.UI.MapPanelView map = null;
            if (mapPlayer != null)
            {
                map = CityX.UIEditor.MapPanelBuilder.AddToScene(
                    streamer,
                    mapPlayer.transform,
                    Object.FindFirstObjectByType<PlayerInputReader>(),
                    Object.FindFirstObjectByType<ThirdPersonCamera>(),
                    console,
                    pause);
            }

            // --- the inventory. I opens it; it guards itself against opening
            // --- on top of the console, the pause menu or the map.
            CityX.UI.InventoryPanelView inventoryView = null;
            var inventoryComponent = Object.FindFirstObjectByType<CityX.Character.Inventory>();
            if (inventoryComponent != null)
            {
                inventoryView = CityX.UIEditor.InventoryPanelBuilder.AddToScene(
                    inventoryComponent,
                    inventoryComponent.gameObject,
                    Object.FindFirstObjectByType<PlayerInputReader>(),
                    Object.FindFirstObjectByType<ThirdPersonCamera>(),
                    console,
                    pause,
                    map);
            }

            // --- chat. Enter opens it; /claim inside it is the only way to
            // --- reach the claim system above. Guards itself against the
            // --- console, the pause menu, the map and the inventory, all of
            // --- which also read Enter or Escape or cover the same part of
            // --- the screen.
            CityX.UI.PlayerChat chat = null;
            if (mapPlayer != null)
            {
                chat = CityX.UIEditor.PlayerChatBuilder.AddToScene(
                    Object.FindFirstObjectByType<CityX.Character.ClaimSystem>(),
                    Object.FindFirstObjectByType<CityX.Character.SpeechBubble>(),
                    Object.FindFirstObjectByType<PlayerInputReader>(),
                    Object.FindFirstObjectByType<ThirdPersonCamera>(),
                    console,
                    pause,
                    map,
                    inventoryView);
                pause.Chat = chat;
                if (map != null) map.Chat = chat;
                if (inventoryView != null) inventoryView.Chat = chat;
            }

            // --- the C-menu. C opens it; it guards itself against opening
            // --- on top of the console, the pause menu, the map, the
            // --- inventory or chat, and each of those now also refuses to
            // --- open on top of IT (see the Menu field each just gained) --
            // --- otherwise closing one while the other is still "open"
            // --- would stomp the shared input/camera-enabled state.
            var placeholder = CityX.UIEditor.PlaceholderPanelBuilder.AddToScene(
                Object.FindFirstObjectByType<PlayerInputReader>(),
                Object.FindFirstObjectByType<ThirdPersonCamera>());

            // --- Loadout, the 8-slot equip panel. No dedicated hotkey --
            // --- reached only through the C-menu's Player submenu, which
            // --- has already closed the C-menu by the time it opens.
            CityX.UI.LoadoutPanelView loadoutView = null;
            if (inventoryComponent != null && mapPlayer != null)
            {
                loadoutView = CityX.UIEditor.LoadoutPanelBuilder.AddToScene(
                    inventoryComponent,
                    mapPlayer.gameObject,
                    Object.FindFirstObjectByType<PlayerInputReader>(),
                    Object.FindFirstObjectByType<ThirdPersonCamera>());
            }

            // --- Stats, the level/XP/stat-points panel. Same no-hotkey
            // --- shape as Loadout -- reached only through the C-menu's
            // --- Player submenu.
            CityX.UI.StatsPanelView statsView = null;
            if (mapPlayer != null)
            {
                statsView = CityX.UIEditor.StatsPanelBuilder.AddToScene(
                    mapPlayer,
                    Object.FindFirstObjectByType<PlayerInputReader>(),
                    Object.FindFirstObjectByType<ThirdPersonCamera>());
            }

            // --- ReDraw. Unlike every other panel above, its creator is
            // --- built AT RUNTIME (see RedrawCreatorRuntime's own doc
            // --- comment for why) -- all that has to happen here is wiring
            // --- the one thing runtime code cannot load by path itself:
            // --- the hair mesh/material CharacterCreatorBuilder's own
            // --- AddHair already uses for the title screen.
            CityX.UI.PlayerRedrawStarter redrawStarter = null;
            if (mapPlayer != null)
            {
                redrawStarter = mapPlayer.gameObject.AddComponent<CityX.UI.PlayerRedrawStarter>();
                redrawStarter.InputReader = Object.FindFirstObjectByType<PlayerInputReader>();
                redrawStarter.OrbitCamera = Object.FindFirstObjectByType<ThirdPersonCamera>();
                redrawStarter.HairMesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                    "Assets/CityX/Character/Hair/embermane.obj");
                redrawStarter.HairMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/CityX/Character/Materials/Hair.mat");
                if (redrawStarter.HairMesh == null || redrawStarter.HairMaterial == null)
                {
                    // CharacterCreatorBuilder.HairMaterial() creates Hair.mat
                    // the first time the title screen is built -- if that has
                    // never happened, ReDraw still works, it just leaves the
                    // hair unrendered rather than throwing.
                    Debug.LogWarning("CityX: no hair mesh/material -- build the title screen " +
                                     "scene once so ReDraw has one to reuse.");
                }
            }

            CityX.UI.PlayerMenuView playerMenu = null;
            if (mapPlayer != null)
            {
                playerMenu = CityX.UIEditor.PlayerMenuBuilder.AddToScene(
                    mapPlayer.gameObject,
                    cameraObject.transform,
                    Object.FindFirstObjectByType<PlayerInputReader>(),
                    Object.FindFirstObjectByType<ThirdPersonCamera>(),
                    console,
                    pause,
                    map,
                    inventoryView,
                    chat,
                    loadoutView,
                    statsView,
                    placeholder,
                    redrawStarter);
                map.Menu = playerMenu;
                if (inventoryView != null) inventoryView.Menu = playerMenu;
                if (chat != null) chat.Menu = playerMenu;
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"CityX: massing scene written to {ScenePath}");
        }

        /// <summary>
        /// Puts a few things to punch in the street outside the spawn.
        ///
        /// Combat cannot be judged against nothing -- reach, arc, how many hits
        /// a chain takes to fell something, whether the finisher reads as
        /// heavier. None of that needs an AI to be felt, and adding one first
        /// would mean tuning the swing and the chasing at once with no way to
        /// tell which was wrong.
        /// </summary>
        private static void AddSparringDummies(float groundHeight, Material material)
        {
            var parent = new GameObject("SparringDummies");
            float chunk = CityLayout.ChunkSize;

            // Spread down the street the player spawns on, at increasing
            // distance, so reach can be judged by walking up to them.
            float[] distances = { 4.0f, 9.0f, 15.0f };
            for (int i = 0; i < distances.Length; i++)
            {
                var dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                dummy.name = $"Dummy {i + 1}";
                dummy.transform.SetParent(parent.transform);
                dummy.transform.position = new Vector3(
                    chunk * 0.5f, groundHeight + 1.0f, chunk * 0.5f + distances[i]);
                dummy.GetComponent<MeshRenderer>().sharedMaterial = material;
                dummy.AddComponent<SparringDummy>();
            }
        }

        /// <summary>
        /// A loose physics prop to pick up and carry -- there is no ported
        /// glass_bottle.gd yet (no Unity weapon/wearable component layer
        /// exists at all today) and nothing in this scene naturally dies
        /// into a limp, grabbable ragdoll, so grab/carry would otherwise
        /// have nothing to demonstrate itself on. A plain crate, not a fake
        /// stand-in for a specific Godot item.
        /// </summary>
        private static void AddGrabbableCrate(float groundHeight, Material material)
        {
            float chunk = CityLayout.ChunkSize;
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "GrabbableCrate";
            crate.transform.position = new Vector3(chunk * 0.5f + 1.5f, groundHeight + 0.4f, chunk * 0.5f + 2.5f);
            crate.transform.localScale = Vector3.one * 0.8f;
            crate.GetComponent<MeshRenderer>().sharedMaterial = material;

            Rigidbody body = crate.AddComponent<Rigidbody>();
            body.mass = 8.0f;
            crate.AddComponent<CityX.Character.Grabbable>();
        }

        /// <summary>
        /// Creates or updates a URP Lit material using one of the generated
        /// textures. Updating in place rather than replacing keeps the scene's
        /// reference to it valid across rebuilds.
        /// </summary>
        /// <param name="metresPerTile">
        /// How much world the texture covers. Tiling is its reciprocal, because
        /// UVs are in metres.
        /// </param>
        /// <summary>
        /// Window glass, in two flavours: one with a light behind it and one
        /// without.
        ///
        /// TWO MATERIALS RATHER THAN ONE WITH A VERTEX COLOUR. Whether a
        /// particular window is lit is a per-pane fact, and the cheap ways to
        /// get a per-pane fact into a shader all cost something on every vertex
        /// in the city. Two submeshes cost nothing per vertex and let
        /// DayNightCycle bring every lit window up at dusk by touching one
        /// material.
        ///
        /// CULL OFF, because a pane fills a real hole through a real wall and
        /// is therefore seen from inside as often as from the street. The
        /// alternative is two quads per window, which is the same triangles
        /// again for something one flag does.
        /// </summary>
        /// <summary>
        /// A flat printed colour: lane paint. Unshaded, which is
        /// city_chunk.gd's own _flat_material(colour, true), and the reason
        /// road markings stay legible in a street that is in shadow all day.
        /// </summary>
        /// <summary>
        /// The sky dome. Its colours are overwritten by DayNightCycle every
        /// frame; what is set here is the geometry of the gradient -- the two
        /// curves and the glow -- which does not change with the time of day.
        /// </summary>
        private static Material EnsureSkyMaterial()
        {
            string path = $"{MaterialDirectory}/CityXSky.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("CityX/ProceduralSky");
                if (shader == null)
                {
                    Debug.LogError("CityX: CityXProceduralSky.shader missing -- the sky will " +
                                   "fall back to a flat clear colour.");
                    return null;
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_SkyCurve", SkyCycle.SkyGradientCurve);
            material.SetFloat("_GroundCurve", SkyCycle.GroundGradientCurve);
            material.SetFloat("_SunAngularRadius", SkyCycle.SunAngularRadius);
            material.SetFloat("_MoonAngularRadius", SkyCycle.MoonAngularRadius);
            material.SetFloat("_SunAngleMax", SkyCycle.GlowRadiusDegrees);
            material.SetFloat("_SunCurve", SkyCycle.GlowCurve);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// The grade: a tonemap and a small contrast/saturation lift, matching
        /// city.tscn's own Environment.
        ///
        /// THE TONEMAP IS THE POINT. Without one the renderer's HDR output is
        /// simply clamped, so every sunlit concrete face lands on flat white
        /// with no roll-off and no highlight detail -- which is most of what
        /// "the graphics looked worse" actually was, and why the sun had to be
        /// held down to 1.15 to be bearable at all.
        ///
        /// ONE HONEST DIVERGENCE: Godot uses AgX (tonemap_mode 4). URP offers
        /// None, Neutral and ACES, and has no AgX. NEUTRAL is the closer of the
        /// two, because both it and AgX preserve hue as they compress; ACES
        /// applies a strong S-curve and visibly shifts blues toward purple and
        /// oranges toward yellow, which would pull the sunset and the sky dome
        /// away from the values Godot tuned. Swap the mode below if a punchier
        /// image is wanted over a faithful one.
        /// </summary>
        private static VolumeProfile EnsureGradeProfile()
        {
            string path = $"{MaterialDirectory}/CityGrade.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>();
            }
            tonemapping.active = true;
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;

            // city.tscn's adjustment_contrast / adjustment_saturation, both
            // 1.08. Unity states these as a percentage offset from neutral
            // rather than as a multiplier, so 1.08 is 8.
            if (!profile.TryGet(out ColorAdjustments adjustments))
            {
                adjustments = profile.Add<ColorAdjustments>();
            }
            adjustments.active = true;
            adjustments.contrast.overrideState = true;
            adjustments.contrast.value = 8.0f;
            adjustments.saturation.overrideState = true;
            adjustments.saturation.value = 8.0f;

            // Bloom, new -- no Godot equivalent to port (city.tscn has none),
            // an explicit ask alongside the fog/render-distance change to
            // read as a more modern, realistic lighting reference rather
            // than a straight match of the original scene. Modest: a real
            // window strip or a car's headlights should glow slightly, nothing
            // should wash out into a soft-focus haze.
            if (!profile.TryGet(out Bloom bloom))
            {
                bloom = profile.Add<Bloom>();
            }
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.35f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.6f;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// A raindrop: unlit, transparent, no shadows either way.
        ///
        /// UNLIT ON PURPOSE, which is Godot's own choice. A lit raindrop would
        /// take its brightness from whichever way its quad happened to be
        /// facing, so a curtain of rain would flicker as the camera turned. It
        /// is a pale streak; it should be that pale streak from everywhere.
        /// </summary>
        private static Material EnsureRainMaterial()
        {
            string path = $"{MaterialDirectory}/Raindrop.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                if (shader == null)
                {
                    Debug.LogError("CityX: no URP unlit shader -- rain will render magenta.");
                    return null;
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_Surface", 1.0f);              // transparent
            material.SetFloat("_Blend", 0.0f);                // alpha
            material.SetFloat("_ZWrite", 0.0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetColor("_BaseColor", RainEffect.DropColour);
            material.color = RainEffect.DropColour;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A plain lit colour, for a prop with no texture of its own.</summary>
        private static Material EnsureLitMaterial(string name, Color colour)
        {
            string path = $"{MaterialDirectory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("CityX: URP Lit shader not found -- is the Universal Render " +
                                   "Pipeline asset assigned in Graphics settings?");
                    shader = Shader.Find("Standard");
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.25f);
            material.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureFlatMaterial(string name, Color colour)
        {
            string path = $"{MaterialDirectory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    Debug.LogError("CityX: URP Unlit shader not found -- is the Universal Render " +
                                   "Pipeline asset assigned in Graphics settings?");
                    shader = Shader.Find("Unlit/Color");
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            material.color = colour;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// One of the glass sounds, copied over from Godot's own
        /// weapons/assets/audio. Missing means silence rather than a failure --
        /// the same way a missing punch sample behaves.
        /// </summary>
        private static AudioClip LoadClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                $"Assets/CityX/View/Audio/{name}.mp3");
            if (clip == null)
            {
                Debug.LogWarning($"CityX: {name}.mp3 missing -- glass will break silently.");
            }
            return clip;
        }

        private static Material EnsureGlassMaterial(string name, bool lit, bool cracked = false)
        {
            string path = $"{MaterialDirectory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("CityX: URP Lit shader not found -- is the Universal Render " +
                                   "Pipeline asset assigned in Graphics settings?");
                    shader = Shader.Find("Standard");
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            // Transparent, and dark rather than clear: city glass at a distance
            // reads as a dark reflective slot, not as an open hole. Fully clear
            // glass would make every building look like it had no windows at
            // all, which is where this started.
            material.SetFloat("_Surface", 1.0f);              // transparent
            material.SetFloat("_Blend", 0.0f);                // alpha
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_Cull", 0.0f);                 // both sides
            material.SetFloat("_AlphaClip", 0.0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            material.SetTexture("_BaseMap", null);
            // Cracked glass goes pale and rough rather than dark and sharp: a
            // window somebody has put a fist through frosts over, and it has to
            // read as damaged from across the street or the first hit will look
            // like it did nothing.
            material.SetColor("_BaseColor", cracked
                ? new Color(0.72f, 0.78f, 0.80f, 0.45f)
                : new Color(0.055f, 0.062f, 0.075f, 0.72f));
            material.SetFloat("_Smoothness", cracked ? 0.18f : 0.86f);
            material.SetFloat("_Metallic", 0.0f);

            if (lit)
            {
                // Colour left black; DayNightCycle raises it as the light goes,
                // so the windows come on at dusk rather than glowing all
                // afternoon. Same arrangement the facade emission map had.
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureMaterial(string name, string textureName,
            float metresPerTile, float smoothness)
        {
            string path = $"{MaterialDirectory}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    // Naming the cause matters here: a missing URP shader means
                    // the render pipeline is not set up, and the symptom would
                    // otherwise be a city rendered in flat magenta.
                    Debug.LogError("CityX: URP Lit shader not found -- is the Universal Render Pipeline " +
                                   "asset assigned in Graphics settings?");
                    shader = Shader.Find("Standard");
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            string textureDirectory = "Assets/CityX/View/Textures";
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureDirectory}/{textureName}.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textureDirectory}/{textureName}Normal.png");

            if (albedo == null)
            {
                Debug.LogError($"CityX: texture {textureName} missing -- run CityX/Rebuild City Textures.");
            }

            float tiling = 1.0f / metresPerTile;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0.0f);

            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", normal);
                material.SetTextureScale("_BumpMap", new Vector2(tiling, tiling));
                material.SetFloat("_BumpScale", 1.0f);
            }

            // Lit windows, if this surface has any. The COLOUR is left black
            // here and driven by DayNightCycle, so the windows come on at dusk
            // rather than glowing through the afternoon.
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{textureDirectory}/{textureName}Emission.png");
            if (emission != null)
            {
                material.EnableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", emission);
                material.SetTextureScale("_EmissionMap", new Vector2(tiling, tiling));
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                // TURNED OFF EXPLICITLY, NOT JUST LEFT ALONE. A material asset
                // remembers: the facade carried an emission map once, and the
                // day/night cycle wrote a night-time glow into its colour.
                // Deleting the map stopped anything SETTING that value but did
                // nothing about the value already saved in the file -- and with
                // no map to multiply against, a leftover 2.4 stopped lighting
                // the window pixels and started lighting every wall in the
                // city. Which is exactly what it did: the whole skyline came
                // back pure white.
                material.DisableKeyword("_EMISSION");
                material.SetTexture("_EmissionMap", null);
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
