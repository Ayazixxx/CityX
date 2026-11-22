using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// EVERYTHING THE PLAYER CAN HOLD, WEAR OR CAST, TRIED ONCE WITH THE REAL KEYS IN THE REAL SCENE, looking for nothing but code that throws:
    /// each item in the registry is put on the game's own player, its left click held and let go, its reload key tapped, its right click
    /// held and let go, and it is put away again; then every spell key is tapped. An exception anywhere on the way fails the test, named by
    /// the item or key it came with. What each thing should DO is its own tests' business.
    /// </summary>
    public sealed class EveryItemAndSpellSmokePlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";

        /// <summary>The graffiti canvas, opened by the spray can's right click, takes the mouse for itself until it is closed.</summary>
        private static readonly string[] NoRightClick = { ItemRegistry.SprayCan };

        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private readonly List<string> _thrown = new List<string>();
        private string _doing = "setting up";

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            _thrown.Clear();
            Application.logMessageReceived += Heard;
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Application.logMessageReceived -= Heard;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private void Heard(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                string where = string.IsNullOrEmpty(stackTrace) ? string.Empty : " @ " + stackTrace.Split('\n')[0].Trim();
                _thrown.Add($"{_doing}: {condition}{where}");
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private IEnumerator Hold(ButtonControl button, int frames)
        {
            Press(button);
            yield return Frames(frames);
            Release(button);
            yield return Frames(2);
        }

        /// <summary>Until the body is on its feet again (a blast of its own may have thrown it), or a few seconds have gone.</summary>
        private static IEnumerator UntilUp(GameObject player)
        {
            var ragdoll = player.GetComponent<Ragdoll>();
            for (int i = 0; i < 400 && ragdoll != null && ragdoll.IsLimp; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator NothingThrowsWhenEveryItemIsUsedAndEverySpellKeyTapped()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;   // the scene's own chatter is not this test's; exceptions are counted by hand (Heard)
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
            GameObject player = input.gameObject;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 600 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");
            var vitals = player.GetComponent<PlayerVitals>();
            vitals.InfiniteHealth = true;   // its own grenade, its own fire: it must live to try the next thing
            var inventory = player.GetComponent<Inventory>();
            Assert.IsNotNull(inventory, "the game's player has no inventory");
            yield return Frames(20);

            var ids = new List<string>(ItemRegistry.Ids);
            ids.Sort(System.StringComparer.Ordinal);
            int tried = 0;
            foreach (string id in ids)
            {
                if (inventory.PrefabForItem(id) == null || !ItemRegistry.TryGet(id, out ItemDefinition definition) || string.IsNullOrEmpty(definition.EquipSlot))
                {
                    continue;   // nothing to hold or wear (a material, a key, a box of ammunition: its prefab is only what lies on the ground)
                }
                _doing = $"item '{id}'";
                if (!inventory.EquipItemId(id, player))
                {
                    _thrown.Add($"{_doing}: could not be equipped");
                    continue;
                }
                tried++;
                yield return Frames(8);
                yield return Hold(_mouse.leftButton, 25);
                yield return Frames(15);
                Press(_keyboard.rKey);
                yield return null;
                Release(_keyboard.rKey);
                yield return Frames(10);
                if (System.Array.IndexOf(NoRightClick, id) < 0)
                {
                    yield return Hold(_mouse.rightButton, 12);
                }
                yield return UntilUp(player);

                // Put away, and out of the bag again so the bag never fills.
                string slot = definition.EquipSlot;
                if (inventory.EquippedItemId(Inventory.PrimarySlot) == id) slot = Inventory.PrimarySlot;
                else if (inventory.EquippedItemId(Inventory.SecondarySlot) == id) slot = Inventory.SecondarySlot;
                if (inventory.EquippedItemId(slot) == id)
                {
                    if (inventory.UnequipToSlot(slot, player))
                    {
                        inventory.RemoveFromStorage(id, 1, out _);
                    }
                    else
                    {
                        _thrown.Add($"{_doing}: could not be put away");
                        inventory.ClearEquipSlotWithoutReturning(slot);
                    }
                }
                yield return Frames(4);
            }
            Assert.Greater(tried, 20, "setup: hardly any item could be equipped");

            KeyControl[] spellKeys =
            {
                _keyboard.digit1Key, _keyboard.digit2Key, _keyboard.digit3Key, _keyboard.digit4Key, _keyboard.digit5Key,
                _keyboard.digit6Key, _keyboard.digit7Key, _keyboard.digit8Key, _keyboard.digit9Key, _keyboard.digit0Key,
            };
            for (int i = 0; i < spellKeys.Length; i++)
            {
                _doing = $"spell key {(i + 1) % 10}";
                Press(spellKeys[i]);
                yield return Frames(3);
                Release(spellKeys[i]);
                yield return Frames(90);
                yield return UntilUp(player);
            }

            TestContext.WriteLine($"tried {tried} items and {spellKeys.Length} spell keys");
            Assert.IsEmpty(_thrown, $"{_thrown.Count} thing(s) threw or would not work:\n  " + string.Join("\n  ", _thrown));
#else
            yield break;
#endif
        }
    }
}
