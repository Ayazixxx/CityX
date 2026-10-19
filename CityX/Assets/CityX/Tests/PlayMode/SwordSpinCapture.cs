using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: films the sword spin in the real scene, on the ground (from a held charge) and in the air, on the frames that matter
    /// (SwordSpinMove's, which are Smash's), from the side as Smash shows it and from behind as the game's camera does, to
    /// Logs/CityShots/spin-*.png, so the animation and the light can be LOOKED AT (green tests are no proof that it looks right).
    /// Explicit, so the suite skips it; run it by name with -Graphics:
    /// ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.SwordSpinCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class SwordSpinCapture : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private static readonly int[] GroundFrames = { 4, 6, 7, 9, 11, 13, 20, 33, 39, 44, 49, 60, 72 };
        private static readonly int[] AirFrames = { 2, 6, 8, 10, 12, 16, 22, 31, 38, 45, 47, 49, 52, 60, 80 };

        /// <summary>The frames the sword hand is also shot close up on, mid-spin.</summary>
        private static readonly int[] HandFrames = { 13, 20, 33 };

        /// <summary>
        /// Shoots the spin under way on each of <paramref name="frames"/> of its timeline. A coroutine resumes before the frame's animation is
        /// laid on the body (and batch mode never ends a frame for WaitForEndOfFrame), so it waits one frame more: the bones then hold the
        /// pose of the frame asked for.
        /// </summary>
        private IEnumerator ShootFrames(Camera camera, MeleeAttacker melee, Transform body, string prefix, string kind, int[] frames)
        {
            foreach (int frame in frames)
            {
                for (int i = 0; i < 600 && melee.IsSpinAttacking && melee.SpinElapsedSeconds < (frame + 1) / 60.0f - 1e-4f; i++)
                {
                    yield return null;
                }
                Vector3 centre = body.position + Vector3.up * 1.3f;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-{kind}-f{frame:00}-side", centre + body.right * 7.5f + Vector3.up * 0.3f, centre);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-{kind}-f{frame:00}-behind", centre - body.forward * 5.5f + Vector3.up * 2.4f, centre + body.forward);
                if (System.Array.IndexOf(HandFrames, frame) >= 0)
                {
                    // The sword hand close up, from above and in front of it: the grip (owner, 2026-10-18: "make sure the hand grip on the sword
                    // look right as well").
                    Transform hand = body.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
                    Vector3 outward = (hand.position - centre).normalized;
                    BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-{kind}-f{frame:00}-hand",
                        hand.position + Vector3.up * 0.7f + Vector3.Cross(Vector3.up, outward) * 0.9f - outward * 0.2f, hand.position + outward * 0.3f);
                }
            }
        }

        [UnityTest]
        public IEnumerator RenderTheSpin()
        {
            yield return Film(ItemRegistry.Sword, "spin");
        }

        /// <summary>The same with the one-handed combat sword: Logs/CityShots/spin-1h-*.png.</summary>
        [UnityTest]
        public IEnumerator RenderTheSpinWithTheOneHandedSword()
        {
            yield return Film(ItemRegistry.CombatSword, "spin-1h");
        }

        private IEnumerator Film(string sword, string prefix)
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            var melee = vitals.GetComponent<MeleeAttacker>();
            Inventory inventory = vitals.GetComponent<Inventory>();
            inventory.AddItem(sword, 1);
            inventory.RemoveFromStorage(sword, 1, out ItemInstanceData data);
            inventory.EquipItemId(sword, vitals.gameObject, data);
            vitals.InfiniteHealth = true;
            for (int i = 0; i < 400 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            yield return Seconds(0.5f);

            var cameraObject = new GameObject("SpinCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 55.0f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600.0f;
            camera.allowHDR = true;
            Transform body = vitals.transform;

            // Frame-exact: a batch frame is a fraction of a millisecond, and the animation is timed.
            Time.captureFramerate = 60;

            // ---- on the ground, from a held charge: the cocked pose, then the spin on its frames ----
            Press(_keyboard.digit2Key);
            yield return Seconds(0.45f);
            {
                Vector3 centre = body.position + Vector3.up * 1.3f;
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-charge-side", centre + body.right * 7.5f + Vector3.up * 0.3f, centre);
                BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"{prefix}-charge-behind", centre - body.forward * 5.5f + Vector3.up * 2.4f, centre + body.forward);
            }
            Release(_keyboard.digit2Key);
            yield return ShootFrames(camera, melee, body, prefix, "ground", GroundFrames);
            yield return Seconds(1.0f);

            // ---- in the air ----
            float groundY = body.position.y;
            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            for (int i = 0; i < 600 && body.position.y - groundY < 0.6f; i++)
            {
                yield return null;
            }
            Press(_keyboard.digit2Key);
            yield return null;
            Release(_keyboard.digit2Key);
            yield return ShootFrames(camera, melee, body, prefix, "air", AirFrames);
            Debug.Log($"CityX: spin capture wrote to {BuildingShapeCapture.ShotsFolder}; kind {melee.CurrentSpinKind}, animation weight {locomotion.BakedPoseWeight:0.00}");
            Time.captureFramerate = 0;
            Object.Destroy(cameraObject);
#else
            yield break;
#endif
        }
    }
}
