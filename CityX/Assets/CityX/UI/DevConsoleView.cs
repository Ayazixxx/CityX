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
using FlightFeel = CityX.Weapons.FlightFeel;

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

        [Tooltip("What /spawn shoota puts down -- a white player-model NPC with an AK-47 who shoots on sight and takes cover.")]
        public GameObject ShootaPrefab;

        [Tooltip("What /spawn swordsman puts down -- a slate-blue player-model NPC with the combat sword who duels you: blocks, rolls, punishes.")]
        public GameObject SwordsmanPrefab;

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

        [Tooltip("What /spawn jacket puts down, and what /equip jacket equips.")]
        public GameObject BlackPufferJacketPrefab;

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

        /// <summary>How far ahead /spawn shoota puts him: far enough to see him turn and raise the rifle.</summary>
        public const float ShootaSpawnDistance = 14.0f;

        /// <summary>How far ahead /spawn swordsman puts him: a few strides off, so you see him come in.</summary>
        public const float SwordsmanSpawnDistance = 8.0f;

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
                    Weather(line.Argument(0), line.Argument(1));
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

                case "skills":
                    SkillsCommand(line.Argument(0), line.Argument(1));
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

                case "jacket":
                    JacketCommand(line.Argument(0), line.Argument(1));
                    return;

                case "pistol":
                    PistolCommand(line.Argument(0), line.Argument(1), line.Argument(2), line.Argument(3));
                    return;

                case "aim":
                    AimCommand(line.Argument(0), line.Argument(1));
                    return;

                case "camera":
                    CameraCommand(line.Argument(0), line.Argument(1));
                    return;

                case "sword":
                    SwordCommand(line.Argument(0), line.Argument(1));
                    return;

                case "gun":
                    GunCommand(line.Argument(0), line.Argument(1));
                    return;

                case "flight":
                    FlightCommand(line.Argument(0), line.Argument(1));
                    return;

                case "posture":
                    PostureCommand(line.Argument(0), line.Argument(1));
                    return;

                case "footik":
                    FootIkCommand(line.Argument(0));
                    return;

                default:
                    Log("unknown command: " + line.Verb + "   (try /help)");
                    return;
            }
        }

        private void Weather(string which, string strength)
        {
            if (Rain == null)
            {
                Log("no rain in this scene");
                return;
            }
            switch (which)
            {
                case "rain":
                    if (!CityX.World.Weather.TryParseIntensity(strength, out float intensity))
                    {
                        Log(DevCommands.UsageFor("weather"));
                        return;
                    }
                    Rain.SetRain(intensity);
                    Log(intensity >= CityX.World.Weather.HeavyRain ? "raining" : $"raining ({intensity:0.00})");
                    return;
                case "clear":
                    Rain.SetRain(0.0f);
                    Log("clear -- the streets dry out slowly (/weather dry for at once)");
                    return;
                case "dry":
                    Rain.Dry();
                    Log("dry");
                    return;
                case "lightning":
                {
                    float metres = 500.0f;
                    if (!string.IsNullOrEmpty(strength) &&
                        !float.TryParse(strength, NumberStyles.Float, CultureInfo.InvariantCulture, out metres))
                    {
                        Log(DevCommands.UsageFor("weather"));
                        return;
                    }
                    metres = Mathf.Max(30.0f, metres);
                    Rain.StrikeLightning(metres);
                    Log($"lightning {metres:0}m away -- thunder in {CityX.World.Weather.ThunderDelaySeconds(metres):0.0}s");
                    return;
                }
                case "status":
                {
                    Vector3 at = (Vitals != null ? Vitals.transform.position : transform.position) + Vector3.up * 1.6f;
                    Log($"rain {Rain.Intensity:0.00} (asked {Rain.TargetIntensity:0.00}), wetness {Rain.Wetness:0.00}, " +
                        $"cover over you {CurrentWeather.Shelter(at):0.00}, rain on you {CurrentWeather.RainOn(at):0.00}");
                    return;
                }
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
            if (what != "slope" && what != "highway" && what != "district")   // "district": the highway's old name here, kept
            {
                Log(DevCommands.UsageFor("goto"));
                return;
            }

            Vector3 from = Vitals.transform.position;
            var world = new CityWorld(Streamer.WorldSeed);

            Vector3 destination;
            string found;
            if (what == "slope")
            {
                if (!TryFindNearestSlope(world, from, out destination))
                {
                    Log("no sloped road found -- try again from somewhere else");
                    return;
                }
                found = "sloped road";
            }
            else
            {
                if (!TryFindNearestHighway(world, from, out destination))
                {
                    Log("no highway found");
                    return;
                }
                found = "highway";
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

        /// <summary>The middle of the nearest road that climbs at least 4% (else the steepest in the city), a couple of metres up.</summary>
        private static bool TryFindNearestSlope(CityWorld world, Vector3 from, out Vector3 destination)
        {
            destination = Vector3.zero;
            double bestNear = double.MaxValue, bestSteep = 0.0;
            CityX.World.Plan.EdgeGeometry near = null, steepest = null;
            foreach (CityX.World.Plan.EdgeGeometry g in world.Plan.Geometry.Edges)
            {
                if (g == null || g.Edge.Bridge || g.Length < 20.0)
                {
                    continue;
                }
                double grade = System.Math.Abs(world.Plan.GroundHeight(g.A.X, g.A.Y) - world.Plan.GroundHeight(g.B.X, g.B.Y)) / g.Length;
                double mx = (g.A.X + g.B.X) * 0.5 - from.x, mz = (g.A.Y + g.B.Y) * 0.5 - from.z;
                double d = mx * mx + mz * mz;
                if (grade >= 0.04 && d < bestNear)
                {
                    bestNear = d;
                    near = g;
                }
                if (grade > bestSteep)
                {
                    bestSteep = grade;
                    steepest = g;
                }
            }
            CityX.World.Plan.EdgeGeometry pick = near ?? steepest;
            if (pick == null)
            {
                return false;
            }
            double x = (pick.A.X + pick.B.X) * 0.5, z = (pick.A.Y + pick.B.Y) * 0.5;
            destination = new Vector3((float)x, (float)world.Terrain.ContinuousHeightAt(x, z) + 2.0f, (float)z);
            return true;
        }

        /// <summary>The nearest point of an expressway's main deck, in the middle of its nearer carriageway, a couple of metres up.</summary>
        /// <summary>/posture: the shared posture every body stands in (BodyPostureSettings), shown or set live.</summary>
        /// <summary>The foot placement's state on the player (FootPlanting), live; /footik on|off switches it.</summary>
        private void FootIkCommand(string what)
        {
            if (_playerLocomotion == null)
            {
                Log("no player to look at");
                return;
            }
            if (what == "on" || what == "off")
            {
                _playerLocomotion.FootIkEnabled = what == "on";
                Log($"foot placement {what}");
                return;
            }
            CityX.Character.FootPlanting feet = _playerLocomotion.FootPlanting;
            string Foot(int i) => feet.IsLocked(i) ? "held" : feet.IsOnFloor(i) ? "on the floor" : "lifted";
            Log($"foot placement: {(_playerLocomotion.FootIkEnabled ? "on" : "OFF")}, {(_playerLocomotion.FootIkActive ? "placing" : "not placing (in the air, on a wall, in a vault, a seat or a dance)")}; " +
                $"hip drop {feet.HipDrop * 100:0.0} cm, landing dip {feet.LandingDip * 100:0.0} cm; left {Foot(0)} ({feet.Of(0).Weight:0.00}), right {Foot(1)} ({feet.Of(1).Weight:0.00}); " +
                $"clip {feet.ClipGroundSpeed:0.0} m/s against body {feet.BodyGroundSpeed:0.0} m/s ({(feet.LocksViable ? "feet can be held" : "too far apart to hold the feet")}), " +
                $"stride x{_playerLocomotion.StrideScale:0.00}, ground normal {_playerLocomotion.GroundNormal:F2}");
        }

        private void PostureCommand(string what, string value)
        {
            var settings = CityX.Character.BodyPostureSettings.Current;
            if (string.IsNullOrEmpty(what) || what == "show")
            {
                Log($"posture: head {settings.HeadPitchUp:0.#} up, {settings.HeadTurnRight:0.#} turned right, {settings.HeadTiltRight:0.#} tilted right (degrees), legs {settings.LegsBack:0.###} m back, head moved {settings.HeadBack:0.###} m back and {settings.HeadUp:0.###} m up -- /posture head|turn|tilt <degrees> | /posture legs|headback|headup <metres>");
                return;
            }
            if (value == null || !float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float number))
            {
                Log(DevCommands.UsageFor("posture"));
                return;
            }
            switch (what)
            {
                case "head":
                    settings.HeadPitchUp = Mathf.Clamp(number, -30.0f, 30.0f);
                    break;
                case "turn":
                    settings.HeadTurnRight = Mathf.Clamp(number, -45.0f, 45.0f);
                    break;
                case "tilt":
                    settings.HeadTiltRight = Mathf.Clamp(number, -30.0f, 30.0f);
                    break;
                case "headback":
                    settings.HeadBack = Mathf.Clamp(number, -0.3f, 0.3f);
                    break;
                case "headup":
                    settings.HeadUp = Mathf.Clamp(number, -0.3f, 0.3f);
                    break;
                case "legs":
                    settings.LegsBack = Mathf.Clamp(number, -0.15f, 0.15f);
                    break;
                default:
                    Log(DevCommands.UsageFor("posture"));
                    return;
            }
            Log($"posture: head {settings.HeadPitchUp:0.#} up, {settings.HeadTurnRight:0.#} turned right, {settings.HeadTiltRight:0.#} tilted right, legs {settings.LegsBack:0.###} m back, head moved {settings.HeadBack:0.###} m back and {settings.HeadUp:0.###} m up");
        }

        private static bool TryFindNearestHighway(CityWorld world, Vector3 from, out Vector3 destination)
        {
            destination = Vector3.zero;
            double best = double.MaxValue;
            foreach (CityX.World.Plan.ViaductPath path in world.Plan.Data.Expressways.Paths)
            {
                if (path.Kind != CityX.World.Plan.ViaductKind.Mainline) continue;
                for (int i = 0; i < path.Count; i++)
                {
                    double dx = path.P[i].X - from.x, dz = path.P[i].Y - from.z;
                    double d = dx * dx + dz * dz;
                    if (d >= best) continue;
                    best = d;
                    CityX.World.Plan.Vec2 lane = path.P[i] + path.N[i] * (path.HalfLeft[i] * 0.5);
                    destination = new Vector3((float)lane.X, (float)path.Top[i] + 2.0f, (float)lane.Y);
                }
            }
            return best < double.MaxValue;
        }

        /// <summary>
        /// "/skills": what the player has learnt, their skill points and their bar; "/skills points 5", "/skills reset", "/skills all",
        /// "/skills new" (a brand new character's: vaulting and the starting point) -- for trying the skill menu out.
        /// </summary>
        private void SkillsCommand(string verb, string amount)
        {
            PlayerSkills skills = PlayerSkills.Local;
            if (skills == null && Vitals != null)
            {
                skills = PlayerSkills.On(Vitals.gameObject);
            }
            if (skills == null)
            {
                Log("no body here");
                return;
            }
            PlayerVitals vitals = skills.GetComponent<PlayerVitals>();
            switch (verb)
            {
                case null:
                    break;
                case "points":
                    if (vitals == null || amount == null || !int.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int points))
                    {
                        Log(DevCommands.UsageFor("skills"));
                        return;
                    }
                    skills.SetPoints(vitals.SkillPoints + points);
                    break;
                case "reset":
                    Log($"forgot everything but vaulting: {skills.ResetAll()} point(s) back");
                    break;
                case "all":
                    skills.LearnEverything();
                    break;
                case "new":
                    skills.ResetAll();
                    skills.SetPoints(CityX.Core.CharacterSave.StartingSkillPoints);
                    break;
                default:
                    Log(DevCommands.UsageFor("skills"));
                    return;
            }

            var learnt = new System.Collections.Generic.List<string>(skills.Book.Learnt);
            learnt.Sort(System.StringComparer.Ordinal);
            var bar = new System.Text.StringBuilder();
            for (int slot = 0; slot < Hotbar.SlotCount; slot++)
            {
                bar.Append(Hotbar.KeyLabel(slot)).Append('=').Append(skills.Bar[slot] ?? "-").Append(slot < Hotbar.SlotCount - 1 ? "  " : string.Empty);
            }
            Log($"{skills.Points} skill point(s); learnt: {string.Join(", ", learnt)}");
            Log("bar: " + bar);
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

                case "shoota":
                    if (ShootaPrefab == null)
                    {
                        Log("no shoota prefab wired");
                        return;
                    }
                    // Further off than the rest: he opens fire the moment he sees you, so you get a moment to react.
                    Instantiate(ShootaPrefab, origin.position + origin.forward * ShootaSpawnDistance, facing);
                    Log("spawned a Shoota -- he shoots on sight, takes cover when hurt or reloading");
                    return;

                case "swordsman":
                    if (SwordsmanPrefab == null)
                    {
                        Log("no swordsman prefab wired");
                        return;
                    }
                    Instantiate(SwordsmanPrefab, origin.position + origin.forward * SwordsmanSpawnDistance, facing);
                    Log("spawned a swordsman -- draw your sword: he blocks, rolls and punishes a blade that bounces off his guard");
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

                case "jacket":
                    if (BlackPufferJacketPrefab == null)
                    {
                        Log("no black puffer jacket prefab wired");
                        return;
                    }
                    Instantiate(BlackPufferJacketPrefab, where + Vector3.up * 0.05f, Quaternion.identity);
                    Log("spawned a black puffer jacket -- press E nearby to pick it up");
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
        /// "/jacket scale|offset|pose" -- same live-and-read-back idiom as
        /// "/car pose". EXPLICIT BUG, found and fixed the hard way: the
        /// first version of this command nudged SkinnedGarmentCombiner's
        /// own renderer Transform, on the mistaken assumption that a
        /// SkinnedMeshRenderer's own Transform affects its rendered result
        /// the way an ordinary MeshRenderer's does -- it does not, once
        /// bones are assigned, which is exactly why "the commands aren't
        /// altering it at all" was reported. This calls
        /// BlackPufferJacket.SetLivePlacement instead, which actually
        /// rebuilds the mesh's vertex positions AND bone weights -- see
        /// that method's own doc comment.
        ///
        /// THE FINAL NUMBERS THIS REPORTS ARE A LIVE PREVIEW, NOT THE
        /// PERMANENT FIX -- once "/jacket pose" reads back numbers that
        /// look right in-game, those same two numbers go into
        /// BlackPufferJacketBuilder's own ManualScale/ManualVerticalOffset
        /// constants for the permanent bake.
        /// </summary>
        private void JacketCommand(string sub, string valueText)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            var jacket = Vitals.GetComponentInChildren<CityX.Wearables.BlackPufferJacket>();
            if (jacket == null)
            {
                Log("no black puffer jacket worn -- /equip jacket first");
                return;
            }

            switch (sub)
            {
                case "scale":
                    if (!TryParseFloat(valueText, out float scale) || scale <= 0.0f)
                    {
                        Log(DevCommands.UsageFor("jacket"));
                        return;
                    }
                    jacket.SetLivePlacement(scale, jacket.LiveVerticalOffset);
                    Log($"jacket scale set to {scale:0.000}");
                    return;

                case "offset":
                    if (!TryParseFloat(valueText, out float offset))
                    {
                        Log(DevCommands.UsageFor("jacket"));
                        return;
                    }
                    jacket.SetLivePlacement(jacket.LiveScale, offset);
                    Log($"jacket vertical offset set to {offset:0.000}");
                    return;

                case "pose":
                    Log($"scale {jacket.LiveScale:0.000}   offset {jacket.LiveVerticalOffset:0.000}");
                    return;

                default:
                    Log(DevCommands.UsageFor("jacket"));
                    return;
            }
        }

        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// "/pistol pos|rot|scale|pose" -- same live-and-read-back idiom as
        /// "/car pose" and "/jacket scale|offset|pose", for EXPLICIT ASK
        /// "let me adjust the pistol position in his hand ingame using the
        /// console." Nudges PistolWeapon's own live grip fields directly
        /// (see that class's own doc comment on LiveGripPosition/Rotation/
        /// Scale), which UpdateHeldPose already applies every frame instead
        /// of the baked GripHandPosition/GripHandRotationEuler/GripScale
        /// constants -- so a change here shows up on the held gun
        /// immediately, with nothing to rebuild.
        /// </summary>
        private void PistolCommand(string sub, string a, string b, string c)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            var pistol = Vitals.GetComponentInChildren<CityX.Weapons.PistolWeapon>();
            if (pistol == null)
            {
                Log("no pistol equipped -- /equip pistol first");
                return;
            }

            switch (sub)
            {
                case "pos":
                    if (!TryParseFloat(a, out float px) || !TryParseFloat(b, out float py) || !TryParseFloat(c, out float pz))
                    {
                        Log(DevCommands.UsageFor("pistol"));
                        return;
                    }
                    pistol.SetLiveGripPosition(new Vector3(px, py, pz));
                    Log($"pistol grip position set to {FormatVector3(new Vector3(px, py, pz))}");
                    return;

                case "rot":
                    if (!TryParseFloat(a, out float rx) || !TryParseFloat(b, out float ry) || !TryParseFloat(c, out float rz))
                    {
                        Log(DevCommands.UsageFor("pistol"));
                        return;
                    }
                    pistol.SetLiveGripRotation(new Vector3(rx, ry, rz));
                    Log($"pistol grip rotation set to {FormatVector3(new Vector3(rx, ry, rz))}");
                    return;

                case "scale":
                    if (!TryParseFloat(a, out float scale) || scale <= 0.0f)
                    {
                        Log(DevCommands.UsageFor("pistol"));
                        return;
                    }
                    pistol.SetLiveGripScale(scale);
                    Log($"pistol grip scale set to {scale:0.000}");
                    return;

                case "pose":
                    Log($"pos {FormatVector3(pistol.LiveGripPosition)}   " +
                        $"rot {FormatVector3(pistol.LiveGripRotation)}   scale {pistol.LiveGripScale:0.000}");
                    return;

                default:
                    Log(DevCommands.UsageFor("pistol"));
                    return;
            }
        }

        /// <summary>
        /// Live-tunes the aim camera's shoulder offset and the body's own
        /// extra yaw while aiming -- EXPLICIT ASK: "let me adjust the pov
        /// and model rotation manually ... set up something using ingame
        /// console," after a guess-rebuild-screenshot loop on the same two
        /// numbers wasn't landing right. Both targets are already plain
        /// mutable fields (ThirdPersonCamera.AimShoulderOffset,
        /// PlayerLocomotion.AimYawOffsetDegrees) rather than consts, so this
        /// writes them directly -- no separate "Live" shadow field needed
        /// the way the pistol's own const grip constants required.
        ///
        /// NEITHER CHANGE SURVIVES A SCENE RELOAD. AimShoulderOffset is
        /// also baked into CityMassing.unity's own serialized default (see
        /// pistol_aim_pov_fortnite_offset memory) -- once a number here
        /// looks right, it still has to be typed into ThirdPersonCamera.cs's
        /// own default and the scene rebuilt, the same "tune live, then
        /// bake the winning number" workflow /jacket and /pistol already
        /// use.
        /// </summary>
        /// <summary>
        /// /gun -- live-tunes the third-person pistol's feel (recoil, kick,
        /// aim sensitivity and speed) without a rebuild, the same "tune live,
        /// then bake the winning numbers into the defaults" workflow as
        /// /jacket, /pistol, /aim and /sword.
        /// </summary>
        private void GunCommand(string name, string value)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            var pistol = Vitals.GetComponentInChildren<CityX.Weapons.PistolWeapon>();

            if (name == "shoulder")
            {
                if (OrbitCamera == null)
                {
                    Log("no camera here");
                    return;
                }
                OrbitCamera.ToggleShoulder();
                Log(OrbitCamera.ShoulderSide > 0.0f ? "camera over the right shoulder" : "camera over the left shoulder");
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                Log(pistol == null
                    ? "no pistol equipped -- /equip pistol first"
                    : $"recoil {pistol.RecoilPitchDegrees}   adsrecoil {pistol.AdsRecoilMultiplier}   shake {pistol.ShakeDegrees}   " +
                      $"kick {pistol.KickBackMetres}   kickangle {pistol.KickUpDegrees}   " +
                      $"recover {(OrbitCamera != null ? OrbitCamera.RecoilRecoverPerSecond : 0.0f)}   " +
                      $"sens {(OrbitCamera != null ? OrbitCamera.AimSensitivityMultiplier : 0.0f)}   " +
                      $"aimspeed {(_playerLocomotion != null ? _playerLocomotion.AimMoveSpeedMultiplier : 0.0f)}");
                return;
            }

            if (!TryParseFloat(value, out float number))
            {
                Log(DevCommands.UsageFor("gun"));
                return;
            }

            switch (name)
            {
                case "recoil": if (!NeedPistol(pistol)) return; pistol.RecoilPitchDegrees = number; break;
                case "adsrecoil": if (!NeedPistol(pistol)) return; pistol.AdsRecoilMultiplier = number; break;
                case "shake": if (!NeedPistol(pistol)) return; pistol.ShakeDegrees = number; break;
                case "kick": if (!NeedPistol(pistol)) return; pistol.KickBackMetres = number; break;
                case "kickangle": if (!NeedPistol(pistol)) return; pistol.KickUpDegrees = number; break;
                case "recover":
                    if (OrbitCamera == null) { Log("no camera here"); return; }
                    OrbitCamera.RecoilRecoverPerSecond = number;
                    break;
                case "sens":
                    if (OrbitCamera == null) { Log("no camera here"); return; }
                    OrbitCamera.AimSensitivityMultiplier = number;
                    break;
                case "aimspeed":
                    if (_playerLocomotion == null) { Log("no body here"); return; }
                    _playerLocomotion.AimMoveSpeedMultiplier = number;
                    break;
                default:
                    Log(DevCommands.UsageFor("gun"));
                    return;
            }
            Log($"{name} = {number.ToString(CultureInfo.InvariantCulture)}");
        }

        private bool NeedPistol(CityX.Weapons.PistolWeapon pistol)
        {
            if (pistol != null)
            {
                return true;
            }
            Log("no pistol equipped -- /equip pistol first");
            return false;
        }

        /// <summary>
        /// /sword -- live-tunes the soulslike sword combat (SoulsSwordCombat)
        /// without a rebuild: every timing, cost and damage number is a public
        /// field, so this lists them all and sets any one by name. Same "tune
        /// live, then bake the winning numbers into the defaults" workflow as
        /// /jacket, /pistol and /aim.
        /// </summary>
        private void SwordCommand(string name, string value)
        {
            SoulsSwordCombat combat = _playerLocomotion != null
                ? _playerLocomotion.GetComponent<SoulsSwordCombat>()
                : null;
            if (combat == null || !combat.IsEquipped)
            {
                Log("no sword equipped -- /equip sword first");
                return;
            }

            if (string.IsNullOrEmpty(name))
            {
                Log(combat.Describe());
                return;
            }
            if (!TryParseFloat(value, out float number))
            {
                Log(DevCommands.UsageFor("sword"));
                return;
            }
            combat.TrySet(name, number, out string message);
            Log(message);
        }

        /// <summary>
        /// Live tuning for how flying feels (see FlightFeel) -- so the numbers can be
        /// set by flying and looking rather than by rebuilding and guessing, the
        /// same reason /aim and /sword exist.
        /// </summary>
        private void FlightCommand(string name, string value)
        {
            if (string.IsNullOrEmpty(name))
            {
                Log($"flight feel: fov {FlightFeel.FovGainDegrees}, kick {FlightFeel.BoostKickFovDegrees}, " +
                    $"kickseconds {FlightFeel.BoostKickSeconds}, pullback {FlightFeel.PullBackMetres}, " +
                    $"kickpullback {FlightFeel.BoostKickPullBackMetres}, smooth {FlightFeel.SmoothSeconds}, " +
                    $"streaks {FlightFeel.MaxStreaksPerSecond}, start {FlightFeel.StartSpeed}, full {FlightFeel.FullSpeed}");
                return;
            }
            if (!TryParseFloat(value, out float number))
            {
                Log(DevCommands.UsageFor("flight"));
                return;
            }

            switch (name.ToLowerInvariant())
            {
                case "fov": FlightFeel.FovGainDegrees = number; break;
                case "kick": FlightFeel.BoostKickFovDegrees = number; break;
                case "kickseconds": FlightFeel.BoostKickSeconds = Mathf.Max(0.0f, number); break;
                case "pullback": FlightFeel.PullBackMetres = number; break;
                case "kickpullback": FlightFeel.BoostKickPullBackMetres = number; break;
                case "smooth": FlightFeel.SmoothSeconds = Mathf.Max(0.01f, number); break;
                case "streaks": FlightFeel.MaxStreaksPerSecond = Mathf.Max(0.0f, number); break;
                case "start": FlightFeel.StartSpeed = number; break;
                case "full": FlightFeel.FullSpeed = Mathf.Max(number, FlightFeel.StartSpeed + 0.1f); break;
                default:
                    Log(DevCommands.UsageFor("flight"));
                    return;
            }
            Log($"flight {name} = {number}");
        }

        /// <summary>
        /// "/camera height|distance|shoulder|fov &lt;value&gt;", "/camera reset" or a bare "/camera": the follow camera's framing, live, to try
        /// other framings in play (owner, 2026-10-17: "i feel like the camera pov needs to be adjusted so i can see more of whats going on in
        /// front of me"). For the session only; the defaults are ThirdPersonCamera's.
        /// </summary>
        private void CameraCommand(string sub, string a)
        {
            if (OrbitCamera == null)
            {
                Log("no camera here");
                return;
            }
            if (sub == null)
            {
                Log($"camera: height {OrbitCamera.FramingHeight:0.00} m over the shoulder, distance {OrbitCamera.Distance:0.00} m, " +
                    $"shoulder {OrbitCamera.ShoulderOffset:0.00} m, fov {OrbitCamera.RestFieldOfView:0}");
                return;
            }
            if (sub == "reset")
            {
                OrbitCamera.FramingHeight = ThirdPersonCamera.DefaultFramingHeight;
                OrbitCamera.Distance = ThirdPersonCamera.DefaultDistance;
                OrbitCamera.ShoulderOffset = ThirdPersonCamera.DefaultShoulderOffset;
                OrbitCamera.RestFieldOfView = ThirdPersonCamera.DefaultFieldOfView;
                Log("camera back to its defaults");
                return;
            }
            if (!TryParseFloat(a, out float value))
            {
                Log(DevCommands.UsageFor("camera"));
                return;
            }
            switch (sub)
            {
                case "height": OrbitCamera.FramingHeight = value; break;
                case "distance": OrbitCamera.Distance = Mathf.Max(0.5f, value); break;
                case "shoulder": OrbitCamera.ShoulderOffset = value; break;
                case "fov": OrbitCamera.RestFieldOfView = Mathf.Clamp(value, 30.0f, 120.0f); break;
                default:
                    Log(DevCommands.UsageFor("camera"));
                    return;
            }
            Log($"camera {sub} set to {value:0.00}");
        }

        private void AimCommand(string sub, string a)
        {
            switch (sub)
            {
                case "offset":
                    if (OrbitCamera == null)
                    {
                        Log("no camera here");
                        return;
                    }
                    if (!TryParseFloat(a, out float offset))
                    {
                        Log(DevCommands.UsageFor("aim"));
                        return;
                    }
                    OrbitCamera.AimShoulderOffset = offset;
                    Log($"aim shoulder offset set to {offset:0.000}");
                    return;

                case "yaw":
                    // ROTATES THE WHOLE CHARACTER -- BODY, ARM, AND HEAD
                    // TOGETHER, AS ONE RIGID UNIT. EXPLICIT ASK, repeated:
                    // "give me a command to rotate the entire body head
                    // body and arms." This used to set ONLY the body root's
                    // own offset (AimYawOffsetDegrees), leaving the arm/head
                    // -- which target the camera's own convergence point,
                    // independent of body yaw, see ApplyGunAimPose's own
                    // comment -- pointing the old direction, which read as
                    // the character visibly TWISTING rather than turning.
                    // Setting both fields to the SAME value here keeps
                    // body/arm/head in agreement at every value, exactly
                    // the single-knob behaviour asked for. /aim armyaw
                    // still exists underneath for the rare case of nudging
                    // JUST the arm/head independently of the body.
                    if (_playerLocomotion == null)
                    {
                        Log("no body here");
                        return;
                    }
                    if (!TryParseFloat(a, out float yaw))
                    {
                        Log(DevCommands.UsageFor("aim"));
                        return;
                    }
                    _playerLocomotion.AimYawOffsetDegrees = yaw;
                    _playerLocomotion.ArmAimYawOffsetDegrees = yaw;
                    Log($"aim yaw offset set to {yaw:0.0} degrees (body, arm, and head together)");
                    return;

                case "armyaw":
                    // THE ARM/HEAD KNOB ALONE, without moving the body --
                    // see "yaw" above (the one most people want) for the
                    // combined version. Kept separate for nudging just the
                    // gun's own angle independently of body rotation.
                    if (_playerLocomotion == null)
                    {
                        Log("no body here");
                        return;
                    }
                    if (!TryParseFloat(a, out float armYaw))
                    {
                        Log(DevCommands.UsageFor("aim"));
                        return;
                    }
                    _playerLocomotion.ArmAimYawOffsetDegrees = armYaw;
                    Log($"arm aim yaw offset set to {armYaw:0.0} degrees (arm and head only)");
                    return;

                case "pose":
                    Log($"offset {(OrbitCamera != null ? OrbitCamera.AimShoulderOffset : 0.0f):0.000}   " +
                        $"yaw {(_playerLocomotion != null ? _playerLocomotion.AimYawOffsetDegrees : 0.0f):0.0}   " +
                        $"armyaw {(_playerLocomotion != null ? _playerLocomotion.ArmAimYawOffsetDegrees : 0.0f):0.0}");
                    return;

                default:
                    Log(DevCommands.UsageFor("aim"));
                    return;
            }
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
                case "jacket": return ItemRegistry.BlackPufferJacket;
                case "jetpack": return ItemRegistry.Jetpack;
                case "parachute": return ItemRegistry.Parachute;
                case "grapple": return ItemRegistry.GrapplingHook;
                case "pistol": return ItemRegistry.Pistol;
                case "sword": return ItemRegistry.Sword;
                case "spikesword":
                case "spike_sword": return ItemRegistry.SpikeSword;
                case "combatsword":
                case "combat_sword":
                case "1h": return ItemRegistry.CombatSword;
                case "crowbar":
                case "metalcrowbar":
                case "metal_crowbar": return ItemRegistry.MetalCrowbar;
                case "bat":
                case "baseballbat":
                case "baseball_bat": return ItemRegistry.BaseballBat;
                case "flamethrower": return ItemRegistry.Flamethrower;
                case "spray":
                case "spraycan":
                case "spray_can":
                case "graffiti": return ItemRegistry.SprayCan;
                case "ak":
                case "ak47":
                case "ak-47":
                case "rifle": return ItemRegistry.AK47;
                case "glock": return ItemRegistry.Pistol;
                case "top_hat":
                case "tophat": return ItemRegistry.TopHat;
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
