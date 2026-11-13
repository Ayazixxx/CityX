using System.Collections;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Punching while the boomerang is in the air, from the real mouse in the real scene (owner, 2026-11-12: "after throwing the boomerang, when you
    /// are unarmed, make it so you can punch when its mid air", "just make that apart of the base game"): a click throws it, a click while it is out
    /// punches with bare fists, and once caught the hands hold it again. Both boomerangs.
    /// </summary>
    public sealed class BoomerangPunchRealInputPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Mouse _mouse;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            foreach (ThrownBoomerang flying in Object.FindObjectsByType<ThrownBoomerang>(FindObjectsSortMode.None))
            {
                Object.Destroy(flying.gameObject);
            }
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private static IEnumerator Until(float seconds, System.Func<bool> done)
        {
            for (float t = 0.0f; t < seconds && !done(); t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private IEnumerator Click()
        {
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WithTheWoodenOneOutAClickPunches()
        {
            yield return PunchWhileItFlies(ItemRegistry.WoodenBoomerang);
        }

        [UnityTest]
        public IEnumerator WithTheGiantOneOutAClickPunches()
        {
            yield return PunchWhileItFlies(ItemRegistry.GiantBoomerang);
        }

        private IEnumerator PunchWhileItFlies(string itemId)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);

            var vitals = Object.FindFirstObjectByType<PlayerVitals>();
            Assert.IsNotNull(vitals, "no player in the scene");
            var locomotion = vitals.GetComponent<PlayerLocomotion>();
            yield return Until(6.0f, () => locomotion.IsGrounded);
            var inventory = vitals.GetComponent<Inventory>();
            inventory.AddItem(itemId, 1);
            Assert.IsTrue(inventory.RemoveFromStorage(itemId, 1, out ItemInstanceData data));
            Assert.IsTrue(inventory.EquipItemId(itemId, vitals.gameObject, data), $"could not equip {itemId}");
            var thrower = vitals.GetComponent<BoomerangThrower>();
            var melee = vitals.GetComponent<MeleeAttacker>();
            IEquippableWeapon weapon = inventory.EquippedInstance(inventory.ActiveWeaponSlot).GetComponent<IEquippableWeapon>();
            yield return Until(0.5f, () => false);

            yield return Click();
            yield return Until(2.0f, () => thrower.IsOut);
            Assert.IsTrue(thrower.IsOut, "a click did not throw it");
            Assert.IsFalse(weapon.InHand, "out in the air, it is still in the hand");
            Assert.IsNull(melee.EquippedWeapon, "out in the air, the fists still swing it");
            Assert.IsFalse(melee.IsSwinging, "setup: the throw is not a punch");

            yield return Click();
            Assert.IsTrue(melee.IsSwinging, "a click while it flies did not punch");
            Assert.IsTrue(thrower.IsOut, "the punch called it back");

            yield return Until(8.0f, () => !thrower.IsOut);
            Assert.IsFalse(thrower.IsOut, "it never came home");
            Assert.IsTrue(weapon.InHand, "caught, it is not in the hand");
            Assert.AreSame(weapon, melee.EquippedWeapon, "caught, the hands are still bare");
#else
            yield break;
#endif
        }
    }
}
