using System.Collections.Generic;
using System.Globalization;
using CityX.Character;
using CityX.Items;
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

        [Tooltip("What /spawn bottle puts down, and what /equip bottle equips.")]
        public GameObject BottlePrefab;

        [Tooltip("What /spawn jeans puts down, and what /equip jeans equips.")]
        public GameObject SkelliJeansPrefab;

        [Tooltip("What /spawn cloak puts down, and what /equip cloak equips.")]
        public GameObject HoodedCloakPrefab;

        [Tooltip("What /spawn grapple puts down, and what /equip grapple equips.")]
        public GameObject GrapplingHookPrefab;

        [Tooltip("Movement/combat input. Disabled while the console is open.")]
        public PlayerInputReader InputReader;

        [Tooltip("Mouse-look. Disabled while the console is open -- otherwise typing WASD into a command also walks the player.")]
        public ThirdPersonCamera OrbitCamera;

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

            if (InputReader != null) InputReader.enabled = false;
            if (OrbitCamera != null) OrbitCamera.enabled = false;
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

                case "equip":
                    Equip(line.Argument(0));
                    return;

                case "unequip":
                    Unequip(line.Argument(0));
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

                case "grapple":
                    if (GrapplingHookPrefab == null)
                    {
                        Log("no grappling hook prefab wired");
                        return;
                    }
                    Instantiate(GrapplingHookPrefab, where + Vector3.up * 0.1f, Quaternion.identity);
                    Log("spawned a grappling hook -- walk up, look at it and press E");
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
        /// Swaps every skin renderer's shader between the normal URP Lit
        /// and CityX/ToonLit.
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
                // Read sharedMaterial to test the name -- it returns
                // whatever is actually bound (instance or asset) without
                // instancing anything just to look. Only a match then goes
                // through .material, so an untouched civilian population
                // still gets one shared swap instead of a thousand new
                // per-renderer clones.
                Material current = renderer.sharedMaterial;
                if (current == null || !IsToonableSkinName(current.name))
                {
                    continue;
                }
                renderer.material.shader = target;
                changed++;
            }

            Log("cel shading " + (_celShadeOn ? "on" : "off") + $" ({changed} renderer(s))");
        }

        private static bool IsToonableSkinName(string materialName) =>
            materialName.StartsWith("PlayerSkin") || materialName.StartsWith("CivilianSkin");

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
                case "grapple": return ItemRegistry.GrapplingHook;
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
