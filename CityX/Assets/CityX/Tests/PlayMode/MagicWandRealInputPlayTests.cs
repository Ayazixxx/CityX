using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A SPELL CAST WITH THE MAGIC WAND IN HAND, FROM THE REAL KEYS IN THE REAL SCENE (owner, 2026-10-30: "when you use hotbar spells will be
    /// casted from the wand"; docs/MAGIC_WAND.md): the fireball leaves the wand's tip, not the chest, the wand pointed with it, and pays the
    /// wand's price in mana.
    /// </summary>
    public sealed class MagicWandRealInputPlayTests : InputTestFixture
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
            foreach (FireballProjectile f in Object.FindObjectsByType<FireballProjectile>(FindObjectsSortMode.None)) Object.Destroy(f.gameObject);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>Loads the city, finds its own player and waits for it to land.</summary>
        private IEnumerator Stage(System.Action<PlayerInputReader> done)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            PlayerInputReader input = null;
            foreach (GameObject root in _scene.GetRootGameObjects())
            {
                if (input == null) input = root.GetComponentInChildren<PlayerInputReader>(true);
            }
            Assert.IsNotNull(input, "no player in the real scene");
            var locomotion = input.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }
            done(input);
#else
            yield break;
#endif
        }

        private IEnumerator Tap(KeyControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AFireballCastWithTheWandInHandLeavesItsTipForLessMana()
        {
            PlayerInputReader input = null;
            yield return Stage(i => input = i);
            GameObject player = input.gameObject;
            Assert.IsTrue(input.GetComponent<Inventory>().EquipItemId(ItemRegistry.MagicWand, player), "the game's player cannot equip the wand");
            yield return null;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            Assert.IsNotNull(locomotion.Wand, "the body was not told it holds a wand");

            var fireball = player.GetComponent<FireballCaster>();
            var vitals = player.GetComponent<PlayerVitals>();
            Vector3? left = null;
            System.Action<AbilityEvent> heard = e => left = e.Origin - e.Direction.normalized * FireballCaster.SpawnForwardOffset;
            fireball.Fired += heard;
            float mana = vitals.Mana;
            try
            {
                yield return Tap(_keyboard.digit1Key);
            }
            finally
            {
                fireball.Fired -= heard;
            }
            Assert.IsTrue(left.HasValue, "key 1 cast no fireball");
            Assert.AreEqual(mana - FireballCaster.SpellManaCost * MagicSpeckRules.WandManaCostFactor, vitals.Mana, 0.3f,
                "a fireball through the wand pays a tenth less");
            Assert.Greater(locomotion.WandRaise, 0.99f, "the wand was not pointed with the cast");
            // Read a few frames after the cast: the arm held straight out (2026-11-01) puts the tip on a longer lever from the shoulder, so the
            // body's idle sway since then moves it about a centimetre.
            Assert.Less(Vector3.Distance(left.Value, locomotion.WandTip.position), 0.1f, "the fireball did not leave the wand's tip");
            Vector3 chest = player.transform.position + Vector3.up * (locomotion.WorldHeight * 0.68f);
            Assert.Greater(Vector3.Distance(left.Value, chest), 0.3f, "the fireball left the chest, as without a wand");
        }
    }
}
