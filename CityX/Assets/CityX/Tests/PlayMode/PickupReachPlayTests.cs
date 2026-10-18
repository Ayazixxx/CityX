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
    /// Who a pickup in the world counts as the player in reach (InteractionReach.LocalPlayerBody, IsLeaving; found 2026-10-17): only this
    /// machine's player, never an NPC or another player's copy walking through its sphere, and never forgotten while they stand there.
    /// </summary>
    public sealed class PickupReachPlayTests : InputTestFixture
    {
        private const string PlayerPath = "Assets/CityX/Character/Player.prefab";
        private const string SwordPath = "Assets/CityX/Weapons/Sword.prefab";

        private Keyboard _keyboard;
        private readonly List<GameObject> _made = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
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

        /// <summary>A body that is not this machine's player: vitals and a controller, no input reader (an NPC; a copy's is switched off).</summary>
        private GameObject OtherBody(Vector3 at)
        {
            GameObject body = Make(new GameObject("Other body"));
            body.transform.position = at;
            var controller = body.AddComponent<CharacterController>();
            controller.height = 2.6f;
            controller.center = new Vector3(0.0f, 1.3f, 0.0f);
            controller.radius = 0.35f;
            body.AddComponent<PlayerVitals>();
            return body;
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AnNpcWalkingThroughASwordsReachDoesNotTakeThePlayersPlace()
        {
#if UNITY_EDITOR
            GameObject ground = Make(GameObject.CreatePrimitive(PrimitiveType.Cube));
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
            GameObject player = Make(Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity));
            var inventory = player.AddComponent<Inventory>();
            Make(Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath), new Vector3(0.0f, 0.3f, 2.0f), Quaternion.identity));
            Physics.SyncTransforms();
            yield return Frames(5);

            // A swordsman (any body without this machine's input) walks into the sword's reach and out again.
            GameObject npc = OtherBody(new Vector3(1.0f, 0.05f, 2.5f));
            Physics.SyncTransforms();
            yield return Frames(5);
            npc.transform.position = new Vector3(40.0f, 0.05f, 40.0f);
            Physics.SyncTransforms();
            yield return Frames(5);

            Press(_keyboard.eKey);
            yield return null;
            Release(_keyboard.eKey);
            yield return null;
            Assert.IsTrue(inventory.HasItem(ItemRegistry.Sword), "E did nothing: the NPC passing through made the sword forget the player standing at it");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator OnlyThisMachinesPlayerIsAPlayerInReachAndOnlyTheirBodyLeavingForgetsThem()
        {
#if UNITY_EDITOR
            GameObject player = Make(Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath), new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity));
            yield return null;
            var controller = player.GetComponent<CharacterController>();
            Collider limb = null;
            foreach (Collider c in player.GetComponentsInChildren<Collider>(true))
            {
                if (!(c is CharacterController)) { limb = c; break; }
            }
            Assert.IsNotNull(limb, "setup: the player's body has no limb colliders (its ragdoll)");

            Assert.AreEqual(player.transform, InteractionReach.LocalPlayerBody(controller));
            Assert.AreEqual(player.transform, InteractionReach.LocalPlayerBody(limb), "a limb of the player's body is the player");
            Assert.IsTrue(InteractionReach.IsLeaving(controller, player.transform));
            Assert.IsFalse(InteractionReach.IsLeaving(limb, player.transform), "an arm swinging out of reach is not the player walking away");

            GameObject npc = OtherBody(new Vector3(5.0f, 0.05f, 0.0f));
            Assert.IsNull(InteractionReach.LocalPlayerBody(npc.GetComponent<CharacterController>()), "an NPC counted as the player");
            Assert.IsFalse(InteractionReach.IsLeaving(npc.GetComponent<CharacterController>(), player.transform), "an NPC leaving made the player go");

            // Another player's copy: their body with its input reader switched off.
            GameObject copy = Make(Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath), new Vector3(-5.0f, 0.05f, 0.0f), Quaternion.identity));
            copy.GetComponent<PlayerInputReader>().enabled = false;
            Assert.IsNull(InteractionReach.LocalPlayerBody(copy.GetComponent<CharacterController>()), "another player's copy counted as this machine's player");
#else
            yield break;
#endif
        }
    }
}
