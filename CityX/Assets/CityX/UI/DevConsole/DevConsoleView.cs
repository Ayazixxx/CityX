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
    public sealed partial class DevConsoleView : MonoBehaviour
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

            // REAL RUNTIME WIRING, not a scene-build-time one -- Bug,
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

            // THE ANIMATOR IS NO LONGER DISABLED HERE -- Requested, after
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

            // Bug report: "pressing console mid jump plays the
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

            // SINGLEPLAYER ONLY PAUSES REAL TIME -- Requested. Called
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

                case "level":
                    SetLevel(line.Argument(0));
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

        private static string FormatVector3(Vector3 v)
        {
            return $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
        }

        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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
