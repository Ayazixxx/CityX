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
            // The ways that are not doorways: the stairs, the open side of the stair core, the outdoor steps, and the front door once its planks
            // are torn off (the tutorial's way in).
            Link("Stair core (street)", "Stair hall");
            Link("Stair core (street)", "Entry hall");
            Link("Entry hall", "Gallery");
            Link("Driveway", "Porch");
            Link("Porch", "Entry hall");

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
        public void OldGuyWaitsInHisDenClearOfTheFurnitureFacingTheDoorway()
        {
            Vector3 spot = House.OldGuyInTheDen;
            Assert.AreEqual("Old Guy's den", House.RoomAt(1, spot.x, spot.z)?.Name);
            Assert.AreEqual(House.LevelY(1), spot.y, 1e-3f, "not on the den's floor");
            foreach (House.Furniture piece in House.FurnitureList)
            {
                if (Mathf.Abs(piece.Position.y - spot.y) > 2.0f || piece.Kind == House.FurnitureKind.Rug)
                {
                    continue;
                }
                float apart = new Vector2(piece.Position.x - spot.x, piece.Position.z - spot.z).magnitude;
                Assert.GreaterOrEqual(apart, 1.5f, $"he stands in the {piece.Kind} at {piece.Position}");
            }
            // The den's doorway from the hall is at x 19-21 on the hall row's wall (z 12).
            Vector3 facing = Quaternion.Euler(0.0f, House.OldGuyInTheDenYaw, 0.0f) * Vector3.forward;
            Vector3 toDoor = new Vector3(20.0f - spot.x, 0.0f, 12.0f - spot.z).normalized;
            Assert.Greater(Vector3.Dot(facing, toDoor), 0.95f, "he does not face the way in");
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
                    if (id == ItemRegistry.BaseballBat) batRoom = House.RoomAt(0, piece.Position.x, piece.Position.z)?.Name;
                }
            }
            foreach (string id in new[] { ItemRegistry.KitchenKnife, ItemRegistry.MagicWand, ItemRegistry.Slingshot, ItemRegistry.ButterKnife, ItemRegistry.BaseballBat })
            {
                Assert.IsTrue(ids.Contains(id), $"the house has no {id}");
            }
            Assert.AreEqual("Kitchen", House.RoomAt(1, House.Items[0].Position.x, House.Items[0].Position.z).Name, "the kitchen knife is not in the kitchen");
            Assert.AreEqual("Cellar", batRoom, "the bat is not hidden in the cellar");
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
        public void TheRoofIsOneGentleSlopeHighOverTheDrivewayAndClearOfTheWallsAtTheBack()
        {
            float front = House.RoofTopAt(-House.RoofFrontOverhang), back = House.RoofTopAt(House.Depth + House.RoofOverhang);
            Assert.AreEqual(House.RoofHighY, front, 1e-4f, "the front edge is not the high one");
            Assert.AreEqual(House.RoofLowY, back, 1e-4f);
            float pitch = Mathf.Atan2(front - back, House.Depth + House.RoofFrontOverhang + House.RoofOverhang) * Mathf.Rad2Deg;
            Assert.That(pitch, Is.InRange(7.0f, 15.0f), "a modern single slope is low");
            Assert.Greater(House.RoofTopAt(House.Depth) - House.RoofThickness, House.EavesY, "the roof's underside dips into the back wall");
        }

        [Test]
        public void TheSwordsmanIsUpstairs()
        {
            Assert.AreEqual("Old Guy's bedroom", House.RoomAt(2, House.SwordsmanSpot.x, House.SwordsmanSpot.z)?.Name);
        }

        /// <summary>
        /// Third-person rooms (docs/OLD_GUYS_HOUSE.md, "Sizes"): with the camera 5 m behind the player, every room but the WC and the gallery's walk
        /// is at least <see cref="House.CameraRoomSide"/> one way and a hall's width the other, and the rooms the tutorial is played in are big both ways.
        /// </summary>
        [Test]
        public void TheRoomsAreSizedForTheThirdPersonCamera()
        {
            foreach (House.Room room in House.Rooms)
            {
                if (room.Outdoors || room.Small) continue;
                foreach (House.Area a in room.Floors)
                {
                    float w = a.X1 - a.X0, d = a.Z1 - a.Z0;
                    Assert.GreaterOrEqual(Mathf.Max(w, d), House.CameraRoomSide, $"{room.Name} is {w:F1} x {d:F1} m: the camera has no room");
                    Assert.GreaterOrEqual(Mathf.Min(w, d), House.HallWidth, $"{room.Name} is {w:F1} x {d:F1} m: narrower than a hall");
                }
            }
            foreach (string name in new[] { "Garage", "Living room", "Kitchen", "Dining room", "Old Guy's den", "Grandson's room", "Study", "Old Guy's bedroom" })
            {
                House.Area a = House.RoomNamed(name).Floors[0];
                Assert.GreaterOrEqual(Mathf.Min(a.X1 - a.X0, a.Z1 - a.Z0), 7.0f, $"{name} is cramped for the camera");
            }
            Assert.GreaterOrEqual(House.ClearHeight, 4.6f, "the ceilings are too low for the camera over the player's head");
        }

        /// <summary>Owner, 2026-10-31: "there is one door that is being blocked by a coach". Nothing stands in the 1.5 m in front of any doorway, either side.</summary>
        [Test]
        public void NoFurnitureStandsInFrontOfADoorway()
        {
            const float clear = 1.5f;
            var pieces = new List<(House.Furniture Piece, Bounds Box)>();
            foreach (House.Furniture piece in House.FurnitureList)
            {
                if (piece.Kind == House.FurnitureKind.Rug || piece.Kind == House.FurnitureKind.Poster || piece.Kind == House.FurnitureKind.Drawing) continue;
                var shape = new CityX.View.MeshBuffer();
                CityX.View.FurnitureKit.Add(shape, piece);
                var box = new Bounds(shape.Vertices[0], Vector3.zero);
                foreach (Vector3 v in shape.Vertices) box.Encapsulate(v);
                pieces.Add((piece, box));
            }
            foreach (House.Wall wall in House.Walls)
            {
                foreach (House.Opening o in wall.Openings)
                {
                    if (!o.Passable && o.Kind != House.OpeningKind.FrontDoor) continue;
                    foreach (float side in new[] { -1.0f, 1.0f })
                    {
                        float near = wall.At + side * wall.Thickness * 0.5f, far = near + side * clear;
                        Vector3 min = wall.AlongX ? new Vector3(o.From, o.Sill + 0.05f, Mathf.Min(near, far)) : new Vector3(Mathf.Min(near, far), o.Sill + 0.05f, o.From);
                        Vector3 max = wall.AlongX ? new Vector3(o.To, o.Sill + 2.0f, Mathf.Max(near, far)) : new Vector3(Mathf.Max(near, far), o.Sill + 2.0f, o.To);
                        var doorway = new Bounds((min + max) * 0.5f, max - min);
                        foreach ((House.Furniture piece, Bounds box) in pieces)
                        {
                            Assert.IsFalse(doorway.Intersects(box), $"a {piece.Kind} at {piece.Position} stands in front of the doorway at {wall.At} ({o.From}-{o.To})");
                        }
                    }
                }
            }
        }

        /// <summary>The front door's planks are pieces 0 to 2 and every window's pane one piece after them: the numbers every machine shares.</summary>
        [Test]
        public void EveryPlankAndPaneHasItsOwnNumber()
        {
            int windows = 0, frontDoors = 0;
            foreach (House.Wall wall in House.Walls)
            {
                foreach (House.Opening o in wall.Openings)
                {
                    if (o.Kind == House.OpeningKind.Window) windows++;
                    if (o.Kind == House.OpeningKind.FrontDoor) frontDoors++;
                }
            }
            Assert.AreEqual(1, frontDoors, "the house has no one front door");
            Assert.AreEqual(windows, House.PaneCount);
            Assert.AreEqual(House.FrontDoorPlanks + windows, House.BreakableCount);
            Assert.IsTrue(TutorialHouseDamage.Acceptable(House.BreakableCount - 1));
            Assert.IsFalse(TutorialHouseDamage.Acceptable(House.BreakableCount));
            Assert.IsFalse(TutorialHouseDamage.Acceptable(-1));
        }
    }
}
