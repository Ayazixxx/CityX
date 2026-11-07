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
    /// The cityXshirt (owner, 2026-11-07: "put this on a black shirt as an item called "cityXshirt"", then "make it adjustable so i can place
    /// it how i want"): its own item, the T-Shirt's mesh and fit in black cotton, the logo printed where its print (CityXPrint) says, and the
    /// print the one the Inspector places.
    /// </summary>
    public sealed class CityXShirtTests
    {
        /// <summary>
        /// Of the texels round the logo's middle, at least this share differ from the cloth: its letters are black like the shirt, so only
        /// their red outlines, the pink strokes and the white lettering show.
        /// </summary>
        private const float InkedShare = 0.08f;

        [Test]
        public void TheCityXShirtIsATopCalledWhatTheOwnerCalledIt()
        {
            Assert.IsTrue(ItemRegistry.TryGet(ItemRegistry.CityXShirt, out ItemDefinition shirt));
            Assert.AreEqual("cityXshirt", shirt.DisplayName);
            Assert.AreEqual(EquipSlots.Torso, shirt.EquipSlot);
        }

        [Test]
        public void ItIsTheTShirtPrintedOnBlackCottonWithItsOwnItem()
        {
            var plain = AssetDatabase.LoadAssetAtPath<GameObject>(TShirtBuilder.PrefabPath).GetComponent<TShirt>();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TShirtBuilder.CityXPrefabPath);
            Assert.IsNotNull(prefab, "no cityXshirt: rebuild the scene (CityMassingSceneBuilder)");
            var shirt = prefab.GetComponent<TShirt>();
            Assert.AreEqual(ItemRegistry.CityXShirt, shirt.ItemId);
            Assert.AreSame(plain.GarmentMesh, shirt.GarmentMesh, "the printed shirt is not the T-Shirt's own mesh");
            CollectionAssert.AreEqual(plain.HiddenBodyTriangles, shirt.HiddenBodyTriangles, "the printed shirt hides the body differently");
            ShirtPrint print = Print();
            Assert.AreEqual(ShirtPrint.BlackCotton, print.Cotton, "the logo is not printed on black cotton");
            Assert.AreEqual(ShirtPrintBaker.TexturePathFor(print), AssetDatabase.GetAssetPath(shirt.GarmentMaterial.GetTexture("_BaseMap")),
                "the shirt does not wear its printed texture");
        }

        [Test]
        public void TheLogoIsPrintedWhereItsPrintSaysOnBlackCloth()
        {
            ShirtPrint print = Print();
            Assert.IsNotNull(print.Logo, "the print has no picture");
            Mesh mesh = AssetDatabase.LoadAssetAtPath<GameObject>(TShirtBuilder.PrefabPath).GetComponent<TShirt>().GarmentMesh;
            bool front = print.Side == ShirtPrint.ShirtSide.Front;
            ShirtPrintBaker.FitSide(mesh, front, out Vector2 middle, out float uvPerMetre);
            ShirtPrintBaker.FitSide(mesh, !front, out Vector2 otherMiddle, out _);
            Vector2 centre = ShirtPrintLayout.CentreUv(middle, uvPerMetre, print.RightCm / 100.0f, print.UpCm / 100.0f);
            var printed = new Texture2D(2, 2);
            printed.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ShirtPrintBaker.TexturePathFor(print))));
            try
            {
                Color32 cloth = printed.GetPixelBilinear(otherMiddle.x, otherMiddle.y);
                Assert.Less(cloth.r + cloth.g + cloth.b, 60, $"the other side's middle is {cloth}, not black cloth");
                float halfWidth = 0.25f * print.WidthCm / 100.0f * uvPerMetre;
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

        private static ShirtPrint Print()
        {
            var print = AssetDatabase.LoadAssetAtPath<ShirtPrint>(TShirtBuilder.CityXPrintPath);
            Assert.IsNotNull(print, "no cityXshirt print: rebuild the scene");
            return print;
        }
    }
}
