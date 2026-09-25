using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The plain-logic half of SkinnedGarmentCombiner -- no Animator/avatar
    /// involved, matching SkinnedGarmentTests' own split between EditMode
    /// logic tests and PlayMode real-rig tests (see
    /// GrappleSwingAnimatorPlayTests.AHoodedCloakHidesItselfWhileSwingingAnd
    /// ReappearsOnRelease for the real-rig half of this same combiner).
    /// </summary>
    public sealed class SkinnedGarmentCombinerTests
    {
        private static Mesh TwoTriangleMesh(int boneIndex)
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(0.0f, 0.0f, 0.0f),
                new Vector3(1.0f, 0.0f, 0.0f),
                new Vector3(0.0f, 1.0f, 0.0f),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.boneWeights = new[]
            {
                new BoneWeight { boneIndex0 = boneIndex, weight0 = 1.0f },
                new BoneWeight { boneIndex0 = boneIndex, weight0 = 1.0f },
                new BoneWeight { boneIndex0 = boneIndex, weight0 = 1.0f },
            };
            mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity };
            return mesh;
        }

        [Test]
        public void RegisteringTwoItemsProducesOneMeshWithTwoSubmeshesAndBothMaterials()
        {
            var holder = new GameObject("Player");
            try
            {
                var combiner = holder.AddComponent<SkinnedGarmentCombiner>();
                var topMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                var legsMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

                combiner.Register("top", TwoTriangleMesh(0), topMaterial);
                combiner.Register("legs", TwoTriangleMesh(1), legsMaterial);

                Assert.IsNotNull(combiner.CombinedMesh, "no combined mesh was built");
                Assert.AreEqual(2, combiner.CombinedMesh.subMeshCount);
                Assert.AreEqual(6, combiner.CombinedMesh.vertexCount, "two 3-vertex meshes should concatenate to 6");
                CollectionAssert.AreEqual(new[] { topMaterial, legsMaterial }, combiner.CombinedMaterials);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void UnregisteringOneItemCollapsesBackToASingleSubmesh()
        {
            var holder = new GameObject("Player");
            try
            {
                var combiner = holder.AddComponent<SkinnedGarmentCombiner>();
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

                combiner.Register("top", TwoTriangleMesh(0), material);
                combiner.Register("legs", TwoTriangleMesh(1), material);
                combiner.Unregister("legs");

                Assert.AreEqual(1, combiner.CombinedMesh.subMeshCount);
                Assert.AreEqual(3, combiner.CombinedMesh.vertexCount);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void UnregisteringTheLastItemClearsTheRenderer()
        {
            var holder = new GameObject("Player");
            try
            {
                var combiner = holder.AddComponent<SkinnedGarmentCombiner>();
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

                combiner.Register("top", TwoTriangleMesh(0), material);
                combiner.Unregister("top");

                Assert.IsNull(combiner.CombinedMesh);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void SetVisibleFalseExcludesTheItemsSubmeshWithoutUnregisteringIt()
        {
            var holder = new GameObject("Player");
            try
            {
                var combiner = holder.AddComponent<SkinnedGarmentCombiner>();
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

                combiner.Register("top", TwoTriangleMesh(0), material);
                combiner.Register("cloak", TwoTriangleMesh(0), material);
                combiner.SetVisible("cloak", false);

                Assert.IsTrue(combiner.IsShowing("top"));
                Assert.IsFalse(combiner.IsShowing("cloak"), "still worn, just hidden mid-swing");
                Assert.AreEqual(1, combiner.CombinedMesh.subMeshCount,
                    "the hidden item's own submesh should be excluded from the rebuild");

                combiner.SetVisible("cloak", true);
                Assert.AreEqual(2, combiner.CombinedMesh.subMeshCount, "showing it again should restore its submesh");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void RegisteringWithNoAnimatorOnThePlayerDoesNotThrow()
        {
            var holder = new GameObject("PlayerWithNoAnimator");
            try
            {
                var combiner = holder.AddComponent<SkinnedGarmentCombiner>();
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

                Assert.DoesNotThrow(() => combiner.Register("top", TwoTriangleMesh(0), material));
                Assert.IsNotNull(combiner.CombinedMesh, "the mesh should still combine even with no bones to bind to");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void RegisteringWithAnAnimatorThatHasNoAvatarDoesNotThrow()
        {
            var holder = new GameObject("PlayerWithBareAnimator");
            try
            {
                holder.AddComponent<Animator>();
                var combiner = holder.AddComponent<SkinnedGarmentCombiner>();
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));

                // Animator.GetBoneTransform THROWS rather than returning null
                // when the Animator has no avatar at all -- the same gap
                // SkinnedGarment.OnEquip's own doc comment already found.
                Assert.DoesNotThrow(() => combiner.Register("top", TwoTriangleMesh(0), material));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
