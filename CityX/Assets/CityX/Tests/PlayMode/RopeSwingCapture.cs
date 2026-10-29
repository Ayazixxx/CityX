using System.Collections;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: renders the ragdoll rope swing (docs/GRAPPLE_RAGDOLL.md) to Logs/CityShots/rope-swing-*.png -- the body hanging limp from the
    /// rope on its hand at three moments of a swing, from the side and from behind, with the real grappling hook drawing the rope. Explicit;
    /// run by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.RopeSwingCapture
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class RopeSwingCapture
    {
        private const float Step = 1.0f / 60.0f;

        [UnityTest]
        public IEnumerator RenderTheRopeSwing()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            bool modeBefore = GrappleSwing.RagdollMode;
            GrappleSwing.RagdollMode = true;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(2000.0f, 1.0f, 2000.0f);
            var sunObject = new GameObject("Sun");
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.transform.rotation = Quaternion.Euler(40.0f, -30.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 45.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.68f, 0.76f);

            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            // In the air, as a body that has jumped off a roof: hooked from the ground it would run with the rope until it swung off.
            GameObject player = Object.Instantiate(prefab, new Vector3(0.0f, 7.0f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var swing = player.GetComponent<GrappleSwing>();
            if (swing == null)
            {
                swing = player.AddComponent<GrappleSwing>();
            }
            swing.Bind();
            player.GetComponent<Inventory>().EquipItemId(ItemRegistry.GrapplingHook, player);
            var ragdoll = player.GetComponent<Ragdoll>();
            var locomotion = player.GetComponent<PlayerLocomotion>();
            if (swing.EquippedHook == null)
            {
                swing.EquippedHook = player.GetComponentInChildren<IGrappleLauncher>();
            }
            Assert.IsNotNull(swing.EquippedHook, "no grappling hook in hand to draw the rope");
            for (int i = 0; i < 3; i++)
            {
                locomotion.Tick(new LocomotionInput { YawDegrees = 0.0f }, Step);
                yield return null;
            }

            Vector3 anchor = player.transform.position + new Vector3(0.0f, 6.0f, 7.0f);
            swing.Attach(anchor, Vector3.forward * 7.0f);
            float[] moments = { 0.4f, 1.1f, 2.0f };
            float t = 0.0f;
            for (int shot = 0; shot < moments.Length; shot++)
            {
                for (; t < moments[shot]; t += Step)
                {
                    swing.Tick(new Vector2(0.0f, 1.0f), 0.0f, false, false, false, false, Step);
                    ragdoll.Tick(Step);
                    yield return null;
                }
                Vector3 torso = ragdoll.Torso.position;
                Vector3 look = (torso + anchor) * 0.5f;
                yield return ArcaneOrbCapture.ShootFacing(camera, $"rope-swing-{shot + 1}-side", look + new Vector3(16.0f, -1.0f, 0.0f), look);
                yield return ArcaneOrbCapture.ShootFacing(camera, $"rope-swing-{shot + 1}-close", torso + new Vector3(4.5f, 0.5f, 1.5f), torso);
            }
            yield return ArcaneOrbCapture.ShootFacing(camera, "rope-swing-behind", ragdoll.Torso.position + new Vector3(0.0f, 1.0f, -7.0f), anchor * 0.3f + ragdoll.Torso.position * 0.7f);
            swing.Release(false);
            for (float w = 0.0f; w < 6.0f && ragdoll.IsLimp; w += Step)
            {
                ragdoll.Tick(Step);
                yield return null;
            }
            yield return ArcaneOrbCapture.ShootFacing(camera, "rope-swing-landed", player.transform.position + new Vector3(5.0f, 2.0f, 4.0f), player.transform.position + Vector3.up * 1.2f);
            Debug.Log($"CityX rope swing capture: limp {ragdoll.IsLimp} after the release, body at {player.transform.position}");

            GrappleSwing.RagdollMode = modeBefore;
            Object.Destroy(player);
            Object.Destroy(camera.gameObject);
            Object.Destroy(ground);
            Object.Destroy(sunObject);
#else
            yield break;
#endif
        }
    }
}
