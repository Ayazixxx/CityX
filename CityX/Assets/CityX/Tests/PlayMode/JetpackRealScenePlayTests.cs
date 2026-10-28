using System.Collections;
using CityX.Character;
using CityX.UI;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Requested, verified end to end through the real saved scene's own
    /// dev console and real player, not just the pure static math already
    /// covered in JetpackTests -- the same "batch diagnostics are not proof,
    /// real Play Mode is" lesson this project's own history keeps relearning
    /// (see SlopedStreetRealScenePlayTests' own doc comment for the same
    /// reasoning applied to a different feature). CityMassingSceneBuilder
    /// rebuilds Jetpack.prefab as a SIDE EFFECT of rebuilding the scene, so
    /// this is also what would catch a stale prefab that never got
    /// regenerated after JetpackBuilder.cs changed.
    /// </summary>
    public sealed class JetpackRealScenePlayTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>One frame of the jetpack's flight in the air (a hover, a climb, a dive), as PlayerInputReader would ask for it.</summary>
        private static void Fly(Jetpack jetpack, bool thrustHeld, Vector2 moveInput, bool boostHeld, float deltaTime, float cameraPitchDegrees = 0.0f)
        {
            jetpack.Tick(new FlightInput
            {
                ThrustHeld = thrustHeld,
                BoostHeld = boostHeld,
                Move = moveInput,
                CameraPitchDegrees = cameraPitchDegrees,
                Airborne = true,
            }, deltaTime);
        }

        [UnityTest]
        public IEnumerator EquippingTheRealJetpackHasTheFlamethrowersTwoLayerFlameAndWidensCameraPitch()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            Assert.IsNotNull(console, "no DevConsoleView in the real saved scene");
            var input = Object.FindFirstObjectByType<PlayerInputReader>();
            Assert.IsNotNull(input, "no PlayerInputReader in the real saved scene");
            ThirdPersonCamera camera = input.OrbitCamera;
            Assert.IsNotNull(camera, "PlayerInputReader never found the real camera");

            float minPitchBefore = camera.MinPitch;
            float maxPitchBefore = camera.MaxPitch;

            // /equip is self-contained -- it adds the item to inventory
            // itself if not already carried (DevConsoleView.Equip's own
            // `if (!inventory.HasItem(itemId)) inventory.AddItem(...)`), so
            // no /spawn-and-walk-over-and-press-E is needed first.
            console.Submit("/equip jetpack");
            yield return null;

            Assert.IsNotNull(input.EquippedFlightGear, "/equip jetpack did not actually equip one");

            // Requested: "make it so you can go all the vertical upward
            // and downward" -- widened the instant it is equipped, not only
            // once thrust is actually held (see Jetpack.OnEquip's own
            // comment on why: a player should be able to aim the camera
            // near-vertical BEFORE pressing thrust).
            Assert.AreEqual(Jetpack.JetpackMinPitchDegrees, camera.MinPitch, 0.01f);
            Assert.AreEqual(Jetpack.JetpackMaxPitchDegrees, camera.MaxPitch, 0.01f);

            var jetpack = Object.FindFirstObjectByType<Jetpack>();
            Assert.IsNotNull(jetpack, "no live Jetpack component after equipping");
            Assert.IsNotNull(jetpack.FlameEffect, "the equipped jetpack has no FlameEffect at all");

            // Requested: "replace the flame effect... with the same one
            // you used for the flamethrower" -- FlamethrowerBuilder's own
            // outer+core layering, not the old single flat cone. Checking
            // the REAL prefab instance, not the builder's own return value,
            // is what would have caught a stale Jetpack.prefab that never
            // got rebuilt after JetpackBuilder.cs changed.
            Transform core = jetpack.FlameEffect.transform.Find("CoreFlame");
            Assert.IsNotNull(core, "the real equipped jetpack's flame has no CoreFlame child -- " +
                "still the old single-layer cone, not the flamethrower's own two-layer flame");

            var flameRenderer = jetpack.FlameEffect.GetComponent<ParticleSystemRenderer>();
            Assert.IsNotNull(flameRenderer.sharedMaterial, "the flame has no material at all");
            Assert.IsNotNull(flameRenderer.sharedMaterial.mainTexture,
                "the flame material has no texture -- it will render as a hard-edged square, " +
                "the exact problem the flamethrower's own material was built to avoid");

            // Unequipping restores the ordinary look-around range, so a
            // player back on foot cannot still stare straight up or down.
            // /unequip takes a SLOT name, not an item id -- the jetpack's
            // own ItemDefinition (ItemRegistry.cs) puts it in "back".
            console.Submit("/unequip back");
            yield return null;

            Assert.AreEqual(minPitchBefore, camera.MinPitch, 0.01f);
            Assert.AreEqual(maxPitchBefore, camera.MaxPitch, 0.01f);
