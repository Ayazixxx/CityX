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
    /// The Blood Raven T-Shirt (owner, 2026-10-20: "put this logo on the shirt and call it "Blood Raven T-Shirt", let me adjust the logo
    /// positioning too in unity"): its own item, the T-Shirt's mesh and fit, the logo printed where its print (BloodRavenPrint) says; and the
    /// printing itself (ShirtPrintLayout).
    /// </summary>
    public sealed class BloodRavenTShirtTests
    {
        private static readonly Color32 Cloth = new Color32(230, 230, 228, 255);
        private static readonly Color32 Red = new Color32(228, 0, 0, 255);
        private static readonly Color32 Clear = new Color32(255, 255, 255, 0);

        /// <summary>Of the texels where the logo is printed, at least this share are ink, not cloth (the picture is mostly red and black).</summary>
        private const float InkedShare = 0.3f;

        [Test]
        public void TheBloodRavenTShirtIsATop()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.BloodRavenTShirt, out ItemDefinition shirt));
            Assert.AreEqual("Blood Raven T-Shirt", shirt.DisplayName);
            Assert.AreEqual(EquipSlots.Torso, shirt.EquipSlot);
        }

        [Test]
        public void ItIsTheTShirtWithItsOwnItemAndPrintedTexture()
        {
            TShirt plain = Prefab(TShirtBuilder.PrefabPath);
            TShirt raven = Prefab(TShirtBuilder.BloodRavenPrefabPath);
            Assert.AreEqual(ItemRegistry.TShirt, plain.ItemId);
            Assert.AreEqual(ItemRegistry.BloodRavenTShirt, raven.ItemId);
            Assert.AreSame(plain.GarmentMesh, raven.GarmentMesh, "the printed shirt is not the T-Shirt's own mesh");
            CollectionAssert.AreEqual(plain.HiddenBodyTriangles, raven.HiddenBodyTriangles, "the printed shirt hides the body differently");
            Assert.AreEqual(ShirtPrintBaker.TexturePathFor(Print()), AssetDatabase.GetAssetPath(raven.GarmentMaterial.GetTexture("_BaseMap")),
                "the printed shirt does not wear its printed texture");
        }

        [Test]
        public void TheLogoIsPrintedWhereItsPrintSaysAndNowhereElse()
        {
            ShirtPrint print = Print();
            Mesh mesh = Prefab(TShirtBuilder.PrefabPath).GarmentMesh;
            bool front = print.Side == ShirtPrint.ShirtSide.Front;
            ShirtPrintBaker.FitSide(mesh, front, out Vector2 middle, out float uvPerMetre);
            ShirtPrintBaker.FitSide(mesh, !front, out Vector2 otherMiddle, out _);
            Vector2 centre = ShirtPrintLayout.CentreUv(middle, uvPerMetre, print.RightCm / 100.0f, print.UpCm / 100.0f);
            Texture2D printed = Decoded(ShirtPrintBaker.TexturePathFor(print));
            try
            {
                Color32 cloth = printed.GetPixelBilinear(otherMiddle.x, otherMiddle.y);
                Assert.Greater(cloth.r, 200, "the other side's middle is not plain cloth");
                float halfWidth = 0.25f * print.WidthCm / 100.0f * uvPerMetre;   // the middle half of the picture, whatever its turn
                int inked = 0, seen = 0;
                for (float du = -halfWidth; du <= halfWidth; du += 1.0f / printed.width)
                {
                    for (float dv = -halfWidth * 0.5f; dv <= halfWidth * 0.5f; dv += 1.0f / printed.height)
                    {
                        Color32 texel = printed.GetPixelBilinear(centre.x + du, centre.y + dv);
                        seen++;
                        inked += Mathf.Abs(texel.r - cloth.r) + Mathf.Abs(texel.g - cloth.g) + Mathf.Abs(texel.b - cloth.b) > 60 ? 1 : 0;
                    }
                }
                Assert.Greater((float)inked / seen, InkedShare, $"only {inked} of {seen} texels round the print's centre {centre} carry the logo");
            }
            finally
            {
                Object.DestroyImmediate(printed);
            }
        }

        [Test]
        public void APictureLandsCentredAtItsPlaceAndLeavesTheRestCloth()
        {
            Color32[] texels = ShirtPrintLayout.Print(new[] { Red, Red, Red, Red }, 2, 2, 100, Cloth, new Vector2(0.3f, 0.6f), 0.1f, 0.0f);
            AssertClose(Red, texels[60 * 100 + 30], "the picture's centre");
            AssertClose(Red, texels[57 * 100 + 26], "inside the picture");
            AssertClose(Cloth, texels[60 * 100 + 40], "right of the picture");
            AssertClose(Cloth, texels[20 * 100 + 30], "far below the picture");
        }

        [Test]
        public void ThePictureKeepsItsShapeTurnsAnticlockwiseAndLetsTheClothThroughItsClearParts()
        {
            // Two texels wide, one high: red on the left, clear on the right.
            var picture = new[] { Red, Clear, Red, Clear };
            Color32[] upright = ShirtPrintLayout.Print(picture, 2, 2, 100, Cloth, new Vector2(0.5f, 0.5f), 0.4f, 0.0f);
            AssertClose(Red, upright[50 * 100 + 35], "the picture's left half");
            AssertClose(Cloth, upright[50 * 100 + 65], "the picture's clear right half");
            Color32[] turned = ShirtPrintLayout.Print(picture, 2, 2, 100, Cloth, new Vector2(0.5f, 0.5f), 0.4f, 90.0f);
            AssertClose(Red, turned[35 * 100 + 50], "turned a quarter anticlockwise, the left half is at the bottom");
            AssertClose(Cloth, turned[65 * 100 + 50], "turned a quarter anticlockwise, the clear half is at the top");
        }

        [Test]
        public void MovingThePrintMovesItAlongTheCloth()
        {
            Vector2 middle = new Vector2(0.25f, 0.7f);
            Assert.AreEqual(middle, ShirtPrintLayout.CentreUv(middle, 0.5f, 0.0f, 0.0f));
            Vector2 moved = ShirtPrintLayout.CentreUv(middle, 0.5f, 0.1f, -0.2f);
            Assert.AreEqual(0.30f, moved.x, 1e-5f, "10 cm right");
            Assert.AreEqual(0.60f, moved.y, 1e-5f, "20 cm down");
        }

        private static TShirt Prefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"no {path}: rebuild the scene (CityMassingSceneBuilder)");
            return prefab.GetComponent<TShirt>();
        }

        private static ShirtPrint Print()
        {
            var print = AssetDatabase.LoadAssetAtPath<ShirtPrint>(TShirtBuilder.BloodRavenPrintPath);
            Assert.IsNotNull(print, "no Blood Raven print: rebuild the scene");
            return print;
        }

        private static Texture2D Decoded(string assetPath)
        {
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath)));
            return texture;
        }

        private static void AssertClose(Color32 expected, Color32 actual, string where)
        {
            Assert.IsTrue(Mathf.Abs(expected.r - actual.r) <= 2 && Mathf.Abs(expected.g - actual.g) <= 2 && Mathf.Abs(expected.b - actual.b) <= 2,
                $"{where}: {actual}, not {expected}");
        }
    }
}
