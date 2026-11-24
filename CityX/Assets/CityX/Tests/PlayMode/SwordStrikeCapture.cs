using System.Collections;
using System.IO;
using System.Text;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// Renders the posed sword attacks (SwordStrikes) on the real rig with the real swords, so they can be LOOKED AT: one contact sheet per
    /// attack (columns across the attack; rows: from the right side, from the front, from behind like the game camera), plus the ready stance
    /// and the guard, for the two-handed greatsword and the one-handed combat sword. The log carries the blade tip's path and, with two hands,
    /// how far the left palm strays from the grip.
    ///
    /// Run with a graphics device: tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SwordStrikeCapture
    /// Output: CityX/Logs/CityShots/strike-&lt;sword&gt;-&lt;attack&gt;.png
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SwordStrikeCapture
    {
        internal const int Cell = 300;
        private const float Dt = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheSwordAttacks()
        {
            yield return RenderTheAttacksOf(new[]
            {
                ("spike", "Assets/CityX/Weapons/SpikeSword.prefab"),   // the spikes are its edge: which side leads each cut is plain to see
                ("1h", "Assets/CityX/Weapons/CombatSword.prefab"),
                ("crowbar", "Assets/CityX/Weapons/MetalCrowbar.prefab"),
                ("bat", "Assets/CityX/Weapons/BaseballBat.prefab"),
                ("hammer", "Assets/CityX/Weapons/Sledgehammer.prefab"),
                ("knife", "Assets/CityX/Weapons/KitchenKnife.prefab"),
                ("spear", "Assets/CityX/Weapons/Spear.prefab"),
            });
        }

        /// <summary>The bo staff alone (docs/BO_STAFF.md): strike-staff-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderTheBoStaffAttacks()
        {
            yield return RenderTheAttacksOf(new[] { ("staff", "Assets/CityX/Weapons/BoStaff.prefab") });
        }

        /// <summary>The machete alone (docs/MACHETE.md): strike-machete-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderTheMacheteAttacks()
        {
            yield return RenderTheAttacksOf(new[] { ("machete", "Assets/CityX/Weapons/Machete.prefab") });
        }

        /// <summary>The spear alone (docs/SPEAR.md): strike-spear-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderTheSpearAttacks()
        {
            yield return RenderTheAttacksOf(new[] { ("spear", "Assets/CityX/Weapons/Spear.prefab"), ("spear-shield", "Assets/CityX/Weapons/Spear.prefab") });
        }

        /// <summary>
        /// Swung in the Blood Raven T-Shirt, with a fourth row close behind the back (owner, 2026-11-19: "some sword animations have a small bit
        /// of the players back clipping through the shirt"): strike-&lt;sword&gt;-shirt-*.png.
        /// </summary>
        [UnityTest]
        public IEnumerator RenderTheSwordAttacksInAShirt()
        {
            yield return RenderTheAttacksOf(new[]
            {
                ("1h" + InAShirt, "Assets/CityX/Weapons/CombatSword.prefab"),
                ("spike" + InAShirt, "Assets/CityX/Weapons/SpikeSword.prefab"),
            });
        }

        private const string InAShirt = "-shirt";

        /// <summary>The row close behind the torso (<see cref="Shoot"/>), shot only in a shirt.</summary>
        private const int CloseBehind = 3;

        private IEnumerator RenderTheAttacksOf((string, string)[] swords)
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(400, 1, 400);   // room to sprint through a swing
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45, -35, 0);
            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            camera.nearClipPlane = 0.05f;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            foreach ((string swordLabel, string swordPath) in swords)
            {
                var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swordPath);
                Assert.IsNotNull(swordPrefab, swordPath + " is missing -- rebuild the scene");
                var player = Object.Instantiate(playerPrefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                var vitals = player.GetComponent<PlayerVitals>();
                vitals.InfiniteStamina = true;
                var sword = Object.Instantiate(swordPrefab, new Vector3(10, 1, 10), Quaternion.identity).GetComponent<SwordWeapon>();
                sword.OnEquip(player, CityX.Items.ItemInstanceData.None);
                if (swordLabel.EndsWith("-shield"))
                {
                    // Beside a shield (docs/SPEAR.md): the left arm is the shield's, the spear the right hand's alone.
                    var shield = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/RoundShield.prefab"));
                    shield.GetComponent<CityX.Items.IEquippableItem>().OnEquip(player, CityX.Items.ItemInstanceData.None);
                }
                int rows = swordLabel.EndsWith(InAShirt) ? CloseBehind + 1 : CloseBehind;
                if (swordLabel.EndsWith(InAShirt))
                {
                    var shirt = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/BloodRavenTShirt.prefab"));
                    shirt.GetComponent<CityX.Items.IEquippableItem>().OnEquip(player, CityX.Items.ItemInstanceData.None);
                }
                var combat = player.GetComponent<SoulsSwordCombat>();
                combat.HitStopSeconds = 0.0f;
                var driver = player.GetComponent<PlayerAnimationDriver>();
                // (A spear walks as an empty hand does, its arm posed.)
                var idle = new LocomotionInput { SwordEquipped = !combat.IsOneHanded && !sword.IsSpear, Sword1HEquipped = combat.IsOneHanded };
                for (int i = 0; i < 60; i++)
                {
                    locomotion.Tick(idle, Dt);
                    yield return null;
                }

                Animator animator = player.GetComponentInChildren<Animator>();
                Transform rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                float armLength = Vector3.Distance(rightShoulder.position, rightElbow.position) + Vector3.Distance(rightElbow.position, rightHand.position);
                var log = new StringBuilder();
                log.AppendLine($"arm (shoulder to wrist) {armLength:0.000} m; shoulder {player.transform.InverseTransformPoint(rightShoulder.position).ToString("0.00")}");

                // The carry: standing with the sword in hand, nothing being done with it.
                {
                    var sheet = new Texture2D(Cell, Cell * rows, TextureFormat.RGB24, false);
                    ShootColumn(camera, sheet, 0, player, rows);
                    Save(sheet, Path.Combine(directory, $"strike-{swordLabel}-carry.png"));
                }

                // The stances: ready (after a swing) and the guard.
                foreach ((string label, System.Action start) in new (string, System.Action)[]
                {
                    ("guard", () => combat.SetBlockHeld(true)),
                })
                {
                    start();
                    for (int i = 0; i < 30; i++)
                    {
                        combat.SetBlockHeld(true);
                        combat.Tick(Dt);
                        vitals.TickHitstun(Dt);
                        locomotion.Tick(idle, Dt);
                        yield return null;
                    }
                    var sheet = new Texture2D(Cell, Cell * rows, TextureFormat.RGB24, false);
                    ShootColumn(camera, sheet, 0, player, rows);
                    Save(sheet, Path.Combine(directory, $"strike-{swordLabel}-{label}.png"));
                    combat.SetBlockHeld(false);
                    for (int i = 0; i < 20; i++)
                    {
                        combat.Tick(Dt);
                        vitals.TickHitstun(Dt);
                        locomotion.Tick(idle, Dt);
                        yield return null;
                    }
                }

                // The attacks: each from a fresh ready stance (the chain is advanced by buffering the next light), then on the move, the
                // keys held all through: the first light at a walk, and the running attack at a sprint.
                var attacks = new (string label, System.Action start, SoulsAttack attack, float pace)[]
                {
                    ("light1", () => combat.RequestLight(0.0f), combat.LightAttack(0), 0.0f),
                    ("light2", () => { combat.RequestLight(0.0f); }, combat.LightAttack(1), 0.0f),
                    ("light3", () => { combat.RequestLight(0.0f); }, combat.LightAttack(2), 0.0f),
                    ("light4", () => { combat.RequestLight(0.0f); }, combat.LightAttack(3), 0.0f),
                    ("heavy", () => combat.RequestHeavy(0.0f), combat.HeavyAttack, 0.0f),
                    ("walk", () => combat.RequestLight(0.0f), combat.OpenerAt(locomotion.WalkSpeed), locomotion.WalkSpeed),
                    ("run", () => combat.RequestLight(0.0f), combat.OpenerAt(locomotion.SprintSpeed), locomotion.SprintSpeed),
                };
                for (int a = 0; a < attacks.Length; a++)
                {
                    (string label, System.Action start, SoulsAttack attack, float pace) = attacks[a];
                    if (label == "light3" && combat.LightChainLength < 3)
                    {
                        continue;   // a knife's chain is two cuts back and forth: no third
                    }
                    if (label == "light4" && combat.LightChainLength < 4)
                    {
                        continue;   // only a staff's chain has a fourth
                    }
                    if (pace > 0.0f && combat.IsKnife)
                    {
                        continue;   // a knife has the one opener
                    }
                    LocomotionInput keys = idle;
                    if (pace > 0.0f)
                    {
                        keys.Move = Vector2.up;
                        keys.Sprint = pace > locomotion.WalkSpeed;
                        for (int i = 0; i < 80; i++)
                        {
                            combat.Tick(Dt);
                            vitals.TickHitstun(Dt);
                            locomotion.Tick(keys, Dt);
                            yield return null;
                        }
                    }
                    // Chain into the later lights: throw the earlier ones and buffer the next at each window.
                    for (int earlier = 0; earlier < a && a < 4; earlier++)
                    {
                        combat.RequestLight(0.0f);
                        SoulsAttack before = combat.LightAttack(earlier);
                        float until = (before.Startup + before.Active + before.Recovery * before.ChainAt) / combat.BladeSwingSpeed + 0.02f;
                        for (float t = 0.0f; t < until; t += Dt)
                        {
                            combat.Tick(Dt);
                            vitals.TickHitstun(Dt);
                            locomotion.Tick(idle, Dt);
                            yield return null;
                        }
                    }
                    start();
                    yield return null;
                    combat.Tick(Dt);
                    Assert.AreEqual(attack.Animation, combat.CurrentAttack?.Animation, $"{swordLabel} {label}: the wrong attack started");

                    // Nine evenly spaced moments, and a couple of frames after the contact itself (the blade trail at its brightest).
                    const int columns = 10;
                    float total = attack.Total / combat.BladeSwingSpeed;   // a knife swings quicker
                    var shots = new float[columns];
                    for (int c = 0; c < columns - 1; c++)
                    {
                        shots[c] = total * c / (columns - 2);
                    }
                    shots[columns - 1] = SwordStrikes.TryGet(attack.Animation, out SwordStrikeKind kind) ? total * SwordStrikes.ContactFraction(kind) + 2.0f * Dt : total;
                    System.Array.Sort(shots);
                    var sheet = new Texture2D(Cell * columns, Cell * rows, TextureFormat.RGB24, false);
                    int column = 0;
                    float lowestTip = float.MaxValue, worstLeft = 0.0f, fastest = 0.0f;
                    Vector3 previousTip = sword.BladeTipWorld;
                    log.AppendLine($"--- {swordLabel} {label} ({attack.Animation}): startup {attack.Startup:0.00} active {attack.Active:0.00} recovery {attack.Recovery:0.00} ---");
                    for (float t = 0.0f; t <= total + Dt && column < columns; t += Dt)
                    {
                        Vector3 tip = sword.BladeTipWorld;
                        Vector3 tipLocal = player.transform.InverseTransformPoint(tip);
                        float tipSpeed = (tip - previousTip).magnitude / Dt;
                        previousTip = tip;
                        lowestTip = Mathf.Min(lowestTip, tip.y);
                        fastest = Mathf.Max(fastest, tipSpeed);
                        float leftError = locomotion.SupportHandError;
                        worstLeft = Mathf.Max(worstLeft, leftError);
                        SwordFrame frame = locomotion.CurrentSwordFrame;
                        if (Mathf.RoundToInt(t / Dt) % 3 == 0)
                        {
                            // Where the wrist actually is against where the frame asked for it (both from the right shoulder, body axes, arm lengths).
                            Vector3 handActual = player.transform.InverseTransformDirection(rightHand.position - rightShoulder.position) / armLength;
                            Vector3 bladeActual = player.transform.InverseTransformDirection(sword.BladeTipWorld - sword.BladeBaseWorld).normalized;
                            log.AppendLine($"t={t:0.00} {combat.Phase,-8} progress={driver.SwordPoser.Progress:0.00} tip=({tipLocal.x:0.00},{tipLocal.y:0.00},{tipLocal.z:0.00}) " +
                                           $"tipSpeed={tipSpeed:0.0} hand=({handActual.x:0.00},{handActual.y:0.00},{handActual.z:0.00}) asked=({frame.Hand.x:0.00},{frame.Hand.y:0.00},{frame.Hand.z:0.00}) " +
                                           $"blade=({bladeActual.x:0.00},{bladeActual.y:0.00},{bladeActual.z:0.00}) asked=({frame.Blade.x:0.00},{frame.Blade.y:0.00},{frame.Blade.z:0.00}) " +
                                           $"twist={frame.TwistDegrees:0} lean={frame.LeanDegrees:0} drop={frame.HipDrop:0.00} left={leftError * 100:0}cm " +
                                           $"feet={(locomotion.SwordFootwork.IsStepping(0) ? "L" : "-")}{(locomotion.SwordFootwork.IsStepping(1) ? "R" : "-")}");
                        }
                        if (t >= shots[column] - 0.0001f)
                        {
                            ShootColumn(camera, sheet, column, player, rows);
                            column++;
                        }
                        combat.Tick(Dt);
                        vitals.TickHitstun(Dt);
                        locomotion.Tick(keys, Dt);
                        yield return null;
                    }
                    log.AppendLine($"{swordLabel} {label}: fastest tip {fastest:0.0} m/s, lowest tip {lowestTip:0.00} m, left palm off the grip at most {worstLeft * 100:0} cm");
                    Save(sheet, Path.Combine(directory, $"strike-{swordLabel}-{label}.png"));

                    // Let the stance settle and the chain expire before the next.
                    for (int i = 0; i < 240; i++)
                    {
                        combat.Tick(Dt);
                        vitals.TickHitstun(Dt);
                        locomotion.Tick(idle, Dt);
                        yield return null;
                    }
                }

                Debug.Log($"CityX sword capture ({swordLabel}):\n{log}");
                Object.Destroy(sword.gameObject);
                Object.Destroy(player);
                yield return null;
            }

            Object.Destroy(cameraObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>
        /// Close-ups of the hands on each sword's grip (the fingers folded round it: HandGrip), from three sides, standing in the ready stance after
        /// a guard. Output: CityX/Logs/CityShots/grip-&lt;sword&gt;.png (three columns: the right hand from the thumb's side, from above, and the whole
        /// hold from the front).
        /// </summary>
        [UnityTest]
        public IEnumerator RenderTheGrips()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(60, 1, 60);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45, -35, 0);
            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            camera.nearClipPlane = 0.02f;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            foreach ((string swordLabel, string swordPath) in new[]
            {
                ("spike", "Assets/CityX/Weapons/SpikeSword.prefab"),
                ("1h", "Assets/CityX/Weapons/CombatSword.prefab"),
                ("crowbar", "Assets/CityX/Weapons/MetalCrowbar.prefab"),
                ("bat", "Assets/CityX/Weapons/BaseballBat.prefab"),
                ("hammer", "Assets/CityX/Weapons/Sledgehammer.prefab"),
                ("knife", "Assets/CityX/Weapons/KitchenKnife.prefab"),
            })
            {
                var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swordPath);
                var player = Object.Instantiate(playerPrefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                var sword = Object.Instantiate(swordPrefab, new Vector3(10, 1, 10), Quaternion.identity).GetComponent<SwordWeapon>();
                sword.OnEquip(player, CityX.Items.ItemInstanceData.None);
                if (swordLabel.EndsWith("-shield"))
                {
                    // Beside a shield (docs/SPEAR.md): the left arm is the shield's, the spear the right hand's alone.
                    var shield = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/RoundShield.prefab"));
                    shield.GetComponent<CityX.Items.IEquippableItem>().OnEquip(player, CityX.Items.ItemInstanceData.None);
                }
                var combat = player.GetComponent<SoulsSwordCombat>();
                // (A spear walks as an empty hand does, its arm posed.)
                var idle = new LocomotionInput { SwordEquipped = !combat.IsOneHanded && !sword.IsSpear, Sword1HEquipped = combat.IsOneHanded };
                for (int i = 0; i < 40; i++)
                {
                    combat.SetBlockHeld(true);
                    combat.Tick(Dt);
                    locomotion.Tick(idle, Dt);
                    yield return null;
                }
                combat.SetBlockHeld(false);
                for (int i = 0; i < 30; i++)
                {
                    combat.Tick(Dt);
                    locomotion.Tick(idle, Dt);
                    yield return null;
                }

                Animator animator = player.GetComponentInChildren<Animator>();
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Vector3 grip = sword.transform.position;
                Vector3 thumbSide = hand.TransformDirection(Vector3.forward);
                Vector3 back = hand.TransformDirection(Vector3.up);
                var sheet = new Texture2D(Cell * 3, Cell, TextureFormat.RGB24, false);
                ShootAt(camera, sheet, 0, grip + thumbSide * 0.75f + Vector3.up * 0.1f, grip, 35.0f);
                ShootAt(camera, sheet, 1, grip + back * 0.7f + Vector3.up * 0.2f, grip, 35.0f);
                Vector3 mid = combat.IsOneHanded ? grip : (grip + leftHand.position) * 0.5f;
                ShootAt(camera, sheet, 2, mid + player.transform.forward * 1.6f + Vector3.up * 0.2f, mid, 40.0f);
                Save(sheet, Path.Combine(directory, $"grip-{swordLabel}.png"));
                Debug.Log($"CityX grip {swordLabel}: right grip {locomotion.RightGrip:0.00} left grip {locomotion.LeftGrip:0.00}, " +
                          $"index1_r folded {Quaternion.Angle(hand.Find("index1_r").localRotation, Quaternion.identity):0} deg");

                Object.Destroy(sword.gameObject);
                Object.Destroy(player);
                yield return null;
            }

            Object.Destroy(cameraObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>
        /// The sword hand up close through the first two cuts of the light chain: standing ready, wound up, as the blow lands and followed
        /// through, then the second cut (the backhand) wound up, halfway through its strike, as it lands and followed through (the columns),
        /// from the thumb's side, from the back of the hand and from in front of the body (the rows). What the fingers and the wrist do on
        /// the handle. Output: CityX/Logs/CityShots/swing-hand-&lt;sword&gt;.png and swing-hand-&lt;sword&gt;-light2.png.
        /// </summary>
        [UnityTest]
        public IEnumerator RenderTheHandThroughASwing()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(60, 1, 60);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45, -35, 0);
            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            camera.nearClipPlane = 0.02f;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            foreach ((string swordLabel, string swordPath) in new[]
            {
                ("1h", "Assets/CityX/Weapons/CombatSword.prefab"),
                ("spike", "Assets/CityX/Weapons/SpikeSword.prefab"),
                ("bat", "Assets/CityX/Weapons/BaseballBat.prefab"),
            })
            {
                var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swordPath);
                var player = Object.Instantiate(playerPrefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                var sword = Object.Instantiate(swordPrefab, new Vector3(10, 1, 10), Quaternion.identity).GetComponent<SwordWeapon>();
                sword.OnEquip(player, CityX.Items.ItemInstanceData.None);
                if (swordLabel.EndsWith("-shield"))
                {
                    // Beside a shield (docs/SPEAR.md): the left arm is the shield's, the spear the right hand's alone.
                    var shield = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/RoundShield.prefab"));
                    shield.GetComponent<CityX.Items.IEquippableItem>().OnEquip(player, CityX.Items.ItemInstanceData.None);
                }
                var combat = player.GetComponent<SoulsSwordCombat>();
                combat.HitStopSeconds = 0.0f;
                // (A spear walks as an empty hand does, its arm posed.)
                var idle = new LocomotionInput { SwordEquipped = !combat.IsOneHanded && !sword.IsSpear, Sword1HEquipped = combat.IsOneHanded };
                // Standing ready: a guard raised and lowered leaves the stance.
                for (int i = 0; i < 70; i++)
                {
                    combat.SetBlockHeld(i < 40);
                    combat.Tick(Dt);
                    locomotion.Tick(idle, Dt);
                    yield return null;
                }
                Transform hand = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
                SoulsAttack attack = combat.LightAttack(0);
                SwordStrikes.TryGet(attack.Animation, out SwordStrikeKind kind);
                SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(kind);
                float[] shots = { -1.0f, timing.WindUp + timing.Hold * 0.5f, timing.Contact, timing.Contact + timing.Follow };
                var sheet = new Texture2D(Cell * shots.Length, Cell * 3, TextureFormat.RGB24, false);
                int column = 0;
                for (float t = -2.0f * Dt; column < shots.Length; t += Dt)
                {
                    if (t >= shots[column] - 0.0001f)
                    {
                        ShootTheHand(camera, sheet, column++, sword, hand, player);
                    }
                    if (Mathf.Abs(t) < Dt * 0.5f)
                    {
                        combat.RequestLight(0.0f);
                    }
                    combat.Tick(Dt);
                    locomotion.Tick(idle, Dt);
                    yield return null;
                }
                Save(sheet, Path.Combine(directory, $"swing-hand-{swordLabel}.png"));

                // The chain's second cut, buffered until it starts.
                SoulsAttack second = combat.LightAttack(1);
                for (int frames = 0; frames < 120 && combat.CurrentAttack?.Animation != second.Animation; frames++)
                {
                    combat.RequestLight(0.0f);
                    combat.Tick(Dt);
                    locomotion.Tick(idle, Dt);
                    yield return null;
                }
                SwordStrikes.TryGet(second.Animation, out kind);
                timing = SwordStrikes.TimingOf(kind);
                shots = new[] { timing.WindUp + timing.Hold * 0.5f, timing.WindUp + timing.Hold + timing.Strike * 0.6f, timing.Contact, timing.Contact + timing.Follow };
                sheet = new Texture2D(Cell * shots.Length, Cell * 3, TextureFormat.RGB24, false);
                column = 0;
                for (float t = Dt; column < shots.Length; t += Dt)
                {
                    if (t >= shots[column] - 0.0001f)
                    {
                        ShootTheHand(camera, sheet, column++, sword, hand, player);
                    }
                    combat.Tick(Dt);
                    locomotion.Tick(idle, Dt);
                    yield return null;
                }
                Save(sheet, Path.Combine(directory, $"swing-hand-{swordLabel}-light2.png"));
                Object.Destroy(sword.gameObject);
                Object.Destroy(player);
                yield return null;
            }

            Object.Destroy(cameraObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>
        /// A blade striking a held guard, a frame every 0.05 s from just before the blow: from the side, from over the attacker's shoulder and from over the
        /// blocker's, as each would see it in the game. Output: CityX/Logs/CityShots/guard-clash.png.
        /// </summary>
        [UnityTest]
        public IEnumerator RenderTheGuardClash()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(60, 1, 60);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45, -35, 0);
            var cameraObject = new GameObject("Cam");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            camera.nearClipPlane = 0.05f;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab");
            var attacker = Object.Instantiate(playerPrefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
            var blocker = Object.Instantiate(playerPrefab, new Vector3(0, 0.05f, 1.9f), Quaternion.Euler(0, 180, 0));
            var bodies = new[] { attacker, blocker };
            var swords = new GameObject[2];
            var combats = new SoulsSwordCombat[2];
            var vitals = new PlayerVitals[2];
            var locomotions = new PlayerLocomotion[2];
            for (int i = 0; i < 2; i++)
            {
                bodies[i].GetComponent<PlayerInputReader>().enabled = false;
                swords[i] = Object.Instantiate(swordPrefab, new Vector3(10 + i, 1, 10), Quaternion.identity);
                swords[i].GetComponent<SwordWeapon>().OnEquip(bodies[i], CityX.Items.ItemInstanceData.None);
                combats[i] = bodies[i].GetComponent<SoulsSwordCombat>();
                vitals[i] = bodies[i].GetComponent<PlayerVitals>();
                vitals[i].InfiniteStamina = true;
                vitals[i].Roll = () => 1.0f;
                locomotions[i] = bodies[i].GetComponent<PlayerLocomotion>();
            }
            var idle = new LocomotionInput { Sword1HEquipped = true };
            System.Action tick = () =>
            {
                combats[1].SetBlockHeld(true);
                for (int i = 0; i < 2; i++)
                {
                    combats[i].Tick(Dt);
                    vitals[i].TickHitstun(Dt);
                    locomotions[i].Tick(idle, Dt);
                }
            };
            for (int i = 0; i < 60; i++)
            {
                tick();
                yield return null;
            }
            var log = new StringBuilder("CityX guard clash:\n");
            bool rebounded = false;
            combats[0].Rebounded += () => rebounded = true;
            combats[0].RequestLight(0.0f);
            const int columns = 12;
            var sheet = new Texture2D(Cell * columns, Cell * 3, TextureFormat.RGB24, false);
            int column = 0;
            int frame = 0;
            while (column < columns && frame < 120)
            {
                tick();
                yield return null;
                bool started = combats[0].Phase == SoulsPhase.Active || rebounded || column > 0;
                if (started && frame % 3 == 0)
                {
                    Vector3 middle = (attacker.transform.position + blocker.transform.position) * 0.5f + Vector3.up * 1.5f;
                    ShootAt(camera, sheet, column, middle + new Vector3(7.0f, 0.5f, 0.0f), middle, 36.0f, 2);
                    Vector3 pastAttacker = attacker.transform.position + attacker.transform.rotation * new Vector3(0.87f, 2.9f, -5.0f);
                    ShootAt(camera, sheet, column, pastAttacker, attacker.transform.position + attacker.transform.forward * 1.5f + Vector3.up * 1.5f, 50.0f, 1);
                    Vector3 pastBlocker = blocker.transform.position + blocker.transform.rotation * new Vector3(0.87f, 2.9f, -5.0f);
                    ShootAt(camera, sheet, column, pastBlocker, blocker.transform.position + blocker.transform.forward * 1.5f + Vector3.up * 1.5f, 50.0f, 0);
                    log.AppendLine($"col {column}: rebounded {rebounded} attacker z {attacker.transform.position.z:0.00} blocker z {blocker.transform.position.z:0.00} " +
                                   $"frozen {locomotions[0].IsImpactFrozen}/{locomotions[1].IsImpactFrozen} tip y {bodies[0].GetComponent<PlayerLocomotion>().HeldSword.BladeTipWorld.y:0.00}");
                    column++;
                }
                frame++;
            }
            Save(sheet, Path.Combine(directory, "guard-clash.png"));
            Debug.Log(log.ToString());

            for (int i = 0; i < 2; i++)
            {
                Object.Destroy(swords[i]);
                Object.Destroy(bodies[i]);
            }
            Object.Destroy(cameraObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            yield return null;
#else
            yield break;
#endif
        }

        /// <summary>One column of a hand sheet: the grip from the thumb's side, from the back of the hand and from in front of the body.</summary>
        private static void ShootTheHand(Camera camera, Texture2D sheet, int column, SwordWeapon sword, Transform hand, GameObject player)
        {
            Vector3 grip = sword.transform.position;
            ShootAt(camera, sheet, column, grip + hand.TransformDirection(Vector3.forward) * 0.7f + Vector3.up * 0.1f, grip, 35.0f, 2);
            ShootAt(camera, sheet, column, grip + hand.TransformDirection(Vector3.up) * 0.7f + Vector3.up * 0.1f, grip, 35.0f, 1);
            ShootAt(camera, sheet, column, grip + player.transform.forward * 1.5f + Vector3.up * 0.3f, grip, 40.0f, 0);
        }

        private static void ShootAt(Camera camera, Texture2D sheet, int column, Vector3 position, Vector3 lookAt, float fov, int row = 0)
        {
            camera.fieldOfView = fov;
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation((lookAt - position).normalized, Vector3.up);
            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            sheet.SetPixels(column * Cell, row * Cell, Cell, Cell, image.GetPixels());
            Object.Destroy(image);
        }

        /// <summary>A column of <paramref name="rows"/> cells, one per row of <see cref="Shoot"/>.</summary>
        private static void ShootColumn(Camera camera, Texture2D sheet, int column, GameObject player, int rows)
        {
            for (int row = 0; row < rows; row++)
            {
                Shoot(camera, sheet, column, player, row);
            }
        }

        /// <summary>
        /// One cell of the sheet: row 0 from the player's right, row 1 from the front, row 2 from behind the shoulder, <see cref="CloseBehind"/>
        /// close behind the back.
        /// </summary>
        internal static void Shoot(Camera camera, Texture2D sheet, int column, GameObject player, int row)
        {
            float height = player.GetComponent<PlayerLocomotion>().StandingHeight;
            Vector3 chest = player.transform.position + Vector3.up * (height * 0.55f);
            Vector3 position;
            Vector3 lookAt;
            float fov;
            switch (row)
            {
                case CloseBehind:
                    position = chest + new Vector3(0.3f, 0.35f, -2.0f);
                    lookAt = chest + Vector3.down * 0.1f;
                    fov = 40.0f;
                    break;
                case 0:
                    position = chest + new Vector3(5.2f, 0.4f, 0.5f);
                    lookAt = chest + Vector3.forward * 0.6f;
                    fov = 42.0f;
                    break;
                case 1:
                    position = chest + new Vector3(0.3f, 0.5f, 5.2f);
                    lookAt = chest;
                    fov = 42.0f;
                    break;
                default:
                    position = player.transform.position + new Vector3(0.6f, height * 0.85f, -4.2f);
                    lookAt = chest + Vector3.forward * 1.5f;
                    fov = 50.0f;
                    break;
            }
            camera.fieldOfView = fov;
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation((lookAt - position).normalized, Vector3.up);

            var target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
            sheet.SetPixels(column * Cell, (sheet.height / Cell - 1 - row) * Cell, Cell, Cell, image.GetPixels());   // row 0 at the top
            Object.Destroy(image);
        }

        internal static void Save(Texture2D sheet, string path)
        {
            sheet.Apply();
            // The test assembly has no ImageConversion reference (same as FistCapture): reached by name.
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(path, (byte[])encode.Invoke(null, new object[] { sheet }));
            Object.Destroy(sheet);
            Debug.Log("CityX: wrote " + path);
        }
    }
}
