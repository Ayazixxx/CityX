using System.Collections;
using System.IO;
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
    /// The swordsman duel, rendered (owner, 2026-10-17: "make a swordsman npc ... so i can duel with him"): the same fight SwordsmanPlayTests
    /// runs, filmed from the side as it happens. Three strips: the player's blade bouncing off his guard and his punish (from the moment the
    /// player's swing rebounds), one of his dashes, and him circling at fighting distance on the sword's walk.
    ///
    /// Run with a graphics device: tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SwordsmanCapture
    /// Output: CityX/Logs/CityShots/duel-rebound.png, duel-dash.png, duel-circle.png
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SwordsmanCapture
    {
        private const int Cell = 300;
        private const int Columns = 10;
        private const float Dt = 1.0f / 60.0f;

        /// <summary>
        /// The swordsman standing, from the front, the side and behind: the owner's character "Knight" and the Knight Helmet (2026-10-18).
        /// Output: CityX/Logs/CityShots/swordsman-look-*.png
        /// </summary>
        [UnityTest]
        public IEnumerator RenderHisLook()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            Camera camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            camera.fieldOfView = 30.0f;
            GameObject swordsman = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            for (int i = 0; i < 90; i++)
            {
                yield return null;   // he puts on his sword and helmet and settles into his stance, with nobody to fight
            }
            Vector3 chest = swordsman.transform.position + Vector3.up * 1.6f;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "swordsman-look-front", chest + new Vector3(1.5f, 0.4f, 8.0f), chest);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "swordsman-look-side", chest + new Vector3(8.0f, 0.4f, 0.0f), chest);
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, "swordsman-look-behind", chest + new Vector3(-2.0f, 0.6f, -8.0f), chest);
            Object.Destroy(swordsman);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator RenderTheDuel()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(directory);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            Camera camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
            camera.fieldOfView = 40.0f;

            GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var playerSword = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab"))
                .GetComponent<SwordWeapon>();
            playerSword.OnEquip(player, CityX.Items.ItemInstanceData.None);
            GameObject swordsman = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Swordsman.prefab"),
                new Vector3(0.0f, 0.05f, 6.0f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
            var brain = swordsman.GetComponent<SwordsmanBrain>();
            brain.Seed = 20261017;
            brain.Target = player.GetComponent<PlayerVitals>();

            var vitals = player.GetComponent<PlayerVitals>();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var combat = player.GetComponent<SoulsSwordCombat>();
            vitals.InfiniteHealth = true;
            vitals.InfiniteStamina = true;
            swordsman.GetComponent<PlayerVitals>().InfiniteHealth = true;

            Texture2D rebound = null, dash = null, circle = null;
            int reboundColumn = 0, dashColumn = 0, circleColumn = 0;
            float clock = 0.0f, nextSwing = 1.5f, nextReboundShot = 0.0f, nextDashShot = 0.0f, nextCircleShot = 0.0f;
            bool filmingRebound = false, filmingDash = false, filmingCircle = false;
            int dashesSeen = 0;
            combat.Rebounded += () =>
            {
                if (rebound == null)
                {
                    rebound = new Texture2D(Cell * Columns, Cell, TextureFormat.RGB24, false);
                    filmingRebound = true;
                    nextReboundShot = clock;
                }
            };

            for (; clock < 40.0f && (rebound == null || dash == null || circle == null || filmingRebound || filmingDash || filmingCircle); clock += Dt)
            {
                combat.Tick(Dt);
                vitals.TickHitstun(Dt);
                vitals.TickStamina(false, Dt);
                Vector3 apart = swordsman.transform.position - player.transform.position;
                apart.y = 0.0f;
                float distance = apart.magnitude;
                float yaw = Quaternion.LookRotation(apart.sqrMagnitude > 1e-4f ? apart : Vector3.forward).eulerAngles.y;
                if (!vitals.IsStunned && !combat.IsCommitted && clock >= nextSwing && distance < 2.6f)
                {
                    combat.RequestLight(yaw);
                    nextSwing = clock + 0.9f;
                }
                Vector2 move = distance > 2.3f ? new Vector2(0.0f, 1.0f) : (distance < 1.6f ? new Vector2(0.0f, -0.5f) : Vector2.zero);
                if (combat.IsCommitted) move *= combat.CommittedMoveSpeedFraction;
                if (vitals.IsStunned) move = Vector2.zero;
                locomotion.Tick(new LocomotionInput { Move = move, YawDegrees = yaw, Sword1HEquipped = true, LockedOn = true }, Dt);
                yield return null;

                if (brain.Dashes > dashesSeen)
                {
                    dashesSeen = brain.Dashes;
                    if (dash == null)
                    {
                        dash = new Texture2D(Cell * Columns, Cell, TextureFormat.RGB24, false);
                        filmingDash = true;
                        nextDashShot = clock;
                    }
                }
                if (circle == null && brain.State == DuelistState.Circle && clock > 3.0f)
                {
                    circle = new Texture2D(Cell * Columns, Cell, TextureFormat.RGB24, false);
                    filmingCircle = true;
                    nextCircleShot = clock;
                }
                if (filmingRebound && clock >= nextReboundShot)
                {
                    Shoot(camera, rebound, reboundColumn++, player.transform.position, swordsman.transform.position);
                    nextReboundShot += 0.1f;
                    filmingRebound = reboundColumn < Columns;
                }
                if (filmingDash && clock >= nextDashShot)
                {
                    Shoot(camera, dash, dashColumn++, player.transform.position, swordsman.transform.position);
                    nextDashShot += 0.06f;
                    filmingDash = dashColumn < Columns;
                }
                if (filmingCircle && clock >= nextCircleShot)
                {
                    Shoot(camera, circle, circleColumn++, player.transform.position, swordsman.transform.position);
                    nextCircleShot += 0.15f;
                    filmingCircle = circleColumn < Columns;
                }
            }

            Assert.IsNotNull(rebound, "no swing of the player's bounced off his guard in 40 s");
            Save(rebound, Path.Combine(directory, "duel-rebound.png"));
            if (dash != null) Save(dash, Path.Combine(directory, "duel-dash.png"));
            if (circle != null) Save(circle, Path.Combine(directory, "duel-circle.png"));

            Object.Destroy(playerSword.gameObject);
            Object.Destroy(player);
            Object.Destroy(swordsman);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sun.gameObject);
            Object.Destroy(ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        /// <summary>From the side of the line between the two, far enough back to hold both, at chest height.</summary>
        private static void Shoot(Camera camera, Texture2D sheet, int column, Vector3 a, Vector3 b)
        {
            Vector3 middle = (a + b) * 0.5f + Vector3.up * 1.4f;
            Vector3 across = Vector3.Cross(Vector3.up, b - a);
            across.y = 0.0f;
            across = across.sqrMagnitude > 1e-4f ? across.normalized : Vector3.right;
            camera.transform.position = middle + across * 8.5f + Vector3.up * 0.6f;
            camera.transform.rotation = Quaternion.LookRotation(middle - camera.transform.position, Vector3.up);
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
