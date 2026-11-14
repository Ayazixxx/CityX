using System.Collections;
using System.Collections.Generic;
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
    /// The sword swung ON THE MOVE (owner, 2026-10-16: "make sure his feet dont lock up during combat animations while moving"): the body walks
    /// forward, sideways and stands still while throwing the light chain, the way the input reader drives it (a swing takes nothing from the
    /// walk). Renders a film strip of each (rows: from the right side, from behind like the game camera) and logs
    /// how the feet behave: how far a foot on the ground SKATES (moves while it should be planted) and how far a planted foot is left
    /// BEHIND its hip (a foot locked while the body walks away from it).
    ///
    /// Run with a graphics device: tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SwordWalkSwingCapture
    /// Output: CityX/Logs/CityShots/swingwalk-&lt;sword&gt;-&lt;scenario&gt;.png
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SwordWalkSwingCapture
    {
        private const int Cell = 240;
        private const int Columns = 16;
        private const float Dt = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderSwingingOnTheMove()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(200, 1, 200);
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
            var log = new StringBuilder();
            foreach ((string swordLabel, string swordPath) in new[]
            {
                ("spike", "Assets/CityX/Weapons/SpikeSword.prefab"),
                ("1h", "Assets/CityX/Weapons/CombatSword.prefab"),
            })
            {
                foreach ((string scenario, Vector2 stick) in new[]
                {
                    ("forward", new Vector2(0.0f, 1.0f)),
                    ("sideways", new Vector2(1.0f, 0.0f)),
                    ("standing", Vector2.zero),
                })
                {
                    var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(swordPath);
                    Assert.IsNotNull(swordPrefab, swordPath + " is missing -- rebuild the scene");
                    var player = Object.Instantiate(playerPrefab, new Vector3(0, 0.05f, -6.0f), Quaternion.identity);
                    player.GetComponent<PlayerInputReader>().enabled = false;
                    var locomotion = player.GetComponent<PlayerLocomotion>();
                    var vitals = player.GetComponent<PlayerVitals>();
                    vitals.InfiniteStamina = true;
                    var sword = Object.Instantiate(swordPrefab, new Vector3(30, 1, 30), Quaternion.identity).GetComponent<SwordWeapon>();
                    sword.OnEquip(player, CityX.Items.ItemInstanceData.None);
                    var combat = player.GetComponent<SoulsSwordCombat>();
                    combat.HitStopSeconds = 0.0f;
                    Animator animator = player.GetComponentInChildren<Animator>();
                    Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
                    Transform[] hips = { animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
                    float soleHeight = BodyRig.FootY * feet[0].lossyScale.y;

                    // Settle, then walk a second before the first swing so the gait is up to speed.
                    var input = new LocomotionInput { SwordEquipped = !combat.IsOneHanded, Sword1HEquipped = combat.IsOneHanded };
                    for (int i = 0; i < 40; i++)
                    {
                        locomotion.Tick(input, Dt);
                        yield return null;
                    }

                    var sheet = new Texture2D(Cell * Columns, Cell * 2, TextureFormat.RGB24, false);
                    int column = 0;
                    const float Lead = 0.8f, Swinging = 2.6f;
                    float total = Lead + Swinging + 0.4f;
                    float shotEvery = (total - Lead) / Columns;
                    float nextShot = Lead;
                    float nextClick = Lead;
                    Vector3[] lastFoot = { feet[0].position, feet[1].position };
                    float[] skate = new float[2];
                    float[] worstSkateSpeed = new float[2];
                    float[] worstLag = new float[2];
                    float[] longestStill = new float[2];
                    float[] stillFor = new float[2];
                    int[] stepsTaken = new int[2];
                    bool[] wasDown = { true, true };
                    Vector3 start = player.transform.position;
                    for (float t = 0.0f; t < total; t += Dt)
                    {
                        // The input reader's rule: a swing takes nothing from the walk.
                        input.Move = stick;
                        input.YawDegrees = 0.0f;
                        if (t >= nextClick && t < Lead + Swinging)
                        {
                            combat.RequestLight(0.0f);
                            nextClick += 0.3f;
                        }
                        combat.Tick(Dt);
                        vitals.TickHitstun(Dt);
                        locomotion.Tick(input, Dt);
                        yield return null;

                        if (t >= Lead)
                        {
                            for (int f = 0; f < 2; f++)
                            {
                                Vector3 p = feet[f].position;
                                Vector3 flat = new Vector3(p.x - lastFoot[f].x, 0.0f, p.z - lastFoot[f].z);
                                bool down = p.y < soleHeight + 0.015f;   // on the ground: a step lifts it 10 cm, so a foot this low is planted or just landing
                                if (down && wasDown[f])
                                {
                                    skate[f] += flat.magnitude;
                                    worstSkateSpeed[f] = Mathf.Max(worstSkateSpeed[f], flat.magnitude / Dt);
                                }
                                if (down && !wasDown[f])
                                {
                                    stepsTaken[f]++;
                                }
                                wasDown[f] = down;
                                stillFor[f] = flat.magnitude / Dt < 0.05f ? stillFor[f] + Dt : 0.0f;
                                if (locomotion.CurrentSpeed > 0.5f)
                                {
                                    longestStill[f] = Mathf.Max(longestStill[f], stillFor[f]);
                                }
                                Vector3 hip = hips[f].position;
                                worstLag[f] = Mathf.Max(worstLag[f], new Vector2(p.x - hip.x, p.z - hip.z).magnitude);
                            }
                        }
                        lastFoot[0] = feet[0].position;
                        lastFoot[1] = feet[1].position;

                        if (t >= nextShot - 0.0001f && column < Columns)
                        {
                            Shoot(camera, sheet, column, player, 0);
                            Shoot(camera, sheet, column, player, 1);
                            column++;
                            nextShot += shotEvery;
                        }
                    }
                    float travelled = Vector3.Distance(start, player.transform.position);
                    log.AppendLine($"{swordLabel} {scenario}: travelled {travelled:0.00} m; steps L {stepsTaken[0]} R {stepsTaken[1]}; " +
                                   $"skated L {skate[0]:0.00} m (worst {worstSkateSpeed[0]:0.0} m/s) R {skate[1]:0.00} m (worst {worstSkateSpeed[1]:0.0} m/s); " +
                                   $"foot from its hip at most L {worstLag[0]:0.00} R {worstLag[1]:0.00} m; longest still while moving L {longestStill[0]:0.00} R {longestStill[1]:0.00} s");
                    Save(sheet, Path.Combine(directory, $"swingwalk-{swordLabel}-{scenario}.png"));
                    Object.Destroy(sword.gameObject);
                    Object.Destroy(player);
                    yield return null;
                }
            }
            Debug.Log("CityX swing-walk capture:\n" + log);

            Object.Destroy(cameraObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>One cell: row 0 from the body's right side, row 1 from behind and above like the game camera.</summary>
        private static void Shoot(Camera camera, Texture2D sheet, int column, GameObject player, int row)
        {
            float height = player.GetComponent<PlayerLocomotion>().StandingHeight;
            Vector3 body = player.transform.position;
            Vector3 chest = body + Vector3.up * (height * 0.45f);
            Vector3 position;
            Vector3 lookAt;
            if (row == 0)
            {
                position = chest + player.transform.right * 6.0f + Vector3.up * 0.3f;
                lookAt = chest;
                camera.fieldOfView = 40.0f;
            }
            else
            {
                position = body - player.transform.forward * 5.0f + Vector3.up * (height * 0.95f) + player.transform.right * 0.8f;
                lookAt = chest + player.transform.forward * 1.2f;
                camera.fieldOfView = 50.0f;
            }
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
            sheet.SetPixels(column * Cell, (1 - row) * Cell, Cell, Cell, image.GetPixels());
            Object.Destroy(image);
        }

        private static void Save(Texture2D sheet, string path)
        {
            sheet.Apply();
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            File.WriteAllBytes(path, (byte[])encode.Invoke(null, new object[] { sheet }));
            Object.Destroy(sheet);
            Debug.Log("CityX: wrote " + path);
        }
    }
}
