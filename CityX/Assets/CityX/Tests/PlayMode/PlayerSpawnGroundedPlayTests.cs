using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Bug report: "i keep trying to load in as ayazixxx and i clip
    /// through the floor and fall infinitely... same with old guy." The real
    /// saved scene's own player spawn used to be placed at
    /// TerrainField.PadHeightFor(0, 0) -- the flat, quantised pad -- but
    /// chunk (0,0) is a street chunk, and a street chunk's own ground has
    /// been tilted to the smooth ContinuousHeightAt field for a while now
    /// (see CityMeshBuilder.BuildGround's own "hillside streets" comment).
    /// The two heights can disagree by a meaningful amount, which put the
    /// spawn point below the REAL collision mesh with nothing there to catch
    /// a CharacterController that starts already underground -- exactly an
    /// infinite fall. Old Guy's own spawn is just an offset of the player's,
    /// so he inherited the same bug for free.
    ///
    /// REAL SCENE, REAL FRAMES, not a synthetic rig -- the bug only ever
    /// showed up in the actual saved scene's own baked spawn position, which
    /// no EditMode test of BuildGround or CityMeshBuilder could ever catch.
    /// Same LoadSceneInPlayMode technique SlopedStreetRealScenePlayTests and
    /// GotoSlopeRealScenePlayTests already established for this exact class
    /// of "looks fine in isolation, breaks in the real saved scene" bug.
    /// </summary>
    public sealed class PlayerSpawnGroundedPlayTests
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

        [UnityTest]
        public IEnumerator TheRealPlayerSpawnSettlesOnTheGroundRatherThanFallingForever()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;

            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;

            _scene = SceneManager.GetSceneByPath(ScenePath);

            var locomotion = Object.FindFirstObjectByType<PlayerLocomotion>();
            Assert.IsNotNull(locomotion, "no PlayerLocomotion in the real saved scene");
            var controller = locomotion.GetComponent<CharacterController>();
            Assert.IsNotNull(controller, "the real player has no CharacterController");

            // PlayerInputReader's own Update() reads Keyboard.current/
            // Mouse.current, which are null outside a real Input System
            // device -- nothing in this project's test runner provides one
            // (see PistolWeaponPlayTests' own established reasoning for the
            // same gap). Left enabled, its automatic per-frame Update()
            // never actually calls PlayerLocomotion.Tick at all here, which
            // would silently pass this test for the wrong reason (nothing
            // moving because nothing is driving it, not because it settled)
            // -- so this drives Tick directly and disables the reader to
            // rule out any double-tick between the two.
            var inputReader = locomotion.GetComponent<PlayerInputReader>();
            if (inputReader != null)
            {
                inputReader.enabled = false;
            }

            float startY = locomotion.transform.position.y;

            // Fixed 1/60 step, not Time.deltaTime -- see
            // PlayerLocomotionPlayTests' own "batch mode runs at roughly
            // 2500fps" comment for why a real frame's own delta cannot be
            // trusted to add up to real seconds here.
            const float Step = 1.0f / 60.0f;
            const int Steps = 300;
            float lowestY = startY;
            bool everGrounded = false;
            for (int i = 0; i < Steps; i++)
            {
                locomotion.Tick(default, Step);
                yield return null;
                float y = locomotion.transform.position.y;
                if (y < lowestY)
                {
                    lowestY = y;
                }
                if (controller.isGrounded)
                {
                    everGrounded = true;
                }
            }

            float finalY = locomotion.transform.position.y;
            TestContext.WriteLine($"spawn Y={startY:F2}, lowest Y reached={lowestY:F2}, " +
                $"final Y={finalY:F2}, isGrounded at end={controller.isGrounded}");

            // An infinite fall never stops dropping and never reports
            // grounded -- either symptom alone is enough to catch the
            // reported bug, so both are checked rather than relying on
            // exactly where the loop happened to end.
            Assert.IsTrue(everGrounded,
                $"the real player never reported isGrounded across {Steps} simulated steps -- " +
                "this is exactly \"load in and fall through the floor forever\"");
            Assert.Greater(finalY, startY - 10.0f,
                $"the real player dropped from {startY:F2}m to {finalY:F2}m and kept dropping -- " +
                "a settling character should not fall metres past its own spawn height");
#else
            yield break;
#endif
        }
    }
}
