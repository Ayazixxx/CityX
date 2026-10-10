using System.Collections.Generic;
using System.IO;
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

        private static byte[] PaintedAtlasPng(out Color32 mark)
        {
            var image = new Texture2D(PaintAtlas.LegacyWidth, PaintAtlas.LegacyHeight,
                TextureFormat.RGBA32, false);
            var pixels = new Color32[PaintAtlas.LegacyWidth * PaintAtlas.LegacyHeight];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            // Texture2D.SetPixels32's own index 0 is the BOTTOM-LEFT of the
            // image (Unity's own convention, confirmed directly with an
            // EncodeToPNG/LoadImage round trip) -- marking the LAST row
            // here is what lands the mark at the visual TOP once this is
            // encoded to a real PNG file, which is where a real Godot-
            // authored atlas puts its head row (PaintAtlas.Parts[0]). See
            // BodyPaint.Adopt's own comment for the full story; this
            // fixture used to mark index 0 directly, which is the BOTTOM
            // of the encoded PNG -- the last atlas row, not the head at
            // all -- and only ever passed because Adopt had no flip yet
            // either, so the two wrongs cancelled out.
            int lastRowStart = (PaintAtlas.LegacyHeight - 1) * PaintAtlas.LegacyWidth;
            mark = new Color32(200, 40, 60, 255);
            pixels[lastRowStart] = mark;

            image.SetPixels32(pixels);
            image.Apply();
            byte[] png = ImageConversion.EncodeToPNG(image);
            Object.DestroyImmediate(image);
            return png;
        }

        [Test]
        public void ACharacterComesAcrossWithItsNameAndPaintButNotItsLevelOrPlaytime()
        {
            byte[] png = PaintedAtlasPng(out Color32 mark);

            List<CharacterSave> imported =
                GodotCharacterImport.Import(Fixture(png), out List<string> notes);

            Assert.AreEqual(2, imported.Count, "both characters should come across");
            Assert.IsEmpty(notes.FindAll(n => n.Contains("dude guy")),
                "the painted character reported a problem: " + string.Join("; ", notes));

            CharacterSave first = imported[0];
            Assert.AreEqual("dude guy", first.Name);
            Assert.AreEqual("embermane", first.HairStyle);
            Assert.IsTrue(first.HasPaint, "the painted atlas did not come across");

            // A real explicit ask: bring across how a character LOOKS, not
            // any of its progress -- even though the fixture says level 7
            // and ~1023s played, the import should land as brand new.
            Assert.AreEqual(1, first.Level, "level should not be carried over from the Godot save");
            Assert.AreEqual(0.0, first.PlaytimeSeconds, 0.01,
                "playtime should not be carried over from the Godot save");

            // And it is THAT paint, landed on the HEAD specifically -- not
            // merely some paint, and not merely landed somewhere. Index 0
            // of the decoded atlas is exactly where PaintAtlas.RowOf(Head)
            // == 0 / HeadMesh's own v0 starts sampling from.
            var paint = new BodyPaint(Color.white);
            Assert.IsTrue(paint.Decode(first.GetPaint()), "the imported paint will not load back");
            Color32 landed = paint.Atlas.GetPixels32()[0];
            Assert.AreEqual(mark.r, landed.r, "the mark did not land on the head's own row");
            Assert.AreEqual(mark.g, landed.g, "the mark did not land on the head's own row");
            Assert.AreEqual(mark.b, landed.b, "the mark did not land on the head's own row");
        }

        /// <summary>
        /// A character with no atlas_png at all is still worth having -- its
        /// name and hairstyle are still real appearance, even with a plain
        /// body underneath.
        /// </summary>
        [Test]
        public void ACharacterWithNoPaintStillComesAcross()
        {
            List<CharacterSave> imported =
                GodotCharacterImport.Import(Fixture(new byte[0]), out List<string> notes);

            CharacterSave second = imported[1];
            Assert.AreEqual("Ayazi", second.Name);
            Assert.AreEqual(1, second.Level, "a fresh import should always land at level 1");
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
