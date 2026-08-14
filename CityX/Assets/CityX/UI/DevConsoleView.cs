using System.Collections.Generic;
using System.Globalization;
using CityX.Character;
using CityX.View;
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

        [Tooltip("What /spawn radio puts down.")]
        public GameObject RadioPrefab;

        [Tooltip("Every character-skin material /celshade toggles -- PlayerSkin, CivilianSkin.")]
        public Material[] ToonableMaterials;

        /// <summary>How far in front of the player something spawns.</summary>
        public const float SpawnDistance = 3.0f;

        private readonly List<string> _lines = new List<string>();
        private Shader _litShader;
        private Shader _toonShader;
        private bool _celShadeOn;

        public bool IsOpen => Panel != null && Panel.activeSelf;

        private void Awake()
        {
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
        }

        public void Close()
        {
            if (Panel != null)
            {
                Panel.SetActive(false);
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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

                case "celshade":
                    ToggleCelShade();
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

        private void Experience(string amount)
        {
            if (Vitals == null)
            {
                Log("no body here");
                return;
            }
            if (amount == null || !float.TryParse(amount, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float points))
            {
                Log(DevCommands.UsageFor("xp"));
                return;
            }

            // Levels are the only thing the port's vitals track so far, so this
            // grants them directly rather than pretending there is an XP curve
            // underneath that nothing else reads.
            int levels = Mathf.Max(1, Mathf.RoundToInt(points / 100.0f));
            Vitals.Level += levels;
            Log($"+{levels} level(s) -- now {Vitals.Level}");
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

                default:
                    Log(DevCommands.UsageFor("spawn"));
                    return;
            }
        }

        /// <summary>
        /// Swaps every wired skin material's shader between the normal URP
        /// Lit and CityX/ToonLit. Lives on the MATERIAL, not per-renderer --
        /// CharacterSkin.Apply shares one material instance across a whole
        /// body (and every civilian wearing the same skin), so this one
        /// swap changes everyone using it at once, the same as Godot's own
        /// /celshade did for its full-screen version.
        /// </summary>
        private void ToggleCelShade()
        {
            if (ToonableMaterials == null || ToonableMaterials.Length == 0)
            {
                Log("no toonable materials wired");
                return;
            }
            if (_litShader == null) _litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (_toonShader == null) _toonShader = Shader.Find("CityX/ToonLit");
            if (_litShader == null || _toonShader == null)
            {
                Log("cel shade shader missing");
                return;
            }

            _celShadeOn = !_celShadeOn;
            Shader target = _celShadeOn ? _toonShader : _litShader;
            foreach (Material material in ToonableMaterials)
            {
                if (material != null)
                {
                    material.shader = target;
                }
            }
            Log("cel shading " + (_celShadeOn ? "on" : "off"));
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
