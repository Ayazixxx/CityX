using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The T-Shirt (owner, 2026-10-20: "a wearable top called T-Shirt ... flatten out the lump and wrinks at the front so we can cleanly put a
    /// various textures and logos on here ... lower the face count"): a top in the registry, within its budget, worn from the shoulders to the
    /// hips with short sleeves, skinned only to what a shirt covers, and laid out so a design's front lands on the chest and its back on the back.
    /// </summary>
    public sealed class TShirtTests
    {
        private const int TriangleBudget = 8000;

        /// <summary>Where on the template each piece lies (tshirt_build.py's PIECE_BOXES): the front on the left of the top, the back on the right.</summary>
        private static readonly Rect FrontPiece = new Rect(0.0f, 0.4f, 0.5f, 0.6f);
        private static readonly Rect BackPiece = new Rect(0.5f, 0.4f, 0.5f, 0.6f);

        /// <summary>The middle of the chest, bind pose (+z ahead).</summary>
        private const float ChestHeight = 1.62f;

        private static SkinnedGarment Shirt()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/TShirt.prefab");
            Assert.IsNotNull(prefab, "no TShirt.prefab: rebuild the scene (CityMassingSceneBuilder)");
            var shirt = prefab.GetComponent<TShirt>();
            Assert.IsNotNull(shirt.GarmentMesh, "the shirt has no mesh");
            return shirt;
        }

        [Test]
        public void TheTShirtIsATopCalledTShirt()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.TShirt, out ItemDefinition shirt));
            Assert.AreEqual("T-Shirt", shirt.DisplayName);
            Assert.AreEqual(EquipSlots.Torso, shirt.EquipSlot);
            Assert.AreEqual(ItemRegistry.TShirt, Shirt().ItemId);
        }

        [Test]
        public void TheShirtIsWithinItsBudgetAndHidesTheBodyItCovers()
        {
            SkinnedGarment shirt = Shirt();
            Assert.LessOrEqual(shirt.GarmentMesh.triangles.Length / 3, TriangleBudget);
            Assert.AreEqual(shirt.GarmentMesh.vertexCount, shirt.GarmentMesh.uv.Length, "the shirt has no texture coordinates for a design");
            Assert.IsNotNull(shirt.HiddenBodyTriangles, "the shirt hides none of the body under it");
            Assert.AreEqual((shirt.HiddenFromBodyTriangles + 7) / 8, shirt.HiddenBodyTriangles.Length);
        }

        [Test]
        public void TheShirtFollowsOnlyWhatAShirtCoversAndEveryWeightAddsUp()
        {
            Mesh mesh = Shirt().GarmentMesh;
            Assert.AreEqual(SkinnedGarment.Bones.Length + GarmentHelperBones.Helpers.Length, mesh.bindposes.Length,
                "the shirt is not bound to the body's bones and the helper bones (shoulders, collar)");
            var never = new System.Collections.Generic.List<int>
            {
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.Head),
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.LeftHand),
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.RightHand),
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.LeftUpperLeg),
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.RightUpperLeg),
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.LeftLowerLeg),
                System.Array.IndexOf(SkinnedGarment.Bones, HumanBodyBones.RightLowerLeg),
            };
            for (int skirt = 0; skirt < 4; skirt++)
            {
                never.Add(SkinnedGarment.Bones.Length + skirt);   // a shirt has no skirt
            }
            foreach (BoneWeight w in mesh.boneWeights)
            {
                Assert.AreEqual(1.0f, w.weight0 + w.weight1 + w.weight2 + w.weight3, 0.01f);
                foreach ((int bone, float weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                {
                    if (weight > 0.001f)
                    {
                        CollectionAssert.DoesNotContain(never, bone, "part of the shirt follows the head, a hand, a leg or a skirt bone");
                    }
                }
            }
        }

        [Test]
        public void TheShirtCoversTheShouldersToTheHipsWithShortSleeves()
        {
            Bounds bounds = Shirt().GarmentMesh.bounds;
            Assert.Greater(bounds.max.y, BodyRig.ShoulderY, "the shirt does not reach the shoulders");
            Assert.Less(bounds.max.y, BodyRig.ShoulderY + 0.15f, "the shirt's collar climbs up the neck");
            Assert.Less(bounds.min.y, BodyRig.HipY + 0.1f, "the shirt does not reach the hips");
            Assert.Greater(bounds.min.y, BodyRig.HipY - 0.1f, "the shirt hangs down the thighs");
            Assert.Greater(bounds.max.x, BodyRig.ShoulderX + 0.1f, "no sleeve reaches out along the arm");
            Assert.Greater(bounds.min.y, 0.0f);
            foreach (Vector3 v in Shirt().GarmentMesh.vertices)
            {
                if (Mathf.Abs(v.x) > BodyRig.ShoulderX + 0.05f)
                {
                    Assert.Greater(v.y, (BodyRig.ShoulderY + BodyRig.HandY) / 2.0f, "a sleeve reaches down past the elbow: it is a short-sleeved shirt");
                }
            }
        }

        [Test]
        public void ADesignsFrontLandsOnTheChestAndItsBackOnTheBack()
        {
            Mesh mesh = Shirt().GarmentMesh;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            int front = -1, back = -1;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (Mathf.Abs(v.x) > 0.03f || Mathf.Abs(v.y - ChestHeight) > 0.05f)
                {
                    continue;
                }
                if (v.z > 0.0f && (front < 0 || v.z > vertices[front].z))
                {
                    front = i;
                }
                if (v.z < 0.0f && (back < 0 || v.z < vertices[back].z))
                {
                    back = i;
                }
            }
            Assert.GreaterOrEqual(front, 0, "no point of the shirt on the middle of the chest");
            Assert.GreaterOrEqual(back, 0, "no point of the shirt on the middle of the back");
            Assert.IsTrue(FrontPiece.Contains(uvs[front]), $"the chest is at {uvs[front]} on the texture, not on its front piece");
            Assert.IsTrue(BackPiece.Contains(uvs[back]), $"the back is at {uvs[back]} on the texture, not on its back piece");
            // Upright and the right way round: higher on the chest is higher on the texture, and the wearer's left (-x) is a viewer's right.
            int above = Nearest(vertices, new Vector3(0.0f, ChestHeight + 0.2f, vertices[front].z));
            int left = Nearest(vertices, new Vector3(-0.15f, ChestHeight, vertices[front].z));
            Assert.Greater(uvs[above].y, uvs[front].y, "the front is upside down on the texture");
            Assert.Greater(uvs[left].x, uvs[front].x, "the front is mirrored on the texture: a logo's writing would read backwards");
        }

        private static int Nearest(Vector3[] vertices, Vector3 to)
        {
            int best = 0;
            for (int i = 1; i < vertices.Length; i++)
            {
                if ((vertices[i] - to).sqrMagnitude < (vertices[best] - to).sqrMagnitude)
                {
                    best = i;
                }
            }
            return best;
        }
    }
}
