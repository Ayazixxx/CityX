using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A fallen enemy can be searched like a drawer (docs/LOOTING.md, bodies; owner, 2026-11-06: "i want enemies to be lootable like skyrim how
    /// you can grab the armor and weapons off of them"): the swordsman's Knight Helmet is listed on his body, taking it puts it in the
    /// inventory and takes it off his head; a player's own fallen body and a body wearing nothing are not loot.
    /// </summary>
    public sealed class CorpseLootPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        private GameObject Make(string path, Vector3 at)
        {
#if UNITY_EDITOR
            var made = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path), at, Quaternion.identity);
            _made.Add(made);
            return made;
#else
            return null;
#endif
        }

        private GameObject Ground()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            _made.Add(ground);
            return ground;
        }

        private static bool Holds(Inventory inventory, string itemId) =>
            inventory.HasItem(itemId) || inventory.EquippedItemId(ItemRegistry.TryGet(itemId, out ItemDefinition d) ? d.EquipSlot : "") == itemId;

        [UnityTest]
        public IEnumerator AFallenSwordsmansHelmetIsTakenOffHisBodyIntoTheInventory()
        {
            Ground();
            GameObject swordsman = Make("Assets/CityX/Character/Swordsman.prefab", new Vector3(0.0f, 0.05f, 2.5f));
            swordsman.GetComponent<SwordsmanBrain>().enabled = false;
            yield return null;
            Assert.AreEqual(1, swordsman.GetComponentsInChildren<KnightHelmet>().Length, "the swordsman put on no helmet");
            swordsman.GetComponent<PlayerVitals>().TakeDamage(10000.0f);
            for (int i = 0; i < 30; i++) yield return null;   // falls

            var loot = swordsman.GetComponent<CorpseLoot>();
            Assert.IsNotNull(loot, "the swordsman's body has no CorpseLoot: rebuild the scene");
            Assert.IsNotNull(loot.Container, "his fallen body cannot be searched");
            Assert.AreEqual("Swordsman", loot.Container.Label);
            CollectionAssert.AreEqual(new[] { ItemRegistry.KnightHelmet }, loot.Container.Items, "his body does not list his helmet");

            // Looked at from beside him, his body is the thing E would search.
            GameObject player = Make("Assets/CityX/Character/Player.prefab", new Vector3(0.0f, 0.05f, 0.0f));
            if (player.GetComponent<Inventory>() == null) player.AddComponent<Inventory>();
            var reader = player.GetComponent<PlayerInputReader>();
            var eye = new GameObject("Eye");
            _made.Add(eye);
            eye.transform.position = player.transform.position + Vector3.up * (2.6f * LootSearcher.EyeShare);
            eye.transform.rotation = Quaternion.LookRotation(loot.Container.Centre - eye.transform.position);
            reader.YawSource = eye.transform;
            reader.enabled = false;
            yield return null;
            var searcher = player.GetComponent<LootSearcher>();
            Assert.AreEqual(loot.Container, searcher.InReach(out _), "his body is not in reach from beside it");

            searcher.Search(loot.Container);
            Assert.IsTrue(searcher.TakeSelected(), "the helmet could not be taken");
            yield return null;
            Assert.IsTrue(Holds(player.GetComponent<Inventory>(), ItemRegistry.KnightHelmet), "the helmet did not go into the inventory");
            Assert.IsEmpty(loot.Container.Items, "the helmet is still listed on his body");
            Assert.AreEqual(0, swordsman.GetComponentsInChildren<KnightHelmet>().Length, "the helmet is still on his head");
            searcher.CloseWindow();
        }

        [UnityTest]
        public IEnumerator APlayersFallenBodyAndABodyWearingNothingAreNotLoot()
        {
            Ground();
            GameObject player = Make("Assets/CityX/Character/Player.prefab", new Vector3(0.0f, 0.05f, 0.0f));
            GameObject shoota = Make("Assets/CityX/Character/Shoota.prefab", new Vector3(4.0f, 0.05f, 0.0f));
            shoota.GetComponent<ShootaBrain>().enabled = false;
            yield return null;
            Assert.IsTrue(PlayerBodies.IsPlayer(player), "the test's player is not a player's body");
            Assert.AreEqual(0, shoota.GetComponentsInChildren<IWornItem>().Length, "the Shoota wears something now: pick a bare body for this test");

            player.GetComponent<PlayerVitals>().TakeDamage(10000.0f);
            shoota.GetComponent<PlayerVitals>().TakeDamage(10000.0f);
            yield return null;
            Assert.IsNull(player.GetComponent<CorpseLoot>().Container, "a player's own fallen body can be looted");
            Assert.IsNull(shoota.GetComponent<CorpseLoot>().Container, "a body wearing nothing offers an empty search");
        }
    }
}
