using System.IO;
using CityX.Wearables;
using CityX.WearablesEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The T-Shirt lying folded on the ground (owner, 2026-10-20: "use this as the model for the t shirt when it is out as an object ... I want
    /// whatever design or texture we put on the worn model to appear on the tee object as well, and in as similar a position as possible"):
    /// a light mesh in the worn shirt's own texture layout, so one texture dresses the shirt folded and worn.
    /// </summary>
    public sealed class FoldedTShirtTests
    {
        private const string FoldedMeshPath = "Assets/CityX/Wearables/TShirtFoldedMesh.asset";

        /// <summary>A small thing on the ground: as light as a helmet.</summary>
        private const int TriangleBudget = 5000;

        /// <summary>Of the folded top's points round the logo's place, at least this share show its ink.</summary>
        private const float InkedShare = 0.3f;

        [Test]
        public void TheFoldedShirtIsLightAndWearsTheWornShirtsTextureLayout()
        {
            Mesh folded = Folded();
            Assert.LessOrEqual(folded.triangles.Length / 3, TriangleBudget);
            Vector2[] uvs = folded.uv;
            Assert.AreEqual(folded.vertexCount, uvs.Length, "the folded shirt has no texture coordinates");
            foreach (Vector2 uv in uvs)
            {
                Assert.IsTrue(uv.x >= 0.0f && uv.x <= 1.0f && uv.y >= 0.0f && uv.y <= 1.0f, $"a point of the folded shirt is off its texture, at {uv}");
            }
            Assert.AreEqual(0.0f, folded.bounds.min.y, 0.005f, "the folded shirt does not lie on the ground");
        }

        [Test]
        public void EachShirtLiesFoldedOnTheGroundInItsOwnMaterial()
        {
            foreach (string path in new[] { TShirtBuilder.PrefabPath, TShirtBuilder.BloodRavenPrefabPath })
            {
                var shirt = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<TShirt>();
                Assert.AreSame(Folded(), shirt.GroundPreview.GetComponent<MeshFilter>().sharedMesh, $"{path} does not lie folded");
                Assert.AreSame(shirt.GarmentMaterial, shirt.GroundPreview.GetComponent<MeshRenderer>().sharedMaterial,
                    $"{path} folded does not wear its own texture");
                Assert.AreEqual(Vector3.one, shirt.GroundPreview.transform.localScale);
                Assert.AreEqual(Quaternion.identity, shirt.GroundPreview.transform.localRotation);
            }
        }

        [Test]
        public void TheLogoPrintedOnTheWornFrontShowsOnTheFoldedTopAndNotUnderneath()
        {
            Mesh folded = Folded();
            Vector3[] vertices = folded.vertices;
            Vector3[] normals = folded.normals;
            Vector2[] uvs = folded.uv;
            var print = AssetDatabase.LoadAssetAtPath<ShirtPrint>(TShirtBuilder.BloodRavenPrintPath);
            Assert.AreEqual(ShirtPrint.ShirtSide.Front, print.Side, "the test reads the logo on the front");
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                ShirtPrintBaker.TexturePathFor(print))));
            try
            {
                Color cloth = texture.GetPixelBilinear(0.75f, 0.2f);   // the middle of a sleeve: never printed
                float middleZ = 0.5f * (folded.bounds.min.z + folded.bounds.max.z);
                int topSeen = 0, topInked = 0, underSeen = 0, underInked = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    // Round the folded top's middle, a little toward the collar: where the logo sits on the chest.
                    if (Mathf.Abs(vertices[i].x) > 0.08f || Mathf.Abs(vertices[i].z - middleZ - 0.05f) > 0.06f || Mathf.Abs(normals[i].y) < 0.7f)
                    {
                        continue;
                    }
                    Color texel = texture.GetPixelBilinear(uvs[i].x, uvs[i].y);
                    bool inked = Mathf.Abs(texel.r - cloth.r) + Mathf.Abs(texel.g - cloth.g) + Mathf.Abs(texel.b - cloth.b) > 0.25f;
                    if (normals[i].y > 0.0f)
                    {
                        topSeen++;
                        topInked += inked ? 1 : 0;
                    }
                    else
                    {
                        underSeen++;
                        underInked += inked ? 1 : 0;
                    }
                }
                Assert.Greater(topSeen, 0);
                Assert.Greater(underSeen, 0);
                Assert.Greater((float)topInked / topSeen, InkedShare, $"only {topInked} of {topSeen} points of the folded top show the logo");
                Assert.AreEqual(0, underInked, "the front's logo shows under the folded shirt, on its back");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void TheFoldedNecklineIsTheEndOfTheDipItsOpeningMakesBelowTheCollar()
        {
            // The middle line of a folded top, 0.4 m long: the collar's back up to z 0.4, the opening dipped from 0.36 to 0.30, then the front.
            var vertices = new System.Collections.Generic.List<Vector3>();
            for (float z = 0.0f; z <= 0.4f; z += 0.0025f)
            {
                bool opening = z > 0.30f && z < 0.36f;
                vertices.Add(new Vector3(0.0f, opening ? 0.01f : 0.03f, z));
            }
            vertices.Add(new Vector3(0.0f, 0.01f, 0.12f));   // a wrinkle low in the front: not the opening
            var up = new Vector3[vertices.Count];
            for (int i = 0; i < up.Length; i++)
            {
                up[i] = Vector3.up;
            }
            Assert.AreEqual(0.30f, FoldedShirtLayout.NecklineZ(vertices.ToArray(), up, 0.4f), 0.006f);
        }

        [Test]
        public void TheFoldedTopIsTheWornFrontAndItsUndersideTheWornBackSeenFromBehind()
        {
            var vertices = new[] { new Vector3(-0.2f, 0.03f, 0.0f), new Vector3(0.2f, 0.03f, 0.0f), new Vector3(0.2f, 0.0f, 0.0f), new Vector3(0.0f, 0.03f, 0.4f) };
            var normals = new[] { Vector3.up, Vector3.up, Vector3.down, Vector3.up };
            var front = new FoldedShirtLayout.WornSide { AnchorUv = new Vector2(0.25f, 0.9f), UvPerMetre = 0.5f, ChestWidth = 0.4f };
            var back = new FoldedShirtLayout.WornSide { AnchorUv = new Vector2(0.75f, 0.95f), UvPerMetre = 0.5f, ChestWidth = 0.4f };
            Vector2[] uvs = FoldedShirtLayout.Uvs(vertices, normals, front, back);
            Assert.Less(uvs[0].x, uvs[1].x, "on the top, +x is to the right of the worn front");
            Assert.Less(uvs[2].x, back.AnchorUv.x, "underneath, +x is to the left of the worn back (seen from behind)");
            Assert.AreEqual(0.25f, 0.5f * (uvs[0].x + uvs[1].x), 1e-5f, "the folded top's middle is the worn front's middle");
            Assert.Less(uvs[0].y, front.AnchorUv.y, "the folded top below its neckline is lower on the worn front");
        }

        private static Mesh Folded()
        {
            var folded = AssetDatabase.LoadAssetAtPath<Mesh>(FoldedMeshPath);
            Assert.IsNotNull(folded, $"no {FoldedMeshPath}: rebuild the scene (CityMassingSceneBuilder)");
            return folded;
        }
    }
}
