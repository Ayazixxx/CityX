using System.Collections.Generic;
using CityX.Character;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A garment's high collar read off its own mesh (GarmentCollar; owner, 2026-11-15: "make it so the kick hair doesnt clip through the midnight coat ... the
    /// back spikes are clipping through"): the rim the hair is tucked under is on the line up the middle of the collar, at the height of its top behind the neck.
    /// </summary>
    public sealed class GarmentCollarTests
    {
        private static BoneWeight On(int bone) => new BoneWeight { boneIndex0 = bone, weight0 = 1.0f };

        [Test]
        public void TheRimIsUpTheCollarsMiddleAtTheHeightOfItsTopBehindTheNeck()
        {
            int collar = GarmentCollar.CollarBone;
            var vertices = new List<Vector3>();
            var weights = new List<BoneWeight>();
            // A ring round x 0.1, z -0.05, its back (z below the middle) up to 2.1 and its open front lower, 1.9.
            for (int i = 0; i < 36; i++)
            {
                float a = i * 10.0f * Mathf.Deg2Rad;
                float z = Mathf.Cos(a);
                vertices.Add(new Vector3(0.1f + 0.3f * Mathf.Sin(a), z < 0.0f ? 2.1f : 1.9f, -0.05f + 0.3f * z));
                weights.Add(On(collar));
            }
            // Something higher that is not the collar's (the shoulders' bone), and nothing of the collar's far off.
            vertices.Add(new Vector3(0.1f, 2.5f, -0.3f));
            weights.Add(On(1));
            Assert.IsTrue(GarmentCollar.TryMeasure(vertices, weights, collar, out Vector3 rim));
            Assert.AreEqual(0.1f, rim.x, 1e-4f, "the rim is off the collar's middle");
            Assert.AreEqual(-0.05f, rim.z, 1e-4f, "the rim is off the collar's middle");
            Assert.AreEqual(2.1f, rim.y, 1e-4f, "the rim is not at the collar's top behind the neck");

            Assert.IsFalse(GarmentCollar.TryMeasure(vertices, weights, collar + 1, out _), "a garment with nothing on the bone has a collar");

            // Read straight behind the head only: a spike beside it, higher, is left out.
            vertices.Add(new Vector3(0.35f, 2.3f, -0.3f));
            weights.Add(On(collar));
            Assert.IsTrue(GarmentCollar.TryMeasure(vertices, weights, collar, out Vector3 behind, 0.08f));
            Assert.AreEqual(2.1f, behind.y, 1e-4f, "the rim read behind the head took the spike beside it");
        }

        [Test]
        public void TheMidnightCoatsRimIsRoundTheLowerHead()
        {
            var coat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/MidnightCoat.prefab").GetComponent<SkinnedGarment>();
            Assert.IsTrue(coat.GarmentMesh.isReadable, "the coat's mesh cannot be read for its collar");
            Assert.IsTrue(GarmentCollar.TryMeasure(coat.GarmentMesh, out Vector3 rim), "the coat has no collar");
            Vector3 head = BodyRig.ShapePositionOf(HumanBodyBones.Head), neck = BodyRig.ShapePositionOf(HumanBodyBones.Neck);
            Assert.Less(Mathf.Abs(rim.x - head.x), 0.03f, "the rim is not up the middle of the neck");
            Assert.Greater(rim.y, neck.y, "the coat's collar does not come up past the neck");
            Assert.Less(rim.y, head.y, "the coat's collar rises past the middle of the head");
        }

        /// <summary>
        /// The parka's rim is the fur's top straight behind the head, under the fur's tips beside it (owner, 2026-11-21: "there are small bits of the scalp
        /// visible on the back of the head when wearing parka"): its hair is drawn in only where the fur covers it.
        /// </summary>
        [TestCase("GreenFurHoodedParka")]
        [TestCase("MaroonFurHoodedParka")]
        public void TheParkasRimIsTheFursTopStraightBehindTheHead(string prefab)
        {
            var parka = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{prefab}.prefab").GetComponent<SkinnedGarment>();
            Assert.IsTrue(GarmentCollar.TryMeasure(parka.GarmentMesh, out Vector3 tips), "the parka has no collar");
            Assert.IsTrue(GarmentCollar.TryMeasure(parka.GarmentMesh, out Vector3 behind, BehindTheHead));
            Assert.Less(behind.y, tips.y - 0.02f, "the rim behind the head is not under the fur's tips");
            Vector3 head = BodyRig.ShapePositionOf(HumanBodyBones.Head), neck = BodyRig.ShapePositionOf(HumanBodyBones.Neck);
            Assert.Greater(behind.y, neck.y + 0.15f, "the rim behind the head is down at the neck: the hair would be drawn in no more");
            Assert.Less(behind.y, head.y, "the rim behind the head rises past the middle of the head");
        }

        /// <summary>FurHoodedParka's own, private: the same width.</summary>
        private const float BehindTheHead = 0.08f;
    }
}
