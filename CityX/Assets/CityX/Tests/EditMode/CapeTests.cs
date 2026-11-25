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

        /// <summary>
        /// Fitted as a LEGO cape is (owner, 2026-11-25, with a picture of one: "make it fit something like this ... just the shoulders neck part"):
        /// a yoke over the whole top of each shoulder, from over its front to behind it, round behind the neck under the head; a collar round the
        /// front of the neck under the chin; nothing of either in the head; the cloth falling behind to the calves.
        /// </summary>
        [Test]
        public void AtRestItSitsOnTheShouldersAsALegoCapeDoesAndFallsBehindToTheCalves()
        {
            var head = new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f);
            Vector3 behindNeck = CapeShape.GridPoint(CapeShape.Columns / 2, 0);
            Assert.Less(behindNeck.z, -0.15f, "the yoke does not go round behind the neck");
            foreach (bool first in new[] { true, false })
            {
                Assert.Greater(Mathf.Abs(CapeShape.Corner(first).x), 0.40f, "the yoke does not reach the shoulder's tip");
            }
            for (int column = 0; column < CapeShape.Columns; column++)
            {
                Vector3 front = CapeShape.GridPoint(column, 0), back = CapeShape.GridPoint(column, CapeShape.YokeRows - 1);
                if (Mathf.Abs(front.x) > 0.25f && Mathf.Abs(front.x) < 0.40f)
                {
                    // Over the shoulder's top (about 10 cm either side of its middle): from in front of it to behind it (that it stays out of
                    // the shoulder is the body-surface check below).
                    Assert.Greater(front.z, 0.06f, $"the yoke does not come over the front of the shoulder at {front}");
                    Assert.Less(back.z, -0.08f, $"the yoke does not reach behind the shoulder at {back}");
                }
                for (int row = 0; row < CapeShape.YokeRows; row++)
                {
                    Vector3 p = CapeShape.GridPoint(column, row);
                    Assert.Greater(Vector3.Distance(p, head), FusedBodyShape.HeadRadius + 0.01f, $"the yoke runs into the head at {p}");
                }
            }
            Vector3 collar = CapeShape.CollarPoint(CapeShape.CollarPoints / 2, false);
            Assert.Greater(collar.z, 0.15f, "the collar does not go round the front of the neck");
            for (int along = 0; along < CapeShape.CollarPoints; along++)
            {
                foreach (bool bottom in new[] { false, true })
                {
                    Vector3 p = CapeShape.CollarPoint(along, bottom);
                    Assert.Greater(Vector3.Distance(p, head), FusedBodyShape.HeadRadius + 0.01f, $"the collar runs into the head at {p}");
                }
            }
            Vector3 collarEnd = CapeShape.CollarPoint(CapeShape.CollarPoints - 1, true);
            float meets = float.MaxValue;
            for (int column = 0; column < CapeShape.Columns; column++)
            {
                for (int row = 0; row < CapeShape.YokeRows; row++) meets = Mathf.Min(meets, Vector3.Distance(collarEnd, CapeShape.GridPoint(column, row)));
            }
            Assert.Less(meets, 0.08f, "the collar's ends do not tuck under the yoke");

            float hem = CapeShape.GridPoint(CapeShape.Columns / 2, CapeShape.Rows - 1).y;
            Assert.That(hem, Is.InRange(0.5f, 0.75f), "the hem is not at the calves");
            // The falling cloth outside the body's own surface everywhere (each point on the outer side of the body's nearest point); the yoke and
            // the collar are fitted onto the body when worn (FittedItLiesJustOffTheShouldersLikeASkinSuit).
            var body = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/FusedBody.asset");
            Vector3[] skin = body.vertices, facing = body.normals;
            Vector3[] restPoints = CapeShape.RestPoints();
            for (int point = 0; point < restPoints.Length; point++)
            {
                if (CapeShape.IsOnBody(point)) continue;
                Vector3 p = restPoints[point];
                int nearest = 0;
                float best = float.MaxValue;
                for (int i = 0; i < skin.Length; i++)
                {
                    float d = (skin[i] - p).sqrMagnitude;
                    if (d < best) { best = d; nearest = i; }
                }
                Assert.Greater(Vector3.Dot(p - skin[nearest], facing[nearest]), 0.005f, $"the cloth at rest is inside the body at {p}");
            }
        }

        /// <summary>
        /// Fitted to the body like a skin suit (owner, 2026-11-25: "like the same dimensions of the shoulder just a tiny bit bigger so its not
        /// clipping through"): every point of the yoke and the collar just off the body's own surface, never in it, never in the head.
        /// </summary>
        [Test]
        public void FittedItLiesJustOffTheShouldersLikeASkinSuit()
        {
            var body = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/FusedBody.asset");
            Vector3[] skin = body.vertices, facing = body.normals;
            Vector3[] fitted = CapeShape.FitToBody(CapeShape.RestPoints(), skin, facing, out int[] nearest);
            var head = new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f);
            float furthest = 0.0f;
            for (int i = 0; i < fitted.Length; i++)
            {
                if (!CapeShape.IsOnBody(i)) continue;
                Assert.GreaterOrEqual(nearest[i], 0, "a point of the yoke is not fitted");
                float off = float.MaxValue;
                int k = 0;
                for (int j = 0; j < skin.Length; j++)
                {
                    float d = (skin[j] - fitted[i]).sqrMagnitude;
                    if (d < off) { off = d; k = j; }
                }
                float above = Vector3.Dot(fitted[i] - skin[k], facing[k]);
                Assert.Greater(above, 0.004f, $"the yoke is in the body at {fitted[i]}");
                furthest = Mathf.Max(furthest, Mathf.Sqrt(off));
                // (Beside the neck it may tuck a little under the head's ball, which hides it: there is no room between the two there.)
                Assert.Greater(Vector3.Distance(fitted[i], head), FusedBodyShape.HeadRadius - 0.04f, $"the yoke stands in the head at {fitted[i]}");
            }
            Assert.Less(furthest, 0.045f, "part of the yoke stands well off the body: not fitted like a skin suit");

            // Nowhere folded over itself (a fold shows as specks of outline): every triangle of the yoke faces away from the body under it.
            int[] faces = CapeLook.Faces();
            var folded = new System.Collections.Generic.List<string>();
            for (int t = 0; t < faces.Length; t += 3)
            {
                int a = faces[t], b = faces[t + 1], c = faces[t + 2];
                if (a >= fitted.Length || b >= fitted.Length || c >= fitted.Length || !CapeShape.IsOnBody(a) || !CapeShape.IsOnBody(b) || !CapeShape.IsOnBody(c))
                {
                    continue;   // the inside's copies, and the falling cloth
                }
                Vector3 face = Vector3.Cross(fitted[b] - fitted[a], fitted[c] - fitted[a]);
                Vector3 centre = (fitted[a] + fitted[b] + fitted[c]) / 3.0f;
                if (new Vector2(centre.x, centre.z).magnitude < 0.22f && centre.y > head.y - FusedBodyShape.HeadRadius - 0.03f)
                {
                    continue;   // tucked under the head's ball beside the neck: hidden by it (there is no room there to lie smooth)
                }
                if (Vector3.Dot(face, facing[nearest[a]] + facing[nearest[b]] + facing[nearest[c]]) <= 0.0f) folded.Add($"{a}:{fitted[a]} {b}:{fitted[b]} {c}:{fitted[c]} body {facing[nearest[a]]}");
            }
            Assert.IsEmpty(folded, $"the yoke folds over itself at {string.Join(", ", folded)}");
        }

        /// <summary>The outside of the cape faces out (up on the shoulders, ahead on the collar, back down the back), so it is lit from out there.</summary>
        [Test]
        public void ItsOutsideFacesOut()
        {
            Vector3[] rest = CapeShape.RestPoints();
            var vertices = new Vector3[rest.Length * 2];
            rest.CopyTo(vertices, 0);
            rest.CopyTo(vertices, rest.Length);
            var mesh = new Mesh { vertices = vertices, triangles = CapeLook.Faces() };
            mesh.RecalculateNormals();
            Vector3[] normals = mesh.normals;
            Object.DestroyImmediate(mesh);
            int middle = CapeShape.Columns / 2, shoulder = CapeShape.Columns * 3 / 4;
            Assert.Greater(normals[CapeShape.GridIndex(shoulder, 2)].y, 0.5f, $"the yoke's outside on the shoulder faces {normals[CapeShape.GridIndex(shoulder, 2)]}");
            Assert.Less(normals[CapeShape.GridIndex(middle, CapeShape.Rows / 2)].z, -0.5f, "the back's outside does not face back");
            Vector3 collar = normals[CapeShape.CollarIndex(CapeShape.CollarPoints / 2, false)];
            Assert.Greater(collar.z + collar.y, 0.5f, $"the collar's outside faces {collar}");
            Assert.Less(Vector3.Dot(normals[0], normals[rest.Length]), -0.9f, "the inside does not face the other way");
        }

        private static CapeCloth Hanging(out Vector3[] rest)
        {
            rest = CapeShape.RestPoints();
            return CapeShape.MakeCloth(rest);
        }

        private static List<(int, Vector3)> Pins(Vector3[] at)
        {
            var pins = new List<(int, Vector3)>();
            bool[] held = CapeShape.MakeCloth(CapeShape.RestPoints()).Pinned;
            for (int i = 0; i < held.Length; i++)
            {
                if (held[i]) pins.Add((i, at[i]));
            }
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
            float stretch = MostStretch(cloth, rest);
            Assert.Less(stretch, 1.08f, $"the cloth stretches like rubber ({stretch:0.000} its length)");
            float moving = 0.0f;
            for (int i = 0; i < cloth.Position.Length; i++) moving = Mathf.Max(moving, (cloth.Position[i] - cloth.Previous[i]).magnitude / CapeCloth.StepSeconds);
            Assert.Less(moving, 0.3f, "after three seconds it still swings");
            Assert.Less(cloth.Position[CapeShape.GridIndex(CapeShape.Columns / 2, CapeShape.Rows - 1)].y, CapeShape.BackEdge[0].y - CapeShape.Length * 0.9f,
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
                Assert.IsTrue(ApproximatelyEqual(colourway.Colour, cape.Fabric.GetColor("_BaseColor")), $"{colourway.ItemId} is the wrong colour");
                Assert.IsNotNull(prefab.GetComponent<ICosmeticMountable>(), "other players would not see it");
                Assert.IsNotNull(cape.GroundPreview);
            }
        }

        private static bool ApproximatelyEqual(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
    }
}
