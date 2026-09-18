using System.Collections.Generic;
using System.Globalization;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Net;
using CityX.Vehicles;
using CityX.View;
using CityX.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The development console: backtick opens it, type a command, Enter runs
    /// it. Ported from ui/dev_console.gd.
    ///
    /// DEVELOPMENT ONLY, and gated the way Godot gates it -- on a debug build,
    /// checked where the thing is created rather than here, so there is no way
    /// to reach it in a release at all.
    ///
    /// WHAT A COMMAND MEANS LIVES IN <see cref="DevCommands"/>, not here. This
    /// is a text box and a dispatch table; the parsing and the time-of-day
    /// names are the parts that can be quietly wrong, and they are the parts a
    /// test can reach.
    ///
    /// ONLY COMMANDS WHOSE SYSTEMS EXIST. Godot's console can spawn a dozen
    /// weapons, NPCs and wearables that this port has not reached yet. Listing
    /// them here as verbs that do nothing would be worse than not listing them
    /// -- so they arrive with the systems they drive.
    /// </summary>
    public sealed class DevConsoleView : MonoBehaviour
    {
        [Tooltip("The whole panel, hidden until the console is opened.")]
        public GameObject Panel;

        [Tooltip("Where typed commands go.")]
        public InputField Input;

        [Tooltip("Where their answers go.")]
        public Text Output;

        [Tooltip("The rain, toggled by /weather.")]
        public RainEffect Rain;

        [Tooltip("The clock, jumped by /time.")]
        public DayNightCycle Cycle;

        [Tooltip("The body /xp, /infinitehealth and /infinitesprint act on.")]
        public PlayerVitals Vitals;

        [Tooltip("The free camera /fly turns on.")]
        public FlyCamera Fly;

        [Tooltip("What /spawn civilian puts down. A dummy is built from a capsule instead.")]
        public GameObject CivilianPrefab;

        [Tooltip("What /spawn flyer puts down -- a white player-model NPC that flies around with a jetpack.")]
        public GameObject FlyingNpcPrefab;

        [Tooltip("What /spawn crimsonclaw puts down -- a hostile hovering claw monster.")]
        public GameObject CrimsonClawPrefab;

        [Tooltip("What /spawn radio puts down.")]
        public GameObject RadioPrefab;

        [Tooltip("What /spawn bottle puts down, and what /equip bottle equips.")]
        public GameObject BottlePrefab;

        [Tooltip("What /spawn jeans puts down, and what /equip jeans equips.")]
        public GameObject SkelliJeansPrefab;

        [Tooltip("What /spawn cloak puts down, and what /equip cloak equips.")]
        public GameObject HoodedCloakPrefab;

        [Tooltip("What /spawn grapple puts down, and what /equip grapple equips.")]
        public GameObject GrapplingHookPrefab;
        public GameObject PistolPrefab;
        public GameObject FlamethrowerPrefab;

        [Tooltip("What /spawn coat puts down, and what /equip coat equips.")]
        public GameObject MidnightPatchworkCoatPrefab;

        [Tooltip("What /spawn jetpack puts down, and what /equip jetpack equips.")]
        public GameObject JetpackPrefab;

        [Tooltip("What /spawn parachute puts down, and what /equip parachute equips.")]
        public GameObject ParachutePrefab;

        [Tooltip("What /spawn sword puts down, and what /equip sword equips.")]
        public GameObject SwordPrefab;
        public GameObject TopHatPrefab;
        public GameObject FarmerHatPrefab;

        [Tooltip("What /spawn mustang puts down.")]
        public GameObject MustangPrefab;

        [Tooltip("Movement/combat input. Disabled while the console is open.")]
        public PlayerInputReader InputReader;

        [Tooltip("Mouse-look. Disabled while the console is open -- otherwise typing WASD into a command also walks the player.")]
        public ThirdPersonCamera OrbitCamera;

        [Tooltip("The grapple swing. While a rope is attached it owns the player's Animator via its own procedural swing pose -- closing the console must not hand it back mid-swing.")]
        public GrappleSwing Swing;

        [Tooltip("What /goto streams real chunks in around the destination before moving the player there.")]
        public CityStreamer Streamer;

        /// <summary>How far in front of the player something spawns.</summary>
        public const float SpawnDistance = 3.0f;

        private readonly List<string> _lines = new List<string>();
        private Shader _litShader;
        private Shader _toonShader;
        private bool _celShadeOn;
        private PlayerLocomotion _playerLocomotion;
        private NetworkBootstrap _bootstrap;
        private bool _pausedGameOnOpen;
        private float _timeScaleBeforeOpen = 1.0f;

        public bool IsOpen => Panel != null && Panel.activeSelf;

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// Public and idempotent, callable by hand in EditMode tests where
        /// Awake never runs -- the same idiom PlayerLocomotion.Bind/
        /// GrappleSwing.Bind already use for the exact same reason. Found
        /// here rather than wired through DevConsoleBuilder's own
        /// already-long parameter list -- Vitals is already wired, and the
        /// watchdog hook below only needs finding at real runtime, not at
        /// scene-build time (see that hook's own doc comment).
        /// </summary>
        public void Bind()
        {
            _playerLocomotion = Vitals != null ? Vitals.GetComponent<PlayerLocomotion>() : null;
            _bootstrap = FindAnyObjectByType<NetworkBootstrap>();

            // REAL RUNTIME WIRING, not a scene-build-time one -- EXPLICIT BUG,
            // found chasing the long-running grapple/console distortion
            // report via PoseDistortionWatchdog's own log: every recorded
            // entry said "no console hook wired," even ones from real play.
            // CityMassingSceneBuilder used to assign this exact delegate
            // (`watchdog.IsConsoleOpen = () => console.IsOpen`) while BUILDING
            // the scene in batch mode -- but a System.Func<bool> field is
            // never Unity-serializable, so that assignment could not survive
            // the scene actually being saved to disk and reopened for real
            // play; it only ever existed for the single in-memory instant the
            // builder script held both objects. Every real session since has
            // silently run with this hook permanently null, which means the
            // watchdog's own "is the console open" column has never once
            // been true evidence either way for the exact bug it exists to
            // chase down. Bind() runs for real every time this component
            // actually wakes up (Awake, not a batch build step), so the
            // closure it creates here lives in real memory for the whole
            // session instead of dying the moment the scene is saved.
            PoseDistortionWatchdog watchdog =
                Vitals != null ? Vitals.GetComponent<PoseDistortionWatchdog>() : null;
            if (watchdog != null)
            {
                watchdog.IsConsoleOpen = () => IsOpen;
            }

            Close();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // READ HERE RATHER THAN THROUGH THE INPUT FIELD, so the toggle still
            // works while the field has focus. A focused text box would
            // otherwise swallow the backtick as a typed character and the
            // console could be opened but never closed.
            if (keyboard.backquoteKey.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
                return;
            }

            if (IsOpen && keyboard.enterKey.wasPressedThisFrame && Input != null)
            {
                Submit(Input.text);
                Input.text = string.Empty;
                Input.ActivateInputField();
            }
        }

        public void Open()
        {
            if (Panel != null)
            {
                Panel.SetActive(true);
            }
            if (Input != null)
            {
                Input.text = string.Empty;
                Input.ActivateInputField();
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (InputReader != null) InputReader.enabled = false;
            if (OrbitCamera != null) OrbitCamera.enabled = false;

            // THE ANIMATOR IS NO LONGER DISABLED HERE -- EXPLICIT ASK, after
            // seven rounds of trying to make freezing it SAFE (forced
            // resyncs, rate-based snap suppression, ownership checks, an
            // execution-order fix) never fully worked: "remove the console
            // function and rebuild it entirely if you have to, just get rid
            // of it." The freeze was never load-bearing -- it existed purely
            // to stop the character visibly looping whatever animation was
            // playing (running in place) while the console stayed open, a
            // cosmetic nicety. Every version of "freeze the pose" carried
            // the same structural risk: Mecanim can occasionally produce an
            // implausible one-frame pose during an ordinary blend-tree
            // transition (confirmed directly, see PlayerLocomotion.
            // SuppressImplausibleArmSnaps's own doc comment), and freezing
            // the Animator on that exact frame makes an otherwise invisible,
            // self-correcting glitch visible and PERMANENT for as long as
            // the console stays open. Simply never freezing it removes the
            // entire class of bug at the root: the Animator keeps evaluating
            // normally the whole time the console is open (the character
            // keeps playing whatever it was doing -- run-in-place if they
            // were running, which is a look, not a bug), so there is no
            // frozen instant for a bad transitional frame to ever get stuck
            // on. GrappleSwing's own ownership of these bones during an
            // active swing is untouched by this change -- it already
            // disables the Animator itself for its own, unrelated reason
            // (needing exclusive control of the bones for its procedural
            // sway), so a console opened mid-swing still shows whatever the
            // swing itself is doing, exactly as before.

            // EXPLICIT BUG REPORT: "pressing console mid jump plays the
            // rest of the jump animation after the console freezes the
            // rest of the game" -- a real, if cosmetic, consequence of the
            // Animator being left live above: nothing calls PlayerLocomotion.
            // Tick any more once InputReader is disabled, so whatever
            // action was already playing keeps running to its own natural
            // end while everything else (movement, physics) is genuinely
            // paused, reading as visibly out of sync. See SettleAnimatorToIdle's
            // own doc comment for why forcing idle parameters here is a
            // safe, ordinary Mecanim transition, not a repeat of the same
            // freeze-on-a-bad-frame risk this whole file's history is
            // about. Harmless to call even mid-swing (GrappleSwing already
            // has the Animator disabled then, so nothing re-evaluates
            // these parameters until it hands control back).
            _playerLocomotion?.SettleAnimatorToIdle();

            // SINGLEPLAYER ONLY PAUSES REAL TIME -- EXPLICIT ASK. Called
            // AFTER SettleAnimatorToIdle on purpose: this frame's Animator
            // update (which runs after Update() returns, using the
            // deltaTime this frame already locked in before timeScale
            // changed) still takes one small, ordinary step toward idle,
            // and only the FOLLOWING frame actually freezes at deltaTime
            // zero -- so the game pauses on a clean, normally-computed
            // pose, not an arbitrary mid-transition snapshot. That
            // distinction is the entire lesson of this file's own
            // multi-week distortion saga: freezing the ANIMATOR
            // (`enabled = false`) on an arbitrary frame was the bug;
            // freezing TIME while the Animator keeps evaluating normally
            // is a completely different, safe mechanism -- Mecanim is
            // never disabled or bypassed, it simply stops being given any
            // more deltaTime to advance with.
            //
            // Time.timeScale governs this WHOLE PROCESS, not just the
            // local view -- in Singleplayer that process is exactly one
            // player's own private simulation, so pausing it is invisible
            // to anyone else by construction (there is no one else).
            // Every other GameMode either runs other real players'
            // simulation in this same process (HostedWorld) or has no
            // standing to freeze somebody else's server at all
            // (RemoteClient, DedicatedShard) -- pausing there would
            // silently freeze other people's game from a debug console
            // they cannot even see. _bootstrap == null (no networking
            // wired at all, e.g. most tests) is treated as Singleplayer,
            // matching every other null-bootstrap fallback in this
            // codebase (see NetworkedPlayerSpawner's own check).
            _pausedGameOnOpen = _bootstrap == null || _bootstrap.Mode == GameMode.Singleplayer;
            if (_pausedGameOnOpen)
            {
                _timeScaleBeforeOpen = Time.timeScale;
                Time.timeScale = 0.0f;
            }
        }

        public void Close()
        {
            if (Panel != null)
            {
                Panel.SetActive(false);
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (InputReader != null) InputReader.enabled = true;
            if (OrbitCamera != null) OrbitCamera.enabled = true;

            // NOTHING TO DO WITH _playerAnimator HERE ANY MORE -- Open() no
            // longer disables it (see Open's own doc comment), so there is
            // nothing for Close() to undo. If the Animator happens to be
            // disabled right now for an unrelated, still-active reason
            // (Ragdoll.Activate on death, or GrappleSwing.Attach owning the
            // bones for an active swing), it stays exactly as those systems
            // left it -- correctly untouched, the same as before.

            // Restores whatever timeScale actually was before this Open(),
            // not a hardcoded 1.0 -- and only when THIS console is the one
            // that paused it. If something else already had time frozen
            // (nothing does today, but PauseMenuView's own Escape pause
            // uses this same mechanism) that state is left exactly as it
            // was rather than being stomped by an unrelated Close().
            if (_pausedGameOnOpen)
            {
                Time.timeScale = _timeScaleBeforeOpen;
                _pausedGameOnOpen = false;
            }
        }

        public void Submit(string text)
        {
            DevCommands.Line line = DevCommands.Parse(text);
            if (line.IsEmpty)
            {
                return;
            }

            Log("> " + text.Trim());
            Run(line);
        }

        private void Run(DevCommands.Line line)
        {
            switch (line.Verb)
            {
                case "help":
                    foreach (string verb in DevCommands.Verbs)
                    {
                        Log(DevCommands.UsageFor(verb));
                    }
                    return;

                case "weather":
                    Weather(line.Argument(0));
                    return;

                case "time":
                    Time_(line.Argument(0));
                    return;

                case "fly":
                    Flying();
                    return;

                case "infinitehealth":
                    if (Vitals == null)
                    {
                        Log("no body here");
                        return;
                    }
                    Vitals.InfiniteHealth = !Vitals.InfiniteHealth;
                    Log("infinite health " + (Vitals.InfiniteHealth ? "on" : "off"));
                    return;

                case "infinitesprint":
                    if (Vitals == null)
                    {
                        Log("no body here");
                        return;
                    }
                    Vitals.InfiniteStamina = !Vitals.InfiniteStamina;
                    Log("infinite sprint " + (Vitals.InfiniteStamina ? "on" : "off"));
                    return;

                case "xp":
                    Experience(line.Argument(0));
                    return;

                case "spawn":
                    Spawn(line.Argument(0));
                    return;

                case "car":
                    CarCommand(line.Argument(0));
                    return;

                case "celshade":
                    ToggleCelShade();
                    return;

                case "equip":
                    Equip(line.Argument(0));
                    return;

                case "unequip":
                    Unequip(line.Argument(0));
                    return;

                case "goto":
                    Goto(line.Argument(0));
                    return;

                default:
                    Log("unknown command: " + line.Verb + "   (try /help)");
                    return;
            }
        }

        private void Weather(string which)
        {
            if (Rain == null)
            {
                Log("no rain in this scene");
                return;
            }
            switch (which)
            {
                case "rain":
                    Rain.SetActive(true);
                    Log("raining");
                    return;
                case "clear":
                    Rain.SetActive(false);
                    Log("clear");
                    return;
                default:
                    Log(DevCommands.UsageFor("weather"));
                    return;
            }
        }

        private void Time_(string which)
        {
            if (Cycle == null)
            {
                Log("no day/night cycle in this scene");
                return;
            }
            if (which == null || !DevCommands.TryTimeOfDay(
                    which, SkyCycle.DaySeconds, SkyCycle.CycleSeconds, out float seconds))
            {
                Log(DevCommands.UsageFor("time"));
                return;
            }

            Cycle.TimeOfDay = seconds;
            // Applied now rather than next frame, so the console reports a
            // state that is already on screen.
            Cycle.Apply(SkyCycle.At(seconds));
            Log($"time set to {which} -- {seconds / 60.0f:0.0} min into the " +
                $"{SkyCycle.CycleSeconds / 60.0f:0} min cycle");
        }

        private void Flying()
        {
            if (Fly == null)
            {
                Log("no free camera in this scene");
                return;
            }
            Fly.enabled = !Fly.enabled;
            Log("fly " + (Fly.enabled ? "on" : "off"));
        }

        /// <summary>
        /// Teleports to the nearest real example of a generated feature --
        /// EXPLICIT ASK, after repeated reports of "there are no sloped
        /// roads" that kept turning out to mean "I have not walked to one
        /// yet": rather than hand out chunk coordinates and hope, jump the
        /// player there directly so the feature can be checked on the spot.
        /// </summary>
        private void Goto(string what)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            if (Streamer == null)
            {
                Log("no city streamer in this scene");
                return;
            }
            if (what != "slope" && what != "district")
            {
                Log(DevCommands.UsageFor("goto"));
                return;
            }

            Vector3 from = Vitals.transform.position;
            int originX = Mathf.FloorToInt(from.x / CityLayout.ChunkSize);
            int originY = Mathf.FloorToInt(from.z / CityLayout.ChunkSize);
            var world = new CityWorld(Streamer.WorldSeed);

            Vector3 destination;
            string found;
            if (what == "slope")
            {
                if (!TryFindNearestSlope(world, originX, originY, out destination, out int foundX, out int foundY))
                {
                    Log("no sloped street found within a reasonable radius -- try again from somewhere else");
                    return;
                }
                found = $"sloped street at chunk ({foundX},{foundY})";
            }
            else
            {
                destination = NearestDistrictBoundary(world, originX, originY, out int arterialX, out int arterialY);
                found = $"district boundary near chunk ({arterialX},{arterialY})";
            }

            Streamer.StreamAround(destination, 800);
            Physics.SyncTransforms();

            CharacterController controller = Vitals.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.enabled = false;
            }
            Vitals.transform.position = destination;
            if (controller != null)
            {
                controller.enabled = true;
            }
            Physics.SyncTransforms();

            Log($"teleported to the nearest {found}");
        }

        /// <summary>
        /// Mirrors AddStreetEdgeFeatures's own roll exactly (CityMeshBuilder.cs)
        /// -- seeded off the neighbour's coordinate -- so this finds a street
        /// edge that will actually render as a sloped ramp, not merely a
        /// terrace drop that happened to keep its stairs.
        /// </summary>
        private static bool TryFindNearestSlope(CityWorld world, int originX, int originY,
            out Vector3 destination, out int foundX, out int foundY)
        {
            const int SearchRadius = 60;
            double bestDistance = double.MaxValue;
            foundX = 0;
            foundY = 0;
            bool found = false;

            for (int dx = -SearchRadius; dx <= SearchRadius; dx++)
            {
                for (int dy = -SearchRadius; dy <= SearchRadius; dy++)
                {
                    int x = originX + dx;
                    int y = originY + dy;
                    if (!CityLayout.IsStreetChunk(x, y))
                    {
                        continue;
                    }
                    float pad = world.Terrain.PadHeightFor(x, y);
                    foreach ((int nx, int ny) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                    {
                        if (!CityLayout.IsStreetChunk(nx, ny))
                        {
                            continue;
                        }
                        float neighbourPad = world.Terrain.PadHeightFor(nx, ny);
                        if (neighbourPad >= pad)
                        {
                            continue;
                        }
                        bool sloped = new DeterministicRandom(world.ChunkSeed(nx, ny)).Below(2) == 0;
                        if (!sloped)
                        {
                            continue;
                        }
                        double distance = (double)dx * dx + (double)dy * dy;
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            foundX = x;
                            foundY = y;
                            found = true;
                        }
                    }
                }
            }

            // ContinuousHeightAt, not foundPad -- foundX/foundY is always a
            // street chunk (the IsStreetChunk check above), whose actual
            // ground is tilted to the smooth continuous field now, not flat
            // at the quantised pad (see CityMeshBuilder.BuildGround's own
            // "hillside streets" comment). Teleporting to the old flat pad
            // height could land the player below the real road surface --
            // the same "load in and fall through the floor forever" bug
            // this command exists to help demonstrate, not reproduce.
            destination = found
                ? new Vector3((float)CityLayout.ChunkCentreX(foundX),
                    (float)world.Terrain.ContinuousHeightAt(
                        CityLayout.ChunkCentreX(foundX), CityLayout.ChunkCentreZ(foundY)) + 2.0f,
                    (float)CityLayout.ChunkCentreZ(foundY))
                : Vector3.zero;
            return found;
        }

        /// <summary>The nearest wide arterial street to (originX, originY), on whichever axis is closer.</summary>
        private static Vector3 NearestDistrictBoundary(CityWorld world, int originX, int originY,
            out int arterialX, out int arterialY)
        {
            int nearestX = Mathf.RoundToInt(originX / (float)CityLayout.DistrictChunks) * CityLayout.DistrictChunks;
            int nearestY = Mathf.RoundToInt(originY / (float)CityLayout.DistrictChunks) * CityLayout.DistrictChunks;

            if (Mathf.Abs(originX - nearestX) <= Mathf.Abs(originY - nearestY))
            {
                arterialX = nearestX;
                arterialY = originY;
            }
            else
            {
                arterialX = originX;
                arterialY = nearestY;
            }

            // ContinuousHeightAt, not PadHeightFor -- an arterial chunk is
            // always a street chunk (CityLayout.IsArterialChunk implies
            // IsStreetChunk), so the same "tilted, not flat" reasoning
            // TryFindNearestSlope's own destination just got applies here
            // too.
            double worldX = CityLayout.ChunkCentreX(arterialX);
            double worldZ = CityLayout.ChunkCentreZ(arterialY);
            float height = (float)world.Terrain.ContinuousHeightAt(worldX, worldZ);
            return new Vector3((float)worldX, height + 2.0f, (float)worldZ);
        }

        private void Experience(string amount)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            if (amount == null || !int.TryParse(amount, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int points) || points <= 0)
            {
                Log(DevCommands.UsageFor("xp"));
                return;
            }

            int levelBefore = Vitals.Level;
            Vitals.AddXp(points);
            int levelsGained = Vitals.Level - levelBefore;
            Log(levelsGained > 0
                ? $"+{points} xp -- +{levelsGained} level(s), now level {Vitals.Level} ({Vitals.StatPoints} point(s) to spend)"
                : $"+{points} xp -- {Vitals.Experience}/{Vitals.ExperienceToNextLevel} to next level");
        }

        private void Spawn(string what)
        {
            Transform origin = Vitals != null ? Vitals.transform : transform;
            Vector3 where = origin.position + origin.forward * SpawnDistance;
            Quaternion facing = Quaternion.LookRotation(-origin.forward, Vector3.up);

            switch (what)
            {
                case "civilian":
                    if (CivilianPrefab == null)
                    {
                        Log("no civilian prefab wired");
                        return;
                    }
                    Instantiate(CivilianPrefab, where, facing);
                    Log("spawned civilian");
                    return;

                case "flyer":
                    if (FlyingNpcPrefab == null)
                    {
                        Log("no flying npc prefab wired");
                        return;
                    }
                    // Airborne, not at ground level like every other /spawn
                    // case -- it is a flier, and dropping it on the street
                    // would just mean watching it climb to altitude before
                    // it does anything interesting.
                    Instantiate(FlyingNpcPrefab, where + Vector3.up * 30.0f, facing);
                    Log("spawned a flying npc");
                    return;

                case "crimsonclaw":
                    if (CrimsonClawPrefab == null)
                    {
                        Log("no crimson claw prefab wired");
                        return;
                    }
                    // A few metres up, like the flyer -- it hovers rather
                    // than falls, and HoverCorrect eases it down to its own
                    // ground-relative height the moment it ticks.
                    Instantiate(CrimsonClawPrefab, where + Vector3.up * 3.0f, facing);
                    Log("spawned a crimson claw -- it will chase and attack you");
                    return;

                case "radio":
                    if (RadioPrefab == null)
                    {
                        Log("no radio prefab wired");
                        return;
                    }
                    // Dropped a little above the ground so it settles onto it
                    // rather than starting intersecting -- it is a real body.
                    Instantiate(RadioPrefab, where + Vector3.up * 0.5f, facing);
                    Log("spawned radio -- press E to switch it on");
                    return;

                case "bottle":
                    if (BottlePrefab == null)
                    {
                        Log("no bottle prefab wired");
                        return;
                    }
                    Instantiate(BottlePrefab, where + Vector3.up * 0.1f, Quaternion.identity);
                    Log("spawned a glass bottle -- walk up, look at it and press E");
                    return;

                case "jeans":
                    if (SkelliJeansPrefab == null)
                    {
                        Log("no skelli jeans prefab wired");
                        return;
                    }
                    Instantiate(SkelliJeansPrefab, where + Vector3.up * 0.05f, Quaternion.identity);
                    Log("spawned skelli jeans -- press E nearby to pick them up");
                    return;

                case "cloak":
                    if (HoodedCloakPrefab == null)
                    {
                        Log("no hooded cloak prefab wired");
                        return;
                    }
                    Instantiate(HoodedCloakPrefab, where + Vector3.up * 0.05f, Quaternion.identity);
                    Log("spawned a hooded cloak -- press E nearby to pick it up");
                    return;

                case "coat":
                    if (MidnightPatchworkCoatPrefab == null)
                    {
                        Log("no midnight patchwork coat prefab wired");
                        return;
                    }
                    Instantiate(MidnightPatchworkCoatPrefab, where + Vector3.up * 0.05f, Quaternion.identity);
                    Log("spawned a midnight patchwork coat -- press E nearby to pick it up");
                    return;

                case "jetpack":
                    if (JetpackPrefab == null)
                    {
                        Log("no jetpack prefab wired");
                        return;
                    }
                    Instantiate(JetpackPrefab, where + Vector3.up * 0.05f, Quaternion.identity);
                    Log("spawned a jetpack -- press E nearby to pick it up");
                    return;

                case "parachute":
                    if (ParachutePrefab == null)
                    {
                        Log("no parachute prefab wired");
                        return;
                    }
                    Instantiate(ParachutePrefab, where + Vector3.up * 0.05f, Quaternion.identity);
                    Log("spawned a parachute -- press E nearby to pick it up");
                    return;

                case "sword":
                    if (SwordPrefab == null)
                    {
                        Log("no sword prefab wired");
                        return;
                    }
                    Instantiate(SwordPrefab, where + Vector3.up * 0.05f, Quaternion.identity);
                    Log("spawned a sword -- press E nearby to pick it up");
                    return;

                case "top_hat":
                case "tophat":
                    if (TopHatPrefab == null)
                    {
                        Log("no top hat prefab wired");
                        return;
                    }
                    Instantiate(TopHatPrefab, where + Vector3.up * 0.05f, Quaternion.identity);
                    Log("spawned a top hat -- press E nearby to pick it up");
                    return;

                case "farmer_hat":
                case "farmerhat":
                    if (FarmerHatPrefab == null)
                    {
                        Log("no farmer hat prefab wired");
                        return;
                    }
                    Instantiate(FarmerHatPrefab, where + Vector3.up * 0.05f, Quaternion.identity);
                    Log("spawned a farmer hat -- press E nearby to pick it up");
                    return;

                case "grapple":
                    if (GrapplingHookPrefab == null)
                    {
                        Log("no grappling hook prefab wired");
                        return;
                    }
                    Instantiate(GrapplingHookPrefab, where + Vector3.up * 0.1f, Quaternion.identity);
                    Log("spawned a grappling hook -- walk up, look at it and press E");
                    return;

                case "pistol":
                    if (PistolPrefab == null)
                    {
                        Log("no pistol prefab wired");
                        return;
                    }
                    Instantiate(PistolPrefab, where + Vector3.up * 0.1f, Quaternion.identity);
                    Log("spawned a pistol -- walk up, look at it and press E");
                    return;

                case "flamethrower":
                    if (FlamethrowerPrefab == null)
                    {
                        Log("no flamethrower prefab wired");
                        return;
                    }
                    Instantiate(FlamethrowerPrefab, where + Vector3.up * 0.1f, Quaternion.identity);
                    Log("spawned a flamethrower -- walk up, look at it and press E");
                    return;

                case "mustang":
                    if (MustangPrefab == null)
                    {
                        Log("no mustang prefab wired");
                        return;
                    }
                    // A bigger distance and drop height than every other
                    // /spawn target (6m and 1.2m, not SpawnDistance's own
                    // 3m at ground level) -- matches car.gd's own
                    // _spawn_mustang: a much bigger object needs more room,
                    // and it settles onto the ground via gravity rather
                    // than being placed exactly on it. Facing the SAME
                    // direction the player already faces, not back toward
                    // them like every other spawn -- a car is walked up to
                    // and entered from the side, not looked at like a
                    // picked-up prop.
                    Vector3 mustangWhere = origin.position + origin.forward * 6.0f + Vector3.up * 1.2f;
                    Quaternion mustangFacing = Quaternion.Euler(0.0f, origin.eulerAngles.y, 0.0f);
                    Instantiate(MustangPrefab, mustangWhere, mustangFacing);
                    Log("spawned a Mustang in front of you -- WASD to drive, Space to handbrake");
                    return;

                case "dummy":
                    // BUILT HERE RATHER THAN INSTANTIATED, because a sparring
                    // dummy has no prefab -- the scene builder makes its own out
                    // of a capsule for the same reason. It is a thing to punch,
                    // not a character.
                    GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    dummy.name = "SparringDummy";
                    dummy.transform.SetPositionAndRotation(
                        where + Vector3.up * 1.0f, facing);
                    dummy.AddComponent<SparringDummy>();
                    Log("spawned dummy");
                    return;

                case "book":
                    // BUILT HERE TOO, same reason as dummy -- Book has no
                    // prefab, CityStreamer.AddBooks builds one procedurally
                    // the same way, and Streamer is already wired here for
                    // /goto so its own BookMaterial needs no new field.
                    if (Streamer == null || Streamer.BookMaterial == null)
                    {
                        Log("no book material wired");
                        return;
                    }
                    GameObject book = new GameObject("Book");
                    book.transform.SetPositionAndRotation(where, facing);
                    book.AddComponent<Book>().Build(Streamer.BookMaterial);
                    Log("spawned a book -- press E to read it");
                    return;

                default:
                    Log(DevCommands.UsageFor("spawn"));
                    return;
            }
        }

        /// <summary>
        /// "/car pose" -- ported from dev_console.gd's own _car_command.
        /// Logs the nearest car's own DriverSeat/PassengerSeat local
        /// position/rotation so they can be nudged directly by dragging
        /// the two child objects in the Editor on Mustang.prefab (or on a
        /// spawned instance) and re-checking here, the same "cheap to
        /// correct once actually seen" workflow the Godot source's own
        /// comment describes for its own Remote-tab equivalent.
        /// </summary>
        private void CarCommand(string sub)
        {
            if (sub != "pose")
            {
                Log(DevCommands.UsageFor("car"));
                return;
            }

            Transform origin = Vitals != null ? Vitals.transform : transform;
            Car nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            foreach (Car candidate in FindObjectsByType<Car>(FindObjectsSortMode.None))
            {
                float distanceSquared = (candidate.transform.position - origin.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearest = candidate;
                }
            }
            if (nearest == null)
            {
                Log("no car nearby -- spawn one with /spawn mustang first");
                return;
            }

            Log($"DriverSeat local pos {FormatVector3(nearest.DriverSeat.localPosition)}");
            Log($"PassengerSeat local pos {FormatVector3(nearest.PassengerSeat.localPosition)}");
        }

        private static string FormatVector3(Vector3 v)
        {
            return $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
        }

        /// <summary>
        /// Swaps every eligible renderer's shader between the normal URP
        /// Lit and CityX/ToonLit -- EVERY ONE OF THEM, not just characters.
        /// Character skins and the whole city (ground, pavement, concrete,
        /// doors, guard rails) are built on the exact same "Universal
        /// Render Pipeline/Lit" shader asset (confirmed directly: every one
        /// of those .mat files points at the identical shader GUID), so
        /// asking "is this material currently Lit" is what "all the
        /// graphics" actually means here -- a name-based allowlist
        /// (formerly PlayerSkin/CivilianSkin only) would need updating by
        /// hand for every new material the city ever adds, and would have
        /// silently stayed character-only forever if nobody remembered to.
        ///
        /// OPAQUE SURFACES ONLY. Glass (CityGlassDark/Lit/Cracked) is built
        /// on this SAME shader with its Surface Type property set to
        /// Transparent rather than a different shader asset -- ToonLit has
        /// no Blend state at all (Tags declare it Opaque, "Queue" =
        /// "Geometry"), so toon-shading a window would not restyle it, it
        /// would make it suddenly solid, undoing the real work that went
        /// into making a building's glass see-through. _Surface (0 =
        /// Opaque, 1 = Transparent) is the same URP Lit property that
        /// decision is already stored in, so checking it is enough to skip
        /// every window without needing a name check for them either.
        ///
        /// WALKS LIVE RENDERERS, NOT A WIRED Material[] -- a material
        /// asset like PlayerSkin.mat is only what a renderer STARTS on.
        /// CharacterIdentity.Recolour/ApplyPaint read `renderer.material`
        /// (deliberately, to tint one body without recolouring everyone
        /// sharing the asset), and that auto-instances a private copy the
        /// first time it's touched -- which for any character who has ever
        /// been painted or recoloured is well before this ever runs. Toggling
        /// the shared asset's own .shader missed that copy entirely, which is
        /// why this used to look like it did nothing on a painted player
        /// while still working on an untouched civilian.
        /// </summary>
        private void ToggleCelShade()
        {
            if (_litShader == null) _litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_toonShader == null) _toonShader = Shader.Find("CityX/ToonLit");
            if (_litShader == null || _toonShader == null)
            {
                Log("cel shade shader missing");
                return;
            }

            _celShadeOn = !_celShadeOn;
            Shader target = _celShadeOn ? _toonShader : _litShader;

            int changed = 0;
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                // Read sharedMaterial to test it -- it returns whatever is
                // actually bound (instance or asset) without instancing
                // anything just to look. Only a match then goes through
                // .material, so an untouched civilian population (or an
                // untouched building) still gets one shared swap instead of
                // a thousand new per-renderer clones.
                Material current = renderer.sharedMaterial;
                if (current == null || !IsToonableMaterial(current))
                {
                    continue;
                }
                renderer.material.shader = target;
                changed++;
            }

            Log("cel shading " + (_celShadeOn ? "on" : "off") + $" ({changed} renderer(s))");
        }

        /// <summary>Public, not private, so a test can drive this exact rule directly.</summary>
        public static bool IsToonableMaterial(Material material)
        {
            if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
            {
                return false;
            }
            // Missing the property at all means it was never given a
            // surface-type choice, which URP Lit only omits for a material
            // stuck on its own default (Opaque) -- treated as opaque rather
            // than excluded, so a plain fresh material is never silently
            // skipped just for not being touched yet.
            return !material.HasProperty("_Surface") || material.GetFloat("_Surface") == 0.0f;
        }

        /// <summary>
        /// Friendly console spellings for the item ids in ItemRegistry.
        /// Null for anything not recognised.
        /// </summary>
        private static string AliasToItemId(string which)
        {
            switch (which)
            {
                case "bottle": return ItemRegistry.GlassBottle;
                case "jeans": return ItemRegistry.SkelliJeans;
                case "cloak": return ItemRegistry.HoodedCloak;
                case "coat": return ItemRegistry.MidnightPatchworkCoat;
                case "jetpack": return ItemRegistry.Jetpack;
                case "parachute": return ItemRegistry.Parachute;
                case "grapple": return ItemRegistry.GrapplingHook;
                case "pistol": return ItemRegistry.Pistol;
                case "sword": return ItemRegistry.Sword;
                case "flamethrower": return ItemRegistry.Flamethrower;
                case "top_hat":
                case "tophat": return ItemRegistry.TopHat;
                case "farmer_hat":
                case "farmerhat": return ItemRegistry.FarmerHat;
                default: return null;
            }
        }

        /// <summary>
        /// TOPS UP STORAGE IF THE ITEM ISN'T ALREADY THERE -- a deliberate
        /// dev-console convenience so /equip bottle works standalone,
        /// without first /spawn-ing one and walking over to pick it up.
        /// </summary>
        private void Equip(string which)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            Inventory inventory = Vitals.GetComponent<Inventory>();
            if (inventory == null)
            {
                Log("no inventory on this body");
                return;
            }

            string itemId = AliasToItemId(which);
            if (itemId == null)
            {
                Log(DevCommands.UsageFor("equip"));
                return;
            }

            if (!inventory.HasItem(itemId))
            {
                inventory.AddItem(itemId, 1);
            }

            if (!inventory.RemoveFromStorage(itemId, 1, out ItemInstanceData instanceData))
            {
                Log("no " + which + " in storage");
                return;
            }

            if (!inventory.EquipItemId(itemId, Vitals.gameObject, instanceData))
            {
                // Put it back rather than leaving it removed from storage
                // and nowhere else -- a prefab wired wrong shouldn't also
                // eat the item.
                inventory.AddItem(itemId, 1, instanceData);
                Log("couldn't equip " + which + " -- check its prefab is wired");
                return;
            }

            Log("equipped " + which);
        }

        private void Unequip(string slot)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            Inventory inventory = Vitals.GetComponent<Inventory>();
            if (inventory == null)
            {
                Log("no inventory on this body");
                return;
            }
            if (string.IsNullOrEmpty(slot))
            {
                Log(DevCommands.UsageFor("unequip"));
                return;
            }

            if (!inventory.UnequipToSlot(slot, Vitals.gameObject))
            {
                Log("nothing equipped in " + slot + " (or storage is full)");
                return;
            }

            Log("unequipped " + slot);
        }

        private void Log(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }
            _lines.Add(message);
            // Only the last handful. A console that grows forever eventually
            // costs a layout pass per line typed.
            while (_lines.Count > 12)
            {
                _lines.RemoveAt(0);
            }
            if (Output != null)
            {
                Output.text = string.Join("\n", _lines);
            }
        }
    }
}
