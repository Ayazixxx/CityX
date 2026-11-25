using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The cape (docs/CAPE.md; owner, 2026-11-24: "make a basic cape that we can make a bunch of different colorways of easily, make it tied around
    /// the front. and research how to make good cape physics in video games"): its shape at rest on the standing body, its cloth (hangs without
    /// stretching, kept out of the body, trails behind a runner), and its colourways.
    /// </summary>
    public sealed class CapeTests
    {
        private static float FromTrunk(Vector3 p) => new Vector2(p.x, p.z).magnitude;

        [Test]
        public void AtRestItLiesOnTheShouldersFallsBehindToTheCalvesAndIsTiedAtTheThroat()
        {
            Vector3 middleTop = CapeShape.GridPoint(CapeShape.Columns / 2, 0);
            Assert.Less(middleTop.z, -0.2f, "the top edge is not behind the neck");
            Assert.AreEqual(CapeShape.TopHeight, middleTop.y, 1e-4f);
            foreach (bool first in new[] { true, false })
            {
                Vector3 corner = CapeShape.Corner(first);
                Assert.Greater(corner.z, 0.0f, "the top edge does not come round to the front of the shoulder");
                Assert.Greater(Mathf.Abs(corner.x), BodyRig.ShoulderX - 0.05f, "the corner is not over the shoulder");
                for (int i = 0; i < CapeShape.CordPoints; i++)
                {
                    Assert.Greater(FromTrunk(CapeShape.CordPoint(first, i)), FusedBodyShape.TorsoRadius, "a cord runs inside the chest");
                }
                Assert.Less(Vector3.Distance(CapeShape.CordPoint(first, CapeShape.CordPoints - 1), CapeShape.Knot), 1e-4f, "a cord does not reach the knot");
            }
            Assert.Greater(CapeShape.Knot.z, FusedBodyShape.TorsoRadius, "the knot is not in front of the chest");
            Assert.Less(CapeShape.Knot.y, BodyRig.HeadCentreY - FusedBodyShape.HeadRadius, "the knot is inside the head");

            float hem = CapeShape.GridPoint(CapeShape.Columns / 2, CapeShape.Rows - 1).y;
            Assert.That(hem, Is.InRange(0.5f, 0.75f), "the hem is not at the calves");
            for (int row = 1; row < CapeShape.Rows; row++)
            {
                for (int column = 0; column < CapeShape.Columns; column++)
                {
                    Vector3 p = CapeShape.GridPoint(column, row);
                    if (p.y > BodyRig.HipY)
                    {
                        Assert.Greater(FromTrunk(p), FusedBodyShape.TorsoRadius, $"the cloth at rest is inside the body at {p}");
                    }
                }
            }
        }

        private static CapeCloth Hanging(out Vector3[] rest)
        {
            rest = CapeShape.RestPoints();
            return CapeShape.MakeCloth(rest);
        }

        private static List<(int, Vector3)> Pins(Vector3[] at)
        {
            var pins = new List<(int, Vector3)>();
            for (int column = 0; column < CapeShape.Columns; column++) pins.Add((CapeShape.GridIndex(column, 0), at[CapeShape.GridIndex(column, 0)]));
            pins.Add((CapeShape.KnotIndex, at[CapeShape.KnotIndex]));
            return pins;
        }

        private static float MostStretch(CapeCloth cloth, Vector3[] rest)
        {
            float most = 0.0f;
            for (int row = 0; row + 1 < CapeShape.Rows; row++)
            {
                for (int column = 0; column < CapeShape.Columns; column++)
                {
                    int a = CapeShape.GridIndex(column, row), b = CapeShape.GridIndex(column, row + 1);
                    most = Mathf.Max(most, Vector3.Distance(cloth.Position[a], cloth.Position[b]) / Vector3.Distance(rest[a], rest[b]));
                }
            }
            return most;
        }

        [Test]
        public void HangingItSettlesWithoutStretching()
        {
            CapeCloth cloth = Hanging(out Vector3[] rest);
            List<(int, Vector3)> pins = Pins(rest);
            for (int step = 0; step < 270; step++)
            {
                cloth.Step(pins, null, null, Vector3.zero, -10.0f);
            }
            Assert.Less(MostStretch(cloth, rest), 1.06f, "the cloth stretches like rubber");
            float moving = 0.0f;
            for (int i = 0; i < cloth.Position.Length; i++) moving = Mathf.Max(moving, (cloth.Position[i] - cloth.Previous[i]).magnitude / CapeCloth.StepSeconds);
            Assert.Less(moving, 0.3f, "after three seconds it still swings");
            Assert.Less(cloth.Position[CapeShape.GridIndex(CapeShape.Columns / 2, CapeShape.Rows - 1)].y, CapeShape.TopHeight - CapeShape.Length * 0.9f,
                "the hem does not hang down");
        }

        [Test]
        public void TheBodyPushesItOut()
        {
            CapeCloth cloth = Hanging(out Vector3[] rest);
            List<(int, Vector3)> pins = Pins(rest);
            // A body leaning back into the cape: its trunk a capsule well behind where the cloth hangs.
            var capsules = new[] { new CapeCloth.Capsule(new Vector3(0.0f, 0.9f, -0.35f), new Vector3(0.0f, 1.6f, -0.2f), 0.3f) };
            for (int step = 0; step < 180; step++)
            {
                cloth.Step(pins, rest, capsules, Vector3.zero, -10.0f);
            }
            for (int i = 0; i < cloth.Position.Length; i++)
            {
                if (cloth.Pinned[i]) continue;
                Vector3 pushed = CapeCloth.PushOut(cloth.Position[i], capsules[0]);
                Assert.Less(Vector3.Distance(pushed, cloth.Position[i]), 0.01f, $"the cloth is inside the body at {cloth.Position[i]}");
            }
        }

        [Test]
        public void RunningItStreamsOutBehind()
        {
            CapeCloth cloth = Hanging(out Vector3[] rest);
            int hem = CapeShape.GridIndex(CapeShape.Columns / 2, CapeShape.Rows - 1);
            for (int step = 0; step < 180; step++)
            {
                cloth.Step(Pins(rest), rest, null, Vector3.zero, -10.0f);
            }
            float standingHem = cloth.Position[hem].y;
            // Running forward (+z) at a sprint for two seconds: the held edge carried along, the cloth behind it.
            var moved = new Vector3[rest.Length];
            const float speed = 7.5f;
            for (int step = 0; step < 180; step++)
            {
                float z = speed * CapeCloth.StepSeconds * (step + 1);
                for (int i = 0; i < rest.Length; i++) moved[i] = rest[i] + Vector3.forward * z;
                cloth.Step(Pins(moved), moved, null, Vector3.zero, -10.0f);
            }
            float topZ = moved[CapeShape.GridIndex(CapeShape.Columns / 2, 0)].z;
            Assert.Less(cloth.Position[hem].z, topZ - 0.6f, "running, the cape does not stream out behind");
            Assert.Greater(cloth.Position[hem].y, standingHem + 0.25f, "running, the cape's hem does not lift");
            Assert.Less(MostStretch(cloth, rest), 1.08f, "running stretches the cloth");
        }

        [Test]
        public void ACapsulePushesAPointToItsSurface()
        {
            var capsule = new CapeCloth.Capsule(Vector3.zero, Vector3.up, 0.5f);
            Assert.AreEqual(new Vector3(0.0f, 0.5f, -0.5f), CapeCloth.PushOut(new Vector3(0.0f, 0.5f, -0.1f), capsule));
            Assert.AreEqual(new Vector3(2.0f, 0.5f, 0.0f), CapeCloth.PushOut(new Vector3(2.0f, 0.5f, 0.0f), capsule), "a point outside moved");
            Assert.AreEqual(0.5f, CapeCloth.PushOut(new Vector3(0.0f, 1.2f, 0.0f), capsule).y - 1.0f, 1e-5f, "the capsule's rounded end does not push out");
        }

        [Test]
        public void EveryColourwayIsAnItemOnTheBackAndAPrefab()
        {
            Assert.GreaterOrEqual(CapeColourways.All.Length, 1);
            CollectionAssert.AllItemsAreUnique(System.Array.ConvertAll(CapeColourways.All, c => c.ItemId));
            Assert.AreEqual(CapeColourways.RedItemId, CapeColourways.ItemIdForAlias("cape"));
            foreach (CapeColourways.Colourway colourway in CapeColourways.All)
            {
                Assert.IsTrue(ItemRegistry.TryGet(colourway.ItemId, out ItemDefinition definition), colourway.ItemId);
                Assert.AreEqual(EquipSlots.Back, definition.EquipSlot);
                Assert.IsTrue(definition.IsWearable);
                Assert.AreEqual(colourway.ItemId, CapeColourways.ItemIdForAlias(colourway.Alias));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CapeColourways.PrefabPath(colourway.ItemId));
                Assert.IsNotNull(prefab, $"{colourway.ItemId} has no prefab -- rebuild the scene");
                var cape = prefab.GetComponent<Cape>();
                Assert.IsNotNull(cape);
                Assert.IsTrue(ApproximatelyEqual(colourway.Outside, cape.Outside.GetColor("_BaseColor")), $"{colourway.ItemId}'s outside is the wrong colour");
                Assert.IsTrue(ApproximatelyEqual(colourway.Lining, cape.Lining.GetColor("_BaseColor")));
                Assert.IsTrue(ApproximatelyEqual(colourway.Ties, cape.Ties.GetColor("_BaseColor")));
                Assert.IsNotNull(prefab.GetComponent<ICosmeticMountable>(), "other players would not see it");
                Assert.IsNotNull(cape.GroundPreview);
            }
        }

        private static bool ApproximatelyEqual(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
    }
}
