using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The body in water (docs/WATER.md), the real player body on flat ground: knee-deep water slows a walk; water over the chest floats it,
    /// sinking while Space is let go and rising with it held to float with its head out; a flowing body of water carries it; a fall into deep
    /// water does not hurt; walking through water soaks the body; and the splashes answer a body wading.
    /// </summary>
    public sealed class WaterPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            CurrentWater.Clear();
        }

        private PlayerLocomotion BuildPlayer(Vector3 at)
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(ground);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(prefab, "Player.prefab is missing");
            GameObject player = Object.Instantiate(prefab, at, Quaternion.identity);
            _made.Add(player);
            var reader = player.GetComponent<PlayerInputReader>();
            if (reader != null) reader.enabled = false;
            Physics.SyncTransforms();
            return player.GetComponent<PlayerLocomotion>();
#else
            return null;
#endif
        }

        private WaterSurface Water(WaterBody body)
        {
#if UNITY_EDITOR
            var material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/Water.mat");
#else
            Material material = null;
#endif
            WaterSurface surface = WaterSurface.Create(body, material);
            _made.Add(surface.gameObject);
            return surface;
        }

        private static IEnumerator Walk(PlayerLocomotion body, float seconds, LocomotionInput input)
        {
            const float step = 1.0f / 60.0f;
            for (float t = 0.0f; t < seconds; t += step)
            {
                body.Tick(input, step);
                yield return null;
            }
        }

        private static LocomotionInput Forward => new LocomotionInput { Move = Vector2.up, YawDegrees = 0.0f };

        private IEnumerator Distance(PlayerLocomotion body, float seconds, System.Action<float> result)
        {
            Vector3 from = body.transform.position;
            yield return Walk(body, seconds, Forward);
            Vector3 travelled = body.transform.position - from;
            travelled.y = 0.0f;
            result(travelled.magnitude);
        }

        [UnityTest]
        public IEnumerator KneeDeepWaterSlowsAWalk()
        {
            PlayerLocomotion body = BuildPlayer(new Vector3(-40.0f, 0.1f, 0.0f));
            yield return Walk(body, 0.5f, default);
            float dry = 0.0f;
            yield return Distance(body, 2.0f, d => dry = d);

            Water(WaterBody.Square(new Vector3(40.0f, 0.0f, 0.0f), 30.0f, 0.7f));
            body.transform.position = new Vector3(40.0f, 0.1f, -10.0f);
            Physics.SyncTransforms();
            yield return Walk(body, 0.5f, default);
            float wet = 0.0f;
            yield return Distance(body, 2.0f, d => wet = d);

            Assert.Greater(body.WaterDepth, 0.5f, "the body is not in the water");
            Assert.IsFalse(body.IsFloating, "knee-deep water floated the body");
            float expected = Wading.SpeedShare(body.WaterDepthShare);
            Assert.AreEqual(expected, wet / dry, 0.08f, $"knee-deep the walk covered {wet:0.00} m against {dry:0.00} m dry");
        }

        [UnityTest]
        public IEnumerator DeepWaterFloatsTheBodySinkingUntilSpaceIsHeldThenItRisesWithItsHeadOut()
        {
            PlayerLocomotion body = BuildPlayer(new Vector3(0.0f, 0.1f, 0.0f));
            yield return Walk(body, 0.3f, default);
            Water(WaterBody.Square(new Vector3(0.0f, 0.0f, 0.0f), 30.0f, 6.0f));
            body.transform.position = new Vector3(0.0f, 4.0f, 0.0f);
            Physics.SyncTransforms();

            yield return Walk(body, 1.0f, default);
            Assert.IsTrue(body.IsFloating, "six metres of water did not float the body");
            float before = body.transform.position.y;
            yield return Walk(body, 1.0f, default);
            Assert.Less(body.transform.position.y, before - 0.2f, "let go, the body did not sink");

            yield return Walk(body, 6.0f, new LocomotionInput { JumpHeld = true });
            float height = body.StandingHeight * body.BodyScale;
            float headOut = body.transform.position.y + height - 6.0f;
            Assert.Greater(headOut, 0.4f, $"Space held, the head is {headOut:0.00} m out of the water");
            Assert.AreEqual(Wading.FloatingDepth, body.WaterDepthShare, 0.04f, "it does not float at its floating height");
            Assert.IsTrue(body.WaterBreaksFall, "deep water should break a fall");
        }

        [UnityTest]
        public IEnumerator FlowingWaterCarriesAFloatingBody()
        {
            PlayerLocomotion body = BuildPlayer(new Vector3(0.0f, 0.1f, 0.0f));
            WaterBody river = WaterBody.Square(new Vector3(0.0f, 0.0f, 0.0f), 60.0f, 6.0f);
            river.Flow = new Vector2(1.5f, 0.0f);
            Water(river);
            body.transform.position = new Vector3(0.0f, 3.0f, 0.0f);
            Physics.SyncTransforms();
            float startX = body.transform.position.x;
            yield return Walk(body, 3.0f, new LocomotionInput { JumpHeld = true });
            Assert.AreEqual(4.5f, body.transform.position.x - startX, 1.0f, "the flow did not carry the body about 1.5 m a second");
        }

        [UnityTest]
        public IEnumerator StandingInWaterSoaksTheBody()
        {
            PlayerLocomotion body = BuildPlayer(new Vector3(0.0f, 0.1f, 0.0f));
            var wetness = body.gameObject.AddComponent<PlayerWetness>();
            Water(WaterBody.Square(new Vector3(0.0f, 0.0f, 0.0f), 20.0f, 1.2f));
            yield return Walk(body, 1.5f, default);
            Assert.Greater(wetness.Wetness, 0.9f, $"waist-deep for 1.5 s the body is only {wetness.Wetness:0.00} wet");
        }

        [UnityTest]
        public IEnumerator WadingRingsAndDropsAnswerABodyRunningThroughWater()
        {
#if UNITY_EDITOR
            PlayerLocomotion body = BuildPlayer(new Vector3(0.0f, 0.1f, -10.0f));
            var host = new GameObject("Splashes");
            _made.Add(host);
            host.SetActive(false);
            var splashes = host.AddComponent<WaterSplashes>();
            splashes.RingMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/View/Materials/Ripple.mat");
            host.SetActive(true);
            Water(WaterBody.Square(new Vector3(0.0f, 0.0f, 0.0f), 40.0f, 0.6f));
            yield return Walk(body, 3.0f, new LocomotionInput { Move = Vector2.up, Sprint = true });
            Assert.Greater(splashes.RingsEmitted, 3, "no rings round a body running through water");
            Assert.Greater(splashes.DropsEmitted, 0, "no drops kicked up running through knee-deep water");
#else
            yield break;
#endif
        }
    }
}
