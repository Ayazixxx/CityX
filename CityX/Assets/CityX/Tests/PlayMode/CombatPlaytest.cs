using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: a bot plays the real game (the real scene, player, camera and keys) against a swordsman and Crazy Guy with each weapon in
    /// turn, the way a fair player would: walks up, swings when in reach, guards or dashes when the enemy winds up, keeps its distance and aims
    /// with a gun or a staff (owner, 2026-11-18: "can you play the game and fight swordsman and crazyguys using various weapons and make
    /// improvements"). It writes what each fight was like to Logs/Playtest/report.txt (how long, who hurt whom, how often blows missed or
    /// were blocked, any stuck spells or errors) and, with a graphics device, a picture from the game's camera every few seconds.
    /// </summary>
    [Explicit("playtest")]
    public sealed class CombatPlaytest : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const float FightSeconds = 60.0f;
        private const float ShotEverySeconds = 3.0f;
        private const float HealBelow = 25.0f;
        private const float TraceEverySeconds = 1.0f;

        /// <summary>Set to a comma-separated list of loadout names (fists, pistol...) to play only those.</summary>
        private const string OnlyVariable = "CITYX_PLAYTEST";

        /// <summary>Set to how many times each fight is played (one by default): one fight is one roll of the dice.</summary>
        private const string RoundsVariable = "CITYX_PLAYTEST_ROUNDS";

        private sealed class Loadout
        {
            public string Name;
            public string Item;          // null: bare fists
            public float Reach;          // melee: how close it walks in; ranged: the distance it keeps
            public bool Ranged;
            public bool AimWithRightClick;
            public bool Automatic;
        }

        private static readonly Loadout[] Loadouts =
        {
            new Loadout { Name = "fists", Item = null, Reach = 1.6f },
            new Loadout { Name = "combat_sword", Item = ItemRegistry.CombatSword, Reach = 2.1f },
            new Loadout { Name = "kitchen_knife", Item = ItemRegistry.KitchenKnife, Reach = 1.6f },
            new Loadout { Name = "baseball_bat", Item = ItemRegistry.BaseballBat, Reach = 2.0f },
            new Loadout { Name = "sledgehammer", Item = ItemRegistry.Sledgehammer, Reach = 2.2f },
            new Loadout { Name = "pistol", Item = ItemRegistry.Pistol, Reach = 7.0f, Ranged = true, AimWithRightClick = true },
            new Loadout { Name = "ak47", Item = ItemRegistry.AK47, Reach = 8.0f, Ranged = true, AimWithRightClick = true, Automatic = true },
            new Loadout { Name = "fireball_staff", Item = ItemRegistry.FireballStaff, Reach = 8.0f, Ranged = true },
            new Loadout { Name = "boomerang", Item = ItemRegistry.WoodenBoomerang, Reach = 6.0f, Ranged = true },
        };

        private static readonly string[] Enemies = { "Swordsman", "CrazyGuy" };

        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private readonly List<string> _errors = new List<string>();
        private readonly List<GameObject> _made = new List<GameObject>();
        private string _doing = "setting up";

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            Application.logMessageReceived += Heard;
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Application.logMessageReceived -= Heard;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        private void Heard(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                string where = string.IsNullOrEmpty(stackTrace) ? string.Empty : " @ " + stackTrace.Split('\n')[0].Trim();
                if (_errors.Count < 200) _errors.Add($"{_doing}: {type} {condition}{where}");
            }
        }

        /// <summary>What one fight was like, counted as it goes.</summary>
        private sealed class Fight
        {
            public string Loadout, Enemy;
            public int Round;
            public float Seconds;
            public bool Won;
            public float DamageDealt, DamageTaken;
            public int HitsDealt, HitsTaken, Clicks, Lives = 1;
            public int EnemyBlocked, EnemyParried, EnemyStaggered, EnemyPoiseBroken, EnemyDashes, EnemyGuards;
            public int PlayerBlocked, PlayerParried, PlayerStaggered, PlayerGuardBroken;
            public float PlayerDownSeconds, EnemyDownSeconds, FarSeconds, CloseQuietSeconds, FirstHitAt = -1.0f;
            public float EnemyAttackStarts;
            public int Errors;
            public readonly StringBuilder Trace = new StringBuilder();
        }

        private static string Flat(Vector3 p) => $"({p.x:0.0}, {p.y:0.0}, {p.z:0.0})";

        /// <summary>One of the duelist mind's own timers, read for the trace only.</summary>
        private static float MindField(SwordsmanBrain brain, string name)
        {
            const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            object mind = typeof(SwordsmanBrain).GetField("_mind", Private)?.GetValue(brain);
            return mind != null && typeof(DuelistMind).GetField(name, Private)?.GetValue(mind) is float value ? value : float.NaN;
        }

        [UnityTest]
        [Timeout(3000000)]
        public IEnumerator PlayEveryWeaponAgainstASwordsmanAndCrazyGuy()
        {
#if UNITY_EDITOR
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            PlayerInputReader input = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (input == null) input = root.GetComponentInChildren<PlayerInputReader>(true);
            }
            Assert.IsNotNull(input, "no player in the real scene");
            GameObject player = input.gameObject;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var vitals = player.GetComponent<PlayerVitals>();
            var inventory = player.GetComponent<Inventory>();
            var camera = Object.FindFirstObjectByType<ThirdPersonCamera>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++) yield return null;
            vitals.InfiniteStamina = true;
            for (int i = 0; i < 60; i++) yield return null;
            Vector3 home = player.transform.position;
            Quaternion homeFacing = player.transform.rotation;

            string folder = Path.Combine(Application.dataPath, "..", "Logs", "Playtest");
            Directory.CreateDirectory(folder);
            bool canShoot = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            var fights = new List<Fight>();
            var rng = new System.Random(1118);

            string only = System.Environment.GetEnvironmentVariable(OnlyVariable);
            var wanted = string.IsNullOrEmpty(only) ? null : new List<string>(only.Split(','));
            int rounds = int.TryParse(System.Environment.GetEnvironmentVariable(RoundsVariable), out int asked) ? Mathf.Max(1, asked) : 1;
            foreach (Loadout loadout in Loadouts)
            {
                if (wanted != null && !wanted.Contains(loadout.Name)) continue;
                for (int round = 0; round < rounds; round++)
                foreach (string enemyName in Enemies)
                {
                    _doing = $"{loadout.Name} vs {enemyName}";
                    yield return PutBack(player, home, homeFacing, vitals, inventory, loadout);
                    var fight = new Fight { Loadout = loadout.Name, Enemy = enemyName, Round = round };
                    int errorsBefore = _errors.Count;
                    yield return PlayOne(fight, loadout, enemyName, player, vitals, locomotion, camera, rng, folder, canShoot);
                    fight.Errors = _errors.Count - errorsBefore;
                    fights.Add(fight);
                    Debug.Log("CityX playtest: " + Line(fight));
                }
            }

            var report = new StringBuilder();
            report.AppendLine("loadout vs enemy | won | seconds | dealt (hits) | taken (hits) | lives | clicks | first hit s | enemy blocked/parried/staggered/poise-broken/dashes/guards | player blocked/parried/staggered/guard-broken | down s player/enemy | far s | close-and-quiet s | enemy attacks | errors");
            foreach (Fight f in fights) report.AppendLine(Line(f));
            report.AppendLine();
            foreach (Fight f in fights)
            {
                report.AppendLine($"{f.Loadout} vs {f.Enemy} #{f.Round}:");
                report.Append(f.Trace);
            }
            report.AppendLine();
            report.AppendLine("errors:");
            foreach (string e in _errors) report.AppendLine("  " + e);
            File.WriteAllText(Path.Combine(folder, "report.txt"), report.ToString());
            TestContext.WriteLine(report.ToString());
