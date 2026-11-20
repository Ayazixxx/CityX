using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The Ice Shard's baked model (Weapons/Editor/IceShardBuilder) is drawn from outside.</summary>
    public sealed class IceShardModelTests
    {
        /// <summary>Of its surface, at most this share may look into it.</summary>
        private const float InwardAtMost = 0.1f;

        /// <summary>
        /// Its faces look out of it (owner, 2026-11-20, of the shards and the Iron Spiked Giant Boomerang: "its like transparent on one side but i can see
        /// the texture on the other side"): only a face's front is drawn, so with its faces looking in, its near side was not drawn and its far side was
        /// seen from inside. A shard is nearly convex, so a face looks out when it faces away from the middle.
        /// </summary>
        [Test]
        public void ItsFacesLookOutOfItSoItIsNotSeenThrough()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Weapons/IceShardMesh.asset");
            Assert.IsNotNull(mesh, "no IceShardMesh.asset -- rebuild the ice shard");
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Vector3 middle = mesh.bounds.center;
            float outward = 0.0f, inward = 0.0f;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a);
                float area = face.magnitude;
                if (area <= 0.0f) continue;
                if (Vector3.Dot(face, (a + b + c) / 3.0f - middle) > 0.0f) outward += area;
                else inward += area;
            }
            Assert.LessOrEqual(inward / (inward + outward), InwardAtMost, $"{inward / (inward + outward):P0} of its surface looks into it: it is inside out");
        }
    }
}