#else
            yield break;
#endif
        }

        /// <summary>
        /// End to end through the real equipped Jetpack, not TiltTarget in
        /// isolation -- proves the camera-pitch value PlayerInputReader
        /// actually reads and threads through Jetpack.Tick every frame
        /// really does reach the body's own lean, in the real scene.
        ///
        /// Driven through actual forward flight (W held, moveInput's Y
        /// component nonzero), not a hover -- since the sixth explicit
        /// pitch correction (see Jetpack.TiltTarget's own doc comment) a
        /// plain hover's pitch is unconditionally 0 regardless of camera
        /// angle, which would defeat the entire point of this test.
        /// </summary>
        [UnityTest]
        public IEnumerator ThrustingWhileEquippedPitchesTheRealBodyTowardWhereTheCameraLooks()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            var input = Object.FindFirstObjectByType<PlayerInputReader>();
            Assert.IsNotNull(console);
            Assert.IsNotNull(input);

            // /equip is self-contained -- it adds the item to inventory
            // itself if not already carried (DevConsoleView.Equip's own
            // `if (!inventory.HasItem(itemId)) inventory.AddItem(...)`), so
            // no /spawn-and-walk-over-and-press-E is needed first.
            console.Submit("/equip jetpack");
            yield return null;
            Assert.IsNotNull(input.EquippedFlightGear, "setup failed -- jetpack never equipped");

            var jetpack = Object.FindFirstObjectByType<Jetpack>();
            Assert.IsNotNull(jetpack);

            // Aimed steeply down -- inside the widened range, well outside
            // the old on-foot MinPitch/MaxPitch, so this also doubles as
            // confirmation the wider clamp is what let the camera reach it.
            input.OrbitCamera.Pitch = 80.0f;

            for (int i = 0; i < 60; i++)
            {
                Fly(jetpack, true, new Vector2(0.0f, 1.0f), false, 1.0f / 60.0f, input.OrbitCamera.Pitch);
            }

            Assert.Greater(jetpack.CurrentTilt.x, 40.0f,
                $"the real body's own pitch is only {jetpack.CurrentTilt.x:0.0} degrees after 60 frames " +
                "of looking almost straight down -- it is not actually tracking the camera");
#else
            yield break;
