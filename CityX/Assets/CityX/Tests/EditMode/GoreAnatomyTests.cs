using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// What a burst throws from inside a body (GoreAnatomy, docs/DISMEMBERMENT.md): every bit is closed all round with a tissue for each of its
    /// submeshes, is about its size, and each part throws what was in it (a head its eyes and jaw, a trunk its ribs and heart, a limb its own
    /// bone).
    /// </summary>
    public sealed class GoreAnatomyTests
    {
        [Test]
        public void EveryBitIsClosedWithATissueForEachSubmesh()
        {
            foreach (GoreAnatomy.Bit bit in System.Enum.GetValues(typeof(GoreAnatomy.Bit)))
            {
                for (int variant = 0; variant < GoreAnatomy.Variants; variant++)
                {
                    Mesh mesh = GoreAnatomy.MeshOf(bit, variant);
                    Assert.Greater(mesh.vertexCount, 0, $"{bit} is empty");
                    Assert.AreEqual(GoreAnatomy.TissuesOf(bit).Length, mesh.subMeshCount, $"{bit}: a submesh with no tissue, or a tissue with no submesh");
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        Assert.Greater(mesh.GetTriangles(s).Length, 0, $"{bit}: its {GoreAnatomy.TissuesOf(bit)[s]} is missing");
                    }
                    Assert.AreEqual(0, GoreShatterTests.OpenEdges(new[] { mesh }), $"{bit} (shape {variant}) has a hole in it");
                    Assert.Less(mesh.bounds.center.magnitude, 0.01f, $"{bit} does not turn about its middle");
                }
            }
        }

        [Test]
        public void EveryBitsCornersAreNumbersAndItsNormalsOneLong()
        {
            foreach (GoreAnatomy.Bit bit in System.Enum.GetValues(typeof(GoreAnatomy.Bit)))
            {
                for (int variant = 0; variant < GoreAnatomy.Variants; variant++)
                {
                    Assert.AreEqual(0, GoreShatterTests.BadCorners(GoreAnatomy.MeshOf(bit, variant)), $"{bit} (shape {variant}) has a broken corner: it would flash");
                }
            }
        }

        [Test]
        public void TheBitsAreAboutTheirSize()
        {
            Assert.AreEqual(GoreAnatomy.FemurLength, GoreAnatomy.MeshOf(GoreAnatomy.Bit.Femur).bounds.size.y, 0.03f, "the thigh bone");
            Assert.AreEqual(GoreAnatomy.ForearmLength, GoreAnatomy.MeshOf(GoreAnatomy.Bit.Forearm).bounds.size.y, 0.02f, "the forearm bone");
            Assert.AreEqual(2.0f * GoreAnatomy.EyeRadius, GoreAnatomy.MeshOf(GoreAnatomy.Bit.Eye).bounds.size.x, 0.005f, "the eye");
            Assert.That(GoreAnatomy.MeshOf(GoreAnatomy.Bit.RibSide).bounds.size.y, Is.InRange(0.25f, 0.4f), "the ribs");
            Assert.That(GoreAnatomy.MeshOf(GoreAnatomy.Bit.Heart).bounds.size.y, Is.InRange(0.15f, 0.3f), "the heart");
        }

        [Test]
        public void EachPartThrowsWhatWasInIt()
        {
            List<GoreAnatomy.Bit> head = GoreAnatomy.BitsOf(BodyPart.Head);
            Assert.AreEqual(2, head.FindAll(bit => bit == GoreAnatomy.Bit.Eye).Count, "a head has two eyes");
            CollectionAssert.Contains(head, GoreAnatomy.Bit.Jaw);
            CollectionAssert.Contains(head, GoreAnatomy.Bit.Brain);
            CollectionAssert.DoesNotContain(head, GoreAnatomy.Bit.Heart);
            List<GoreAnatomy.Bit> trunk = GoreAnatomy.BitsOf(BodyPart.Torso);
            foreach (GoreAnatomy.Bit bit in new[] { GoreAnatomy.Bit.RibSide, GoreAnatomy.Bit.Spine, GoreAnatomy.Bit.Heart, GoreAnatomy.Bit.Lung, GoreAnatomy.Bit.Guts })
            {
                CollectionAssert.Contains(trunk, bit, $"a trunk has no {bit}");
            }
            CollectionAssert.DoesNotContain(trunk, GoreAnatomy.Bit.Eye);
            CollectionAssert.Contains(GoreAnatomy.BitsOf(BodyPart.LeftUpperLeg), GoreAnatomy.Bit.Femur);
            CollectionAssert.Contains(GoreAnatomy.BitsOf(BodyPart.RightLowerArm), GoreAnatomy.Bit.Forearm);
            CollectionAssert.Contains(GoreAnatomy.BitsOf(BodyPart.LeftLowerLeg), GoreAnatomy.Bit.Shin);
            CollectionAssert.Contains(GoreAnatomy.BitsOf(BodyPart.RightUpperArm), GoreAnatomy.Bit.Humerus);
        }

        /// <summary>
        /// Nothing flung out of a burst is a speck: loose teeth, tiny and pale, lay in the dark as points of light (owner, 2026-11-08: "there are
        /// still little dots of shining light coming from some of the dismembered parts").
        /// </summary>
        [Test]
        public void NoBitFlungOutIsASpeck()
        {
            foreach (BodyPart part in System.Enum.GetValues(typeof(BodyPart)))
            {
                foreach (GoreAnatomy.Bit bit in GoreAnatomy.BitsOf(part))
                {
                    Vector3 size = GoreAnatomy.MeshOf(bit).bounds.size;
                    Assert.Greater(Mathf.Max(size.x, Mathf.Max(size.y, size.z)), 0.04f, $"a {part}'s {bit} is a speck");
                }
            }
        }
    }
}
