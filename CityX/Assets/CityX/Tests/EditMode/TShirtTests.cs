using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The T-Shirt (owner, 2026-10-19/20: "a wearable top called T-Shirt ... flatten out the lump and wrinks at the front so we can cleanly put
    /// a various textures and logos on here ... lower the face count"; "detatch the sleeve from the body"): a top in the registry, within its
    /// budget, worn from the shoulders to the hips with short sleeves, skinned only to what a shirt covers, its sleeves cut loose from its body
    /// along seams that never open, and laid out so a design's front lands on the chest and its back on the back.
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
                Bone(HumanBodyBones.Head), Bone(HumanBodyBones.LeftHand), Bone(HumanBodyBones.RightHand),
                Bone(HumanBodyBones.LeftUpperLeg), Bone(HumanBodyBones.RightUpperLeg), Bone(HumanBodyBones.LeftLowerLeg), Bone(HumanBodyBones.RightLowerLeg),
                HelperIndex("skirt_front_l"), HelperIndex("skirt_back_l"), HelperIndex("skirt_front_r"), HelperIndex("skirt_back_r"),
            };
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

        /// <summary>
        /// Owner, 2026-10-20: "the shirt stretches around the armpits please detatch the sleeve from the body". Every point is either on a
        /// sleeve, following its arm (the upper arm, the forearm near the cuff and, round the armhole, that side's half-turning shoulder bone)
        /// and nothing of the torso, or on the shirt's body, following no arm: nothing is blended between the torso and an arm to stretch.
        /// </summary>
        [Test]
        public void EachSleeveFollowsItsArmAndTheBodyNoArm()
        {
            Mesh mesh = Shirt().GarmentMesh;
            int[] left = { Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), HelperIndex("shoulder_l") };
            int[] right = { Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), HelperIndex("shoulder_r") };
            var arms = new System.Collections.Generic.HashSet<int>
            {
                Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), Bone(HumanBodyBones.LeftHand),
                Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), Bone(HumanBodyBones.RightHand),
            };
            int onSleeves = 0;
            foreach (BoneWeight w in mesh.boneWeights)
            {
                var used = new System.Collections.Generic.List<int>();
                foreach ((int bone, float weight) in new[] { (w.boneIndex0, w.weight0), (w.boneIndex1, w.weight1), (w.boneIndex2, w.weight2), (w.boneIndex3, w.weight3) })
                {
                    if (weight > 0.001f)
                    {
                        used.Add(bone);
                    }
                }
                if (!used.Exists(arms.Contains))
                {
                    continue;   // the shirt's body
                }
                onSleeves++;
                Assert.IsTrue(used.TrueForAll(b => System.Array.IndexOf(left, b) >= 0) || used.TrueForAll(b => System.Array.IndexOf(right, b) >= 0),
                    "a point of the shirt is shared between the torso and an arm (it stretches)");
            }
            Assert.Greater(onSleeves, mesh.vertexCount / 8, "the sleeves do not follow the arms");
        }

        /// <summary>
        /// The sleeves are cut loose from the shirt's body, and the texture's pieces apart from each other, by points doubled along the cut:
        /// every two at one place move alike, so no seam ever opens (at the armholes, both edges follow the half-turning shoulder bone; skinned
        /// apart, a swinging arm pulled them 25 cm apart and the arm showed through the slit at rest).
        /// </summary>
        [Test]
        public void NoSeamEverOpens()
        {
            Mesh mesh = Shirt().GarmentMesh;
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            var first = new System.Collections.Generic.Dictionary<Vector3, int>();
            int doubled = 0;
            for (int v = 0; v < vertices.Length; v++)
            {
                if (!first.TryGetValue(vertices[v], out int other))
                {
                    first[vertices[v]] = v;
                    continue;
                }
                doubled++;
                Assert.IsTrue(Alike(weights[v], weights[other]), $"the shirt's seam at {vertices[v]:F3} (bind pose) opens: its two sides are skinned apart");
            }
            Assert.Greater(doubled, 100, "the shirt has no seams: the sleeves are not cut loose and the texture's pieces not apart");
        }

        private static bool Alike(BoneWeight a, BoneWeight b)
        {
            var share = new System.Collections.Generic.Dictionary<int, float>();
            foreach ((int bone, float weight) in new[] { (a.boneIndex0, a.weight0), (a.boneIndex1, a.weight1), (a.boneIndex2, a.weight2), (a.boneIndex3, a.weight3) })
            {
                share[bone] = share.TryGetValue(bone, out float s) ? s + weight : weight;
            }
            foreach ((int bone, float weight) in new[] { (b.boneIndex0, b.weight0), (b.boneIndex1, b.weight1), (b.boneIndex2, b.weight2), (b.boneIndex3, b.weight3) })
            {
                share[bone] = (share.TryGetValue(bone, out float s) ? s : 0.0f) - weight;
            }
            foreach (float difference in share.Values)
            {
                if (Mathf.Abs(difference) > 0.002f)
                {
                    return false;
                }
            }
            return true;
        }

        private static int Bone(HumanBodyBones bone)
        {
            return System.Array.IndexOf(SkinnedGarment.Bones, bone);
        }

        private static int HelperIndex(string name)
        {
            return SkinnedGarment.Bones.Length + System.Array.FindIndex(GarmentHelperBones.Helpers, h => h.name == name);
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
