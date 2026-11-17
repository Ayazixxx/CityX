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
    /// Not a test: the one-handed sword's first two cuts, chained, from where the game's camera stands at rest behind the player, at moments
    /// through each from its wind-up to the end of its follow-through, the blade's trail drawn (owner, 2026-11-17: the first cut "more diagonal
    /// like the red line", the second following through as far). Written to Logs/CityShots/sword-cuts-behind-*.png. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SwordCutsFromBehindCapture
    {
        private const float Dt = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheFirstTwoCuts()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            LogAssert.ignoreFailingMessages = true;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45.0f, -35.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);
            camera.fieldOfView = 50.0f;
            // The game's camera at rest (SwordGifComboPlayTests.AsTheCameraSeesIt): 0.87 m right of the pivot, DefaultDistance back, DefaultPitch down.
            float pitch = ThirdPersonCamera.DefaultPitch;
            camera.transform.position = new Vector3(0.87f, 0.73f * BodyRig.StandingHeight + 0.6f + ThirdPersonCamera.DefaultDistance * Mathf.Sin(pitch * Mathf.Deg2Rad),
                -ThirdPersonCamera.DefaultDistance * Mathf.Cos(pitch * Mathf.Deg2Rad));
            camera.transform.rotation = Quaternion.Euler(pitch, 0.0f, 0.0f);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            SwordWeapon sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab"),
                new Vector3(40.0f, 1.0f, 40.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            sword.OnEquip(player, ItemInstanceData.None);
            var combat = player.GetComponent<SoulsSwordCombat>();
            var poser = player.GetComponent<SwordPoseDriver>();
            combat.HitStopSeconds = 0.0f;
            var input = new LocomotionInput { Sword1HEquipped = true };
            for (int i = 0; i < 60; i++)
            {
                combat.Tick(Dt);
                locomotion.Tick(input, Dt);
                yield return null;
            }
            string folder = BuildingShapeCapture.ShotsFolder;
            combat.RequestLight(0.0f);
            foreach ((SwordStrikeKind kind, string name) in new[] { (SwordStrikeKind.Slash1H, "first"), (SwordStrikeKind.Cut1H, "second") })
            {
                SwordStrikes.StrikeTiming timing = SwordStrikes.TimingOf(kind);
                float[] moments = { SwordStrikes.ActiveStart(kind), timing.Contact / timing.Total, (timing.Contact + timing.Follow) / timing.Total };
                int shot = 0;
                for (int frame = 0; frame < 200 && shot < moments.Length; frame++)
                {
                    if (poser.Kind == SwordStrikeKind.Slash1H && poser.Progress > SwordStrikes.ActiveEnd(SwordStrikeKind.Slash1H))
                    {
                        combat.RequestLight(0.0f);
                    }
                    combat.Tick(Dt);
                    locomotion.Tick(input, Dt);
                    yield return null;
                    if (poser.IsStriking && poser.Kind == kind && poser.Progress >= moments[shot])
                    {
                        BuildingShapeCapture.Shoot(camera, folder, $"sword-cuts-behind-{name}-{shot}", camera.transform.position,
                            camera.transform.position + camera.transform.forward);
                        shot++;
                    }
                }
            }
            Object.Destroy(sword.gameObject);
            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(sunObject);
            Object.Destroy(ground);
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
#else
            yield break;
#endif
        }
    }
}
