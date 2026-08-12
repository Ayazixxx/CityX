using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using CityX.Character;
using CityX.CharacterEditor;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Reading characters painted in the Godot build.
    ///
    /// The parsing runs against a written-out fixture so it works on any
    /// machine; the one test that needs a real file finds it if it is there and
    /// says so if it is not, rather than failing on a machine that never had
    /// the Godot build installed.
    /// </summary>
    public class GodotCharacterImportTests
    {
        /// <summary>
        /// A ConfigFile shaped exactly like the real one: a quoted name, a
        /// float playtime with a dot, and a PackedByteArray holding a real PNG.
        /// </summary>
        private static string Fixture(byte[] png)
        {
            var bytes = new System.Text.StringBuilder();
            for (int i = 0; i < png.Length; i++)
            {
                bytes.Append(i == 0 ? "" : ", ").Append(png[i]);
            }

            return "[character_0]\n\n"
                   + "name=\"dude guy\"\n"
                   + "head_shape=\"sphere\"\n"
                   + "hair_style=\"embermane\"\n"
                   + "level=7\n"
                   + $"atlas_png=PackedByteArray({bytes})\n"
                   + "playtime_seconds=1023.3833333333208\n\n"
                   + "[character_1]\n\n"
                   + "name=\"Ayazi\"\n"
                   + "playtime_seconds=40.5\n";
        }

        private static byte[] PaintedAtlasPng(out Color32 corner)
        {
            var image = new Texture2D(PaintAtlas.Width, PaintAtlas.Height,
                TextureFormat.RGBA32, false);
            var pixels = new Color32[PaintAtlas.Width * PaintAtlas.Height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }
            // One deliberate mark, in the head's row, to follow through.
            pixels[0] = new Color32(200, 40, 60, 255);
            corner = pixels[0];

            image.SetPixels32(pixels);
            image.Apply();
            byte[] png = ImageConversion.EncodeToPNG(image);
            Object.DestroyImmediate(image);
            return png;
        }

        [Test]
        public void ACharacterComesAcrossWithItsNamePlaytimeAndPaint()
        {
            byte[] png = PaintedAtlasPng(out Color32 corner);

            List<CharacterSave> imported =
                GodotCharacterImport.Import(Fixture(png), out List<string> notes);

            Assert.AreEqual(2, imported.Count, "both characters should come across");
            Assert.IsEmpty(notes.FindAll(n => n.Contains("dude guy")),
                "the painted character reported a problem: " + string.Join("; ", notes));

            CharacterSave first = imported[0];
            Assert.AreEqual("dude guy", first.Name);
            Assert.AreEqual(7, first.Level);
            Assert.AreEqual("embermane", first.HairStyle);
            Assert.AreEqual(1023.383, first.PlaytimeSeconds, 0.01);
            Assert.IsTrue(first.HasPaint, "the painted atlas did not come across");

            // And it is THAT paint, not merely some paint.
            var paint = new BodyPaint(Color.white);
            Assert.IsTrue(paint.Decode(first.GetPaint()), "the imported paint will not load back");
            Color32 landed = paint.Atlas.GetPixels32()[0];
            Assert.AreEqual(corner.r, landed.r);
            Assert.AreEqual(corner.g, landed.g);
            Assert.AreEqual(corner.b, landed.b);
        }

        /// <summary>
        /// A character with no atlas_png at all is still worth having -- name,
        /// level and playtime are most of what a save is.
        /// </summary>
        [Test]
        public void ACharacterWithNoPaintStillComesAcross()
        {
            List<CharacterSave> imported =
                GodotCharacterImport.Import(Fixture(new byte[0]), out List<string> notes);

            CharacterSave second = imported[1];
            Assert.AreEqual("Ayazi", second.Name);
            Assert.AreEqual(1, second.Level, "an absent level should default rather than be zero");
            Assert.AreEqual("none", second.HairStyle);
            Assert.IsFalse(second.HasPaint);
            Assert.IsNotEmpty(notes, "importing a character with no atlas said nothing about it");
        }

        /// <summary>
        /// AN ATLAS OF THE WRONG SIZE IS REFUSED, not stretched. The rows are
        /// body parts: scaling a different-shaped atlas onto them would put an
        /// arm's paint on a leg, and a plain body beats a scrambled one.
        /// </summary>
        [Test]
        public void AnAtlasOfTheWrongSizeIsRefusedRatherThanStretched()
        {
            var odd = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            odd.SetPixels32(new Color32[32 * 32]);
            odd.Apply();
            byte[] png = ImageConversion.EncodeToPNG(odd);
            Object.DestroyImmediate(odd);

            List<CharacterSave> imported =
                GodotCharacterImport.Import(Fixture(png), out List<string> notes);

            Assert.IsFalse(imported[0].HasPaint, "a 32x32 atlas was accepted onto a 384x448 body");
            Assert.IsNotEmpty(notes.FindAll(n => n.Contains("32x32")),
                "refusing the atlas was not reported");
        }

        /// <summary>
        /// Godot writes 1023.383333 with a dot whatever the machine's locale
        /// is. On a comma-decimal locale a careless parse reads that as a
        /// thousands separator and imports about thirty years of playtime.
        /// </summary>
        [Test]
        public void PlaytimeParsesTheSameInACommaDecimalLocale()
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                List<CharacterSave> imported =
                    GodotCharacterImport.Import(Fixture(new byte[0]), out List<string> _);
                Assert.AreEqual(1023.383, imported[0].PlaytimeSeconds, 0.01,
                    "a comma-decimal locale misread the playtime");
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }

        [Test]
        public void APackedByteArrayReadsBackAsItsOwnBytes()
        {
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71 },
                GodotCharacterImport.Bytes("PackedByteArray(137, 80, 78, 71)"));
            CollectionAssert.IsEmpty(GodotCharacterImport.Bytes("PackedByteArray()"));
            Assert.IsNull(GodotCharacterImport.Bytes("not an array"));
            Assert.IsNull(GodotCharacterImport.Bytes(null));
        }

        /// <summary>
        /// The real file, if this machine has one. Reported rather than
        /// asserted when it does not -- the Godot build is not a checked-in
        /// dependency, and a machine without it is not a broken machine.
        /// </summary>
        [Test]
        public void TheRealGodotSaveOnThisMachineReadsIfItIsHere()
        {
            string path = GodotCharacterImport.Find();
            if (path == null)
            {
                Assert.Ignore("no Godot characters.cfg on this machine");
                return;
            }

            List<CharacterSave> imported =
                GodotCharacterImport.Import(File.ReadAllText(path), out List<string> notes);

            Assert.IsNotEmpty(imported, $"{path} parsed to no characters at all");
            foreach (CharacterSave character in imported)
            {
                Assert.IsNotEmpty(character.Name, "a character came across with no name");
                Assert.GreaterOrEqual(character.PlaytimeSeconds, 0.0);
            }

            int painted = imported.FindAll(c => c.HasPaint).Count;
            Debug.Log($"CityX: {path} holds {imported.Count} character(s), {painted} painted. "
                      + string.Join(" | ", notes));
            Assert.Greater(painted, 0, "not one character's paint came across");
        }
    }
}
