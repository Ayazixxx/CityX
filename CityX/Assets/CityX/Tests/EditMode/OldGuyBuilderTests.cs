using CityX.CharacterEditor;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Reading a design's own painted atlas out of npc/designs/*.tres --
    /// a DIFFERENT PackedByteArray serialization from GodotCharacterImport's
    /// own (a Resource's own base64-in-a-quoted-string, not ConfigFile's
    /// comma-separated integer literal), so it gets its own coverage rather
    /// than assuming the two agree.
    /// </summary>
    public sealed class OldGuyBuilderTests
    {
        [Test]
        public void APackedByteArrayInsideAResourceDecodesFromBase64()
        {
            // "SGVsbG8=" is the base64 encoding of the bytes for "Hello".
            string text = "[gd_resource type=\"Resource\"]\n\n[resource]\n"
                           + "display_name = \"Test\"\n"
                           + "atlas_png = PackedByteArray(\"SGVsbG8=\")\n";

            byte[] bytes = OldGuyBuilder.ParseAtlasPngFromTres(text);

            CollectionAssert.AreEqual(new byte[] { 72, 101, 108, 108, 111 }, bytes,
                "the base64-encoded atlas_png did not decode to its own real bytes");
        }

        [Test]
        public void ATresWithNoAtlasFieldAtAllReturnsNullRatherThanThrowing()
        {
            string text = "[gd_resource type=\"Resource\"]\n\n[resource]\n"
                           + "display_name = \"Test\"\n";

            Assert.IsNull(OldGuyBuilder.ParseAtlasPngFromTres(text));
        }

        [Test]
        public void MalformedBase64ReturnsNullRatherThanThrowing()
        {
            string text = "atlas_png = PackedByteArray(\"not valid base64!!\")\n";

            Assert.IsNull(OldGuyBuilder.ParseAtlasPngFromTres(text));
        }

        [Test]
        public void ADesignThatDoesNotExistOnDiskReturnsNull()
        {
            Assert.IsNull(OldGuyBuilder.LoadDesignAtlasPng("no_such_design_xyz"));
        }

        /// <summary>
        /// The real file, if this checkout has the Godot half of the project
        /// alongside it. Reported rather than asserted when it does not,
        /// same reasoning as GodotCharacterImportTests' own copy of this.
        /// </summary>
        [Test]
        public void TheRealOldGuyDesignDecodesIfItIsHere()
        {
            byte[] png = OldGuyBuilder.LoadDesignAtlasPng("old_guy");
            if (png == null)
            {
                Assert.Ignore("no npc/designs/old_guy.tres next to this checkout, or it would not decode");
                return;
            }

            Assert.Greater(png.Length, 0);
            // A PNG's own magic number -- confirms this is real image bytes,
            // not base64 that happened to decode into garbage.
            Assert.AreEqual(0x89, png[0]);
            Assert.AreEqual((byte)'P', png[1]);
            Assert.AreEqual((byte)'N', png[2]);
            Assert.AreEqual((byte)'G', png[3]);
        }
    }
}
