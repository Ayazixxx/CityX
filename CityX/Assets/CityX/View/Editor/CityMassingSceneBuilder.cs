using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.View;
using CityX.World;
using FishNet.Component.Transforming;
using FishNet.Object;
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

        /// <summary>
        /// The real, played game world's own seed. EXPLICIT ASK: "generate
        /// a new world every time you make a change to the city
        /// generation" -- was two separate hardcoded 54145500 literals
        /// (TerrainField and CityStreamer.WorldSeed below) that had to be
        /// kept in sync by hand; a single named constant here is what
        /// actually makes rerolling it, going forward, a one-line, can't-
        /// miss-a-spot change. TitleScreenBuilder/CityMassingCapture/
        /// GrapplingHookBuilder each still carry their OWN copy of this
        /// same number (different files, no shared constant to reference
        /// across assemblies) -- change all of them together, or the title
        /// screen's backdrop and the massing scene's own city stop
        /// matching.
        /// </summary>
        private const int WorldSeed = 61472093;

        /// <summary>How glossy the city's three shared surfaces are when DRY. The rain restores to exactly these.</summary>
        private const float GroundDrySmoothness = 0.10f;
        private const float PavementDrySmoothness = 0.05f;
        private const float ConcreteDrySmoothness = 0.04f;

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
            Material ground = EnsureMaterial("CityGround", "CityRoad", 8.0f, GroundDrySmoothness);
            // Also carries roofs and walkway decks -- same material in the real
            // world, and the buffer is named flatConcrete for that reason.
            Material pavement = EnsureMaterial("CityPavement", "CityPavement", 4.0f, PavementDrySmoothness);
            // The walls (and, since a building is one draw, its roof and trim) use the facade shader: eight wall materials in a texture
            // array, picked per building by the look stamped on its vertices. Still named CityConcrete so everything that finds it by
            // name keeps working.
            FacadeTextureBuilder.Build(false);
            Material structure = FacadeTextureBuilder.EnsureMaterial();
            Material darkGlass = EnsureGlassMaterial("CityGlassDark", false);
            Material litGlass = EnsureGlassMaterial("CityGlassLit", true);
            Material crackedGlass = EnsureGlassMaterial("CityGlassCracked", false, cracked: true);
            Material crackDecal = EnsureCrackDecalMaterial();
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

            // Warm wood, the same "park furniture" brown a real bench slat
            // is painted or stained -- distinct from the guardrail's cool
            // steel and the pavement's light concrete for the same reason.
            Material benchWood = EnsureLitMaterial("CityBenchWood", new Color(0.42f, 0.28f, 0.16f));
            // Darker, aged wood -- a reading stand reads as furniture in
            // its own right, distinct from a bench's own lighter warm brown.
            Material bookStand = EnsureLitMaterial("CityBookStand", new Color(0.24f, 0.16f, 0.10f));

            // Park lawns and tree canopies -- a flat, slightly cool green that
            // sits between the concrete grey and the night sky without
            // fighting either. Trunks reuse the bench wood.
            Material grass = EnsureLitMaterial("CityGrass", new Color(0.17f, 0.33f, 0.14f));

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

            // Screen-space lens flare -- the RP asset already has
            // m_SupportScreenSpaceLensFlare on, but nothing was ever
            // attached to make use of it. useOcclusion matters more here
            // than it would in an open scene: this is a dense city of
            // narrow canyons, so most of the time the sun is behind a
            // building, and without occlusion the flare would shine
            // straight through it.
            LensFlareComponentSRP sunFlare = sunObject.AddComponent<LensFlareComponentSRP>();
            sunFlare.lensFlareData = EnsureSunLensFlareData();
            sunFlare.intensity = 1.4f;
            sunFlare.useOcclusion = true;
            sunFlare.occlusionRadius = 0.2f;
            sunFlare.sampleCount = 16;

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
            // player.gd's own camera.fov -- ThirdPersonCamera.Awake reads
            // _restFieldOfView straight off this Camera component (the same
            // "captured from whatever's already configured" idiom player.gd's
            // own _default_fov uses), so it has to be set explicitly here or
            // it silently falls back to Unity's own unrelated default (60,
            // narrower than Godot's real 75 -- never noticed because nothing
            // ever set it either way). EXPLICIT ASK: "I WANT IT EXACTLY THE
            // SAME," found by actually reading player.gd instead of guessing.
            camera.fieldOfView = 75.0f;
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
            // EXPLICIT BUG, the camera-side twin of the Volume persistence bug
            // documented on EnsureGradeProfile: UniversalAdditionalCameraData's
            // own renderPostProcessing defaults to FALSE, and nothing here ever
            // set it true. The grade Volume below has been correctly wired and
            // correctly persisted since that earlier fix, but this camera was
            // never actually asking to use it -- so tonemapping, bloom,
            // vignette, chromatic aberration and film grain have never actually
            // rendered in the real game at all. Confirmed directly against the
            // committed scene file: m_RenderPostProcessing serialized as 0 on
            // both cameras in CityMassing.unity.
            cameraData.renderPostProcessing = true;
            cameraObject.AddComponent<FlyCamera>();
            cameraObject.AddComponent<AudioListener>();

            // The city's own plan decides where the player starts: the junction nearest the origin, which is downtown.
            CityWorld cityWorld = new CityWorld(WorldSeed);
            cityWorld.Plan.TryFindSpawn(out double spawnX, out double spawnZ);
            float groundHeight = (float)cityWorld.Terrain.ContinuousHeightAt(spawnX, spawnZ);
            // Captured here, not looked up later with FindFirstObjectByType --
            // the scene ends up with SEVERAL Ragdoll components (the sparring
            // dummies are Civilian.prefab-based too, and each carries its
            // own), so a type-only lookup after they all exist is genuinely
            // ambiguous about which one it finds. EXPLICIT
            // BUG REPORT this caused: "/sleep" wired to whichever Ragdoll
            // FindFirstObjectByType happened to return first -- some NPC
            // somewhere, never the player -- so typing it visibly did nothing
            // to the character actually being watched.
            Ragdoll playerRagdoll = null;
            // Same reasoning, same fix shape: captured off the player
            // directly rather than looked up by type later, once the
            // watchdog needs GrappleSwing/PlayerLocomotion to log which
            // systems were in play when something looked wrong.
            GrappleSwing playerGrappleSwing = null;
            PlayerLocomotion playerLocomotion = null;
            GameObject playerObjectForWatchdog = null;
            cameraObject.transform.position = new Vector3(
                (float)spawnX, groundHeight + 1.7f, (float)spawnZ);
            cameraObject.transform.rotation = Quaternion.Euler(4.0f, 35.0f, 0.0f);

            // --- weapons/wearables. Built here, once, so both the player's
            // --- own Inventory.ItemPrefabs and the dev console's /spawn +
            // --- /equip can point at the very same prefabs.
            GameObject bottlePrefab = CityX.WeaponsEditor.BottleBuilder.Build();
            GameObject skelliJeansPrefab = CityX.WearablesEditor.SkinnedGarmentBuilder.BuildJeans();
            GameObject hoodedCloakPrefab = CityX.WearablesEditor.SkinnedGarmentBuilder.BuildCloak();
            GameObject midnightPatchworkCoatPrefab = CityX.WearablesEditor.MidnightPatchworkCoatBuilder.Build();
            GameObject blackPufferJacketPrefab = CityX.WearablesEditor.BlackPufferJacketBuilder.Build();
            GameObject grapplingHookPrefab = CityX.WeaponsEditor.GrapplingHookBuilder.Build();
            GameObject pistolPrefab = CityX.WeaponsEditor.PistolBuilder.Build();
            GameObject jetpackPrefab = CityX.WeaponsEditor.JetpackBuilder.Build();
            GameObject parachutePrefab = CityX.WeaponsEditor.ParachuteBuilder.Build();
            GameObject swordPrefab = CityX.WeaponsEditor.SwordBuilder.Build();
            GameObject flamethrowerPrefab = CityX.WeaponsEditor.FlamethrowerBuilder.Build();
            GameObject topHatPrefab = CityX.WearablesEditor.TopHatBuilder.Build();
            GameObject mustangPrefab = CityX.VehiclesEditor.CarBuilder.Build();

            // --- the player. The flycam stays on the same object but disabled,
            // --- so switching between walking the city and flying over it is a
            // --- checkbox rather than a scene edit.
            GameObject playerPrefab = CityX.CharacterEditor.PlayerRigBuilder.Build();

            // --- the flying NPC. Run after the player rig (whose animator
            // --- controller asset it shares) and after the jetpack built
            // --- just above (whose prefab it wears, permanently equipped).
            // --- Not placed in the scene here -- like Civilian.prefab, it
            // --- is only ever spawned deliberately, via /spawn flyer.
            CityX.CharacterEditor.FlyingNpcBuilder.Build(jetpackPrefab);

            // --- the Crimson Claw monster. Not placed in the scene here --
            // --- like Civilian.prefab/FlyingNpc.prefab, only ever spawned
            // --- deliberately, via /spawn crimsonclaw.
            CityX.CharacterEditor.CrimsonClawBuilder.Build();
            if (playerPrefab != null)
            {
                GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                player.name = "Player";
                // The player wears the hairstyle their character chose (CharacterIdentity
                // applies it). Until now only the creator and the character list did -- in
                // the world the hair simply was not there.
                CityX.UIEditor.HairAssets.AddTo(player);
                // A little above the road so the controller settles onto it rather than starting intersecting it.
                player.transform.position = new Vector3((float)spawnX, groundHeight + 0.5f, (float)spawnZ);
                playerRagdoll = player.GetComponent<Ragdoll>();
                playerLocomotion = player.GetComponent<PlayerLocomotion>();
                playerObjectForWatchdog = player;

                cameraObject.GetComponent<FlyCamera>().enabled = false;
                var follow = cameraObject.AddComponent<ThirdPersonCamera>();
                follow.Target = player.transform;
                follow.Ragdoll = playerRagdoll;

                CityX.UIEditor.PlayerHudBuilder.AddToScene(player.GetComponent<PlayerVitals>());

                // Rain hangs off the PLAYER, not the camera -- see RainEffect
                // for why. It starts off; /weather turns it on.
                var rainObject = new GameObject("Rain");
                rainObject.transform.SetParent(player.transform, false);
                RainEffect rain = rainObject.AddComponent<RainEffect>();
                rain.RainSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/CityX/UI/Audio/rain_noises.mp3");
                rain.DropMaterial = EnsureRainMaterial();
                rain.SplashMaterial = EnsureSplashMaterial();
                rain.RippleMaterial = EnsureRippleMaterial();

                // NO AMBIENT CROWD. There was one -- a pooled dozen recycled
                // around the viewer as they walked -- and it went at the user's
                // request, matching the Godot build, which retired its own
                // civilian_population.gd to archive/civilian_population/ for
                // the same reason. Nothing places a pedestrian automatically
                // now; Civilian is still built and still placed deliberately by
                // the scale screenshots.
                //
                // NO SPARRING DUMMIES AT SPAWN EITHER -- EXPLICIT ASK: "get
                // rid of these objects from spawn." Still fully available on
                // demand via the real console (`/spawn dummy`, see
                // DevConsoleView.Spawn's own identical capsule-building
                // case), just not dropped in the player's own path
                // automatically every time the scene builds.

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
                // --- ragdoll bone) to lift it; a tap while carrying drops
                // --- it. Movement/looking stay free -- see GrabCarry's own
                // --- doc comment for why it never touches
                // --- PlayerInputReader/ThirdPersonCamera.enabled.
                //
                // NO DEMO CRATE AT SPAWN -- EXPLICIT ASK: "get rid of these
                // objects from spawn." A knocked-out ragdoll already gives
                // Grabbable something real to pick up; the crate was only
                // ever a stand-in for testing this before ragdolls existed.
                var grabCarry = player.AddComponent<CityX.Character.GrabCarry>();
                grabCarry.Vitals = player.GetComponent<PlayerVitals>();
                grabCarry.Eye = cameraObject.transform;

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
                        { ItemId = CityX.Items.ItemRegistry.BlackPufferJacket, Prefab = blackPufferJacketPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.GrapplingHook, Prefab = grapplingHookPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.Pistol, Prefab = pistolPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.Jetpack, Prefab = jetpackPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.Parachute, Prefab = parachutePrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.Sword, Prefab = swordPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.TopHat, Prefab = topHatPrefab },
                    new CityX.Character.Inventory.ItemPrefabEntry
                        { ItemId = CityX.Items.ItemRegistry.Flamethrower, Prefab = flamethrowerPrefab },
                };

                // --- the grapple swing. Aim (left-click while a grappling
                // --- hook is equipped), fire, and it owns the whole
                // --- movement frame for as long as the rope is attached --
                // --- see GrappleSwing's own doc comment.
                var grappleSwing = player.AddComponent<CityX.Character.GrappleSwing>();
                playerGrappleSwing = grappleSwing;
                // See GrappleSwing.SwingCameraDistance's own doc comment --
                // pulls the camera back for the duration of a swing so the
                // reach pose never has to fight for room this close to a
                // wall-pulled-in camera.
                grappleSwing.OrbitCamera = follow;

                // --- the fireball spell. Innate, not equipped -- see
                // --- FireballCaster's own doc comment for why this lives
                // --- directly on the player rather than behind a pickup
                // --- like the sword/jetpack above.
                // THE INNATE ABILITIES -- the fireball (key 1), the ice shards (key 3) and the sword's crescent wave (key 7) -- go on through
                // PlayerAbilityWiring, the one list shared with the NetworkedPlayer.prefab that every joining player is spawned as. The projectile
                // prefabs are (re)built first; the wiring loads them by path.
                CityX.WeaponsEditor.FireballProjectileBuilder.Build();
                CityX.WeaponsEditor.IceShardBuilder.Build();
                CityX.WeaponsEditor.SwordWaveBuilder.Build();
                CityX.CharacterEditor.PlayerAbilityWiring.AddAbilities(player);
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

            // --- rain's own environmental reach, wired here rather than
            // --- where Rain itself was built above: `rain` there is a local
            // --- variable scoped inside the earlier `if (playerPrefab !=
            // --- null)` block, and Sun/grade/fog all only exist (or are
            // --- only in their final form) by this point in the method --
            // --- same FindFirstObjectByType late-wiring idiom the console
            // --- lookup just below already uses, and unambiguous for the
            // --- same reason: there is only ever one RainEffect in this
            // --- scene, unlike the several Ragdolls the comment above
            // --- Ragdoll playerRagdoll already warns about.
            RainEffect rainEffect = Object.FindFirstObjectByType<RainEffect>();
            if (rainEffect != null)
            {
                rainEffect.Sun = sun;
                rainEffect.Cycle = cycle;
                rainEffect.Grade = grade;
                rainEffect.WetGroundMaterials = new[] { ground, pavement, structure };
                // The DRY look, stated from the same constants that built the materials, so the
                // rain never has to trust what the files say (see RainEffect.DryGroundColours).
                rainEffect.DryGroundColours = new[] { Color.white, Color.white, Color.white };
                rainEffect.DryGroundSmoothness = new[] { GroundDrySmoothness, PavementDrySmoothness, ConcreteDrySmoothness };
                // The EXACT same numbers set on RenderSettings two lines
                // above -- see RainEffect.ClearFogStart's own doc comment
                // for why these are passed in explicitly rather than read
                // back from RenderSettings at runtime.
                rainEffect.ClearFogStart = RenderSettings.fogStartDistance;
                rainEffect.ClearFogEnd = RenderSettings.fogEndDistance;
            }

            // --- the dev console. Backtick opens it. Wired here rather than
            // --- shipped as a prefab because half of what it drives only
            // --- exists in a scene, and a prefab of empty slots is the exact
            // --- class of mistake the capture rig has already been caught by.
            CityX.UI.DevConsoleView console = CityX.UIEditor.DevConsoleBuilder.AddToScene(
                Object.FindFirstObjectByType<PlayerVitals>(),
                rainEffect,
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
                flamethrowerPrefab,
                parachutePrefab,
                blackPufferJacketPrefab);

            // Watches the player's own rig every real frame during ordinary
            // play and logs a full snapshot the instant it looks distorted
            // -- see PoseDistortionWatchdog's own class doc comment for why
            // this exists instead of another synthetic diagnostic.
            //
            // IsConsoleOpen IS DELIBERATELY NOT WIRED HERE -- EXPLICIT BUG,
            // found chasing the grapple/console distortion report: a
            // System.Func<bool> field can never survive Unity's own scene
            // serialization, so an assignment made here, while BUILDING the
            // scene in batch mode, is silently gone the moment this saved
            // scene is reopened for real play -- every real session ran with
            // it permanently null. See DevConsoleView.Bind's own doc comment
            // for the real fix: wired at ACTUAL RUNTIME instead, from the
            // console's own Awake, which is not scene-serialization-dependent.
            if (playerObjectForWatchdog != null)
            {
                var watchdog = playerObjectForWatchdog.AddComponent<PoseDistortionWatchdog>();
                watchdog.Swing = playerGrappleSwing;
                watchdog.Locomotion = playerLocomotion;
            }

            // --- multiplayer: turns a live FishNet session into more than
            // --- one visible player. No-op for Singleplayer (checked at
            // --- runtime, see NetworkedPlayerSpawner's own doc comment) --
            // --- the baked player above works exactly as it always has when
            // --- nobody hosted or joined anything on the title screen.
            //
            // NETWORKING COMPONENTS GO DIRECTLY ON THIS ONE SCENE INSTANCE,
            // NOT ON Player.prefab ITSELF -- Player.prefab is Object.
            // Instantiate'd by roughly 80 pre-existing PlayMode tests with
            // no NetworkManager involved at all, and FishNet's own
            // NetworkObject.Awake() unconditionally errors for any instance
            // that is neither a real scene object nor spawned through
            // FishNet's own object pool. THIS instance IS a real scene
            // object once OnValidate assigns it a genuine SceneId (which
            // only happens with it parented into an actual, loaded scene --
            // a prefab asset in isolation has no scene to assign one from),
            // so it does not hit that gap. Joining peers get a SEPARATE
            // derived prefab instead (NetworkedPlayerBuilder), spawned
            // through the object pool -- see NetworkedPlayerSpawner's own
            // doc comment for why a clone needs that different mechanism
            // even though it is never this exact GameObject.
            if (playerObjectForWatchdog != null)
            {
                NetworkObject bakedNetworkObject = playerObjectForWatchdog.AddComponent<NetworkObject>();
                playerObjectForWatchdog.AddComponent<NetworkTransform>();
                playerObjectForWatchdog.AddComponent<CityX.Character.NetworkedPlayerIdentity>();
                playerObjectForWatchdog.AddComponent<CityX.Character.NetworkedAnimatorSync>();
                playerObjectForWatchdog.AddComponent<CityX.Character.NetworkedAbilitySync>();
                // Same view-distance culling the derived clone prefab gets
                // just below (NetworkedPlayerBuilder.Build) -- without this,
                // the HOST's own baked body would observe (and be observed
                // by) every other player regardless of distance even though
                // every joining clone already correctly culls. See
                // NetworkedPlayerIdentity.ViewRadius's own doc comment.
                CityX.CharacterEditor.NetworkedPlayerBuilder.AddViewDistanceObserver(playerObjectForWatchdog);
                // OnValidate is what actually assigns a real SceneId (and
                // the WasActiveDuringEdit flag) -- Unity calls it
                // automatically after an Editor AddComponent normally, but
                // never during a headless -batchmode script's own call, so
                // it is forced directly. Internal, not private -- and
                // reflection, not SendMessage, reaches it: see
                // NetworkedPlayerReserialize's own doc comment for why
                // SendMessage("OnValidate", ...) looked like it worked but
                // silently never actually ran (this is also why every real
                // Play session logged "is expected to be initialized but
                // was not" regardless of this call -- SceneId stayed 0).
                // ReserializeSceneObject, not the plain per-object call --
                // THIS instance is a real scene object and needs an actual
                // SceneId assigned (see that method's own doc comment for
                // why the obvious "setSceneId: true" one-liner silently
                // does nothing). The derived NetworkedPlayer.prefab built
                // just below is a pure prefab asset and must NOT get one,
                // hence its own call in NetworkedPlayerBuilder uses the
                // plain per-object path instead.
                CityX.CharacterEditor.NetworkedPlayerReserialize.ReserializeSceneObject(
                    bakedNetworkObject, scene);

                GameObject networkedPlayerPrefab = CityX.CharacterEditor.NetworkedPlayerBuilder.Build();

                var spawnPointObject = new GameObject("NetworkSpawnPoint");
                spawnPointObject.transform.position = playerObjectForWatchdog.transform.position;

                var spawner = new GameObject("NetworkedPlayerSpawner").AddComponent<CityX.Character.NetworkedPlayerSpawner>();
                spawner.LocalPlayer = bakedNetworkObject;
                spawner.PlayerPrefab = networkedPlayerPrefab;
                spawner.SpawnPoint = spawnPointObject.transform;
            }

            // --- the pause menu. Escape opens it; it guards itself against
            // --- opening on top of the console above.
            CityX.UI.PauseMenuView pause = CityX.UIEditor.PauseMenuBuilder.AddToScene(
                console, Object.FindFirstObjectByType<CityX.Character.PlayerVoiceCapture>());

            GameObject streamerObject = new GameObject("CityStreamer");
            CityStreamer streamer = streamerObject.AddComponent<CityStreamer>();
            streamer.WorldSeed = WorldSeed;
            // 24, not the original 14 (336m -> 576m) -- an explicit "increase
            // the render distance" ask. ChunkBuildBudgetPerFrame already caps
            // new chunks built per frame regardless of radius, so this raises
            // steady-state chunk count/memory, not a one-frame hitch when the
            // streamed edge moves. See the fog comment above for how its own
            // distances were retimed to this new edge.
            streamer.RadiusChunks = 24;
            // The spawn above is for the world at the scale in force NOW; the game may run at another, so the streamer re-places the player.
            streamer.PlaceAtSpawn = true;
            streamer.GroundMaterial = ground;
            streamer.PavementMaterial = pavement;
            streamer.StructureMaterial = structure;
            streamer.DarkGlassMaterial = darkGlass;
            streamer.LitGlassMaterial = litGlass;
            streamer.CrackedGlassMaterial = crackedGlass;
            streamer.CrackDecalMaterial = crackDecal;
            streamer.RoadLineMaterial = roadLine;
            streamer.CrosswalkMaterial = crosswalk;
            streamer.DoorMaterial = door;
            streamer.DoorHandleMaterial = doorHandle;
            streamer.GuardRailMaterial = guardRail;
            streamer.BenchMaterial = benchWood;
            streamer.BookMaterial = bookStand;
            streamer.GrassMaterial = grass;
            streamer.BarkMaterial = benchWood;

            // Punching a window is the streamer's business because the damage
            // is: chunks come and go, the record of what is broken does not.
            GlassBreaker breaker = streamerObject.AddComponent<GlassBreaker>();
            breaker.CrackSounds = new[] { LoadClip("glass_crack") };
            breaker.BreakSounds = new[] { LoadClip("glass_break") };
            breaker.ShardPrefab = shards;
            streamer.Viewer = cameraObject.transform;
            console.Streamer = streamer;

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
                    inventoryView,
                    playerRagdoll);
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

            // The book-reading page -- EXPLICIT ASK: "add books and when
            // you press E on them it opens a page on screen with text ive
            // prepared but just make the system for now." Same one-shared-
            // instance shape as the placeholder above.
            CityX.UIEditor.BookPanelBuilder.AddToScene(
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
                redrawStarter.HairMeshes = CityX.UIEditor.HairAssets.StyleMeshes();
                redrawStarter.HairMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/CityX/Character/Materials/Hair.mat");
                if (redrawStarter.HairMeshes.Length == 0 || System.Array.IndexOf(redrawStarter.HairMeshes, null) >= 0
                    || redrawStarter.HairMaterial == null)
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

            // EXPLICIT BUG, found and fixed in the same session as the
            // vignette/chromatic-aberration additions below: VolumeProfile.
            // Add<T> only appends the new component to the profile's own
            // in-memory list -- it never calls AssetDatabase.AddObjectToAsset,
            // so a component created this way was NEVER actually written
            // into CityGrade.asset as a real sub-asset. Confirmed directly
            // in the committed file (git history back to the commit that
            // introduced this method): its own `components:` list has
            // always serialized as three `{fileID: 0}` NULLS. Loading that
            // file back gives a VolumeProfile with ZERO real overrides, so
            // this method's own tonemapping/bloom/etc. were being rebuilt
            // in memory and thrown away on every single scene rebuild --
            // the tonemap, the bloom, the colour grade have never actually
            // been active in the real game, exactly the "HDR simply
            // clamped, every sunlit face flat white" failure mode
            // Tonemapping's own doc comment already warned about, just
            // never actually prevented. AddObjectToAsset ONLY on first
            // creation (the guard below already only enters when the
            // component does not exist yet) is what actually embeds it.
            if (!profile.TryGet(out Tonemapping tonemapping))
            {
                tonemapping = profile.Add<Tonemapping>();
                AssetDatabase.AddObjectToAsset(tonemapping, profile);
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
                AssetDatabase.AddObjectToAsset(adjustments, profile);
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
                AssetDatabase.AddObjectToAsset(bloom, profile);
            }
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.35f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.6f;

            // Vignette and a touch of chromatic aberration, new -- same
            // "read as a modern AAA reference" ask Bloom's own comment
            // above names. Both kept deliberately subtle: this is a frame
            // finish, not a stylistic filter, and a heavy vignette would
            // fight the wide street canyons this world is built around
            // (their own corners already go dark from the buildings
            // themselves without any help from a post effect).
            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>();
                AssetDatabase.AddObjectToAsset(vignette, profile);
            }
            vignette.active = true;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.2f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.5f;

            if (!profile.TryGet(out ChromaticAberration chromaticAberration))
            {
                chromaticAberration = profile.Add<ChromaticAberration>();
                AssetDatabase.AddObjectToAsset(chromaticAberration, profile);
            }
            chromaticAberration.active = true;
            chromaticAberration.intensity.overrideState = true;
            // 0.08 until this session -- tuned by eye against the maths alone,
            // since renderPostProcessing being off on the camera (see the fix
            // in Build's own camera setup) meant nobody had actually SEEN this
            // effect composited before. At 0.08 the fringing on this city's
            // own hard-edged window grids reads as a lens-error artefact by
            // the time it reaches the frame edge in a wide shot (confirmed via
            // CityMassingCapture's skyline/rooftops screenshots), not as the
            // subtle filmic touch the value was chosen for.
            chromaticAberration.intensity.value = 0.04f;

            // Film grain, new -- same "read as a modern AAA reference" ask as
            // Bloom/Vignette above. Thin preset kept deliberately faint: this
            // is meant to be felt as film texture in flat, evenly-lit
            // surfaces (sky, concrete facades) rather than seen as noise.
            if (!profile.TryGet(out FilmGrain filmGrain))
            {
                filmGrain = profile.Add<FilmGrain>();
                AssetDatabase.AddObjectToAsset(filmGrain, profile);
            }
            filmGrain.active = true;
            filmGrain.type.overrideState = true;
            filmGrain.type.value = FilmGrainLookup.Thin1;
            filmGrain.intensity.overrideState = true;
            filmGrain.intensity.value = 0.12f;
            filmGrain.response.overrideState = true;
            filmGrain.response.value = 0.8f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>
        /// The sun's screen-space flare: a bright core, a soft surrounding
        /// halo, and two small off-axis ghosts. All four are procedural SDF
        /// shapes (Circle/Ring), not textures -- there is no flare art in
        /// this project, and URP's own procedural shapes already read as a
        /// convincing disc/ghost without needing any. modulateByLightColor
        /// on the core and halo ties them into SkyCycle's own sun colour for
        /// free, so the flare goes warm at dawn/dusk and white at noon along
        /// with everything else, with no per-frame code needed here.
        /// </summary>
        internal static LensFlareDataSRP EnsureSunLensFlareData()
        {
            string path = $"{MaterialDirectory}/SunLensFlare.asset";
            var data = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<LensFlareDataSRP>();
                AssetDatabase.CreateAsset(data, path);
            }

            var core = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle,
                position = 0.0f,
                localIntensity = 1.0f,
                uniformScale = 0.25f,
                fallOff = 0.6f,
                tint = new Color(1.0f, 0.95f, 0.85f),
                modulateByLightColor = true,
                blendMode = SRPLensFlareBlendMode.Additive,
            };
            var halo = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle,
                position = 0.0f,
                localIntensity = 0.35f,
                uniformScale = 1.4f,
                fallOff = 1.0f,
                tint = new Color(1.0f, 0.9f, 0.75f),
                modulateByLightColor = true,
                blendMode = SRPLensFlareBlendMode.Additive,
            };
            var ghost1 = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Circle,
                position = 0.6f,
                localIntensity = 0.18f,
                uniformScale = 0.12f,
                fallOff = 0.5f,
                tint = new Color(0.7f, 0.85f, 1.0f),
                blendMode = SRPLensFlareBlendMode.Additive,
            };
            var ghost2 = new LensFlareDataElementSRP
            {
                flareType = SRPLensFlareType.Ring,
                position = -0.4f,
                localIntensity = 0.15f,
                uniformScale = 0.2f,
                ringThickness = 0.35f,
                fallOff = 0.4f,
                tint = new Color(0.85f, 0.75f, 1.0f),
                blendMode = SRPLensFlareBlendMode.Additive,
            };
            data.elements = new[] { core, halo, ghost1, ghost2 };

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
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
            // A soft vertical streak, not a flat solid-alpha rectangle --
            // EXPLICIT ASK ("make it look reallllly good"). Without this the
            // stretched quad has hard rectangular edges and reads as a
            // little grey bar falling rather than as a drop of rain.
            Texture2D streak = EnsureRainStreakTexture();
            material.SetTexture("_BaseMap", streak);
            material.mainTexture = streak;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A soft vertical gradient -- bright along the centre of the
        /// stretch, fading to nothing at both ends and at the side edges.
        /// Same Texture2D/EncodeToPNG/AssetDatabase.ImportAsset technique
        /// FlamethrowerBuilder.BuildParticleTexture already establishes for
        /// a radial dot, adapted here for an elongated streak instead, built
        /// fresh rather than shared cross-assembly since this file already
        /// builds its own rain material independently.
        /// </summary>
        private static Texture2D EnsureRainStreakTexture()
        {
            string path = $"{MaterialDirectory}/RaindropStreak.png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            const int width = 16;
            const int height = 64;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = (y + 0.5f) / height;
                // 1 at the centre, 0 at both ends -- a softer taper than
                // linear so the streak keeps most of its length looking
                // solid rather than fading immediately off both tips.
                float lengthFade = Mathf.Clamp01(1.0f - Mathf.Pow(Mathf.Abs(t - 0.5f) * 2.0f, 2.2f));
                for (int x = 0; x < width; x++)
                {
                    float xt = (x + 0.5f) / width;
                    // A flat-topped profile across the width: a drop is only a
                    // couple of centimetres wide, and a peaked one leaves so
                    // little bright core that it is sub-pixel at any distance.
                    float widthFade = Mathf.Clamp01(1.0f - Mathf.Pow(Mathf.Abs(xt - 0.5f) * 2.0f, 4.0f));
                    pixels[y * width + x] = new Color(1.0f, 1.0f, 1.0f, lengthFade * widthFade);
                }
            }
            return SaveGeneratedTexture(pixels, width, height, path);
        }

        /// <summary>A soft radial dot -- the same shape a splash burst needs, just its own asset from the rain streak.</summary>
        private static Texture2D EnsureSplashTexture()
        {
            string path = $"{MaterialDirectory}/Splash.png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            const int size = 32;
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxDist = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / maxDist;
                    float alpha = Mathf.Pow(Mathf.Clamp01(1.0f - dist), 1.6f);
                    pixels[y * size + x] = new Color(1.0f, 1.0f, 1.0f, alpha);
                }
            }
            return SaveGeneratedTexture(pixels, size, size, path);
        }

        /// <summary>Shared write-to-disk/import step for EnsureRainStreakTexture/EnsureSplashTexture.</summary>
        private static Texture2D SaveGeneratedTexture(Color32[] pixels, int width, int height, string assetPath)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            string directory = Path.GetDirectoryName(assetPath);
            if (!AssetDatabase.IsValidFolder(directory))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetParent(Application.dataPath).FullName, directory));
            }
            File.WriteAllBytes(
                Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath), png);
            AssetDatabase.ImportAsset(assetPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        /// <summary>The ground-splash burst material -- same unlit/transparent recipe as EnsureRainMaterial, its own texture and colour.</summary>
        private static Material EnsureSplashMaterial()
        {
            string path = $"{MaterialDirectory}/Splash.mat";
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
                    Debug.LogError("CityX: no URP unlit shader -- splashes will render magenta.");
                    return null;
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f);
            material.SetFloat("_ZWrite", 0.0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetColor("_BaseColor", RainEffect.SplashColour);
            material.color = RainEffect.SplashColour;
            Texture2D dot = EnsureSplashTexture();
            material.SetTexture("_BaseMap", dot);
            material.mainTexture = dot;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// A thin soft ring, bright along one radius and fading either side:
        /// the shape of a ripple spreading on standing water. 64px is plenty
        /// for something a metre wide and gone in under a second.
        /// </summary>
        private static Texture2D EnsureRippleTexture()
        {
            string path = $"{MaterialDirectory}/Ripple.png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            const int size = 64;
            var pixels = new Color32[size * size];
            Vector2 centre = new Vector2(size * 0.5f, size * 0.5f);
            float maxDistance = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) / maxDistance;
                    // A gaussian ring at 0.78 of the radius, with the very rim
                    // faded out so it never shows the quad's own edge.
                    float ring = Mathf.Exp(-Mathf.Pow((d - 0.78f) / 0.13f, 2.0f));
                    float rim = Mathf.Clamp01((1.0f - d) / 0.12f);
                    pixels[y * size + x] = new Color(1.0f, 1.0f, 1.0f, ring * rim);
                }
            }
            return SaveGeneratedTexture(pixels, size, size, path);
        }

        /// <summary>The ripple ring material -- the same unlit/transparent recipe as the splash, its own ring texture, white so the particle colour tints it.</summary>
        private static Material EnsureRippleMaterial()
        {
            string path = $"{MaterialDirectory}/Ripple.mat";
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
                    Debug.LogError("CityX: no URP unlit shader -- ripples will render magenta.");
                    return null;
                }
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f);
            material.SetFloat("_ZWrite", 0.0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetColor("_BaseColor", Color.white);
            material.color = Color.white;
            Texture2D ring = EnsureRippleTexture();
            material.SetTexture("_BaseMap", ring);
            material.mainTexture = ring;
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

            // Intact glass (dark and lit) is the glass shader: tint per building, a kind of light per window, blinds. Cracked glass stays
            // plain frosted Lit, so a window somebody has hit reads as damaged against its neighbours.
            if (!cracked)
            {
                Shader glass = Shader.Find("CityX/Glass");
                if (glass == null)
                {
                    Debug.LogError("CityX: shader CityX/Glass not found -- did it fail to compile?");
                }
                else
                {
                    material.shader = glass;
                    material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    material.SetColor("_BaseColor", new Color(0.055f, 0.062f, 0.075f, 0.72f));
                    material.SetFloat("_Smoothness", 0.86f);
                    // Black; DayNightCycle raises it as the light goes, so the windows come on at dusk.
                    material.SetColor("_EmissionColor", Color.black);
                    EditorUtility.SetDirty(material);
                    return material;
                }
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

        /// <summary>
        /// The real crack-texture decal a shot's own impact point gets --
        /// EXPLICIT ASK: replacing the plain square hole with the user's own
        /// reference PNG (radial cracks with a real see-through centre),
        /// checked into Assets/CityX/View/Textures/BulletHoleCrack.png
        /// rather than procedurally generated like every other texture in
        /// this file -- a hand-authored photo reference has no "regenerate
        /// from code" equivalent the way a tiling facade pattern does.
        ///
        /// SAME TRANSPARENT BLEND SETTINGS AS EnsureGlassMaterial, because
        /// this decal sits directly on top of that same Cracked glass and
        /// has to composite over it the same way. White base colour so the
        /// texture's own colours/alpha show through unmodified.
        /// </summary>
        /// <summary>
        /// One-off cleanup of BulletHoleCrack.png. EXPLICIT BUG REPORT: "the bullet holes have a visible square
        /// around them due to the image i used."
        ///
        /// Measured directly (a rendered pane WITH the decal against the identical pane without it, pixel-
        /// diffed): the difference was not a hard rectangle from a shader or compression problem -- those stayed
        /// nearly identical whether the texture was compressed or not, alpha-clipped or not. The image itself
        /// has a scattered, low-level alpha NOISE FLOOR across almost the entire 1024x856 canvas (a real
        /// measured sample: only 0.16% of pixels were exactly alpha=0 at the far corners, and non-zero alpha
        /// pixels appear as far as 2px from every edge) -- not confined to the crack's own radiating lines.
        /// Composited onto glass at the decal's real size (34x28cm on a much smaller pane), that faint,
        /// widely-spread grain reads as a soft, roughly rectangular tint rather than individual specks.
        ///
        /// THE FIX IS TO THE ASSET, NOT THE SHADER: keep only the crack's own connected shape and zero every
        /// other pixel's alpha. A real fracture radiates as ONE connected pattern from the impact point, so
        /// anything that does not touch that shape is capture noise/artifact, not intended crack detail --
        /// dilating the candidate mask by a couple of pixels before labelling (then intersecting back with the
        /// real mask) bridges ordinary anti-aliasing gaps in thin crack lines without pulling in anything that
        /// was not already part of the shape.
        /// </summary>
        [MenuItem("CityX/Clean Bullet Hole Decal Texture")]
        public static void CleanBulletHoleDecalTexture()
        {
            const string texturePath = "Assets/CityX/View/Textures/BulletHoleCrack.png";
            const byte AlphaFloor = 24;      // ~9%: comfortably above the measured noise floor, comfortably below the crack's own body
            const int DilateRadius = 2;      // bridges anti-aliasing gaps in thin crack lines for CONNECTIVITY only

            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"CityX: {texturePath} missing -- nothing to clean.");
                return;
            }

            bool wasReadable = importer.isReadable;
            importer.isReadable = true;
            importer.SaveAndReimport();

            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            int width = source.width, height = source.height;
            Color32[] pixels = source.GetPixels32();   // row 0 is the BOTTOM of the image, same convention throughout

            var candidate = new bool[width * height];
            int candidates = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > AlphaFloor)
                {
                    candidate[i] = true;
                    candidates++;
                }
            }

            // Dilate the candidate mask so a thin line's own anti-aliased single-pixel gaps do not split it into
            // several components -- used only to decide what is CONNECTED, not which pixels survive.
            var dilated = (bool[])candidate.Clone();
            for (int pass = 0; pass < DilateRadius; pass++)
            {
                var next = (bool[])dilated.Clone();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        if (dilated[i])
                        {
                            continue;
                        }
                        for (int dy = -1; dy <= 1 && !next[i]; dy++)
                        {
                            int ny = y + dy;
                            if (ny < 0 || ny >= height) continue;
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                if (nx < 0 || nx >= width) continue;
                                if (dilated[ny * width + nx]) { next[i] = true; break; }
                            }
                        }
                    }
                }
                dilated = next;
            }

            // Label connected components of the DILATED mask (8-connectivity, flood fill via an explicit queue --
            // ~876k pixels is nowhere near enough to need anything cleverer).
            var label = new int[width * height];
            int biggestLabel = -1, biggestSize = 0, nextLabel = 0;
            var queue = new Queue<int>();
            for (int start = 0; start < dilated.Length; start++)
            {
                if (!dilated[start] || label[start] != 0)
                {
                    continue;
                }
                nextLabel++;
                int size = 0;
                queue.Enqueue(start);
                label[start] = nextLabel;
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    size++;
                    int x = i % width, y = i / width;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= height) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= width) continue;
                            int ni = ny * width + nx;
                            if (dilated[ni] && label[ni] == 0)
                            {
                                label[ni] = nextLabel;
                                queue.Enqueue(ni);
                            }
                        }
                    }
                }
                if (size > biggestSize)
                {
                    biggestSize = size;
                    biggestLabel = nextLabel;
                }
            }

            int cleared = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                // A pixel survives only if it was a real candidate itself (never promote a dilated-only pixel)
                // AND its component (found via the dilated mask) is the biggest one in the image.
                bool keep = candidate[i] && label[i] == biggestLabel;
                byte alpha = keep ? pixels[i].a : (byte)0;
                if (alpha == 0)
                {
                    if (pixels[i].a != 0)
                    {
                        cleared++;
                    }
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // PREMULTIPLIED: RGB scaled by this pixel's own alpha. Without this, anything that reads the
                // colour channel WITHOUT going through the alpha-blend stage -- an emissive pass sampling this
                // same texture so the decal stays visible at night, see EnsureCrackDecalMaterial -- sees the
                // crack graphic's own solid white fill regardless of alpha and lights the WHOLE quad, which is
                // exactly the visible-square regression this asset used to (still) have once emission was added.
                float a = alpha / 255.0f;
                pixels[i] = new Color32((byte)(pixels[i].r * a), (byte)(pixels[i].g * a), (byte)(pixels[i].b * a), alpha);
            }

            var cleaned = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cleaned.SetPixels32(pixels);
            cleaned.Apply();
            File.WriteAllBytes(Path.GetFullPath(texturePath), cleaned.EncodeToPNG());
            Object.DestroyImmediate(cleaned);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            importer.isReadable = wasReadable;
            importer.SaveAndReimport();

            Debug.Log($"CityX: cleaned {texturePath} -- kept the largest connected shape ({biggestSize} px, " +
                      $"{biggestSize * 100.0f / candidates:0.0}% of {candidates} candidate pixels), cleared {cleared} stray pixels.");
        }

        private static Material EnsureCrackDecalMaterial()
        {
            const string texturePath = "Assets/CityX/View/Textures/BulletHoleCrack.png";
            string path = $"{MaterialDirectory}/BulletHoleDecal.mat";

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogError($"CityX: {texturePath} missing -- bullet holes will show as blank quads.");
            }
            else if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
            {
                // CLAMP, NOT REPEAT -- this quad shows the WHOLE image once,
                // never tiled, and Repeat would smear the crack's own edge
                // pixels across the opposite side under any filtering.
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;

                // Uncompressed, and alpha-clipped below -- belt and braces against any residual softness in the
                // image (compression or a future re-export) ever blending a faint tint instead of being fully
                // discarded. Measured to make no visible difference on their own (see the doc comment above),
                // but they cost nothing and rule the texture itself back out for certain.
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("CityX: URP Lit shader not found -- is the Universal Render " +
                               "Pipeline asset assigned in Graphics settings?");
                lit = Shader.Find("Standard");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && material.shader != lit)
            {
                // An asset from a previous, briefly-tried Unlit version of this material: Unity does not carry
                // properties across a shader swap, so it is reset below exactly as if newly created.
                material.shader = lit;
            }
            if (material == null)
            {
                material = new Material(lit);
                AssetDatabase.CreateAsset(material, path);
            }

            // THE SAME TRANSPARENCY RECIPE AS THE WORKING GLASS MATERIALS (CityGlassLit/Dark/Cracked), COPIED
            // VERBATIM RATHER THAN WORKED OUT BY HAND. This decal's own first attempt at "Alpha" transparency
            // used the textbook SrcAlpha/OneMinusSrcAlpha blend factors, which is wrong for THIS shader: measured
            // directly (dumping the .mat after a rebuild), _DstBlend came back as 10, not the 6 the call asked
            // for -- this URP version's own Lit shader validates its blend state against its OWN premultiplied-
            // alpha convention (_ALPHAPREMULTIPLY_ON, SrcBlend=One) and silently corrects a mismatched DstBlend,
            // which is what made the "transparent" background of the quad render as a solid, plainly visible
            // rectangle instead of see-through. Every other transparent material already shipped in this project
            // uses this exact combination and renders correctly; this is that same combination.
            material.SetFloat("_Surface", 1.0f);              // transparent
            material.SetFloat("_Blend", 0.0f);                // alpha
            material.SetFloat("_AlphaClip", 0.0f);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetFloat("_Cull", 0.0f);                 // both sides -- seen from inside the building too
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", 10);

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.0f);
            material.SetFloat("_Metallic", 0.0f);

            // EMISSIVE -- THE ACTUAL FIX FOR THE REPORT. Found by rendering the same pane by day and by night:
            // by day this lit decal (plain white crack lines) was bright enough against the glass to hide the
            // plain rectangle cut into the mesh underneath it (AddPaneWithHoles' own real geometric opening,
            // sized off this texture's own centre -- see its own doc comment); by night, with little ambient
            // light left to catch, the same lit material went almost black, and what remained visible was that
            // bare rectangle -- a flat, hard-edged square, exactly what was reported. A crack decal is a sticker
            // on the glass, not a surface that should darken with the room around it: emission keeps it reading
            // the same crack pattern at a fixed brightness whatever the time of day, so the rectangle underneath
            // is never left uncovered.
            material.SetTexture("_EmissionMap", texture);
            material.SetColor("_EmissionColor", Color.white);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

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
