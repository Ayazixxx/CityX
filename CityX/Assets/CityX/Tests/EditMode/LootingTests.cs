using System.Collections.Generic;
using CityX.Character;
using CityX.View;
using CityX.World;
using NUnit.Framework;
using UnityEngine;
using Kind = CityX.World.OldGuysHouse.FurnitureKind;

namespace CityX.Tests
{
    /// <summary>
    /// Looking into drawers and cupboards on paper (docs/LOOTING.md): taking one item or all of them, the way each part opens (never back into the
    /// wall behind it), and every part of the furniture that opens sitting on its piece.
    /// </summary>
    public sealed class LootingTests
    {
        [Test]
        public void TakingAnItemLeavesTheRestAndARefusedOneStays()
        {
            var inside = new List<string> { "a", "b", "c" };
            var list = new LootList(inside);
            list.Move(1);
            Assert.AreEqual(1, list.Selected);
            Assert.IsTrue(list.TakeSelected(id => true));
            CollectionAssert.AreEqual(new[] { "a", "c" }, inside, "taking from the list did not take from the container");
            Assert.IsFalse(list.TakeSelected(id => false), "a full inventory took the item");
            CollectionAssert.AreEqual(new[] { "a", "c" }, inside);
            list.Move(1);
            Assert.AreEqual(0, list.Selected, "picking past the last item does not come round to the first");
            list.Move(-1);
            Assert.AreEqual(1, list.Selected);
        }

        [Test]
        public void TakingAllLeavesOnlyWhatWillNotFit()
        {
            var inside = new List<string> { "shirt", "anvil", "jeans" };
            int taken = new LootList(inside).TakeAll(id => id != "anvil");
            Assert.AreEqual(2, taken);
            CollectionAssert.AreEqual(new[] { "anvil" }, inside);
            Assert.IsTrue(new LootList(new List<string>()).IsEmpty);
        }

        [Test]
        public void EachPartOpensOutwardNeverIntoTheWallBehindIt()
        {
            ContainerMotion.PoseAt(ContainerKind.Drawer, 1.0f, 0.4f, out Vector3 slide, out _);
            Assert.AreEqual(0.4f, slide.z, 1e-4f, "a drawer did not come out its whole way");
            foreach (ContainerKind kind in new[] { ContainerKind.DoorLeft, ContainerKind.DoorRight, ContainerKind.Lid, ContainerKind.Flap })
            {
                // A point on the part away from its hinge: a left-hinged door reaches to +x, a right-hinged one to -x, a lid forward, a flap up.
                Vector3 reach = kind == ContainerKind.DoorLeft ? Vector3.right : kind == ContainerKind.DoorRight ? Vector3.left : kind == ContainerKind.Lid ? Vector3.forward : Vector3.up;
                for (float t = 0.0f; t <= 1.0f; t += 0.1f)
                {
                    ContainerMotion.PoseAt(kind, t, 0.0f, out _, out Quaternion turn);
                    Assert.GreaterOrEqual((turn * reach).z, -1e-3f, $"a {kind} swings back into the wall at {t:F1} open");
                }
                ContainerMotion.PoseAt(kind, 1.0f, 0.0f, out _, out Quaternion open);
                Assert.Greater(Quaternion.Angle(Quaternion.identity, open), 80.0f, $"a {kind} hardly opens");
            }
        }

        [Test]
        public void EveryPieceThatOpensHasItsPartsOnIt()
        {
            foreach (Kind kind in new[] { Kind.Counter, Kind.Sink, Kind.Stove, Kind.Fridge, Kind.UpperCabinet, Kind.Sideboard, Kind.Dresser, Kind.Nightstand, Kind.Desk, Kind.Wardrobe, Kind.ToyChest, Kind.Crate, Kind.Workbench, Kind.Basin })
            {
                List<FurnitureKit.ContainerPart> parts = FurnitureKit.PartsOf(kind, Color.white);
                Assert.IsNotEmpty(parts, $"a {kind} has nothing that opens");
                foreach (FurnitureKit.ContainerPart part in parts)
                {
                    Assert.IsTrue(part.Max.x > part.Min.x && part.Max.y > part.Min.y && part.Max.z > part.Min.z, $"a {kind}'s {part.Label} has no size");
                    Assert.Less(Mathf.Abs(part.Pivot.x), 1.3f, $"a {kind}'s {part.Label} turns about a point off the piece");
                    Assert.IsFalse(string.IsNullOrEmpty(part.Label));
                }
            }
            Assert.IsEmpty(FurnitureKit.PartsOf(Kind.Sofa, Color.white), "a sofa opens");
        }
    }
}
