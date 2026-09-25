using System.Collections;
using CityX.Character;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The player in the rain, in real frames on real ground: wet ground costs
    /// traction (slower to get up to speed, slower to stop), and the body itself
    /// gets soaked -- darker and glossier -- and dries again, without ever
    /// touching the shared materials.
    /// </summary>
    public sealed class RainPlayerPlayTests
    {
        private const float Step = 1.0f / 60.0f;

        private sealed class FakeCover : IRainCover
        {
            public float Value;
            public float Shelter(Vector3 worldPoint) => Value;
        }

        private GameObject _ground;
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private Material _shared;

        [SetUp]
        public void SetUp()
        {
            CurrentWeather.Clear();
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);

            _player = new GameObject("Player");
            _player.transform.position = new Vector3(0.0f, 0.4f, 0.0f);
            _player.AddComponent<CharacterController>();
            _locomotion = _player.AddComponent<PlayerLocomotion>();
        }

        [TearDown]
        public void TearDown()
        {
            CurrentWeather.Clear();
            if (_player != null) Object.DestroyImmediate(_player);
            if (_ground != null) Object.DestroyImmediate(_ground);
            if (_shared != null) Object.DestroyImmediate(_shared);
        }

        private IEnumerator Drive(LocomotionInput input, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                _locomotion.Tick(input, Step);
                yield return null;
            }
        }

        private static LocomotionInput Run() => new LocomotionInput { Move = new Vector2(0.0f, 1.0f), Sprint = true, YawDegrees = 0.0f };

        private static LocomotionInput Stand() => new LocomotionInput { YawDegrees = 0.0f };

        /// <summary>
        /// Metres per second gained (or, negative, lost) per step by the ticks
        /// that began GROUNDED -- the only ones traction applies to -- averaged.
        /// The controller's grounded flag flickers while a body runs at speed in
        /// a test rig, and a step that started in the air is legitimately
        /// untouched by the ground, so those steps are left out rather than
        /// pretending every step is a ground step.
        /// </summary>
        private IEnumerator GroundedChangePerStep(LocomotionInput input, int steps, System.Action<float, int> result)
        {
            float total = 0.0f;
            int counted = 0;
            for (int i = 0; i < steps; i++)
            {
                bool grounded = _locomotion.IsGrounded;
                float before = _locomotion.CurrentSpeed;
                _locomotion.Tick(input, Step);
                if (grounded)
                {
                    total += _locomotion.CurrentSpeed - before;
                    counted++;
                }
                yield return null;
            }
            result(counted > 0 ? total / counted : 0.0f, counted);
        }

        private IEnumerator StartFromRest(System.Action<float, int> result)
        {
            yield return Drive(Stand(), 40);
            yield return GroundedChangePerStep(Run(), 8, result);   // still under 5 m/s, so never at the speed limit
        }

        private IEnumerator StopFromARun(System.Action<float, int> result)
        {
            yield return Drive(Stand(), 40);
            yield return Drive(Run(), 40);
            // Keep running until the feet are on the ground, so the stop starts from a grounded step.
            for (int i = 0; i < 80 && !_locomotion.IsGrounded; i++)
            {
                yield return Drive(Run(), 1);
            }
            yield return GroundedChangePerStep(Stand(), 5, result);
        }

        [UnityTest]
        public IEnumerator DryGroundGripsAtTheFullRate()
        {
            float gain = 0.0f; int counted = 0;
            yield return StartFromRest((g, n) => { gain = g; counted = n; });

            Assert.GreaterOrEqual(counted, 4, "setup: the body was rarely grounded");
            Assert.AreEqual(_locomotion.HorizontalAcceleration * Step, gain, 0.12f);
        }

        [UnityTest]
        public IEnumerator WetGroundIsSlowerToGetUpToSpeedThanDryGround()
        {
            float dry = 0.0f, wet = 0.0f; int dryCounted = 0, wetCounted = 0;
            yield return StartFromRest((g, n) => { dry = g; dryCounted = n; });

            yield return Drive(Stand(), 60);   // come to a stop
            CurrentWeather.Intensity = 1.0f;
            CurrentWeather.Wetness = 1.0f;
            yield return StartFromRest((g, n) => { wet = g; wetCounted = n; });

            TestContext.WriteLine($"gain per step: {dry:0.00} m/s dry over {dryCounted} grounded steps, {wet:0.00} wet over {wetCounted}");
            Assert.GreaterOrEqual(wetCounted, 4);
            Assert.Less(wet, dry * 0.75f, "wet ground should cost a good share of the grip");
            Assert.Greater(wet, dry * 0.3f, "but the feet still get somewhere");
        }

        [UnityTest]
        public IEnumerator WetGroundTakesLongerToStopOn()
        {
            float dry = 0.0f, wet = 0.0f; int dryCounted = 0, wetCounted = 0;
            yield return StopFromARun((g, n) => { dry = g; dryCounted = n; });

            CurrentWeather.Intensity = 1.0f;
            CurrentWeather.Wetness = 1.0f;
            yield return StopFromARun((g, n) => { wet = g; wetCounted = n; });

            TestContext.WriteLine($"change per step while stopping: {dry:0.00} dry ({dryCounted} grounded), {wet:0.00} wet ({wetCounted})");
            Assert.GreaterOrEqual(dryCounted, 2);
            Assert.GreaterOrEqual(wetCounted, 2);
            Assert.Less(dry, -0.5f, "setup: a dry body should be shedding speed");
            Assert.Greater(wet, dry * 0.75f, "a soaked street should make the body slide on after the run stops (a smaller loss per step)");
        }

        [UnityTest]
        public IEnumerator UnderARoofTheGroundIsMostlyDryAndTractionMostlyReturns()
        {
            float open = 0.0f, covered = 0.0f;
            var cover = new FakeCover { Value = 0.0f };
            CurrentWeather.Cover = cover;
            CurrentWeather.Intensity = 1.0f;
            CurrentWeather.Wetness = 1.0f;
            yield return StartFromRest((g, n) => open = g);

            yield return Drive(Stand(), 60);
            cover.Value = 1.0f;
            yield return StartFromRest((g, n) => covered = g);

            Assert.Greater(covered, open * 1.2f, "the same ground under a roof should grip better than in the open");
        }

        [UnityTest]
        public IEnumerator ADryDayHasNoTractionPenaltyAtAll()
        {
            float first = 0.0f, second = 0.0f;
            yield return StartFromRest((g, n) => first = g);
            yield return Drive(Stand(), 60);
            yield return StartFromRest((g, n) => second = g);

            Assert.AreEqual(first, second, 0.03f);
            Assert.AreEqual(_locomotion.HorizontalAcceleration * Step, first, 0.12f);
        }

        [UnityTest]
        public IEnumerator InTheAirTheFeetHaveNothingToSlipOn()
        {
            // A body that starts 300m up and never lands during the test: nothing is under it.
            float AirGain()
            {
                if (_player != null) Object.DestroyImmediate(_player);
                _player = new GameObject("Player");
                _player.transform.position = new Vector3(0.0f, 300.0f, 0.0f);
                _player.AddComponent<CharacterController>();
                _locomotion = _player.AddComponent<PlayerLocomotion>();
                Physics.SyncTransforms();
                return 0.0f;
            }

            AirGain();
            float dry = 0.0f, wet = 0.0f;
            yield return Drive(Stand(), 5);
            Assert.IsFalse(_locomotion.IsGrounded, "setup: the body should be in the air");
            float before = _locomotion.CurrentSpeed;
            yield return Drive(Run(), 3);
            dry = _locomotion.CurrentSpeed - before;

            CurrentWeather.Intensity = 1.0f;
            CurrentWeather.Wetness = 1.0f;
            AirGain();
            yield return Drive(Stand(), 5);
            Assert.IsFalse(_locomotion.IsGrounded);
            before = _locomotion.CurrentSpeed;
            yield return Drive(Run(), 3);
            wet = _locomotion.CurrentSpeed - before;

            TestContext.WriteLine($"airborne gain over three steps: {dry:0.00} dry, {wet:0.00} wet");
            Assert.Greater(dry, 0.5f);
            Assert.AreEqual(dry, wet, 0.05f, "air control was slowed by wet ground the body is not touching");
        }

        // --- the body getting wet ---

        private GameObject MakeBody(out Renderer renderer)
        {
            var body = new GameObject("Body");
            body.transform.SetParent(_player.transform, false);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(body.transform, false);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            renderer = cube.GetComponent<Renderer>();
            _shared = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            _shared.SetColor("_BaseColor", new Color(0.8f, 0.6f, 0.4f, 1.0f));
            _shared.SetFloat("_Smoothness", 0.2f);
            renderer.sharedMaterial = _shared;
            return body;
        }

        [UnityTest]
        public IEnumerator StandingInARealDownpourDarkensAndGlossesTheBodyAndTheSharedMaterialIsUntouched()
        {
            MakeBody(out Renderer renderer);
            PlayerWetness wetness = _player.AddComponent<PlayerWetness>();
            CurrentWeather.Intensity = 1.0f;

            wetness.SetWetness(0.95f);
            yield return null;
            yield return null;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            Color soaked = block.GetColor("_BaseColor");
            float gloss = block.GetFloat("_Smoothness");

            Assert.Less(soaked.r, 0.8f * 0.75f, "a soaked body should be darker");
            Assert.Greater(gloss, 0.45f, "and glossier");
            Assert.AreEqual(0.8f, _shared.GetColor("_BaseColor").r, 1e-5f, "the SHARED material was edited");
            Assert.AreEqual(0.2f, _shared.GetFloat("_Smoothness"), 1e-5f);
        }

        [UnityTest]
        public IEnumerator UnderCoverTheBodyDriesBackToExactlyHowItLookedDry()
        {
            MakeBody(out Renderer renderer);
            PlayerWetness wetness = _player.AddComponent<PlayerWetness>();
            CurrentWeather.Cover = new FakeCover { Value = 1.0f };   // nothing falling on them
            CurrentWeather.Intensity = 1.0f;

            wetness.SetWetness(0.02f);
            yield return null;
            yield return null;
            // Dries at 1/25 per second under cover; 0.02 is gone within a second.
            yield return new WaitForSeconds(1.0f);
            yield return null;

            Assert.AreEqual(0.0f, wetness.Wetness, 1e-4f);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            if (block.HasColor("_BaseColor"))
            {
                Assert.AreEqual(0.8f, block.GetColor("_BaseColor").r, 1e-4f, "a dry body should look exactly as it did before the rain");
                Assert.AreEqual(0.2f, block.GetFloat("_Smoothness"), 1e-4f);
            }
        }

        [UnityTest]
        public IEnumerator ADryBodyInADryWorldIsNeverTouched()
        {
            MakeBody(out Renderer renderer);
            _player.AddComponent<PlayerWetness>();
            yield return null;
            yield return null;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block, 0);
            Assert.IsTrue(block.isEmpty, "a dry player's renderers should carry no property block at all");
        }

        [UnityTest]
        public IEnumerator TheLocalPlayerGetsAWetnessComponentByItselfWhenItHasAnInputReader()
        {
            var reader = new GameObject("Local");
            reader.AddComponent<CharacterController>();
            reader.AddComponent<PlayerLocomotion>();
            reader.AddComponent<PlayerInputReader>();
            yield return null;

            Assert.IsNotNull(reader.GetComponent<PlayerWetness>(), "the real, local player never got wet");
            Object.DestroyImmediate(reader);
        }
    }
}
