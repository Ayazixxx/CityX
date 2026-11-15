using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the kitchen knife (docs/KNIFE.md) to Logs/CityShots/knife-*.png -- the stand-in model on its own, flat on and from
    /// the edge, and held in the hand, close. Its swings are in SwordStrikeCapture (strike-knife-*.png). Explicit; run by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.KnifeCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class KnifeCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheKnife()
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
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/KitchenKnife.prefab");
            Assert.IsNotNull(prefab, "no KitchenKnife.prefab -- rebuild the scene");
            GameObject alone = Object.Instantiate(prefab, new Vector3(5.0f, 1.0f, 0.0f), Quaternion.identity);
            Vector3 middle = alone.transform.position + Vector3.up * 0.04f;
            yield return Shoot(camera, "knife-model-flat", middle + Vector3.forward * 0.75f, middle);
            yield return Shoot(camera, "knife-model-quarter", middle + new Vector3(0.45f, 0.25f, 0.55f), middle);
            yield return Shoot(camera, "knife-model-edge", middle + Vector3.left * 0.75f, middle);
            Object.Destroy(alone);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var knife = Object.Instantiate(prefab).GetComponent<SwordWeapon>();
            knife.OnEquip(player, ItemInstanceData.None);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 60; i++)
            {
                locomotion.Tick(new LocomotionInput { Sword1HEquipped = true }, Step);
                yield return null;
            }
            Vector3 hand = knife.BladeBaseWorld;
            yield return Shoot(camera, "knife-in-hand", hand + new Vector3(0.9f, 0.35f, 0.9f), hand + Vector3.up * 0.05f);
            yield return Shoot(camera, "knife-held", player.transform.position + new Vector3(2.6f, 2.0f, 3.4f), player.transform.position + Vector3.up * 1.3f);

            foreach (GameObject go in new[] { player, knife.gameObject, camera.gameObject, ground, sun.gameObject })
            {
                if (go != null) Object.Destroy(go);
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// The knife's heavy, the icepick stab (owner, 2026-11-15), frame by frame round the spin into the reverse grip and back, to
        /// Logs/CityShots/knife-stab.png (rows: from the right, from the front, from behind as the game's camera) and close-ups of the fist at the top
        /// of the raise (knife-stab-top-*.png). The log has the spin and the knife's direction each frame.
        /// </summary>
        [UnityTest]
        public IEnumerator RenderTheStab()
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
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var knife = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/KitchenKnife.prefab")).GetComponent<SwordWeapon>();
            knife.OnEquip(player, ItemInstanceData.None);
            var locomotion = player.GetComponent<PlayerLocomotion>();
            var combat = player.GetComponent<SoulsSwordCombat>();
            combat.HitStopSeconds = 0.0f;
            var poser = player.GetComponent<SwordPoseDriver>();
            var input = new LocomotionInput { Sword1HEquipped = true };
            for (int i = 0; i < 60; i++)
            {
                combat.Tick(Step);
                locomotion.Tick(input, Step);
                yield return null;
            }

            // Every frame from the click past the top (the knife swings at 1.3 times: the whole stab is about 41 frames), then every third, to the end.
            const int columns = 22;
            var sheet = new Texture2D(SwordStrikeCapture.Cell * columns, SwordStrikeCapture.Cell * 3, TextureFormat.RGB24, false);
            var log = new System.Text.StringBuilder();
            combat.RequestHeavy(0.0f);
            int column = 0, frame = 0;
            bool shotTheTop = false;
            SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(SwordStrikeKind.Thrust1H);
            float top = (timing.WindUp + timing.Hold * 0.6f) / timing.Total;
            while (poser.IsStriking && column < columns && frame < 120)
            {
                combat.Tick(Step);
                locomotion.Tick(input, Step);
                yield return null;
                frame++;
                Vector3 blade = player.transform.InverseTransformDirection(knife.BladeTipWorld - knife.BladeBaseWorld).normalized;
                log.AppendLine($"frame {frame} progress {poser.Progress:0.00} spin {poser.GripFlip:0.00} knife ({blade.x:0.00},{blade.y:0.00},{blade.z:0.00})");
                if (frame <= 12 || frame % 3 == 0)
                {
                    for (int row = 0; row < 3; row++)
                    {
                        SwordStrikeCapture.Shoot(camera, sheet, column, player, row);
                    }
                    column++;
                }
                if (!shotTheTop && poser.Progress >= top)
                {
                    shotTheTop = true;
                    Vector3 fist = knife.BladeBaseWorld;
                    camera.fieldOfView = 30.0f;
                    yield return Shoot(camera, "knife-stab-top-side", fist + new Vector3(1.4f, 0.1f, 0.3f), fist + Vector3.down * 0.1f);
                    yield return Shoot(camera, "knife-stab-top-front", fist + new Vector3(0.2f, 0.1f, 1.5f), fist + Vector3.down * 0.1f);
                    // (The game's camera is 0.87 m right of the body and 5 m behind it.)
                    yield return Shoot(camera, "knife-stab-top-game", fist + new Vector3(0.3f, 0.1f, -2.6f), fist + Vector3.down * 0.1f);
                }
            }
            SwordStrikeCapture.Save(sheet, System.IO.Path.Combine(BuildingShapeCapture.ShotsFolder, "knife-stab.png"));
            Debug.Log("CityX knife stab:\n" + log);

            foreach (GameObject go in new[] { player, knife.gameObject, camera.gameObject, ground, sun.gameObject })
            {
                if (go != null) Object.Destroy(go);
            }
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }

        private static IEnumerator Shoot(Camera camera, string name, Vector3 position, Vector3 lookAt)
        {
            yield return null;
            BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, name, position, lookAt);
        }
    }
}