#else
            yield break;
#endif
        }

        private static string Line(Fight f)
        {
            return $"{f.Loadout} vs {f.Enemy} #{f.Round} | {(f.Won ? "WON" : "no")} | {f.Seconds:0.0} | {f.DamageDealt:0} ({f.HitsDealt}) | {f.DamageTaken:0} ({f.HitsTaken}) | {f.Lives} | {f.Clicks} | {f.FirstHitAt:0.0} | "
                + $"{f.EnemyBlocked}/{f.EnemyParried}/{f.EnemyStaggered}/{f.EnemyPoiseBroken}/{f.EnemyDashes}/{f.EnemyGuards} | {f.PlayerBlocked}/{f.PlayerParried}/{f.PlayerStaggered}/{f.PlayerGuardBroken} | "
                + $"{f.PlayerDownSeconds:0.0}/{f.EnemyDownSeconds:0.0} | {f.FarSeconds:0.0} | {f.CloseQuietSeconds:0.0} | {f.EnemyAttackStarts} | {f.Errors}";
        }

        private IEnumerator PutBack(GameObject player, Vector3 home, Quaternion facing, PlayerVitals vitals, Inventory inventory, Loadout loadout)
        {
            yield return ReleaseAll();
            var ragdoll = player.GetComponent<Ragdoll>();
            for (int i = 0; i < 600 && ragdoll != null && ragdoll.IsLimp; i++) yield return null;
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.SetPositionAndRotation(home, facing);
            controller.enabled = true;
            Physics.SyncTransforms();
            vitals.Revive();
            foreach (string slot in new[] { Inventory.PrimarySlot, Inventory.SecondarySlot })
            {
                string held = inventory.EquippedItemId(slot);
                if (!string.IsNullOrEmpty(held) && inventory.UnequipToSlot(slot, player))
                {
                    inventory.RemoveFromStorage(held, 1, out _);
                }
            }
            for (int i = 0; i < 10; i++) yield return null;
            if (loadout.Item != null)
            {
                inventory.AddItem(loadout.Item, 1);
                Assert.IsTrue(inventory.RemoveFromStorage(loadout.Item, 1, out ItemInstanceData data));
                Assert.IsTrue(inventory.EquipItemId(loadout.Item, player, data), $"could not equip {loadout.Item}");
            }
            for (int i = 0; i < 30; i++) yield return null;
        }

        // ---- held keys, one change per device per frame (InputTestFixture takes one event a frame) ----

        // Two changes to one device in a frame and the second's state event overwrites the first's: a released button stayed down (the bot
        // guarded a whole fight with its hands up). So what it wants is queued, and Flush sends at most one change per device a frame, shift first.

        private readonly Dictionary<ButtonControl, bool> _wanted = new Dictionary<ButtonControl, bool>();
        private readonly Dictionary<ButtonControl, bool> _held = new Dictionary<ButtonControl, bool>();

        private void Want(ButtonControl button, bool down) => _wanted[button] = down;

        /// <summary>Whether the button is down as the game sees it (sent, not only wanted).</summary>
        private bool IsHeld(ButtonControl button) => _held.TryGetValue(button, out bool down) && down;

        private void Flush()
        {
            FlushOne(_keyboard.leftShiftKey, _keyboard.wKey, _keyboard.sKey, _keyboard.aKey, _keyboard.dKey);
            FlushOne(_mouse.leftButton, _mouse.rightButton);
        }

        private void FlushOne(params ButtonControl[] inOrder)
        {
            foreach (ButtonControl button in inOrder)
            {
                bool wanted = _wanted.TryGetValue(button, out bool down) && down;
                if (wanted == IsHeld(button)) continue;
                if (wanted) Press(button); else Release(button);
                _held[button] = wanted;
                return;
            }
        }

        private IEnumerator ReleaseAll()
        {
            foreach (ButtonControl b in new List<ButtonControl>(_wanted.Keys)) _wanted[b] = false;
            for (int i = 0; i < 8; i++)
            {
                Flush();
                yield return null;
            }
        }

        private IEnumerator PlayOne(Fight fight, Loadout loadout, string enemyName, GameObject player, PlayerVitals vitals,
            PlayerLocomotion locomotion, ThirdPersonCamera camera, System.Random rng, string folder, bool canShoot)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Character/{enemyName}.prefab");
            Vector3 ahead = player.transform.forward;
            Vector3 at = player.transform.position + ahead * 8.0f;
            if (Physics.Raycast(at + Vector3.up * 3.0f, Vector3.down, out RaycastHit ground, 10.0f, ~0, QueryTriggerInteraction.Ignore))
            {
                at = ground.point + Vector3.up * 0.05f;
            }
            GameObject enemy = Object.Instantiate(prefab, at, Quaternion.LookRotation(-ahead, Vector3.up));
            _made.Add(enemy);
            var brain = enemy.GetComponent<SwordsmanBrain>();
            brain.Seed = rng.Next(1, 100000);
            brain.Target = vitals;
            var foe = enemy.GetComponent<PlayerVitals>();
            var foeRagdoll = enemy.GetComponent<Ragdoll>();
            var ragdoll = player.GetComponent<Ragdoll>();
            Animator foeAnimator = enemy.GetComponentInChildren<Animator>();
            Transform foeChest = foeAnimator != null ? foeAnimator.GetBoneTransform(HumanBodyBones.Chest) : null;

            float lastHit = 0.0f;
            float t = 0.0f;
            System.Action<float> dealt = amount => { fight.DamageDealt += amount; fight.HitsDealt++; lastHit = t; if (fight.FirstHitAt < 0) fight.FirstHitAt = t; };
            System.Action<float> taken = amount => { fight.DamageTaken += amount; fight.HitsTaken++; lastHit = t; };
            System.Action<Vector3, DamageKind> foeDeflect = (p, k) => fight.EnemyBlocked++;
            System.Action<Vector3> foeGuard = p => fight.EnemyBlocked++;
            System.Action<Vector3> foeParry = p => fight.EnemyParried++;
            System.Action foeStagger = () => fight.EnemyStaggered++;
            System.Action foePoise = () => fight.EnemyPoiseBroken++;
            System.Action<Vector3, DamageKind> meDeflect = (p, k) => fight.PlayerBlocked++;
            System.Action<Vector3> meGuard = p => fight.PlayerBlocked++;
            System.Action<Vector3> meParry = p => fight.PlayerParried++;
            System.Action meStagger = () => fight.PlayerStaggered++;
            System.Action meGuardBroken = () => fight.PlayerGuardBroken++;
            foe.Damaged += dealt;
            vitals.Damaged += taken;
            foe.Deflected += foeDeflect;
            foe.GuardStruck += foeGuard;
            foe.Parried += foeParry;
            foe.Staggered += foeStagger;
            foe.PoiseBroken += foePoise;
            vitals.Deflected += meDeflect;
            vitals.GuardStruck += meGuard;
            vitals.Parried += meParry;
            vitals.Staggered += meStagger;
            vitals.GuardBroken += meGuardBroken;

            float nextShot = 0.5f;
            float nextTrace = 0.0f;
            var playerMelee = player.GetComponent<MeleeAttacker>();
            var playerSouls = player.GetComponent<SoulsSwordCombat>();
            int shot = 0;
            float clickCooldown = 0.0f;
            float guardUntil = -1.0f;
            float strafeFlip = 0.0f;
            bool strafeRight = rng.Next(2) == 0;
            SoulsPhase lastFoePhase = SoulsPhase.Idle;
            int dashStep = 0;
            float wonAt = -1.0f;
            int clickHeldFrames = 0;

            while (t < FightSeconds)
            {
                float dt = Time.deltaTime > 0.0f ? Time.deltaTime : 1.0f / 60.0f;
                if (foe.IsDead)
                {
                    if (wonAt < 0.0f)
                    {
                        wonAt = t;
                        fight.Won = true;
                        fight.Seconds = t;
                    }
                    if (t - wonAt > 1.5f) break;
                }
                if (vitals.Health < HealBelow)
                {
                    vitals.Heal(vitals.MaxHealth);
                    fight.Lives++;
                }

                Vector3 me = player.transform.position;
                Vector3 them = enemy.transform.position;
                Vector3 flat = them - me;
                flat.y = 0.0f;
                float distance = flat.magnitude;
                bool down = ragdoll != null && ragdoll.IsLimp;
                if (down) fight.PlayerDownSeconds += dt;
                if (foeRagdoll != null && foeRagdoll.IsLimp) fight.EnemyDownSeconds += dt;
                if (distance > loadout.Reach + 3.0f && !foe.IsDead) fight.FarSeconds += dt;
                if (distance < 3.5f && t - lastHit > 2.0f && !foe.IsDead) fight.CloseQuietSeconds += dt;

                SoulsPhase foePhase = brain.Combat != null ? brain.Combat.Phase : SoulsPhase.Idle;
                bool foeWindingUp = foePhase == SoulsPhase.Startup && lastFoePhase != SoulsPhase.Startup;
                if (foeWindingUp) fight.EnemyAttackStarts++;
                lastFoePhase = foePhase;

                // Look at it: the camera's yaw toward it, and for a ranged weapon the crosshair onto its chest.
                if (distance > 0.1f && !foe.IsDead)
                {
                    Vector3 aimAt = foeChest != null ? foeChest.position : them + Vector3.up * 1.6f;
                    Vector3 from = camera.transform.position;
                    Vector3 look = loadout.Ranged ? aimAt - from : flat;
                    camera.Yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                    camera.Pitch = loadout.Ranged ? -Mathf.Asin(Mathf.Clamp(look.normalized.y, -1.0f, 1.0f)) * Mathf.Rad2Deg : ThirdPersonCamera.DefaultPitch;
                }

                bool forward = false, back = false, left = false, right = false, rmb = false;
                bool click = false;
                if (!down && !foe.IsDead)
                {
                    if (loadout.Ranged)
                    {
                        if (distance > loadout.Reach + 1.0f) forward = true;
                        else if (distance < loadout.Reach - 2.5f) back = true;
                        strafeFlip -= dt;
                        if (strafeFlip <= 0.0f)
                        {
                            strafeRight = !strafeRight;
                            strafeFlip = 1.0f + (float)rng.NextDouble() * 1.5f;
                        }
                        if (distance < loadout.Reach + 3.0f)
                        {
                            right = strafeRight;
                            left = !strafeRight;
                        }
                        rmb = loadout.AimWithRightClick && distance < loadout.Reach + 6.0f;
                        click = distance < loadout.Reach + 6.0f && (!loadout.AimWithRightClick || camera.AimCameraBlend > 0.8f);
                    }
                    else
                    {
                        // A wind-up close by: guard most times, dash back now and then.
                        if (foeWindingUp && distance < 3.5f)
                        {
                            double roll = rng.NextDouble();
                            if (roll < 0.55) guardUntil = t + 0.7f;
                            else if (roll < 0.75) dashStep = 1;
                        }
                        if (t < guardUntil) rmb = true;
                        else if (distance > loadout.Reach) forward = true;
                        else click = true;
                    }
                }

                // A dash back: S held, then shift tapped twice.
                if (dashStep > 0)
                {
                    back = true;
                    forward = false;
                    if (dashStep == 2 || dashStep == 6) Want(_keyboard.leftShiftKey, true);
                    else if (dashStep == 4) Want(_keyboard.leftShiftKey, false);
                    else if (dashStep == 8) { Want(_keyboard.leftShiftKey, false); dashStep = -1; }
                    dashStep++;
                }

                Want(_keyboard.wKey, forward);
                Want(_keyboard.sKey, back);
                Want(_keyboard.aKey, left);
                Want(_keyboard.dKey, right);
                Want(_mouse.rightButton, rmb);

                clickCooldown -= dt;
                if (loadout.Automatic)
                {
                    if (click && !IsHeld(_mouse.leftButton)) fight.Clicks++;
                    Want(_mouse.leftButton, click);
                }
                else if (IsHeld(_mouse.leftButton))
                {
                    if (++clickHeldFrames >= 2) Want(_mouse.leftButton, false);   // a click: down a couple of frames, then up
                }
                else if (click && clickCooldown <= 0.0f)
                {
                    Want(_mouse.leftButton, true);
                    fight.Clicks++;
                    clickHeldFrames = 0;
                    clickCooldown = loadout.Ranged ? 0.45f : 0.28f + (float)rng.NextDouble() * 0.15f;
                }

                if (t >= nextTrace)
                {
                    nextTrace += TraceEverySeconds;
                    fight.Trace.AppendLine($"    {t,5:0.0}s me {Flat(me)} them {Flat(them)} {distance:0.0} m, them {brain.State} {foePhase}"
                        + $"{(foe.HitstunRemaining > 0.0f ? " stunned" : string.Empty)}{(foe.StaggerChain.ShruggingAt(Time.time) ? " shrugging" : string.Empty)}"
                        + $", me {vitals.Health:0} hp{(down ? " down" : string.Empty)}"
                        + $" | their stamina {foe.Stamina / Mathf.Max(1.0f, foe.MaxStamina):0.00}, cooldown {MindField(brain, "_attackCooldown"):0.0}, backoff {MindField(brain, "_backOff"):0.0},"
                        + $" turn {EnemyPack.Turns.Holds(enemy.transform)}, me swinging {(playerMelee != null && playerMelee.IsSwinging)}, me blocking {vitals.IsBlocking}/{(playerSouls != null && playerSouls.IsBlocking)}, rmb {IsHeld(_mouse.rightButton)}/{_mouse.rightButton.isPressed}, round {Vector3.SignedAngle(Vector3.ProjectOnPlane(player.transform.forward, Vector3.up), -(me - them), Vector3.up):0}");
                }

                if (canShoot && t >= nextShot && Camera.main != null)
                {
                    nextShot += ShotEverySeconds;
                    Texture2D image = BuildingShapeCapture.Render(Camera.main);
                    BuildingShapeCapture.Save(image, folder, $"{fight.Loadout}-{fight.Enemy}-r{fight.Round}-{shot++:00}");
                    Object.Destroy(image);
                }

                Flush();
                yield return null;
                t += dt;
            }
            if (!fight.Won) fight.Seconds = t;
            fight.EnemyDashes = brain.Dashes;
            fight.EnemyGuards = brain.GuardsRaised;

            foe.Damaged -= dealt;
            vitals.Damaged -= taken;
            vitals.Deflected -= meDeflect;
            vitals.GuardStruck -= meGuard;
            vitals.Parried -= meParry;
            vitals.Staggered -= meStagger;
            vitals.GuardBroken -= meGuardBroken;
            yield return ReleaseAll();
            Object.Destroy(enemy);
            yield return null;
#else
            yield break;
#endif
        }
    }
}
