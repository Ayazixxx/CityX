using System.Collections.Generic;
using System.IO;
using CityX.Items;
using CityX.Wearables;
using CityX.WearablesEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Red Striped T-Shirt (owner, 2026-11-20: "make a red/white striped t shirt like this. use our tshirt model and just make the texture"):
    /// its own item, the T-Shirt's mesh and fit, and its texture in level red and white bands by height on the body, the same on the front and the
    /// back, round the sleeves too, with a white crew neck.
    /// </summary>
    public sealed class RedStripedTShirtTests
    {
        /// <summary>How far a texel's colour may be from its band's, each channel (the texture is compressed).</summary>
        private const int ColourSlack = 24;

        /// <summary>Cloth this near a band's edge (or the crew neck's) is not checked: a texel there may be either colour.</summary>
        private const float BoundarySlack = 0.01f;

        [Test]
        public void TheRedStripedTShirtIsATop()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.RedStripedTShirt, out ItemDefinition shirt));
            Assert.AreEqual("Red Striped T-Shirt", shirt.DisplayName);
            Assert.AreEqual(EquipSlots.Torso, shirt.EquipSlot);
        }

        [Test]
        public void ItIsTheTShirtWithItsOwnItemAndItsStripedTexture()
        {
            var plain = AssetDatabase.LoadAssetAtPath<GameObject>(TShirtBuilder.PrefabPath).GetComponent<TShirt>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TShirtBuilder.RedStripedPrefabPath);
            Assert.IsNotNull(prefab, "no Red Striped T-Shirt: rebuild the scene (CityMassingSceneBuilder)");
            var shirt = prefab.GetComponent<TShirt>();
            Assert.AreEqual(ItemRegistry.RedStripedTShirt, shirt.ItemId);
            Assert.AreSame(plain.GarmentMesh, shirt.GarmentMesh, "the striped shirt is not the T-Shirt's own mesh");
            CollectionAssert.AreEqual(plain.HiddenBodyTriangles, shirt.HiddenBodyTriangles, "the striped shirt hides the body differently");
            Assert.AreEqual(TShirtBuilder.RedStripedTexturePath, AssetDatabase.GetAssetPath(shirt.GarmentMaterial.GetTexture("_BaseMap")),
                "the shirt does not wear its striped texture");
        }

        [Test]
        public void TheBandsAlternateRedAndWhiteUpFromTheHem()
        {
            ShirtStripes stripes = TShirtBuilder.RedStripes(Mesh());
            Assert.AreEqual(Hem(Mesh()), stripes.Hem, "the bands do not start at the hem");
            Assert.AreEqual(stripes.First, stripes.BandAt(stripes.Hem + 0.5f * stripes.BandHeight), "the hem's band is not red");
            Assert.AreEqual(stripes.Second, stripes.BandAt(stripes.Hem + 1.5f * stripes.BandHeight), "the band over it is not white");
            Assert.AreEqual(stripes.First, stripes.BandAt(stripes.Hem + 2.5f * stripes.BandHeight), "the third is not red again");
            Assert.Greater(stripes.First.r, 150);
            Assert.Less(stripes.First.g, 60, "the first colour is not red");
            Assert.Greater(Mathf.Min(stripes.Second.r, Mathf.Min(stripes.Second.g, stripes.Second.b)), 220, "the second colour is not white");
        }

        [Test]
        public void EveryPointOfTheClothIsTheColourOfTheBandAtItsHeight()
        {
            Mesh mesh = Mesh();
            ShirtStripes stripes = TShirtBuilder.RedStripes(mesh);
            Texture2D texture = Texture();
            try
            {
                Vector3[] vertices = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                List<Vector3> neck = ShirtPrintBaker.NeckRim(mesh);
                var bands = new HashSet<int>();
                int checkedPoints = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    float y = vertices[i].y;
                    float intoBand = Mathf.Repeat(y - stripes.Hem, stripes.BandHeight);
                    bool nearTheNeck = neck.Exists(p => Vector3.Distance(p, vertices[i]) < stripes.CollarReach + BoundarySlack);
                    if (intoBand < BoundarySlack || intoBand > stripes.BandHeight - BoundarySlack || nearTheNeck)
                    {
                        continue;
                    }
                    AssertNear(stripes.BandAt(y), texture.GetPixelBilinear(uvs[i].x, uvs[i].y), $"the cloth at {vertices[i]}");
                    bands.Add(Mathf.FloorToInt((y - stripes.Hem) / stripes.BandHeight));
                    checkedPoints++;
                }
                Assert.GreaterOrEqual(bands.Count, 10, "too few bands up the shirt");
                Assert.Greater(checkedPoints, vertices.Length / 2, "too few of the cloth's points checked");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void EachSleeveHasBothColoursAndTheCrewNeckIsWhite()
        {
            Mesh mesh = Mesh();
            ShirtStripes stripes = TShirtBuilder.RedStripes(mesh);
            Texture2D texture = Texture();
            try
            {
                Vector3[] vertices = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                foreach (float side in new[] { -1.0f, 1.0f })
                {
                    var seen = new HashSet<bool>();
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        if (uvs[i].y < 0.35f && Mathf.Sign(vertices[i].x) == side && Mathf.Abs(vertices[i].x) > 0.35f)
                        {
                            seen.Add(texture.GetPixelBilinear(uvs[i].x, uvs[i].y).g < 0.4f);
                        }
                    }
                    Assert.AreEqual(2, seen.Count, $"the sleeve on the {(side > 0 ? "left" : "right")} is not striped red and white");
                }
                foreach (Vector3 p in ShirtPrintBaker.NeckRim(mesh))
                {
                    int i = System.Array.FindIndex(vertices, v => (v - p).sqrMagnitude < 1e-8f);
                    AssertNear(stripes.Collar, texture.GetPixelBilinear(uvs[i].x, uvs[i].y), $"the crew neck at {p}");
                }
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static Mesh Mesh() => AssetDatabase.LoadAssetAtPath<GameObject>(TShirtBuilder.PrefabPath).GetComponent<TShirt>().GarmentMesh;

        private static float Hem(Mesh mesh)
        {
            float hem = float.MaxValue;
            foreach (Vector3 p in mesh.vertices)
            {
                hem = Mathf.Min(hem, p.y);
            }
            return hem;
        }

        private static Texture2D Texture()
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, TShirtBuilder.RedStripedTexturePath)));
            return texture;
        }

        private static void AssertNear(Color32 want, Color got, string where)
        {
            Color32 texel = got;
            int off = Mathf.Max(Mathf.Abs(texel.r - want.r), Mathf.Max(Mathf.Abs(texel.g - want.g), Mathf.Abs(texel.b - want.b)));
            Assert.LessOrEqual(off, ColourSlack, $"{where} is {texel}, not {want}");
        }
    }
}
