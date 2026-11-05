using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>How much of the hair follows the neck (HairBend): the lowest locks all of it, the top of the head none, with no step between.</summary>
    public sealed class HairBendTests
    {
        [Test]
        public void TheTopFollowsTheHeadAndTheLowestLocksTheNeck()
        {
            Assert.AreEqual(0.0f, HairBend.NeckWeight(0.3f), 1e-4f, "the crown");
            Assert.AreEqual(0.0f, HairBend.NeckWeight(0.0f), 1e-4f, "level with the head's centre");
            Assert.AreEqual(1.0f, HairBend.NeckWeight(-0.33f), 1e-4f, "the kick hair's lowest tips");
        }

        [Test]
        public void TheBendHasNoStepInIt()
        {
            float last = 0.0f;
            for (float y = 0.0f; y >= -0.35f; y -= 0.005f)
            {
                float weight = HairBend.NeckWeight(y);
                Assert.GreaterOrEqual(weight, last - 1e-5f, "it goes back down");
                Assert.Less(weight - last, 0.08f, $"a step at {y:0.000}: a crease would show in the hair");
                last = weight;
            }
        }

        [Test]
        public void EveryCornerOfABakedMeshHasWholeWeights()
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.up * 2.0f } };
            HairBend.AddTo(mesh, new CharacterHair.Fit(new Vector3(0.0f, -1.8f, 0.0f), Vector3.zero, Vector3.one));
            Assert.AreEqual(HairBend.BoneCount, mesh.bindposes.Length);
            foreach (BoneWeight weight in mesh.boneWeights)
            {
                Assert.AreEqual(1.0f, weight.weight0 + weight.weight1, 1e-4f);
                Assert.GreaterOrEqual(weight.weight0, weight.weight1, "the heavier bone must come first");
            }
            Object.DestroyImmediate(mesh);
        }
    }
}
