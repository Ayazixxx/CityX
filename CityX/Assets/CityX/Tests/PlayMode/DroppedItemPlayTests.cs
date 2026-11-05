using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A body that dies drops the weapon in its hand (owner, 2026-10-17: "make it so enemies and players drop their weapon they are holding when
    /// they die it just becomes a separate object with gravity and can be picked up and added to inventory. so the swordsman would drop the combat
    /// sword he is holding"): a player's leaves their inventory, the swordsman's combat sword and the Shoota's rifle fall from their hands, and each
    /// falls to the ground and lies there until a player looks at it and presses E.
    /// </summary>
    public sealed class DroppedItemPlayTests
    {
        private const string PlayerPath = "Assets/CityX/Character/Player.prefab";
        private const string SwordsmanPath = "Assets/CityX/Character/Swordsman.prefab";
        private const string ShootaPath = "Assets/CityX/Character/Shoota.prefab";
        private const string SwordPath = "Assets/CityX/Weapons/SpikeSword.prefab";

        private GameObject _ground;
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            DroppedItems.Shared.Clear();
            DroppedItems.Shared.Relay = null;
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            DroppedItems.Shared.Clear();
            foreach (GameObject go in _made)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _made.Clear();
            if (_ground != null) Object.DestroyImmediate(_ground);
        }

        private GameObject Spawn(string path, Vector3 at, float yaw = 0.0f)
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"no {path} -- run CityX/Rebuild Massing Scene first");
            GameObject made = Object.Instantiate(prefab, at, Quaternion.Euler(0.0f, yaw, 0.0f));
            _made.Add(made);
            return made;
#else
            return null;
#endif
        }

        private static GameObject SwordPrefab()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SwordPath);
            Assert.IsNotNull(prefab, $"no {SwordPath}");
            return prefab;
#else
            return null;
