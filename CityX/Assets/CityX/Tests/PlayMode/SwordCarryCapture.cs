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
    /// The one-handed sword CARRIED on the move (owner, 2026-10-17: "he is moving is arm very akwardly make it fluid motion no jumps too no
    /// weird bends", then "make you own sword running and walking animations and make them look good"): the combat sword stood, walked,
    /// sprinted, strafed and backed up with, on its own carry clips. Renders a film strip of each (rows: from the right side, from behind like
    /// the game camera, from the front-right) and logs how the sword arm moves: its fastest turn of the hand, jump of the hand, change of the
    /// elbow's bend, and the wrist's bend.
    ///
    /// Run with a graphics device: tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SwordCarryCapture
    /// Output: CityX/Logs/CityShots/carry-&lt;scenario&gt;.png
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SwordCarryCapture
    {
        private const int Cell = 240;
        private const int Columns = 12;
        private const int Rows = 3;
        private const float Dt = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheCarryOnTheMove()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -0.5f, 0);
            ground.transform.localScale = new Vector3(400, 1, 400);
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
            var log = new StringBuilder();
            foreach ((string scenario, Vector2 stick, bool sprint, bool swing) in new[]
            {
                ("idle", Vector2.zero, false, false),
                ("walk", new Vector2(0.0f, 1.0f), false, false),
                ("sprint", new Vector2(0.0f, 1.0f), true, false),
                ("strafe", new Vector2(1.0f, 0.0f), false, false),
                ("back", new Vector2(0.0f, -1.0f), false, false),
                // A light attack at the start, then the stance held after it (owner, 2026-10-17: "this is the pose after swinging").
                ("ready", Vector2.zero, false, true),
                ("ready-walk", new Vector2(0.0f, 1.0f), false, true),
            })
            {
                var player = Object.Instantiate(playerPrefab, new Vector3(0, 0.05f, -100.0f), Quaternion.identity);
                player.GetComponent<PlayerInputReader>().enabled = false;
                var locomotion = player.GetComponent<PlayerLocomotion>();
                player.GetComponent<PlayerVitals>().InfiniteStamina = true;
                SwordWeapon sword = Object.Instantiate(swordPrefab, new Vector3(30, 1, 30), Quaternion.identity).GetComponent<SwordWeapon>();
                sword.OnEquip(player, CityX.Items.ItemInstanceData.None);
                Animator animator = player.GetComponentInChildren<Animator>();
                Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                Transform elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);

                var input = new LocomotionInput { Sword1HEquipped = true, Move = stick, Sprint = sprint };
                for (int i = 0; i < 90; i++)
                {
                    locomotion.Tick(input, Dt);
                    yield return null;
                }

                var sheet = new Texture2D(Cell * Columns, Cell * Rows, TextureFormat.RGB24, false);
                const float Span = 1.6f;
                float shotEvery = Span / Columns;
                float nextShot = 0.0f;
                int column = 0;
                Quaternion lastHand = hand.rotation;
                Vector3 lastHandLocal = player.transform.InverseTransformPoint(hand.position);
                float lastBend = ElbowBend(shoulder, elbow, hand);
                float worstTurn = 0.0f, worstJump = 0.0f, worstBendRate = 0.0f, worstWrist = 0.0f, worstTurnAt = 0.0f;
                float bendMin = 180.0f, bendMax = 0.0f, foreMin = 9.0f, foreMax = -9.0f;
                var combat = player.GetComponent<SoulsSwordCombat>();
                combat.HitStopSeconds = 0.0f;
                if (swing)
                {
                    combat.RequestLight(0.0f);
                }
                for (float t = 0.0f; t < Span; t += Dt)
                {
                    combat.Tick(Dt);
                    input.Move = combat.IsCommitted ? stick * combat.CommittedMoveSpeedFraction : stick;
                    locomotion.Tick(input, Dt);
                    yield return null;
                    float turn = Quaternion.Angle(lastHand, hand.rotation);
                    Vector3 handLocal = player.transform.InverseTransformPoint(hand.position);
                    float bend = ElbowBend(shoulder, elbow, hand);
                    if (turn > worstTurn)
                    {
                        worstTurn = turn;
                        worstTurnAt = t;
                    }
                    worstJump = Mathf.Max(worstJump, (handLocal - lastHandLocal).magnitude);
                    worstBendRate = Mathf.Max(worstBendRate, Mathf.Abs(bend - lastBend));
                    worstWrist = Mathf.Max(worstWrist, Vector3.Angle(hand.position - elbow.position, hand.right));
                    bendMin = Mathf.Min(bendMin, bend);
                    bendMax = Mathf.Max(bendMax, bend);
                    float forward = Vector3.Dot(hand.position - shoulder.position, player.transform.forward);
                    foreMin = Mathf.Min(foreMin, forward);
                    foreMax = Mathf.Max(foreMax, forward);
                    lastHand = hand.rotation;
                    lastHandLocal = handLocal;
                    lastBend = bend;

                    if (t >= nextShot - 0.0001f && column < Columns)
                    {
                        for (int row = 0; row < Rows; row++)
                        {
                            Shoot(camera, sheet, column, player, row);
                        }
                        column++;
                        nextShot += shotEvery;
                    }
                }
                log.AppendLine($"{scenario}: hand turns at most {worstTurn:0.0} deg/frame (at {worstTurnAt:0.00} s), jumps at most {worstJump * 100.0f:0.0} cm/frame; " +
                               $"elbow bend {bendMin:0}-{bendMax:0} deg, changing at most {worstBendRate:0.0} deg/frame; wrist bent at most {worstWrist:0} deg; " +
                               $"hand fore/aft {foreMin:0.00}..{foreMax:0.00} m");
                Save(sheet, Path.Combine(directory, $"carry-{scenario}.png"));
                Object.Destroy(sword.gameObject);
                Object.Destroy(player);
                yield return null;
            }
            Debug.Log("CityX carry capture:\n" + log);

            Object.Destroy(cameraObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>How far the elbow is bent, degrees: 0 with the arm straight.</summary>
        private static float ElbowBend(Transform shoulder, Transform elbow, Transform hand)
        {
            return Vector3.Angle(elbow.position - shoulder.position, hand.position - elbow.position);
        }

        /// <summary>One cell: row 0 from the body's right side, row 1 from behind and above like the game camera, row 2 from the front, a little to its left.</summary>
        private static void Shoot(Camera camera, Texture2D sheet, int column, GameObject player, int row)
        {
            float height = player.GetComponent<PlayerLocomotion>().StandingHeight;
            Vector3 body = player.transform.position;
            Vector3 chest = body + Vector3.up * (height * 0.45f);
            Vector3 position;
            Vector3 lookAt;
            if (row == 0)
            {
                position = chest + player.transform.right * 5.0f + Vector3.up * 0.3f;
                lookAt = chest;
                camera.fieldOfView = 40.0f;
            }
            else if (row == 1)
            {
                position = body - player.transform.forward * 4.0f + Vector3.up * (height * 0.95f) + player.transform.right * 1.2f;
                lookAt = chest + player.transform.forward * 1.2f;
                camera.fieldOfView = 50.0f;
            }
            else
            {
                position = chest + player.transform.forward * 4.8f - player.transform.right * 1.2f + Vector3.up * 0.2f;
                lookAt = chest + Vector3.down * 0.3f;
                camera.fieldOfView = 42.0f;
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
            sheet.SetPixels(column * Cell, (Rows - 1 - row) * Cell, Cell, Cell, image.GetPixels());
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
