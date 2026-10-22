using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// E as Skyrim has it, with the real key, a real sword and real physics (owner, 2026-10-21: "make it so holding E on objects carrys them
    /// while tapping E key equips it. make the holding system like skyrims"): a tap equips the sword; a hold lifts it in front of the player,
    /// it stays there while E is held, falls to the ground when E is let go, and flies off when clicked.
    /// </summary>
    public sealed class CarryPlayTests : InputTestFixture
    {
        private const string PlayerPath = "Assets/CityX/Character/Player.prefab";
        private const string SwordPath = "Assets/CityX/Weapons/SpikeSword.prefab";

        private Keyboard _keyboard;
        private Mouse _mouse;
        private readonly List<GameObject> _made = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            Time.captureFramerate = 60;
            Time.timeScale = 1.0f;
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Time.captureFramerate = 0;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            yield return null;
        }

        private GameObject Make(GameObject go)
        {
            _made.Add(go);
            return go;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

#if UNITY_EDITOR
        private GameObject Sword(Vector3 at) =>
            Make(Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath), at, Quaternion.Euler(0.0f, 0.0f, 90.0f)));

        /// <summary>The player on a floor, looking ahead (+Z) from a camera behind and above as the game's is, with an inventory and the carry.</summary>
        private (GameObject player, GrabCarry carry, Inventory inventory) Stage()
        {
            GameObject ground = Make(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
            GameObject player = Make(Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity));
            GameObject eye = Make(new GameObject("Eye"));
            eye.transform.position = new Vector3(0.0f, 3.2f, -4.5f);
            eye.transform.rotation = Quaternion.LookRotation(new Vector3(0.0f, 0.3f, 2.0f) - eye.transform.position);
            player.GetComponent<PlayerInputReader>().YawSource = eye.transform;
            var carry = player.AddComponent<GrabCarry>();
            carry.Eye = eye.transform;
            carry.Vitals = player.GetComponent<PlayerVitals>();
            var inventory = player.AddComponent<Inventory>();
            inventory.ItemPrefabs = new[]
            {
                new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.SpikeSword, Prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath) },
            };
            return (player, carry, inventory);
        }
#endif

        [UnityTest]
        public IEnumerator HoldingELiftsTheSwordLettingGoDropsItAndAClickThrowsIt()
        {
#if UNITY_EDITOR
            (GameObject player, GrabCarry carry, Inventory inventory) = Stage();
            GameObject sword = Sword(new Vector3(0.0f, 0.15f, 2.0f));
            Physics.SyncTransforms();
            yield return Frames(10);

            Press(_keyboard.eKey);
            yield return Frames(Mathf.CeilToInt(InteractKey.HoldSeconds * 60.0f) + 2);
            Assert.IsTrue(carry.IsCarrying, "holding E on the sword did not lift it");
            carry.Eye.rotation = Quaternion.LookRotation(Vector3.forward);   // looks up from the floor: the sword follows the look
            yield return Frames(60);
            Vector3 held = sword.GetComponent<Rigidbody>().worldCenterOfMass;
            Assert.Greater(held.y, 1.0f, $"the sword is not held up in front of the player (it is at {held})");
            Assert.Greater(held.z, 0.8f, "the sword is not in front of the player");
            Assert.AreEqual(string.Empty, inventory.EquippedItemId(Inventory.PrimarySlot), "a hold equipped the sword as a tap does");
            Assert.IsTrue(sword != null, "the sword was taken, not carried");

            Release(_keyboard.eKey);
            yield return Frames(120);
            Assert.IsFalse(carry.IsCarrying, "letting go of E did not drop the sword");
            Assert.Less(sword.GetComponent<Rigidbody>().worldCenterOfMass.y, 0.4f, "the dropped sword did not fall to the ground");

            Press(_keyboard.eKey);
            yield return Frames(Mathf.CeilToInt(InteractKey.HoldSeconds * 60.0f) + 30);
            Assert.IsTrue(carry.IsCarrying, "the sword could not be lifted again");
            Press(_mouse.leftButton);
            yield return Frames(2);
            Assert.IsFalse(carry.IsCarrying, "a click did not throw the sword");
            Assert.Greater(sword.GetComponent<Rigidbody>().linearVelocity.z, 3.0f, "the thrown sword did not fly where the player looks");
            Release(_keyboard.eKey);
            yield return null;
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TappingEOnTheSwordEquipsItAndLiftsNothing()
        {
#if UNITY_EDITOR
            (GameObject player, GrabCarry carry, Inventory inventory) = Stage();
            Sword(new Vector3(0.0f, 0.15f, 2.0f));
            Physics.SyncTransforms();
            yield return Frames(10);

            Press(_keyboard.eKey);
            yield return Frames(3);
            Release(_keyboard.eKey);
            yield return Frames(2);
            Assert.AreEqual(ItemRegistry.SpikeSword, inventory.EquippedItemId(Inventory.PrimarySlot), "a tap of E did not equip the sword");
            Assert.IsNotNull(inventory.EquippedInstance(Inventory.PrimarySlot), "the tapped sword is not in the hand");
            Assert.IsFalse(carry.IsCarrying);
#else
            yield break;
#endif
        }
    }
}