#endif
        }

        /// <summary>The scene's player is given an inventory with the item prefabs (CityMassingSceneBuilder); Player.prefab alone has none.</summary>
        private static Inventory GiveInventory(GameObject player)
        {
            var inventory = player.AddComponent<Inventory>();
            inventory.ItemPrefabs = new[] { new Inventory.ItemPrefabEntry { ItemId = ItemRegistry.SpikeSword, Prefab = SwordPrefab() } };
            return inventory;
        }

        /// <summary>Slower than this, metres a second, a drop is at rest.</summary>
        private const float AtRest = 0.25f;

        /// <summary>
        /// At least <paramref name="atLeast"/> seconds, then until the drop is at rest, at most <paramref name="atMost"/>: flung from a dying
        /// hand, the sword was once still sliding at three seconds in a busy full run, and came to rest in time on its own.
        /// </summary>
        private static IEnumerator UntilAtRest(DroppedItem item, float atLeast, float atMost)
        {
            for (float t = 0.0f; t < atMost && (t < atLeast || item.Body.linearVelocity.magnitude >= AtRest); t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private static IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private static DroppedItem OnlyDrop()
        {
            Assert.AreEqual(1, DroppedItems.Shared.Count, "not exactly one item was dropped");
            foreach (DroppedItem item in DroppedItems.Shared.Shown)
            {
                return item;
            }
            Assert.Fail("the dropped item is not shown");
            return null;
        }

        /// <summary>It has come to rest lying on the ground (the box's lowest point on the street), not far from where it was dropped.</summary>
        private static void AssertLyingOnTheGround(DroppedItem item, Vector3 droppedNear, string what)
        {
            Assert.IsFalse(item.WaitingForGround, $"the {what} waits in the air over the ground");
            Bounds lying = item.Solid.bounds;
            Assert.Less(lying.min.y, 0.15f, $"the {what} is not lying on the ground (its lowest point {lying.min.y:0.00} m up)");
            Assert.Greater(lying.min.y, -0.1f, $"the {what} sank into the ground");
            Assert.Less(item.Body.linearVelocity.magnitude, AtRest, $"the {what} never came to rest");
            Vector3 away = item.Centre - droppedNear;
            away.y = 0.0f;
            // Flung from a dying hand it tumbles and slides 3 to 4.5 m on the flat before it rests (4.1 and 4.3 m seen on 2026-10-31): near, not far.
            Assert.Less(away.magnitude, 6.0f, $"the {what} landed {away.magnitude:0.0} m from where it fell");
        }

        [UnityTest]
        public IEnumerator APlayerWhoDiesDropsTheWeaponInTheirHandWhichFallsToTheGroundForAnyoneToPickUp()
        {
            GameObject player = Spawn(PlayerPath, new Vector3(0.0f, 0.05f, 0.0f));
            Inventory inventory = GiveInventory(player);
            var vitals = player.GetComponent<PlayerVitals>();
            Assert.IsTrue(inventory.AddItem(ItemRegistry.SpikeSword, 1));
            Assert.IsTrue(inventory.RemoveFromStorage(ItemRegistry.SpikeSword, 1, out ItemInstanceData data));
            Assert.IsTrue(inventory.EquipItemId(ItemRegistry.SpikeSword, player, data), "setup: the sword was not equipped");
            yield return Seconds(0.3f);
            Assert.IsNotNull(inventory.EquippedInstance(Inventory.PrimarySlot), "setup: no sword in hand");
            Transform handBone = player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);

            vitals.TakeDamage(100000.0f);
            Vector3 hand = handBone.position;   // after the ragdoll took the body, which may lift it out of the ground
            Assert.IsTrue(vitals.IsDead);
            Assert.IsEmpty(inventory.EquippedItemId(Inventory.PrimarySlot), "the sword is still in the dead player's hand");
            Assert.IsFalse(inventory.HasItem(ItemRegistry.SpikeSword), "the sword went back into the dead player's storage");
            DroppedItem dropped = OnlyDrop();
            Assert.AreEqual(ItemRegistry.SpikeSword, dropped.ItemId);
            Assert.Less(Vector3.Distance(dropped.transform.position, hand), 0.4f, "the sword did not start in the dead player's hand");
            Assert.Less(dropped.Id, 0L, "with no session the drop is this machine's alone");

            yield return UntilAtRest(dropped, 3.0f, 6.0f);
            AssertLyingOnTheGround(dropped, player.transform.position, "sword");

            // Another player walks up to it, looks at it and presses E.
            Vector3 lying = dropped.Centre;
            Vector3 standAt = new Vector3(lying.x - 1.6f, 0.05f, lying.z);
            GameObject other = Spawn(PlayerPath, standAt, 90.0f);
            Inventory otherInventory = GiveInventory(other);
            var eye = new GameObject("Eye");
            _made.Add(eye);
            eye.transform.position = standAt + Vector3.up * 2.3f;
            eye.transform.rotation = Quaternion.LookRotation(lying - eye.transform.position);
            other.GetComponent<PlayerInputReader>().YawSource = eye.transform;
            yield return null;
            var picker = other.GetComponent<DroppedItemPicker>();
            Assert.IsNotNull(picker, "the player's body has no way to pick up a dropped item");
            Assert.AreSame(dropped, picker.InReach(), "the sword at the player's feet, looked at, is not in reach");
            Assert.IsTrue(picker.TryPickUp());
            // Empty-handed, the sword goes straight into the hand, in the primary slot (owner, 2026-10-21).
            Assert.AreEqual(ItemRegistry.SpikeSword, otherInventory.EquippedItemId(Inventory.PrimarySlot), "picking it up did not put the sword in the hand");
            Assert.IsNotNull(otherInventory.EquippedInstance(Inventory.PrimarySlot), "the picked-up sword is not out in the hand");
            Assert.AreEqual(0, DroppedItems.Shared.Count);
            yield return null;
            Assert.IsTrue(dropped == null, "the sword still lies there after it was picked up");

            // Looking away, nothing is in reach.
            DroppedItem second = DroppedItems.Shared.Drop(new ItemDrop { ItemId = ItemRegistry.SpikeSword, Position = lying + Vector3.up * 0.2f, Rotation = Quaternion.identity },
                false, null, SwordPrefab());
            eye.transform.rotation = Quaternion.LookRotation(eye.transform.position - lying);
            Assert.IsNull(picker.InReach(), "a sword behind the player's back was in reach");
            Assert.IsNotNull(second);
        }

        [UnityTest]
        public IEnumerator TheSwordsmanDropsHisCombatSwordWhenBeaten()
        {
            GameObject swordsman = Spawn(SwordsmanPath, new Vector3(0.0f, 0.05f, 0.0f));
            yield return Seconds(0.3f);
            var melee = swordsman.GetComponent<MeleeAttacker>();
            Assert.IsTrue(melee.HoldsSword, "setup: the swordsman holds no sword");
            Transform hand = swordsman.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);

            swordsman.GetComponent<PlayerVitals>().TakeDamage(100000.0f);
            Assert.IsFalse(melee.HoldsSword, "the beaten swordsman still holds his sword");
            DroppedItem dropped = OnlyDrop();
            Assert.AreEqual(ItemRegistry.CombatSword, dropped.ItemId, "he dropped something other than his combat sword");
            Assert.Less(dropped.Id, 0L, "an NPC's drop is this machine's alone, as he is");

            yield return Seconds(3.0f);
            AssertLyingOnTheGround(dropped, swordsman.transform.position, "combat sword");
            Assert.IsNull(hand.GetComponentInChildren<SwordWeapon>(), "a sword still hangs from his hand");

            var go = new GameObject("Taker");
            _made.Add(go);
            var inventory = go.AddComponent<Inventory>();
            Assert.IsTrue(DroppedItems.Shared.Take(dropped.Id, inventory));
            Assert.IsTrue(inventory.HasItem(ItemRegistry.CombatSword), "the combat sword did not go into the inventory");
        }

        [UnityTest]
        public IEnumerator TheShootaDropsHisRifleWhenKilled()
        {
            GameObject shoota = Spawn(ShootaPath, new Vector3(0.0f, 0.05f, 0.0f));
            yield return Seconds(0.3f);
            Transform hand = shoota.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
            Assert.IsNotNull(shoota.GetComponentInChildren<PistolWeapon>(), "setup: the Shoota holds no rifle");

            shoota.GetComponent<PlayerVitals>().TakeDamage(100000.0f);
            DroppedItem dropped = OnlyDrop();
            Assert.AreEqual(ItemRegistry.AK47, dropped.ItemId, "he dropped something other than his AK-47");

            yield return Seconds(3.0f);
            AssertLyingOnTheGround(dropped, shoota.transform.position, "AK-47");
            Assert.IsNull(shoota.GetComponentInChildren<PistolWeapon>(), "a rifle is still in his hands");
            Assert.IsNotNull(hand);
        }

        [UnityTest]
        public IEnumerator AnItemDroppedWithNoGroundUnderItWaitsInTheAirUntilThereIs()
        {
            // Another player's drop far across the city, where this machine has not built the street yet.
            var high = new Vector3(500.0f, 300.0f, 500.0f);
            DroppedItem item = DroppedItems.Shared.Drop(new ItemDrop { ItemId = ItemRegistry.SpikeSword, Position = high, Rotation = Quaternion.identity },
                false, null, SwordPrefab());
            Assert.IsNotNull(item, "the sword was not shown");
            yield return Seconds(1.0f);
            Assert.IsTrue(item.WaitingForGround, "with nothing below it the sword should wait where it was dropped");
            Assert.Less(Vector3.Distance(item.transform.position, high), 0.01f, "the sword fell with nothing to land on");

            // The street is built under it: it lands.
            GameObject street = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(street);
            street.transform.position = new Vector3(500.0f, 279.5f, 500.0f);
            street.transform.localScale = new Vector3(30.0f, 1.0f, 30.0f);
            Physics.SyncTransforms();
            yield return Seconds(3.0f);
            Assert.IsFalse(item.WaitingForGround, "the sword never noticed the street under it");
            Assert.That(item.Solid.bounds.min.y, Is.InRange(279.9f, 280.15f), "the sword is not lying on the street that was built under it");
        }
    }
}
