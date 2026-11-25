using System.IO;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Pressed shirt designs (docs/HEAT_PRESS.md; owner, 2026-11-24: "I want the design to stay in the game forever unless the item is lost
    /// somehow", "make it use images off your computer and visible across computers"): the design's bytes and id, the store that keeps them,
    /// the canvas they are drawn on, where they land on a shirt, and the design riding with its shirt through the bag, a drop and the wire.
    /// </summary>
    public sealed class ShirtDesignTests
    {
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "cityx-designs-" + System.Guid.NewGuid().ToString("N"));
            DesignLibrary.Store = new DesignStore(_folder);
            DesignLibrary.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            DesignLibrary.Store = null;
            DesignLibrary.Reset();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private static byte[] RedSquarePng()
        {
            var pixels = new Color32[ShirtDesignArea.Width * ShirtDesignArea.Height];
            for (int y = 200; y < 300; y++)
            {
                for (int x = 150; x < 250; x++) pixels[y * ShirtDesignArea.Width + x] = new Color32(255, 0, 0, 255);
            }
            return DesignPng.Encode(pixels);
        }

        [Test]
        public void ADesignIsTwoPicturesNamedByTheirContentAndJunkIsRefused()
        {
            byte[] front = RedSquarePng();
            byte[] blob = ShirtDesignBlob.Pack(front, null);
            Assert.IsTrue(ShirtDesignBlob.TryUnpack(blob, out byte[] backFront, out byte[] back));
            CollectionAssert.AreEqual(front, backFront);
            Assert.AreEqual(0, back.Length);
            Assert.AreEqual(DesignStore.IdOf(blob), DesignStore.IdOf((byte[])blob.Clone()), "the same design has two ids");
            Assert.IsTrue(DesignStore.IsValidId(DesignStore.IdOf(blob)));
            Assert.IsFalse(DesignStore.IsValidId("../../etc"), "a path is taken for an id");
            Assert.IsFalse(DesignStore.IsValidId(DesignStore.IdOf(blob).ToUpperInvariant()));

            Assert.IsFalse(ShirtDesignBlob.IsSane(ShirtDesignBlob.Pack(null, null)), "an empty design is kept");
            Assert.IsFalse(ShirtDesignBlob.IsSane(ShirtDesignBlob.Pack(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, null)), "a side that is not a PNG is kept");
            byte[] cut = new byte[blob.Length - 5];
            System.Array.Copy(blob, cut, cut.Length);
            Assert.IsFalse(ShirtDesignBlob.IsSane(cut), "a cut-off design is kept");
            byte[] wrong = (byte[])blob.Clone();
            wrong[0] = (byte)'X';
            Assert.IsFalse(ShirtDesignBlob.IsSane(wrong), "a design with the wrong header is kept");
            byte[] heavy = new byte[ShirtDesignBlob.MaxSideBytes + 1];
            System.Array.Copy(front, heavy, front.Length);
            Assert.IsFalse(ShirtDesignBlob.IsSane(ShirtDesignBlob.Pack(heavy, null)), "a side over the limit is kept");
        }

        [Test]
        public void TheStoreKeepsADesignForGoodAndHandsOutOnlyWhatItsIdSays()
        {
            var store = new DesignStore(_folder);
            byte[] blob = ShirtDesignBlob.Pack(RedSquarePng(), RedSquarePng());
            string id = store.Keep(blob);
            Assert.IsNotNull(id);
            Assert.AreEqual(id, store.Keep(blob), "keeping it again names it differently");
            Assert.IsTrue(new DesignStore(_folder).TryLoad(id, out byte[] read), "not kept on disk");
            CollectionAssert.AreEqual(blob, read);
            Assert.IsNull(store.Keep(new byte[] { 1, 2, 3 }), "junk is kept");

            File.WriteAllBytes(Path.Combine(_folder, id + DesignStore.Extension), ShirtDesignBlob.Pack(RedSquarePng(), null));
            Assert.IsFalse(store.TryLoad(id, out _), "a file that is not what its name says is handed out");
        }

        [Test]
        public void ADesignNotHereIsWaitedOnAndAnsweredWhenItArrives()
        {
            byte[] blob = ShirtDesignBlob.Pack(RedSquarePng(), null);
            string id = DesignStore.IdOf(blob);
            byte[] got = null;
            DesignLibrary.Get(id, b => got = b);
            Assert.IsNull(got, "answered before it was here");
            CollectionAssert.Contains(new System.Collections.Generic.List<string>(DesignLibrary.StillWaiting), id);
            DesignLibrary.Arrived(id, ShirtDesignBlob.Pack(null, RedSquarePng()));   // another design under its name: refused
            Assert.IsNull(got, "a design that is not what its id says was taken");
            DesignLibrary.Arrived(id, blob);
            CollectionAssert.AreEqual(blob, got, "the one waiting was not answered");
            Assert.IsTrue(DesignLibrary.Has(id), "not kept once it arrived");
            Assert.AreEqual(id, DesignLibrary.Publish(blob), "a design pressed here is named differently");
        }

        [Test]
        public void OnTheCanvasTheBrushPaintsTheEraserTakesOnlyNewInkAndUndoTakesBack()
        {
            var canvas = new ShirtDesignCanvas();
            var printed = new Color32[ShirtDesignCanvas.Width * ShirtDesignCanvas.Height];
            int middle = 280 * ShirtDesignCanvas.Width + 224;
            printed[middle] = new Color32(0, 0, 255, 255);
            canvas.Begin(printed, null);
            Assert.IsFalse(canvas.HasInk);
            Assert.IsTrue(canvas.Shows(ShirtDesignCanvas.Side.Front));

            canvas.BeginChange(ShirtDesignCanvas.Side.Front);
            canvas.Stroke(ShirtDesignCanvas.Side.Front, new Vector2(100, 100), new Vector2(300, 100), 6.0f, Color.red, false);
            Assert.IsTrue(canvas.HasInk);
            Assert.AreEqual(255, canvas.ColourAt(ShirtDesignCanvas.Side.Front, 200, 100).r, "the stroke left no ink in its middle");
            Assert.AreEqual(0, canvas.ColourAt(ShirtDesignCanvas.Side.Front, 200, 140).a, "the stroke spread far off its line");
            Assert.AreEqual(0, canvas.Composite(ShirtDesignCanvas.Side.Back)[100 * ShirtDesignCanvas.Width + 200].a, "the front's ink is on the back");

            canvas.BeginChange(ShirtDesignCanvas.Side.Front);
            canvas.Stroke(ShirtDesignCanvas.Side.Front, new Vector2(224, 280), new Vector2(224, 280), 20.0f, Color.white, true);
            Assert.AreEqual(255, canvas.ColourAt(ShirtDesignCanvas.Side.Front, 224, 280).b, "the eraser took off what is printed on the shirt");

            canvas.Undo();
            canvas.Undo();
            Assert.IsFalse(canvas.HasInk, "undo did not take the stroke back");
            canvas.Redo();
            Assert.IsTrue(canvas.HasInk, "redo did not bring it back");
        }

        [Test]
        public void APictureIsLaidWhereItIsPutUprightAndInProportion()
        {
            // A 2 x 4 picture: its bottom half green, its top half yellow.
            var picture = new Color32[8];
            for (int i = 0; i < 8; i++) picture[i] = i < 4 ? new Color32(0, 255, 0, 255) : new Color32(255, 255, 0, 255);
            var canvas = new ShirtDesignCanvas();
            canvas.Begin(null, null);
            canvas.Place(ShirtDesignCanvas.Side.Back, picture, 2, 4, new Vector2(224, 280), 100.0f, 0.0f);
            Color32 low = canvas.ColourAt(ShirtDesignCanvas.Side.Back, 224, 280 - 80), high = canvas.ColourAt(ShirtDesignCanvas.Side.Back, 224, 280 + 80);
            Assert.IsTrue(low.g > 200 && low.r < 60, $"the picture's bottom is not at the bottom ({low})");
            Assert.IsTrue(high.r > 200 && high.g > 200, $"the picture's top is not at the top ({high})");
            Assert.AreEqual(0, canvas.ColourAt(ShirtDesignCanvas.Side.Back, 224, 280 + 110).a, "the picture is taller than its proportion");
            Assert.AreEqual(0, canvas.ColourAt(ShirtDesignCanvas.Side.Back, 224 + 60, 280).a, "the picture is wider than asked");
            Assert.IsFalse(canvas.Shows(ShirtDesignCanvas.Side.Front));
            canvas.Undo();
            Assert.IsFalse(canvas.HasInk, "a placed picture cannot be taken back");
        }

        [Test]
        public void ASideBecomesAPngAndBackAndAnEmptySideIsNothing()
        {
            Assert.AreEqual(0, DesignPng.Encode(new Color32[ShirtDesignArea.Width * ShirtDesignArea.Height]).Length, "an empty side weighs something");
            byte[] png = RedSquarePng();
            Color32[] back = DesignPng.Pixels(png);
            Assert.AreEqual(255, back[250 * ShirtDesignArea.Width + 200].r);
            Assert.AreEqual(0, back[10 * ShirtDesignArea.Width + 10].a);
        }

        /// <summary>Every T-shirt knows where a design goes on its texture: the print area's middle is on its front piece, at the chest.</summary>
        [TestCase("TShirt")]
        [TestCase("BlackTShirt")]
        [TestCase("LongSleeveTShirt")]
        public void EveryShirtKnowsWhereADesignGoes(string prefab)
        {
            var shirt = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Wearables/{prefab}.prefab").GetComponent<TShirt>();
            Assert.Greater(shirt.UvPerMetre, 0.1f, "no print fit: rebuild the scene");
            Vector2 centre = ShirtDesignArea.CentreUv(shirt.FrontMiddleUv, shirt.UvPerMetre);
            Assert.IsTrue(ShirtPrintLayout.FrontPiece.Contains(centre), $"the front's print area is at {centre}, off the front piece");
            Assert.IsTrue(ShirtPrintLayout.BackPiece.Contains(ShirtDesignArea.CentreUv(shirt.BackMiddleUv, shirt.UvPerMetre)), "the back's is off the back piece");

            // The mesh's point nearest the print area's middle on the chest lies on the texture where the fit puts it (within 2.5 cm).
            Vector3[] vertices = shirt.GarmentMesh.vertices;
            Vector2[] uvs = shirt.GarmentMesh.uv;
            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].z <= 0.0f || !ShirtPrintLayout.FrontPiece.Contains(uvs[i])) continue;
                float d = new Vector2(vertices[i].x, vertices[i].y - ShirtDesignArea.MiddleHeight).magnitude;
                if (d < best) { best = d; nearest = i; }
            }
            Vector3 p = vertices[nearest];
            Vector2 expected = ShirtPrintLayout.CentreUv(shirt.FrontMiddleUv, shirt.UvPerMetre, -p.x, p.y - ShirtPrintLayout.MiddleHeight);
            Assert.Less(Vector2.Distance(uvs[nearest], expected), 0.025f * shirt.UvPerMetre, $"the chest's point {p} is not on the texture where a print lands");

            // Laid on a cloth texture, a design's middle comes out at the chest and nothing spills off the front piece.
            const int size = 256;
            var texels = new Color32[size * size];
            var red = new Color32[16];
            for (int i = 0; i < red.Length; i++) red[i] = new Color32(255, 0, 0, 255);
            ShirtPrintLayout.Overlay(texels, size, red, 4, 4, centre, 2.0f, ShirtPrintLayout.FrontPiece);
            Assert.AreEqual(255, texels[(int)(centre.y * size) * size + (int)(centre.x * size)].r, "the design is not at the chest");
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (texels[y * size + x].r > 0)
                        Assert.IsTrue(ShirtPrintLayout.FrontPiece.Contains(new Vector2((x + 0.5f) / size, (y + 0.5f) / size)), "the design spilt off the front piece");
                }
            }
        }

        [Test]
        public void SeenFromInFrontTheShirtFillsItsMiddleAndLeavesTheCornersClear()
        {
            var shirt = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/TShirt.prefab").GetComponent<TShirt>();
            const int width = 230, height = 258;
            Color32[] pixels = ShirtSilhouette.Draw(shirt.GarmentMesh, true, null, 0, Color.white, width, height, 235.0f, new Vector2(0.0f, 1.58f));
            Assert.Greater(pixels[(height / 2) * width + width / 2].a, 0, "nothing of the shirt at its middle");
            Assert.AreEqual(0, pixels[0].a, "the shirt fills a corner");
            Assert.AreEqual(0, pixels[pixels.Length - 1].a);
        }

        [Test]
        public void TheDesignRidesWithTheShirtThroughTheBagADropAndTheWire()
        {
            string id = DesignStore.IdOf(ShirtDesignBlob.Pack(RedSquarePng(), null));
            var data = new ItemInstanceData { DesignId = id };
            Assert.IsFalse(data.IsEmpty, "a printed shirt counts as plain (it would stack with plain ones)");
            Assert.AreEqual(id, Inventory.DataOf(new BagEntry { ItemId = ItemRegistry.TShirt, DesignId = id }).DesignId, "lost from the save");
            Assert.IsNull(Inventory.DataOf(new BagEntry { ItemId = ItemRegistry.TShirt, DesignId = "not an id" }).DesignId, "a broken id is taken");

            var drop = new ItemDrop { Id = 5, ItemId = ItemRegistry.TShirt, Data = data, Rotation = Quaternion.identity };
            ItemDropMessage message = DroppedItemsNetwork.ToMessage(drop);
            Assert.IsTrue(DroppedItemsNetwork.Acceptable(message));
            Assert.AreEqual(id, DroppedItemsNetwork.ToDrop(message).Data.DesignId, "lost when dropped");
            message.DesignId = "x/../y";
            Assert.IsFalse(DroppedItemsNetwork.Acceptable(message), "a drop with a broken design id is taken");

            Assert.AreEqual("t_shirt", Inventory.ShownDesign("t_shirt#" + id, out string shown));
            Assert.AreEqual(id, shown);
            Assert.AreEqual("t_shirt", Inventory.ShownDesign("t_shirt", out string none));
            Assert.AreEqual(string.Empty, none);

            Assert.IsTrue(HeatPressNetwork.Acceptable(new HeatPressPressingMessage { ItemId = ItemRegistry.TShirt, NewDesignId = id }));
            Assert.IsFalse(HeatPressNetwork.Acceptable(new HeatPressPressingMessage { ItemId = ItemRegistry.TShirt, NewDesignId = "nope" }));
            Assert.IsFalse(HeatPressNetwork.Acceptable(new HeatPressPressingMessage { ItemId = "spoon", NewDesignId = id }));
        }
    }
}