#endif
        }

        /// <summary>
        /// SECOND Requested, end to end: "holding shift and w... looking
        /// forward... horizontal like superman flies." Through the real
        /// equipped Jetpack and the real widened camera range, not
        /// TiltTarget in isolation.
        /// </summary>
        [UnityTest]
        public IEnumerator BoostingForwardWhileLookingForwardSettlesTheRealBodyHorizontal()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            var input = Object.FindFirstObjectByType<PlayerInputReader>();
            Assert.IsNotNull(console);
            Assert.IsNotNull(input);

            console.Submit("/equip jetpack");
            yield return null;
            Assert.IsNotNull(input.EquippedFlightGear, "setup failed -- jetpack never equipped");

            var jetpack = Object.FindFirstObjectByType<Jetpack>();
            Assert.IsNotNull(jetpack);

            // Looking dead level -- the "looking forward" case the ask
            // itself names as the Superman pose.
            input.OrbitCamera.Pitch = 0.0f;

            for (int i = 0; i < 60; i++)
            {
                Fly(jetpack, thrustHeld: true, moveInput: new Vector2(0.0f, 1.0f), boostHeld: true,
                    deltaTime: 1.0f / 60.0f, cameraPitchDegrees: input.OrbitCamera.Pitch);
            }

            Assert.AreEqual(Jetpack.SupermanPitchOffsetDegrees, jetpack.CurrentTilt.x, 2.0f,
                $"the real body pitched to {jetpack.CurrentTilt.x:0.0} degrees, not the expected " +
                $"{Jetpack.SupermanPitchOffsetDegrees:0.0} -- boosting forward while level should lay it " +
                "out fully horizontal, Superman-style");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Bug report: "when your using jetpack and crash into a
        /// surface and ragdoll, the jetpack also turns off even if your
        /// holding space." Through the real PlayerInputReader.Update() --
        /// not calling Jetpack.Tick directly the way the flame/audio are
        /// first started here -- since the actual fix lives in
        /// PlayerInputReader's own ragdoll branch, which Tick alone never
        /// exercises.
        ///
        /// A REAL Keyboard DEVICE HAS TO EXIST, even though nothing here
        /// ever presses a key -- Update()'s very first line bails out
        /// entirely when Keyboard.current is null (confirmed directly: this
        /// test failed with the flame still playing until this was added,
        /// because Update() was returning before ever reaching the ragdoll
        /// branch at all). This file's other tests never hit this because
        /// none of them rely on the real Update() loop running -- they all
        /// call Jetpack.Tick directly instead.
        /// </summary>
        [UnityTest]
        public IEnumerator RagdollingWhileThrustingStopsTheFlameAndAudioEvenWithoutReleasingSpace()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
                yield return null;
                yield return null;
                _scene = SceneManager.GetSceneByPath(ScenePath);

                var console = Object.FindFirstObjectByType<DevConsoleView>();
                var input = Object.FindFirstObjectByType<PlayerInputReader>();
                Assert.IsNotNull(console);
                Assert.IsNotNull(input);

                console.Submit("/equip jetpack");
                yield return null;
                Assert.IsNotNull(input.EquippedFlightGear, "setup failed -- jetpack never equipped");

                var jetpack = Object.FindFirstObjectByType<Jetpack>();
                Assert.IsNotNull(jetpack);

                // Started directly through Jetpack.Tick, same as this file's
                // other tests -- gets the flame/audio genuinely playing without
                // needing a real simulated key press.
                Fly(jetpack, true, new Vector2(0.0f, 1.0f), false, 1.0f / 60.0f);
                Assert.IsTrue(jetpack.FlameEffect.isEmitting, "setup failed -- the flame never started");
                Assert.IsTrue(jetpack.ThrustAudio.isPlaying, "setup failed -- the thrust audio never started");

                var ragdoll = console.Vitals.GetComponent<Ragdoll>();
                Assert.IsNotNull(ragdoll, "no Ragdoll on the real player");

                // The same call a real jetpack-crash knockdown makes -- see
                // PlayerVitals.TriggerCrashKnockdown.
                ragdoll.Activate(Vector3.zero);

                // ONE real frame of PlayerInputReader's own Update() -- its
                // ragdoll branch is what actually calls
                // EquippedFlightGear.StopThrustEffects() now.
                yield return null;

                Assert.IsFalse(jetpack.FlameEffect.isEmitting,
                    "the flame kept burning after the ragdoll went limp");
                Assert.IsFalse(jetpack.ThrustAudio.isPlaying,
                    "the thrust audio kept looping after the ragdoll went limp");
            }
            finally
            {
                if (keyboard != null)
                {
                    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                }
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// Requested: "add this wind noise as a looping sound when
        /// flying through the air at shift speed using jetpack." Boost
        /// (shift) specifically, not thrust alone -- an ordinary unboosted
        /// thrust should keep only the ordinary engine loop (ThrustAudio)
        /// going, silent on wind. Also covers the explicit follow-up:
        /// "end when stopped flying at that speed and starts from the top
        /// when resumed" -- Jetpack.Tick uses Stop()/Play(), not Pause()/
        /// UnPause(), and Stop() always resets an AudioSource's own
        /// playback position, so this checks that directly rather than
        /// trusting the pattern by inspection alone.
        /// </summary>
        [UnityTest]
        public IEnumerator BoostedThrustPlaysTheWindLoopAndStoppingRestartsItFromTheTop()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            console.Submit("/equip jetpack");
            yield return null;

            var jetpack = Object.FindFirstObjectByType<Jetpack>();
            Assert.IsNotNull(jetpack);
            Assert.IsNotNull(jetpack.WindAudio, "the real jetpack prefab has no WindAudio wired");
            Assert.IsNotNull(jetpack.WindAudio.clip, "WindAudio has no clip assigned");

            // Thrusting WITHOUT boost -- the wind loop must stay silent;
            // only the ordinary engine loop is for plain thrust.
            Fly(jetpack, true, new Vector2(0.0f, 1.0f), false, 1.0f / 60.0f);
            Assert.IsFalse(jetpack.WindAudio.isPlaying,
                "the wind loop started on plain thrust -- it should only play while boosting");
            Assert.IsTrue(jetpack.ThrustAudio.isPlaying, "setup failed -- plain thrust never started the engine loop");

            // NOW add boost -- "at shift speed."
            Fly(jetpack, true, new Vector2(0.0f, 1.0f), true, 1.0f / 60.0f);
            Assert.IsTrue(jetpack.WindAudio.isPlaying, "boosting while thrusting never started the wind loop");

            // Let real playback advance a little so timeSamples is
            // meaningfully away from zero before stopping.
            for (int i = 0; i < 10; i++)
            {
                Fly(jetpack, true, new Vector2(0.0f, 1.0f), true, 1.0f / 60.0f);
                yield return null;
            }
            int samplesBeforeStop = jetpack.WindAudio.timeSamples;
            Assert.Greater(samplesBeforeStop, 0, "setup failed -- playback never actually advanced");

            // Releasing boost (still thrusting forward) fades out over
            // WindFadeSeconds rather than cutting instantly -- see
            // BoostingFadesTheWindInAndReleasingFadesItOutRatherThanSnapping
            // for the fade curve itself; this just needs enough ticks for
            // the fade to actually finish before it counts as "ended."
            int fadeSteps = Mathf.CeilToInt(Jetpack.WindFadeSeconds / (1.0f / 60.0f)) + 5;
            for (int i = 0; i < fadeSteps; i++)
            {
                Fly(jetpack, true, new Vector2(0.0f, 1.0f), false, 1.0f / 60.0f);
            }
            Assert.IsFalse(jetpack.WindAudio.isPlaying,
                "releasing boost should end the wind loop entirely once it finishes fading out, not leave it hanging");

            // Boosting again should start it fresh from the top, not resume
            // from wherever it left off.
            Fly(jetpack, true, new Vector2(0.0f, 1.0f), true, 1.0f / 60.0f);
            Assert.IsTrue(jetpack.WindAudio.isPlaying);
            Assert.Less(jetpack.WindAudio.timeSamples, samplesBeforeStop,
                "resuming boost should restart the wind loop from the top, not resume mid-clip");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Follow-up request: "make the wind slowly fade in and out as
        /// it begins and stops when you enter and exit shift jetpack mode
        /// [wind sound]." Checks the actual VOLUME curve, not just
        /// isPlaying -- a plain Play()/Stop() would also leave isPlaying
        /// true for a frame or two by coincidence, so this has to confirm
        /// the volume itself is really ramping rather than sitting at full/
        /// zero the whole time.
        /// </summary>
        [UnityTest]
        public IEnumerator BoostingFadesTheWindInAndReleasingFadesItOutRatherThanSnapping()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var console = Object.FindFirstObjectByType<DevConsoleView>();
            console.Submit("/equip jetpack");
            yield return null;

            var jetpack = Object.FindFirstObjectByType<Jetpack>();
            Assert.IsNotNull(jetpack);

            const float step = 1.0f / 60.0f;

            // Exactly half of WindFadeSeconds' worth of ticks -- a linear
            // ramp should sit at almost exactly half volume here, neither
            // still near-silent nor already at full.
            int halfFadeSteps = Mathf.RoundToInt((Jetpack.WindFadeSeconds * 0.5f) / step);
            for (int i = 0; i < halfFadeSteps; i++)
            {
                Fly(jetpack, true, new Vector2(0.0f, 1.0f), true, step);
            }
            Assert.IsTrue(jetpack.WindAudio.isPlaying, "should already be playing partway through the fade-in");
            Assert.AreEqual(0.5f, jetpack.WindAudio.volume, 0.1f,
                $"expected roughly half volume halfway through a {Jetpack.WindFadeSeconds}s fade-in, " +
                $"got {jetpack.WindAudio.volume:0.00}");

            // Finish the fade-in.
            int remainingFadeSteps = Mathf.CeilToInt(Jetpack.WindFadeSeconds / step) + 5;
            for (int i = 0; i < remainingFadeSteps; i++)
            {
                Fly(jetpack, true, new Vector2(0.0f, 1.0f), true, step);
            }
            Assert.AreEqual(1.0f, jetpack.WindAudio.volume, 0.01f, "should have reached full volume by now");

            // Release boost -- should start fading OUT, not cut instantly.
            Fly(jetpack, true, new Vector2(0.0f, 1.0f), false, step);
            Assert.IsTrue(jetpack.WindAudio.isPlaying,
                "should still be playing (mid fade-out) on the very next frame after releasing boost");
            Assert.Less(jetpack.WindAudio.volume, 1.0f, "volume should already have started dropping");
            Assert.Greater(jetpack.WindAudio.volume, 0.0f, "should not have snapped straight to silent either");

            // Let the fade-out finish.
            for (int i = 0; i < remainingFadeSteps; i++)
            {
                Fly(jetpack, true, new Vector2(0.0f, 1.0f), false, step);
            }
            Assert.IsFalse(jetpack.WindAudio.isPlaying, "should have fully stopped once the fade-out completed");
#else
            yield break;
#endif
        }
    }
}
