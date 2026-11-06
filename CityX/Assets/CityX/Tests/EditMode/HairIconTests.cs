using System.Collections.Generic;
using System.IO;
using CityX.Character;
using CityX.UI;
using CityX.UIEditor;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The pictures of the hairstyles on the creator's HAIR tab (HairIcons), baked by CityX/Bake Hair Icons (HairIconBaker): one for every style,
    /// bald included, each its own, and the frame they share holding the whole head and every style's hair.
    /// </summary>
    public sealed class HairIconTests
    {
        /// <summary>Of a picture, at least this share is not its backdrop: the head is in it.</summary>
        private const float SubjectShare = 0.05f;

        /// <summary>Of the bald picture, at least this share visibly changes when a style is worn: the hair shows.</summary>
        private const float HairShare = 0.01f;   // oldmanhair, a fringe round a bald crown, changes 1.9% of its picture; a style missing from its picture changes none

        /// <summary>A pixel has visibly changed when its three channels moved this much between them.</summary>
        private const int VisibleChange = 30;

        [Test]
        public void EveryStyleHasAPictureTheCreatorCanLoad()
        {
            foreach (string style in CharacterHair.ValidStyles)
            {
                string path = HairIconBaker.PathFor(style);
                Assert.IsTrue(File.Exists(OnDisk(path)), $"no picture of \"{style}\" at {path} -- run CityX/Bake Hair Icons");
                Texture2D icon = HairIcons.For(style);
                Assert.IsNotNull(icon, $"the creator cannot load the picture of \"{style}\" from Resources");
                Assert.AreEqual(HairIcons.Size, icon.width, $"the picture of \"{style}\" is imported at another size");
                Assert.AreEqual(1, icon.mipmapCount, $"the picture of \"{style}\" is imported with mipmaps");
            }
        }

        [Test]
        public void EachPictureIsTheIconSizeAndShowsAHead()
        {
            foreach (string style in CharacterHair.ValidStyles)
            {
                Assert.That(ShareUnlikeBackdrop(Pixels(style)), Is.GreaterThan(SubjectShare),
                    $"the picture of \"{style}\" is (nearly) all one colour: there is no head in it");
            }
        }

        [Test]
        public void NoTwoStylesHaveTheSamePicture()
        {
            var pictures = new List<Color32[]>();
            foreach (string style in CharacterHair.ValidStyles)
            {
                pictures.Add(Pixels(style));
            }
            for (int a = 0; a < pictures.Count; a++)
            {
                for (int b = a + 1; b < pictures.Count; b++)
                {
                    Assert.IsFalse(Same(pictures[a], pictures[b]),
                        $"\"{CharacterHair.ValidStyles[a]}\" and \"{CharacterHair.ValidStyles[b]}\" have the same picture: one style was baked twice");
                }
            }
        }

        [Test]
        public void EveryHairstyleShowsOnTheBaldHead()
        {
            Color32[] bald = Pixels(CharacterHair.None);
            foreach (string style in CharacterHair.ValidStyles)
            {
                if (style == CharacterHair.None)
                {
                    continue;
                }
                Color32[] haired = Pixels(style);
                int changed = 0;
                for (int i = 0; i < bald.Length; i++)
                {
                    int moved = Mathf.Abs(bald[i].r - haired[i].r) + Mathf.Abs(bald[i].g - haired[i].g) + Mathf.Abs(bald[i].b - haired[i].b);
                    changed += moved > VisibleChange ? 1 : 0;
                }
                Assert.That((float)changed / bald.Length, Is.GreaterThan(HairShare), $"the picture of \"{style}\" looks like the bald head: no hair in it");
            }
        }

        [Test]
        public void TheSharedFrameHoldsTheWholeBoxCentredAndSeenFromTheFront()
        {
            var box = new Bounds(new Vector3(0.1f, 2.3f, -0.05f), new Vector3(0.9f, 1.1f, 0.95f));
            Pose pose = HairIconBaker.Framing(box, Vector3.forward, HairIconBaker.FieldOfView);
            var camera = new GameObject("FramingCamera").AddComponent<Camera>();
            try
            {
                camera.fieldOfView = HairIconBaker.FieldOfView;
                camera.aspect = 1.0f;
                camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                float furthest = 0.0f;
                for (int corner = 0; corner < 8; corner++)
                {
                    var at = new Vector3(
                        (corner & 1) == 0 ? box.min.x : box.max.x,
                        (corner & 2) == 0 ? box.min.y : box.max.y,
                        (corner & 4) == 0 ? box.min.z : box.max.z);
                    Vector3 seen = camera.WorldToViewportPoint(at);
                    Assert.That(seen.x, Is.InRange(-1e-4f, 1.0001f), $"corner {at} is off the side of the picture");
                    Assert.That(seen.y, Is.InRange(-1e-4f, 1.0001f), $"corner {at} is off the top or bottom of the picture");
                    Assert.Greater(seen.z, camera.nearClipPlane, $"corner {at} is behind the camera");
                    furthest = Mathf.Max(furthest, Mathf.Max(Mathf.Abs(seen.x - 0.5f), Mathf.Abs(seen.y - 0.5f)));
                }
                Assert.AreEqual(0.5f, furthest, 1e-3f, "the box does not reach the picture's edge: the camera stands further back than it needs to");
                Vector3 middle = camera.WorldToViewportPoint(box.center);
                Assert.AreEqual(0.5f, middle.x, 1e-4f, "the box is not centred across");
                Assert.AreEqual(0.5f, middle.y, 1e-4f, "the box is not centred up and down");
                Vector3 toCamera = pose.position - box.center;
                Assert.Greater(Vector3.Dot(toCamera, Vector3.forward), 0.0f, "the camera looks at the back of the head");
                Assert.Greater(toCamera.y, 0.0f, "the camera is not above the head's middle");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        /// <summary>A style's picture decoded from its file's own bytes, checked for size.</summary>
        /// <summary>Every haired style has its hair as a layer of its own, part see-through, so the tab can tint it; the bald head has none.</summary>
        [Test]
        public void EveryHairstyleHasItsHairAsALayerToTint()
        {
            foreach (string style in CharacterHair.ValidStyles)
            {
                if (style == CharacterHair.None)
                {
                    Assert.IsNull(HairIcons.HairOf(style), "the bald head has a hair layer");
                    continue;
                }
                Assert.IsNotNull(HairIcons.HairOf(style), $"\"{style}\" has no hair layer for the creator to tint");
                string path = HairIconBaker.HairPathFor(style);
                var texture = new Texture2D(2, 2);
                try
                {
                    Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(OnDisk(path))), $"{path} is not a picture");
                    int hair = 0, clear = 0;
                    foreach (Color32 pixel in texture.GetPixels32())
                    {
                        hair += pixel.a > 200 ? 1 : 0;
                        clear += pixel.a < 50 ? 1 : 0;
                    }
                    Assert.Greater(hair, 0, $"\"{style}\"'s hair layer has no hair in it");
                    Assert.Greater(clear, texture.width * texture.height / 2, $"\"{style}\"'s hair layer covers more than its hair");
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        [Test]
        public void TheHairIsWhereTheWhiteAndBlackPicturesDiffer()
        {
            Color[] white = { Color.white, new Color(0.3f, 0.3f, 0.3f), new Color(0.12f, 0.13f, 0.16f) };
            Color[] black = { Color.black, Color.black, new Color(0.12f, 0.13f, 0.16f) };
            Color[] layer = HairIconBaker.HairLayer(white, black);
            Assert.AreEqual(1.0f, layer[0].a, "lit hair is not in the layer");
            Assert.AreEqual(1.0f, layer[1].a, "shaded hair is not in the layer");
            Assert.AreEqual(0.0f, layer[2].a, "the backdrop is in the layer");
        }

        private static Color32[] Pixels(string style)
        {
            string path = HairIconBaker.PathFor(style);
            Assert.IsTrue(File.Exists(OnDisk(path)), $"no picture of \"{style}\" at {path} -- run CityX/Bake Hair Icons");
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(OnDisk(path))), $"{path} is not a picture");
                Assert.AreEqual(HairIcons.Size, texture.width, $"the picture of \"{style}\" is not {HairIcons.Size} wide");
                Assert.AreEqual(HairIcons.Size, texture.height, $"the picture of \"{style}\" is not {HairIcons.Size} high");
                return texture.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        /// <summary>The share of a picture that is not its commonest colour (its backdrop).</summary>
        private static float ShareUnlikeBackdrop(Color32[] pixels)
        {
            var counts = new Dictionary<int, int>();
            int most = 0;
            foreach (Color32 p in pixels)
            {
                int key = (p.r << 16) | (p.g << 8) | p.b;
                counts.TryGetValue(key, out int count);
                counts[key] = ++count;
                most = Mathf.Max(most, count);
            }
            return 1.0f - (float)most / pixels.Length;
        }

        private static bool Same(Color32[] a, Color32[] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b)
                {
                    return false;
                }
            }
            return true;
        }

        private static string OnDisk(string assetPath) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
    }
}
