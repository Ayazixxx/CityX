using System.Collections.Generic;
using CityX.Items;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using House = CityX.World.OldGuysHouse;

namespace CityX.Tests
{
    /// <summary>
    /// The tutorial house on paper (docs/OLD_GUYS_HOUSE.md): sized for the 2.6 m player, stairs never steep, rooms that do not overlap, every
    /// room reachable from the street by the route the tutorial teaches, and the five weapons real items lying in rooms.
    /// </summary>
    public sealed class OldGuysHousePlanTests
    {
        [Test]
        public void EveryDoorwayFitsThePlayer()
        {
            foreach (House.Wall wall in House.Walls)
            {
                foreach (House.Opening o in wall.Openings)
                {
                    if (!o.Passable) continue;
                    Assert.GreaterOrEqual(o.Width, House.DoorWidth - 0.01f, $"a doorway at {wall.At} is {o.Width:F2} m wide");
                    Assert.GreaterOrEqual(o.Head - o.Sill, House.DoorHeight - 0.01f, $"a doorway at {wall.At} is {o.Head - o.Sill:F2} m tall");
                }
            }
        }

        [Test]
        public void TheStairsAreNeverSteepAndArriveOnTheFloorAbove()
        {
            Assert.LessOrEqual(House.Riser, 0.235f, "a riser taller than the stair kit's");
            Assert.Less(Mathf.Atan2(House.Riser, House.Tread) * Mathf.Rad2Deg, 34.0f, "the stairs are steeper than 34 degrees");
            List<House.Step> core = House.CoreSteps();
            Assert.AreEqual(House.LevelY(2), core[core.Count - 1].Top + House.Riser, 1e-3f, "the core's last step does not arrive on the upper floor");
            Assert.AreEqual(House.LevelY(1), core[core.Count / 2 - 1].Top + House.Riser, 1e-3f, "the first storey's last step does not arrive on the main floor");
            foreach (House.Step s in core)
            {
                Assert.GreaterOrEqual(s.Tread.Z1 - s.Tread.Z0, House.Tread - 1e-3f, "a tread shorter than 0.36 m");
            }
            List<House.Step> outside = House.OutdoorSteps();
            Assert.AreEqual(House.LevelY(1), outside[outside.Count - 1].Top + House.Riser, 1e-3f, "the outdoor steps do not arrive on the porch");
        }

        [Test]
        public void RoomsOnTheSameFloorDoNotOverlap()
        {
            for (int i = 0; i < House.Rooms.Length; i++)
            for (int j = i + 1; j < House.Rooms.Length; j++)
            {
                House.Room a = House.Rooms[i], b = House.Rooms[j];
                if (a.Level != b.Level) continue;
                foreach (House.Area p in a.Floors)
                foreach (House.Area q in b.Floors)
                    Assert.IsFalse(p.Overlaps(q), $"{a.Name} and {b.Name} overlap");
            }
        }

        [Test]
        public void EveryRoomCanBeReachedFromTheStreet()
        {
            var links = new Dictionary<string, HashSet<string>>();
            void Link(string a, string b)
            {
                if (a == null || b == null || a == b) return;
                if (!links.ContainsKey(a)) links[a] = new HashSet<string>();
                if (!links.ContainsKey(b)) links[b] = new HashSet<string>();
                links[a].Add(b);
                links[b].Add(a);
            }
            // Through every doorway and arch: the rooms either side of it.
            foreach (House.Wall wall in House.Walls)
            {
                foreach (House.Opening o in wall.Openings)
                {
                    if (!o.Passable) continue;
                    int level = Mathf.FloorToInt((o.Sill + 0.5f) / House.Storey);
                    float mid = (o.From + o.To) * 0.5f, off = wall.Thickness * 0.5f + 0.25f;
                    House.Room a = wall.AlongX ? House.RoomAt(level, mid, wall.At - off) : House.RoomAt(level, wall.At - off, mid);
                    House.Room b = wall.AlongX ? House.RoomAt(level, mid, wall.At + off) : House.RoomAt(level, wall.At + off, mid);
                    Link(a?.Name, b?.Name);
                }
            }
            // The ways that are not doorways: the stairs, the open side of the stair core, the outdoor steps, the vault over the garden wall,
            // the open ground round the house, the conservatory's garden door, the bridge.
            Link("Stair core (street)", "Stair hall");
            Link("Stair core (street)", "Entry hall");
            Link("Entry hall", "Gallery");
            Link("Driveway", "Porch");
            Link("Porch", "Side path");
            Link("Side path", "Garden");
            Link("Patio", "Garden");
            Link("Garden", "Conservatory");
            Link("Balcony", "Tree house platform");

            var reached = new HashSet<string> { "Driveway" };
            var queue = new Queue<string>(reached);
            while (queue.Count > 0)
            {
                string here = queue.Dequeue();
                if (!links.TryGetValue(here, out HashSet<string> next)) continue;
                foreach (string n in next)
                {
                    if (reached.Add(n)) queue.Enqueue(n);
                }
            }
            foreach (House.Room room in House.Rooms)
            {
                Assert.IsTrue(reached.Contains(room.Name), $"{room.Name} cannot be reached from the street");
            }
        }

