using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Another player's body in a crowd (CrowdDetail; owner, 2026-11-25: "something that will not tank performance in large crowds of people
    /// remember there is a hub"): near the camera in full, its shadow as it was; far off lighter, with no shadow and its feet not placed; off the
    /// screen unseen; near again, its shadow given back; and the same when the crowd's detail is taken off it. It needs things drawn to know
    /// what is seen: run with -Graphics.
    /// </summary>
    public sealed class CrowdDetailPlayTests
    {
        private const float Dt = 1.0f / 60.0f;
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (Object made in _made)
            {
                if (made != null) Object.Destroy(made);
            }
            _made.Clear();
        }

        private static IEnumerator Run(PlayerLocomotion body, Camera camera, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                body.Tick(new LocomotionInput(), Dt);
                camera.Render();
                yield return null;
            }
        }

        private static void Aim(Camera camera, Vector3 from, Vector3 at)
        {
            camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
        }

        [UnityTest]
        public IEnumerator NearInFullFarLighterOffTheScreenUnseenNearAgainGivenBack()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            _made.Add(ground);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var body = player.GetComponent<PlayerLocomotion>();
            var cameraObject = new GameObject("Cam") { tag = "MainCamera" };
            _made.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = new RenderTexture(320, 180, 24);
            _made.Add(camera.targetTexture);
            Vector3 chest = player.transform.position + Vector3.up * 1.4f;

            var shadowsWere = new Dictionary<Renderer, ShadowCastingMode>();
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>()) shadowsWere[r] = r.shadowCastingMode;
            Assert.IsTrue(shadowsWere.ContainsValue(ShadowCastingMode.On), "the body casts no shadow: nothing is tested");

            var detail = player.AddComponent<CrowdDetail>();
            Aim(camera, chest + new Vector3(0.0f, 0.5f, 6.0f), chest);
            yield return Run(body, camera, 40);
            Assert.AreEqual(CrowdTier.Full, detail.Tier, "near the camera the body is not in full");
            foreach (KeyValuePair<Renderer, ShadowCastingMode> was in shadowsWere)
            {
                Assert.AreEqual(was.Value, was.Key.shadowCastingMode, $"near the camera {was.Key.name} lost its shadow");
            }

            Aim(camera, chest + new Vector3(0.0f, 4.0f, CrowdTiers.FullWithin + 25.0f), chest);
            yield return Run(body, camera, 40);
            Assert.AreEqual(CrowdTier.Light, detail.Tier, "far off the body is still in full");
            foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
            {
                Assert.AreEqual(ShadowCastingMode.Off, r.shadowCastingMode, $"far off {r.name} casts a shadow");
            }
            Assert.IsFalse(body.FootIkActive, "far off the feet are placed");

            Aim(camera, chest + new Vector3(0.0f, 0.5f, 6.0f), chest + new Vector3(0.0f, 0.5f, 20.0f));   // turned away
            yield return Run(body, camera, 40);
            Assert.AreEqual(CrowdTier.Unseen, detail.Tier, "off the screen the body is still worked out");

            Aim(camera, chest + new Vector3(0.0f, 0.5f, 6.0f), chest);
            yield return Run(body, camera, 40);
            Assert.AreEqual(CrowdTier.Full, detail.Tier, "near again the body is not in full");
            foreach (KeyValuePair<Renderer, ShadowCastingMode> was in shadowsWere)
            {
                Assert.AreEqual(was.Value, was.Key.shadowCastingMode, $"near again {was.Key.name} did not get its shadow back");
            }

            Aim(camera, chest + new Vector3(0.0f, 4.0f, CrowdTiers.FullWithin + 25.0f), chest);
            yield return Run(body, camera, 40);
            Object.Destroy(detail);
            yield return null;
            foreach (KeyValuePair<Renderer, ShadowCastingMode> was in shadowsWere)
            {
                Assert.AreEqual(was.Value, was.Key.shadowCastingMode, $"taken out of the crowd, {was.Key.name} did not get its shadow back");
            }
#else
            yield break;
#endif
        }
    }
}
