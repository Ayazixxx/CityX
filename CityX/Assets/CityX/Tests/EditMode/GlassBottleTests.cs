using System.Collections.Generic;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The glass bottles (docs/GLASS_BOTTLE.md): every kind is a weapon with its shape, its three models (whole, a half with sharp points, the
    /// neck), its bottle in the air and its pieces; the break is a ring of uneven points; the pieces are the whole bottle and nothing else;
    /// the hold puts the bottle out past the thumb as a club is held.
    /// </summary>
    public sealed class GlassBottleTests
    {
        private static IEnumerable<string> Kinds()
        {
            foreach (GlassBottleKinds.Kind kind in GlassBottleKinds.All)
            {
                yield return kind.ItemId;
            }
        }

        [Test]
        public void TheFirstBottleKeepsItsIdAndThereAreSeveralShapes()
        {
            Assert.AreEqual("glass_bottle", GlassBottleKinds.Classic, "a save's bottle would be lost");
            Assert.AreEqual(ItemRegistry.GlassBottle, GlassBottleKinds.Classic);
            Assert.GreaterOrEqual(GlassBottleKinds.All.Length, 4);
            Assert.AreEqual(GlassBottleKinds.Classic, GlassBottleKinds.ItemIdForAlias("bottle"));
            Assert.AreEqual(GlassBottleKinds.Beer, GlassBottleKinds.ItemIdForAlias("beer"));
            Assert.AreEqual(GlassBottleKinds.Wine, GlassBottleKinds.ItemIdForAlias("wine_bottle"));
            Assert.IsNull(GlassBottleKinds.ItemIdForAlias("teapot"));
        }

        [TestCaseSource(nameof(Kinds))]
        public void EveryKindIsAWeaponWithItsShapeAndItsModels(string itemId)
        {
            Assert.IsTrue(ItemRegistry.TryGet(itemId, out ItemDefinition definition), itemId);
            Assert.AreEqual(EquipSlots.Primary, definition.EquipSlot);
            Assert.AreEqual(1, definition.StackSize, "a bottle carries its own state: it does not stack");
            Assert.IsTrue(HitTypes.TryGet(itemId, out string hits), "its card says nothing of its hits");
            StringAssert.StartsWith("Blunt", hits, "a whole bottle is blunt");
            StringAssert.Contains("Pierce", hits, "a broken bottle pierces");

            BottleShape shape = BottleShape.For(itemId);
            Assert.IsNotNull(shape, $"{itemId} has no shape");
            Assert.Less(shape.BodyTop, shape.NeckBase);
            Assert.Less(shape.NeckBase, shape.Height);
            Assert.That(shape.GripHeight, Is.InRange(shape.NeckBase, shape.Height), "it is held by the neck");
            Assert.Less(shape.BrokenAt, shape.BodyTop, "a blow leaves some of the body");
            Assert.Greater(shape.BrokenReach, 0.1f);
            Assert.Less(shape.BrokenReach, shape.Reach, "a broken bottle is shorter");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlassBottleKinds.PrefabPath(itemId));
            Assert.IsNotNull(prefab, $"{itemId} has no prefab: run CityX/Rebuild Glass Bottles");
            var bottle = prefab.GetComponent<BottleWeapon>();
            Assert.AreEqual(itemId, bottle.ItemId);
            foreach ((string what, GameObject visual, int most) in new[]
            {
                ("whole", bottle.IntactVisual, 5000), ("broken", bottle.BrokenVisual, 5000), ("neck", bottle.ShatteredVisual, 3000),
            })
            {
                Assert.IsNotNull(visual, $"{itemId} has no {what} model");
                int triangles = 0;
                foreach (MeshFilter part in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    Assert.IsNotNull(part.sharedMesh, $"{itemId}'s {what} model lost a mesh");
                    triangles += part.sharedMesh.triangles.Length / 3;
                }
                Assert.Greater(triangles, 50, $"{itemId}'s {what} model is empty");
                Assert.Less(triangles, most, $"{itemId}'s {what} model is over a one-handed weapon's budget");
            }
            Assert.IsTrue(bottle.IntactVisual.activeSelf && !bottle.BrokenVisual.activeSelf && !bottle.ShatteredVisual.activeSelf, "a new bottle is whole");

            // Held by the neck with the body out along +Y: the whole one reaches furthest.
            float Furthest(GameObject visual)
            {
                float furthest = float.MinValue;
                foreach (MeshFilter part in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    furthest = Mathf.Max(furthest, part.sharedMesh.bounds.max.y);
                }
                return furthest;
            }
            Assert.AreEqual(shape.Reach, Furthest(bottle.IntactVisual), 0.02f, "the base is not where the shape says");
            Assert.AreEqual(shape.BrokenReach, Furthest(bottle.BrokenVisual), 0.03f, "the broken half does not end at its longest point");
            Assert.Less(Furthest(bottle.ShatteredVisual), Furthest(bottle.BrokenVisual));

            Assert.IsNotNull(bottle.ThrownBottlePrefab, "it cannot be thrown");
            Assert.AreSame(bottle.ThrownBottlePrefab, AssetDatabase.LoadAssetAtPath<GameObject>(GlassBottleKinds.ThrownPrefabPath(itemId)));
            Assert.IsNotNull(bottle.ThrownBottlePrefab.GetComponent<ThrownBottle>().ShatterPrefab, "thrown, it would vanish instead of shattering");
            var shatter = bottle.ShatterPrefab.GetComponent<BottleShatter>();
            Assert.GreaterOrEqual(shatter.Pieces.Length, 8, "it hardly shatters");
            Assert.AreEqual(shatter.Pieces.Length, shatter.AlongBottle.Length);
            int beyondTheBreak = 0;
            foreach (float along in shatter.AlongBottle)
            {
                beyondTheBreak += along > (shape.GripHeight - shape.BrokenAt) * BottleShape.Scale ? 1 : 0;
            }
            Assert.Greater(beyondTheBreak, 0, "broken in the hand, nothing would fly off");
            Assert.Less(beyondTheBreak, shatter.Pieces.Length, "broken in the hand, the neck would fly off too");
        }

        [Test]
        public void TheBreakIsARingOfUnevenPoints()
        {
            int tips = 0, notches = 0;
            float before = BottleMesh.Fracture(-1.0f), longest = 0.0f, shortestTip = 1.0f;
            float at = BottleMesh.Fracture(0.0f);
            for (int degrees = 1; degrees <= 360; degrees++)
            {
                float after = BottleMesh.Fracture(degrees);
                Assert.That(at, Is.InRange(0.0f, 1.0f));
                if (at > before && at >= after && at > 0.05f)
                {
                    tips++;
                    longest = Mathf.Max(longest, at);
                    shortestTip = Mathf.Min(shortestTip, at);
                }
                if (at < before && at <= after)
                {
                    notches++;
                }
                before = at;
                at = after;
            }
            Assert.GreaterOrEqual(tips, 6, "too few points for a broken bottle");
            Assert.GreaterOrEqual(notches, 6);
            Assert.Greater(longest, 0.9f);
            Assert.Less(shortestTip, 0.5f, "the points are all of a length: glass does not break evenly");
        }

        [Test]
        public void ABrokenBottleIsTheBottleFromItsMouthDownToThePoints()
        {
            BottleShape shape = BottleShape.For(GlassBottleKinds.Beer);
            BottleMesh.Model whole = BottleMesh.Turned(shape.Profile, 36, 0.006f);
            BottleMesh.Model broken = BottleMesh.BrokenOff(whole, shape.BrokenAt, BottleShape.LongestPoint);
            Assert.Greater(broken.TriangleCount, 100);
            Assert.Less(broken.TriangleCount, whole.TriangleCount);
            float lowest = float.MaxValue, highest = float.MinValue;
            foreach (Vector3 v in broken.Vertices)
            {
                lowest = Mathf.Min(lowest, v.y);
                highest = Mathf.Max(highest, v.y);
            }
            Assert.AreEqual(shape.Height, highest, 0.002f, "the mouth is gone");
            Assert.AreEqual(shape.BrokenAt - BottleShape.LongestPoint, lowest, 0.004f, "the longest point does not reach where it should");
        }

        [Test]
        public void ThePiecesAreTheWholeBottleAndTheNeckIsOneOfThem()
        {
            BottleShape shape = BottleShape.For(GlassBottleKinds.Wine);
            BottleMesh.Model whole = BottleMesh.Turned(shape.Profile, 36, 0.006f);
            List<BottleMesh.Piece> pieces = BottleMesh.Pieces(whole, 14, shape.NeckBase, 7);
            Assert.That(pieces.Count, Is.InRange(10, 14));
            int triangles = 0;
            bool neck = false;
            foreach (BottleMesh.Piece piece in pieces)
            {
                triangles += piece.Model.TriangleCount;
                Assert.Greater(piece.Model.TriangleCount, 0);
                neck |= piece.Centre.y > shape.NeckBase;
            }
            Assert.AreEqual(whole.TriangleCount, triangles, "glass was lost or made in the shattering");
            Assert.IsTrue(neck, "the neck broke up: it is the piece everyone knows a bottle by");
        }

        [Test]
        public void ItIsHeldByTheNeckWithItsBodyOutPastTheThumb()
        {
            Vector3 bottle = BottleWeapon.HoldRotation * Vector3.up;
            Assert.Greater(Vector3.Dot(bottle, SwordWeapon.OneHandBladeAxisInHand), 0.999f, "the bottle is not held as a club is");
            Assert.Greater(bottle.z, 0.9f, "+Z is the thumb's side of the hand");
            Assert.AreEqual(SwordWeapon.FistPoint, BottleWeapon.HoldPosition);
            // A standing bottle's base is at the far end of the held one, its mouth behind the fist.
            BottleShape shape = BottleShape.For(GlassBottleKinds.Soda);
            Assert.AreEqual(shape.Reach, BottleMesh.Held(Vector3.zero, shape.GripHeight, BottleShape.Scale).y, 1e-4f);
            Assert.Less(BottleMesh.Held(new Vector3(0.0f, shape.Height, 0.0f), shape.GripHeight, BottleShape.Scale).y, 0.0f);
        }
    }
}
