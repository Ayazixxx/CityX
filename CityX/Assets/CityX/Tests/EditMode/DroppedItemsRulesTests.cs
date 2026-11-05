using CityX.Character;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The rules a dropped item's messages are held to (DroppedItemsNetwork), and the inventory's side of dropping and picking up: what the server
    /// lets through, whose id is whose, a drop's state surviving the trip, and whether an item would fit.
    /// </summary>
    public sealed class DroppedItemsRulesTests
    {
        private static ItemDropMessage Sword(long id) => new ItemDropMessage
        {
            Id = id, ItemId = ItemRegistry.SpikeSword, Durability = -1, Position = new Vector3(3.0f, 1.2f, -4.0f), Rotation = Quaternion.identity,
            Velocity = new Vector3(0.5f, 1.5f, 0.0f), Spin = new Vector3(0.0f, 0.0f, 4.0f),
        };

        [Test]
        public void TheServerLetsThroughOnlyAKnownItemWithSaneNumbers()
        {
            long id = (1L << 32) | 1;
            Assert.IsTrue(DroppedItemsNetwork.Acceptable(Sword(id)));

            ItemDropMessage unknown = Sword(id);
            unknown.ItemId = "no_such_item";
            Assert.IsFalse(DroppedItemsNetwork.Acceptable(unknown), "an item nobody knows was let through");

            Assert.IsFalse(DroppedItemsNetwork.Acceptable(Sword(0)), "an id of nobody's was let through");
            Assert.IsFalse(DroppedItemsNetwork.Acceptable(Sword(-5)), "a machine's own local id was let through");

            ItemDropMessage nan = Sword(id);
            nan.Position = new Vector3(float.NaN, 0.0f, 0.0f);
            Assert.IsFalse(DroppedItemsNetwork.Acceptable(nan), "a position that is not a number was let through");

            ItemDropMessage fast = Sword(id);
            fast.Velocity = Vector3.up * 500.0f;
            Assert.IsFalse(DroppedItemsNetwork.Acceptable(fast), "a sword thrown at 500 m/s was let through");
        }

        [Test]
        public void ADropsIdSaysWhichPlayerMadeIt()
        {
            for (int client = 0; client < 5; client++)
            {
                long id = ((long)(client + 1) << 32) | 77u;
                Assert.AreEqual(client, DroppedItemsNetwork.IdOwner(id));
            }
        }

        [Test]
        public void ADropsStateAndPathSurviveTheTrip()
        {
            var drop = new ItemDrop
            {
                Id = (3L << 32) | 9, ItemId = ItemRegistry.GlassBottle, Data = new ItemInstanceData { HasDurability = true, Durability = 2 },
                Position = new Vector3(1.0f, 2.0f, 3.0f), Rotation = Quaternion.Euler(10.0f, 20.0f, 30.0f), Velocity = Vector3.up, Spin = Vector3.right,
            };
            ItemDrop back = DroppedItemsNetwork.ToDrop(DroppedItemsNetwork.ToMessage(drop));
            Assert.AreEqual(drop.Id, back.Id);
            Assert.AreEqual(drop.ItemId, back.ItemId);
            Assert.IsTrue(back.Data.HasDurability);
            Assert.AreEqual(2, back.Data.Durability, "a bottle's cracks were lost on the way");
            Assert.AreEqual(drop.Position, back.Position);
            Assert.AreEqual(drop.Velocity, back.Velocity);

            ItemDrop plain = DroppedItemsNetwork.ToDrop(DroppedItemsNetwork.ToMessage(new ItemDrop { Id = 1L << 32, ItemId = ItemRegistry.SpikeSword }));
            Assert.IsFalse(plain.Data.HasDurability, "a sword arrived with a durability it never had");
            Assert.IsTrue(plain.Data.IsEmpty, "a sword arrived with a state it never had");
        }

        /// <summary>A gun let go of and picked up again, by anyone on any machine, has the rounds it was dropped with: dropping it is no reload.</summary>
        [Test]
        public void ADroppedGunsRoundsSurviveTheTrip()
        {
            var drop = new ItemDrop
            {
                Id = (2L << 32) | 4, ItemId = ItemRegistry.Pistol, Data = new ItemInstanceData { HasAmmo = true, Magazine = 3, Reserve = 0 },
                Position = new Vector3(1.0f, 2.0f, 3.0f), Rotation = Quaternion.identity,
            };
            ItemDropMessage message = DroppedItemsNetwork.ToMessage(drop);
            Assert.IsTrue(DroppedItemsNetwork.Acceptable(message));
            ItemDrop back = DroppedItemsNetwork.ToDrop(message);
            Assert.IsTrue(back.Data.HasAmmo, "a gun arrived without its rounds: picked up, it would be full");
            Assert.AreEqual(3, back.Data.Magazine);
            Assert.AreEqual(0, back.Data.Reserve);
            Assert.IsFalse(back.Data.HasDurability, "a gun arrived with a durability it never had");

            message.Reserve = -40;
            Assert.IsFalse(DroppedItemsNetwork.Acceptable(message), "a gun with fewer than no rounds was let through");
            message.Reserve = DroppedItemsNetwork.MaxRounds + 1;
            Assert.IsFalse(DroppedItemsNetwork.Acceptable(message), "a gun with more rounds than any carries was let through");
        }

        [Test]
        public void AnItemFitsWhenThereIsAStackWithRoomOrAnEmptySlot()
        {
            var go = new GameObject("Inventory");
            try
            {
                var inventory = go.AddComponent<Inventory>();
                Assert.IsTrue(inventory.HasRoomFor(ItemRegistry.SpikeSword));
                for (int i = 0; i < Inventory.StorageSlotCount; i++)
                {
                    Assert.IsTrue(inventory.AddItem(ItemRegistry.CombatSword, 1));
                }
                Assert.IsFalse(inventory.HasRoomFor(ItemRegistry.SpikeSword), "a full inventory said a sword would fit");
                Assert.IsFalse(inventory.HasRoomFor("no_such_item"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
