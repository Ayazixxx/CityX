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
    /// the front"): its shape at rest on the standing body and over clothes, how it is skinned (the yoke on the body's bones, the hanging part on
    /// a chain), how it swings (owner, 2026-11-25: "world of warcraft cape physics ... maybe the back flies up a bit when you jump"), and its
    /// colourways.
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
            int[] faces = CapeSkin.Triangles();
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

        /// <summary>
        /// Over a shirt (owner, 2026-11-25: "make cape fit over shirt without clipping"): every point of the yoke and the collar lies outside the
        /// shirt under it, and so does every point of the hanging part near it.
        /// </summary>
        [TestCase("TShirt")]
        [TestCase("LongSleeveTShirt")]
        public void OverAShirtTheYokeLiesOutsideIt(string shirtPrefab)
        {
            var body = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/FusedBody.asset");
            Mesh shirt = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{shirtPrefab}.prefab").GetComponent<SkinnedGarment>().GarmentMesh;
            Vector3[] skin = body.vertices, facing = body.normals, worn = shirt.vertices, wornFacing = shirt.normals;
            Vector3[] onSkin = CapeShape.FitToBody(CapeShape.RestPoints(), skin, facing, out int[] nearest);
            Vector3[] over = CapeShape.FitOver(onSkin, nearest, facing, worn);
            int lifted = 0;
            for (int i = 0; i < over.Length; i++)
            {
                if (!CapeShape.IsOnBody(i)) continue;
                if ((over[i] - onSkin[i]).sqrMagnitude > 1e-6f) lifted++;
                float best = float.MaxValue;
                int k = -1;
                for (int j = 0; j < worn.Length; j++)
                {
                    float d = (worn[j] - over[i]).sqrMagnitude;
                    if (d < best) { best = d; k = j; }
                }
                if (Mathf.Sqrt(best) > CapeShape.CoverReach) continue;   // no shirt under it here (beside the neck, under the head)
                Assert.Greater(Vector3.Dot(over[i] - worn[k], wornFacing[k]), 0.0f, $"the yoke is inside the {shirtPrefab} at {over[i]}");
            }
            Assert.Greater(lifted, 0, "nothing of the yoke was lifted over the shirt");

            Vector3[] clear = CapeShape.ClearHanging(over, worn, wornFacing);
            for (int i = CapeShape.GridIndex(0, CapeShape.YokeRows); i < CapeShape.GridPoints; i++)
            {
                float best = float.MaxValue;
                int k = -1;
                for (int j = 0; j < worn.Length; j++)
                {
                    float d = (worn[j] - clear[i]).sqrMagnitude;
                    if (d < best) { best = d; k = j; }
                }
                if (Mathf.Sqrt(best) > CapeShape.HangingReach) continue;
                Assert.Greater(Vector3.Dot(clear[i] - worn[k], wornFacing[k]), CapeShape.CoverGap * 0.5f, $"the hanging part is in the {shirtPrefab} at {clear[i]}");
            }
        }

        private static CapeSwing SwungFor(Vector3 velocity, float turning, float seconds)
        {
            var swing = new CapeSwing();
            for (float time = 0.0f; time < seconds; time += 1.0f / 60.0f)
            {
                swing.Update(velocity, turning, 0.0f, 1.0f / 60.0f);
            }
            return swing;
        }

        /// <summary>
        /// The swing (owner, 2026-11-25: "it really doesnt need to be anything crazy ... maybe the back flies up a bit when you jump"): hanging
        /// straight standing, lifting back the faster the wearer runs, flying up in a jump or a drop, trailing to the side they turn away from,
        /// and never swinging in towards the body.
        /// </summary>
        [Test]
        public void ItLiftsRunningFliesUpJumpingAndTrailsATurn()
        {
            Assert.AreEqual(0.0f, SwungFor(Vector3.zero, 0.0f, 3.0f).Lift, 0.5f, "standing still it does not hang straight");
            float walk = SwungFor(new Vector3(0.0f, 0.0f, 4.0f), 0.0f, 3.0f).Lift;
            float sprint = SwungFor(new Vector3(0.0f, 0.0f, 7.5f), 0.0f, 3.0f).Lift;
            Assert.That(walk, Is.InRange(10.0f, 25.0f), "walking it does not lift a little");
            Assert.Greater(sprint, walk + 8.0f, "sprinting it does not lift more than walking");
            Assert.Greater(SwungFor(new Vector3(0.0f, -8.0f, 0.0f), 0.0f, 1.0f).Lift, 35.0f, "dropping it does not fly up");
            Assert.Greater(SwungFor(new Vector3(0.0f, 4.5f, 0.0f), 0.0f, 0.4f).Lift, 12.0f, "jumping it does not fly up");
            Assert.Less(SwungFor(Vector3.zero, 180.0f, 1.0f).Sway, -4.0f, "turning right it does not trail left");
            Assert.Greater(SwungFor(new Vector3(-3.0f, 0.0f, 0.0f), 0.0f, 1.0f).Sway, 4.0f, "stepping left it does not trail right");

            // Never into the legs: backing up fast, or stopping dead from a sprint.
            var swing = SwungFor(new Vector3(0.0f, 0.0f, 7.5f), 0.0f, 2.0f);
            float least = float.MaxValue;
            for (int i = 0; i < 180; i++)
            {
                swing.Update(new Vector3(0.0f, 0.0f, -4.0f), 0.0f, 0.0f, 1.0f / 60.0f);
                least = Mathf.Min(least, swing.Lift);
            }
            Assert.GreaterOrEqual(least, CapeSwing.LeastLift, "it swings in towards the body");

            // Steady however long a frame is.
            var slow = new CapeSwing();
            for (int i = 0; i < 100; i++) slow.Update(new Vector3(0.0f, 0.0f, 7.5f), 0.0f, 0.0f, 0.5f);
            Assert.That(slow.Lift, Is.InRange(0.0f, CapeSwing.MostLift), "a long frame throws it");
        }

        /// <summary>
        /// Skinned (owner: "something that will not tank performance in large crowds of people"): every point on four bones at most, adding to
        /// one; the yoke and the collar on the body's bones alone; the hem on the chain alone; and at rest the skinning puts every point where it
        /// was fitted (the chain's bind poses agree with where its bones sit).
        /// </summary>
        [Test]
        public void TheYokeIsOnTheBodysBonesAndTheHangingPartOnTheChain()
        {
            var onBody = new BoneWeight[CapeShape.Points];
            for (int i = 0; i < onBody.Length; i++)
            {
                onBody[i] = new BoneWeight { boneIndex0 = 1, weight0 = 0.7f, boneIndex1 = 2, weight1 = 0.3f };
            }
            const int bodyBones = 5;
            BoneWeight[] weights = CapeSkin.Weights(onBody, bodyBones);
            for (int i = 0; i < weights.Length; i++)
            {
                BoneWeight w = weights[i];
                Assert.AreEqual(1.0f, w.weight0 + w.weight1 + w.weight2 + w.weight3, 1e-4f, $"point {i}'s weights do not add to one");
                if (CapeSkin.Down(i) <= 0.0f)
                {
                    Assert.Less(w.boneIndex0, bodyBones, "the yoke is not on the body");
                }
            }
            BoneWeight hem = weights[CapeShape.GridIndex(CapeShape.Columns / 2, CapeShape.Rows - 1)];
            Assert.GreaterOrEqual(hem.boneIndex0, bodyBones, "the hem is not on the chain");
            Assert.AreEqual(1.0f, hem.weight0, 1e-4f, "the hem is not on the chain alone");

            // At rest: the anchor where its bind pose puts it, the chain where its own local places put it, each point skinned back onto itself.
            Vector3[] rest = CapeShape.RestPoints();
            Matrix4x4 anchorBind = Matrix4x4.TRS(new Vector3(0.0f, -1.2f, 0.05f), Quaternion.Euler(8.0f, 0.0f, 0.0f), Vector3.one);
            Vector3[] chain = CapeSkin.ChainPoints(rest);
            Vector3[] local = CapeSkin.ChainLocalPositions(chain, anchorBind);
            Matrix4x4[] binds = CapeSkin.ChainBindposes(chain, anchorBind);
            Matrix4x4 bone = anchorBind.inverse;   // the anchor's place in body space at rest
            for (int k = 0; k < chain.Length; k++)
            {
                bone *= Matrix4x4.Translate(local[k]);
                foreach (Vector3 p in new[] { rest[0], rest[CapeShape.GridIndex(3, CapeShape.Rows - 1)] })
                {
                    Assert.Less(Vector3.Distance((bone * binds[k]).MultiplyPoint3x4(p), p), 1e-4f, $"chain bone {k} moves the cape at rest");
                }
            }
        }

        /// <summary>The outside of the cape faces out (up on the shoulders, ahead on the collar, back down the back), so it is lit from out there.</summary>
        [Test]
        public void ItsOutsideFacesOut()
        {
            Vector3[] rest = CapeShape.RestPoints();
            var vertices = new Vector3[rest.Length * 2];
            rest.CopyTo(vertices, 0);
            rest.CopyTo(vertices, rest.Length);
            var mesh = new Mesh { vertices = vertices, triangles = CapeSkin.Triangles() };
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