        [Test]
        public void TheFiveWeaponsAreRealItemsLyingInRooms()
        {
            var ids = new HashSet<string>();
            foreach (House.ItemSpot spot in House.Items)
            {
                Assert.IsTrue(ItemRegistry.TryGet(spot.ItemId, out _), $"{spot.ItemId} is not an item");
                ids.Add(spot.ItemId);
                int level = Mathf.FloorToInt((spot.Position.y + 0.1f) / House.Storey);
                Assert.IsNotNull(House.RoomAt(level, spot.Position.x, spot.Position.z), $"the {spot.ItemId} lies outside every room");
            }
            // What is inside the furniture (docs/LOOTING.md) counts too: real items, in a part the piece has.
            string batRoom = null;
            foreach (House.Furniture piece in House.FurnitureList)
            {
                if (piece.Stash.Length == 0) continue;
                Assert.Less(piece.StashPart, CityX.View.FurnitureKit.PartsOf(piece.Kind, Color.white).Count, $"a {piece.Kind}'s things are in a part it does not have");
                foreach (string id in piece.Stash)
                {
                    Assert.IsTrue(ItemRegistry.TryGet(id, out _), $"{id} is not an item");
                    ids.Add(id);
                    if (id == ItemRegistry.BaseballBat) batRoom = House.RoomAt(2, piece.Position.x, piece.Position.z)?.Name;
                }
            }
            foreach (string id in new[] { ItemRegistry.KitchenKnife, ItemRegistry.MagicWand, ItemRegistry.Slingshot, ItemRegistry.ButterKnife, ItemRegistry.BaseballBat })
            {
                Assert.IsTrue(ids.Contains(id), $"the house has no {id}");
            }
            Assert.AreEqual("Kitchen", House.RoomAt(1, House.Items[0].Position.x, House.Items[0].Position.z).Name, "the kitchen knife is not in the kitchen");
            Assert.AreEqual("Tree house", batRoom, "the bat is not hidden in the tree house");
        }

        /// <summary>Every piece stands in a room or the garden (the wand's case once stood at the house's corner, read before its place was set).</summary>
        [Test]
        public void EveryPieceOfFurnitureStandsInARoom()
        {
            foreach (House.Furniture piece in House.FurnitureList)
            {
                int level = Mathf.FloorToInt((piece.Position.y + 0.1f) / House.Storey);
                Assert.IsNotNull(House.RoomAt(level, piece.Position.x, piece.Position.z), $"a {piece.Kind} stands outside every room, at {piece.Position}");
            }
            Assert.AreEqual("Study", House.RoomAt(2, House.GlassCase.x, House.GlassCase.z)?.Name, "the wand's case is not in the study");
        }

        [Test]
        public void TheSwordsmanIsUpstairs()
        {
            Assert.AreEqual("Old Guy's bedroom", House.RoomAt(2, House.SwordsmanSpot.x, House.SwordsmanSpot.z)?.Name);
        }
    }
}
