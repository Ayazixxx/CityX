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
        private const int Cell = 300;
        private const float Dt = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheSwordAttacks()
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
            foreach ((string swordLabel, string swordPath) in new[]
            {
                ("spike", "Assets/CityX/Weapons/SpikeSword.prefab"),   // the spikes are its edge: which side leads each cut is plain to see
                ("1h", "Assets/CityX/Weapons/CombatSword.prefab"),
                ("crowbar", "Assets/CityX/Weapons/MetalCrowbar.prefab"),
                ("bat", "Assets/CityX/Weapons/BaseballBat.prefab"),
                ("2h", "Assets/CityX/Weapons/Sword.prefab"),
            })
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
                var combat = player.GetComponent<SoulsSwordCombat>();
                combat.HitStopSeconds = 0.0f;
                var driver = player.GetComponent<PlayerAnimationDriver>();
                var idle = new LocomotionInput { SwordEquipped = !combat.IsOneHanded };
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
                    var sheet = new Texture2D(Cell, Cell * 3, TextureFormat.RGB24, false);
                    Shoot(camera, sheet, 0, player, 0);
                    Shoot(camera, sheet, 0, player, 1);
                    Shoot(camera, sheet, 0, player, 2);
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
                    var sheet = new Texture2D(Cell, Cell * 3, TextureFormat.RGB24, false);
                    Shoot(camera, sheet, 0, player, 0);
                    Shoot(camera, sheet, 0, player, 1);
                    Shoot(camera, sheet, 0, player, 2);
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

                // The attacks: each from a fresh ready stance (the chain is advanced by buffering the next light).
                var attacks = new (string label, System.Action start, SoulsAttack attack)[]
                {
                    ("light1", () => combat.RequestLight(0.0f), combat.LightAttack(0)),
                    ("light2", () => { combat.RequestLight(0.0f); }, combat.LightAttack(1)),
                    ("light3", () => { combat.RequestLight(0.0f); }, combat.LightAttack(2)),
                    ("heavy", () => combat.RequestHeavy(0.0f), combat.HeavyAttack),
                };
                for (int a = 0; a < attacks.Length; a++)
                {
                    (string label, System.Action start, SoulsAttack attack) = attacks[a];
                    if (label == "light3" && combat.LightChainLength < 3)
                    {
                        continue;   // a one-handed chain is two slashes back and forth: no third
                    }
                    // Chain into the second and third lights: throw the earlier ones and buffer the next at each window.
                    for (int earlier = 0; earlier < a && a < 3; earlier++)
                    {
                        combat.RequestLight(0.0f);
                        SoulsAttack before = combat.LightAttack(earlier);
                        float until = before.Startup + before.Active + before.Recovery * before.ChainAt + 0.02f;
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

                    const int columns = 9;
                    float total = attack.Total;
                    var sheet = new Texture2D(Cell * columns, Cell * 3, TextureFormat.RGB24, false);
                    int column = 0;
                    float nextShot = 0.0f;
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
                                           $"twist={frame.TwistDegrees:0} lean={frame.LeanDegrees:0} drop={frame.HipDrop:0.00} left={leftError * 100:0}cm");
                        }
                        if (t >= nextShot - 0.0001f)
                        {
                            Shoot(camera, sheet, column, player, 0);
                            Shoot(camera, sheet, column, player, 1);
                            Shoot(camera, sheet, column, player, 2);
                            column++;
                            nextShot += total / (columns - 1);
                        }
                        combat.Tick(Dt);
                        vitals.TickHitstun(Dt);
                        locomotion.Tick(idle, Dt);
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
                ("2h", "Assets/CityX/Weapons/Sword.prefab"),
            })
            {
                var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swordPath);
                var player = Object.Instantiate(playerPrefab, new Vector3(0, 0.05f, 0), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                var sword = Object.Instantiate(swordPrefab, new Vector3(10, 1, 10), Quaternion.identity).GetComponent<SwordWeapon>();
                sword.OnEquip(player, CityX.Items.ItemInstanceData.None);
                var combat = player.GetComponent<SoulsSwordCombat>();
                var idle = new LocomotionInput { SwordEquipped = !combat.IsOneHanded };
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

        private static void ShootAt(Camera camera, Texture2D sheet, int column, Vector3 position, Vector3 lookAt, float fov)
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
            sheet.SetPixels(column * Cell, 0, Cell, Cell, image.GetPixels());
            Object.Destroy(image);
        }

        /// <summary>One cell of the sheet: row 0 from the player's right, row 1 from the front, row 2 from behind the shoulder.</summary>
        private static void Shoot(Camera camera, Texture2D sheet, int column, GameObject player, int row)
        {
            float height = player.GetComponent<PlayerLocomotion>().StandingHeight;
            Vector3 chest = player.transform.position + Vector3.up * (height * 0.55f);
            Vector3 position;
            Vector3 lookAt;
            float fov;
            switch (row)
            {
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
            sheet.SetPixels(column * Cell, (2 - row) * Cell, Cell, Cell, image.GetPixels());
            Object.Destroy(image);
        }

        private static void Save(Texture2D sheet, string path)
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
