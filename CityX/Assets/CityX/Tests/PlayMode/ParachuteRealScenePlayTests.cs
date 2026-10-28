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
    /// dev console and real player, not just the pure mechanic already
    /// covered in ParachuteTests -- same "batch diagnostics are not proof,
    /// real Play Mode is" reasoning JetpackRealScenePlayTests' own doc
    /// comment gives. CityMassingSceneBuilder rebuilds Parachute.prefab as a
    /// side effect of rebuilding the scene, so this is also what would catch
    /// a stale prefab that never got regenerated after ParachuteBuilder.cs
    /// changed.
    /// </summary>
    public sealed class ParachuteRealScenePlayTests
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
        public IEnumerator EquippingTheRealParachuteWiresItIntoPlayerInputReader()
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

            console.Submit("/equip parachute");
            yield return null;

            Assert.IsNotNull(input.EquippedParachute, "/equip parachute did not actually equip one");

            var parachute = Object.FindFirstObjectByType<Parachute>();
            Assert.IsNotNull(parachute, "no live Parachute component after equipping");
#else
            yield break;
#endif
        }

        /// <summary>
        /// Design decision: Parachute reuses Jetpack's own "back"
        /// equip slot (see ItemRegistry.Parachute's own doc comment) so
        /// wearing both at once -- which would not mean anything, since
        /// Space could only ever talk to one of them -- is impossible for
        /// free, through Inventory's existing slot-eviction logic, with no
        /// new mutual-exclusion code anywhere.
        /// </summary>
        [UnityTest]
        public IEnumerator EquippingEitherOneEvictsTheOtherFromTheSharedBackSlot()
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

            console.Submit("/equip parachute");
            yield return null;
            Assert.IsNotNull(input.EquippedParachute, "/equip parachute did not equip one");
            Assert.IsNull(input.EquippedFlightGear,
                "equipping the parachute should have evicted the jetpack from their shared back slot");

            console.Submit("/equip jetpack");
            yield return null;
            Assert.IsNotNull(input.EquippedFlightGear, "/equip jetpack did not equip one back");
            Assert.IsNull(input.EquippedParachute,
                "equipping the jetpack again should have evicted the parachute from their shared back slot");
#else
            yield break;
#endif
        }
    }
}
