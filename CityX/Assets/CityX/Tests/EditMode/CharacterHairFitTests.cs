using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// How a hairstyle is sized and placed on the head: the automatic fit, the
    /// saved fit that overrides it, and the real anime hair's baked mesh.
    /// </summary>
    public sealed class CharacterHairFitTests
    {
        private HairFitSettings _previous;
        private HairFitSettings _scratch;

        [SetUp]
        public void SetUp()
        {
            // A settings object of its own, so a test can save and reset fits without touching the real asset.
            _previous = HairFitSettings.Current;
            _scratch = ScriptableObject.CreateInstance<HairFitSettings>();
            HairFitSettings.Current = _scratch;
        }

        [TearDown]
        public void TearDown()
        {
            HairFitSettings.Current = _previous;
            Object.DestroyImmediate(_scratch);
        }

        // ---- the automatic fit ----

        /// <summary>The corners of a bounds after the fit's yaw, scale and position, in the head bone's space.</summary>
        private static Bounds Placed(Bounds mesh, CharacterHair.Fit fit)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(fit.Position, Quaternion.Euler(fit.Euler), fit.Scale);
            var placed = new Bounds(matrix.MultiplyPoint3x4(mesh.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? mesh.min.x : mesh.max.x,
                    (i & 2) == 0 ? mesh.min.y : mesh.max.y,
                    (i & 4) == 0 ? mesh.min.z : mesh.max.z);
                placed.Encapsulate(matrix.MultiplyPoint3x4(corner));
            }
            return placed;
        }

        // A hairstyle model as it comes: not centred, nowhere near the head, not head-sized.
        private static readonly Bounds AwkwardHair = new Bounds(new Vector3(0.015f, 2.34f, -0.04f), new Vector3(1.0f, 0.84f, 0.82f));

        [Test]
        public void TheAutomaticFitMakesTheHairJustWiderThanTheHead()
        {
            CharacterHair.Fit fit = CharacterHair.FitToHead(AwkwardHair, FusedBodyShape.HeadRadius, 0.0f);
            Bounds placed = Placed(AwkwardHair, fit);

            Assert.AreEqual(FusedBodyShape.HeadRadius * 2.0f * CharacterHair.WidthOverHead, placed.size.x, 0.001f,
                "the hair is not the width of the head times the margin");
        }

        [Test]
        public void TheAutomaticFitStandsTheHairOnTheCrownAndCentresItOverTheHead()
        {
            CharacterHair.Fit fit = CharacterHair.FitToHead(AwkwardHair, FusedBodyShape.HeadRadius, 0.0f);
            Bounds placed = Placed(AwkwardHair, fit);

            Assert.AreEqual(FusedBodyShape.HeadRadius * (1.0f + CharacterHair.CrownLift), placed.max.y, 0.001f,
                "the top of the hair is not just above the crown of the head");
            Assert.AreEqual(0.0f, placed.center.x, 0.001f, "the hair is off to one side of the head");
            Assert.AreEqual(0.0f, placed.center.z, 0.001f, "the hair is off the front or the back of the head");
        }

        [Test]
        public void TurningTheHairDoesNotSlideItOffTheHead()
        {
            foreach (float yaw in new[] { 90.0f, 180.0f, -90.0f })
            {
                CharacterHair.Fit fit = CharacterHair.FitToHead(AwkwardHair, FusedBodyShape.HeadRadius, yaw);
                Bounds placed = Placed(AwkwardHair, fit);

                Assert.AreEqual(yaw, fit.Euler.y, 0.0001f, "the fit lost its turn");
                Assert.AreEqual(0.0f, placed.center.x, 0.001f, $"yaw {yaw}: the hair slid sideways");
                Assert.AreEqual(0.0f, placed.center.z, 0.001f, $"yaw {yaw}: the hair slid front to back");
            }
        }

        /// <summary>
        /// "embermane" and "animehair0" were removed as outdated. A character saved wearing one still
        /// carries the name; it must load as bald and not break, and must not be offered as a style.
        /// </summary>
        [TestCase("embermane")]
        [TestCase("animehair0")]
        public void ARemovedStyleIsNotOfferedAndALeftoverNameLoadsAsBald(string removed)
        {
            CollectionAssert.DoesNotContain(CharacterHair.ValidStyles, removed, "a removed style is still offered");

            _host = new GameObject("Head", typeof(RectTransform));
            var hair = _host.AddComponent<CharacterHair>();
            hair.Head = _host.transform;

            hair.SetStyle(removed);

            Assert.AreEqual(CharacterHair.None, hair.Style, "a leftover style name did not load as bald");
            Assert.IsNull(hair.Worn, "a leftover style name put something on the head");
        }

        // ---- the colour ----

        private GameObject _host;

        [TearDown]
        public void DestroyHost()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }
        }

        /// <summary>A head wearing a stand-in mesh, so the colour can be checked without a scene.</summary>
        private CharacterHair MakeHairOnAHead(out GameObject cube)
        {
            _host = new GameObject("Body");
            var head = new GameObject("head");
            head.transform.SetParent(_host.transform, false);

            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(_host.transform, false);
            var hair = _host.AddComponent<CharacterHair>();
            hair.Head = head.transform;
            hair.StyleMeshes = new[] { cube.GetComponent<MeshFilter>().sharedMesh };
            hair.HairMaterial = new Material(CityX.Core.LitShader.Find());
            return hair;
        }

        private static Color ColourOn(Transform worn)
        {
            var block = new MaterialPropertyBlock();
            worn.GetComponent<MeshRenderer>().GetPropertyBlock(block, 0);
            return block.GetColor("_BaseColor");
        }

        [Test]
        public void HairStartsTheDefaultColourAndCanBeDyedAnyColour()
        {
            CharacterHair hair = MakeHairOnAHead(out _);
            Assert.AreEqual(CharacterSaveDefault, hair.Colour, "hair does not start the default colour");

            foreach (var dye in new[] { Color.black, Color.white, new Color(0.2f, 0.4f, 0.85f), new Color(0.9f, 0.1f, 0.6f) })
            {
                hair.SetColour(dye);
                Assert.AreEqual(dye.r, hair.Colour.r, 0.0001f);
                Assert.AreEqual(dye.g, hair.Colour.g, 0.0001f);
                Assert.AreEqual(dye.b, hair.Colour.b, 0.0001f);
            }
        }

        private static readonly Color CharacterSaveDefault = Core.CharacterSave.DefaultHairColour;

        [Test]
        public void DyeingTheHairColoursTheWornMeshAndOnlyThatHead()
        {
            CharacterHair hair = MakeHairOnAHead(out _);
            hair.SetStyle(CharacterHair.Hair1);
            var blue = new Color(0.2f, 0.4f, 0.85f);

            hair.SetColour(blue);

            Color worn = ColourOn(hair.Worn);
            Assert.AreEqual(blue.r, worn.r, 0.001f);
            Assert.AreEqual(blue.g, worn.g, 0.001f);
            Assert.AreEqual(blue.b, worn.b, 0.001f);
            // The shared material is untouched, so another head keeps its own colour.
            Assert.AreNotEqual(blue, hair.HairMaterial.GetColor("_BaseColor"), "the shared hair material was dyed");
        }

        [Test]
        public void TheColourIsKeptWhileBaldAndWornByTheNextStyle()
        {
            CharacterHair hair = MakeHairOnAHead(out _);
            var pink = new Color(0.9f, 0.45f, 0.65f);

            hair.SetColour(pink);                      // bald: nothing to dye yet
            Assert.IsNull(hair.Worn);
            hair.SetStyle(CharacterHair.Hair1);

            Assert.AreEqual(pink.r, ColourOn(hair.Worn).r, 0.001f, "the colour chosen while bald was lost");

            hair.SetStyle(CharacterHair.None);
            hair.SetStyle(CharacterHair.Hair1);
            Assert.AreEqual(pink.g, ColourOn(hair.Worn).g, 0.001f, "changing style forgot the colour");
        }

        [Test]
        public void ADyedColourIsAlwaysOpaque()
        {
            CharacterHair hair = MakeHairOnAHead(out _);
            hair.SetColour(new Color(0.3f, 0.3f, 0.3f, 0.2f));
            Assert.AreEqual(1.0f, hair.Colour.a);
        }

        // ---- the saved fit ----

        [Test]
        public void ASavedFitOverridesTheAutomaticOneAndResettingBringsItBack()
        {
            CharacterHair.Fit automatic = CharacterHair.CurrentFit(CharacterHair.Hair1, AwkwardHair);
            Assert.IsFalse(CharacterHair.IsTuned(CharacterHair.Hair1));

            var tuned = new CharacterHair.Fit(new Vector3(0.0f, 0.1f, -0.05f), new Vector3(0.0f, 10.0f, 0.0f), Vector3.one * 0.5f);
            CharacterHair.SaveFit(CharacterHair.Hair1, tuned);

            Assert.IsTrue(CharacterHair.IsTuned(CharacterHair.Hair1));
            CharacterHair.Fit now = CharacterHair.CurrentFit(CharacterHair.Hair1, AwkwardHair);
            Assert.AreEqual(tuned.Position, now.Position);
            Assert.AreEqual(tuned.Euler, now.Euler);
            Assert.AreEqual(tuned.Scale, now.Scale);

            CharacterHair.ResetFit(CharacterHair.Hair1);
            Assert.IsFalse(CharacterHair.IsTuned(CharacterHair.Hair1));
            Assert.AreEqual(automatic.Position, CharacterHair.CurrentFit(CharacterHair.Hair1, AwkwardHair).Position,
                "resetting did not bring the automatic fit back");
        }

        [Test]
        public void SavingOneStylesFitLeavesTheOthersAlone()
        {
            CharacterHair.SaveFit(CharacterHair.Hair1, new CharacterHair.Fit(Vector3.up, Vector3.zero, Vector3.one));

            Assert.IsFalse(CharacterHair.IsTuned("some-other-style"), "tuning one style tuned another");
        }

        [Test]
        public void SavingTheSameStyleTwiceKeepsOneEntry()
        {
            CharacterHair.SaveFit(CharacterHair.Hair1, new CharacterHair.Fit(Vector3.up, Vector3.zero, Vector3.one));
            CharacterHair.SaveFit(CharacterHair.Hair1, new CharacterHair.Fit(Vector3.down, Vector3.zero, Vector3.one));

            Assert.AreEqual(1, _scratch.Entries.Count, "saving twice piled up entries");
            Assert.AreEqual(Vector3.down, CharacterHair.CurrentFit(CharacterHair.Hair1, AwkwardHair).Position,
                "the second save did not replace the first");
        }

        // ---- the model hairstyles themselves ----

        /// <summary>
        /// The baked mesh of a hairstyle that comes from a model file -- THAT style's (this once loaded Hair1Mesh.asset whatever
        /// the style, so a second case would have tested the bob cut twice). Baked from the model if no builder has yet.
        /// </summary>
        private static Mesh BakedMesh(string style)
        {
            Mesh mesh = UIEditor.HairAssets.MeshFor(style);
            if (mesh == null)
            {
                Assert.Ignore($"{style}'s model is not in this checkout.");
            }
            return mesh;
        }

        [TestCase(CharacterHair.Hair1)]
        [TestCase(CharacterHair.Hyde)]
        public void AModelHairMeshIsBakedAtTheSizeAndHeightOfAHead(string style)
        {
            Mesh mesh = BakedMesh(style);

            // Each glb's node scales its mesh and lifts it about a metre. Unbaked, the anime hair was
            // half a metre wide and hovering above the head, and the fit -- which only measures
            // bounds -- hid the mistake for the size but not for the shape. The raw vertices of both
            // sit around y = 0.7; the baked ones around 2.3, where a head is.
            Assert.That(mesh.bounds.size.x, Is.InRange(0.6f, 1.2f), $"{style}: the model's node scale was not baked into the mesh");
            Assert.That(mesh.bounds.center.y, Is.GreaterThan(1.5f), $"{style}: the model's node lift was not baked into the mesh");
        }

        [TestCase(CharacterHair.Hair1)]
        [TestCase(CharacterHair.Hyde)]
        public void ARealModelHairLandsOnTheTopOfTheHeadNotHalfwayDownIt(string style)
        {
            Mesh mesh = BakedMesh(style);

            CharacterHair.Fit fit = CharacterHair.DefaultFit(style, mesh.bounds);
            Bounds placed = Placed(mesh.bounds, fit);

            Assert.Greater(placed.max.y, FusedBodyShape.HeadRadius, $"{style}: the hair does not reach the crown of the head");
            Assert.Less(placed.max.y, FusedBodyShape.HeadRadius * 1.3f, $"{style}: the hair floats well above the head");
            Assert.Greater(placed.min.y, -FusedBodyShape.HeadRadius * 1.6f, $"{style}: the hair hangs down past the chin and neck");
        }

        /// <summary>
        /// DREADS (2026-10-21) splay wider than the cap they hang from, so the automatic fit (sized on the outer width) sank the head through
        /// them; its saved fit sizes it on the hollow inside instead. Placed, the hair holds the whole head and covers the crown.
        /// </summary>
        [Test]
        public void TheDreadsHoldTheWholeHeadAndCoverTheCrown()
        {
            Mesh mesh = BakedMesh(CharacterHair.Dreads);
            // The real saved fits: this fixture puts a scratch set in force.
            var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<HairFitSettings>(UIEditor.HairAssets.FitSettingsPath);
            Assert.IsNotNull(saved, "no saved hair fits");
            Assert.IsTrue(saved.TryGet(CharacterHair.Dreads, out CharacterHair.Fit fit), "the dreads have no saved fit");
            // Owner, 2026-10-22: the head poked through between the locks. Every way out from the head's centre above the hairline meets the
            // scalp cap just off the skin.
            Matrix4x4 place = Matrix4x4.TRS(fit.Position, Quaternion.Euler(fit.Euler), fit.Scale);
            var cover = new GameObject("DreadsCover");
            bool hitBackfaces = Physics.queriesHitBackfaces;
            try
            {
                Mesh placedMesh = UIEditor.HairAssets.Bake(mesh, place);
                cover.AddComponent<MeshCollider>().sharedMesh = placedMesh;
                Physics.SyncTransforms();
                Physics.queriesHitBackfaces = true;
                for (float turn = 0.0f; turn < 360.0f; turn += 15.0f)
                {
                    for (float height = HairScalp.Hairline(turn) + 5.0f; height < 90.0f; height += 10.0f)
                    {
                        float h = height * Mathf.Deg2Rad, t = turn * Mathf.Deg2Rad;
                        var direction = new Vector3(Mathf.Cos(h) * Mathf.Sin(t), Mathf.Sin(h), Mathf.Cos(h) * Mathf.Cos(t));
                        Assert.IsTrue(Physics.Raycast(Vector3.zero, direction, FusedBodyShape.HeadRadius * 1.05f),
                            $"the head shows through the dreads {turn:0} degrees round, {height:0} up");
                    }
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = hitBackfaces;
                Object.DestroyImmediate(cover);
            }
            Bounds placed = Placed(mesh.bounds, fit);
            // The locks stand thick on the crown (0.2 m at this fit), but no higher than that.
            Assert.That(placed.max.y, Is.InRange(FusedBodyShape.HeadRadius, FusedBodyShape.HeadRadius * 1.7f), "the dreads do not sit on the crown");
        }

        /// <summary>The kick hair (2026-11-03) came as 164,057 triangles with specks a metre under the head: cut down, in one piece, and worn where the owner placed it.</summary>
        [Test]
        public void TheKickHairIsCutDownAndWornWhereTheOwnerPlacedIt()
        {
            Mesh mesh = BakedMesh(CharacterHair.KickHair);
            Assert.LessOrEqual(mesh.triangles.Length / 3, 11000, "the kick hair is not cut down");
            Assert.Less(mesh.bounds.size.y, 1.0f, "stray pieces are left far from the hair");
            var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<HairFitSettings>(UIEditor.HairAssets.FitSettingsPath);
            Assert.IsTrue(saved.TryGet(CharacterHair.KickHair, out CharacterHair.Fit fit), "the kick hair has no saved fit");
            Assert.AreEqual(new Vector3(0.0f, -2.27f, 0.0f), fit.Position);
            Assert.AreEqual(Vector3.one, fit.Scale);
        }

        /// <summary>
        /// Owner, 2026-11-04: "there is a slit where you can see underneath the hair and the sky shows behind it". A surface is drawn only from outside, so any open edge standing off
        /// the head lets the eye in under the hair and out through its far side. Worn where the owner placed it (the head bone at the head's centre), every open edge ends inside the head.
        /// </summary>
        [Test]
        public void EveryOpenEdgeOfTheKickHairEndsInsideTheHead()
        {
            Mesh mesh = BakedMesh(CharacterHair.KickHair);
            var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<HairFitSettings>(UIEditor.HairAssets.FitSettingsPath);
            Assert.IsTrue(saved.TryGet(CharacterHair.KickHair, out CharacterHair.Fit fit));
            Matrix4x4 worn = Matrix4x4.TRS(fit.Position, Quaternion.Euler(fit.Euler), fit.Scale);

            // Corners at the same place are one corner, however the mesh split them.
            Vector3[] vertices = mesh.vertices;
            var corner = new int[vertices.Length];
            var ids = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i] * 100000.0f;
                var key = new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
                if (!ids.TryGetValue(key, out int id))
                {
                    id = i;
                    ids[key] = id;
                }
                corner[i] = id;
            }
            var uses = new Dictionary<(int, int), int>();
            int[] triangles = mesh.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                for (int side = 0; side < 3; side++)
                {
                    int a = corner[triangles[t + side]];
                    int b = corner[triangles[t + (side + 1) % 3]];
                    (int, int) edge = a < b ? (a, b) : (b, a);
                    uses.TryGetValue(edge, out int count);
                    uses[edge] = count + 1;
                }
            }
            int open = 0;
            foreach (KeyValuePair<(int, int), int> edge in uses)
            {
                if (edge.Value != 1)
                {
                    continue;
                }
                open++;
                foreach (int end in new[] { edge.Key.Item1, edge.Key.Item2 })
                {
                    Vector3 placed = worn.MultiplyPoint3x4(vertices[end]);
                    Assert.Less(placed.magnitude, FusedBodyShape.HeadRadius, $"an open edge of the kick hair stands off the head at {placed}: the sky shows under it");
                }
            }
            Assert.Greater(open, 0, "the hair is closed all round: the test no longer says anything");
        }

        [Test]
        public void EveryStyleAfterNoneHasALabelAndAModel()
        {
            for (int i = 1; i < CharacterHair.ValidStyles.Length; i++)
            {
                string style = CharacterHair.ValidStyles[i];
                StringAssert.DoesNotContain("\n", CharacterHair.LabelFor(style));
                Assert.AreNotEqual(style.ToUpperInvariant(), CharacterHair.LabelFor(style),
                    $"\"{style}\" has no short label of its own");
            }

            Mesh[] meshes = UIEditor.HairAssets.StyleMeshes();
            Assert.AreEqual(CharacterHair.ValidStyles.Length - 1, meshes.Length, "a style has no slot in the mesh list");
            for (int i = 0; i < meshes.Length; i++)
            {
                Assert.IsNotNull(meshes[i], $"\"{CharacterHair.ValidStyles[i + 1]}\" has no mesh wired");
            }
        }
    }
}
