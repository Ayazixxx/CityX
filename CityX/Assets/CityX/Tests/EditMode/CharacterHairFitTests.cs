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
            hair.HairMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            return hair;
        }

        private static Color ColourOn(Transform worn)
        {
            var block = new MaterialPropertyBlock();
            worn.GetComponent<MeshRenderer>().GetPropertyBlock(block);
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

        /// <summary>The baked mesh of each hairstyle that comes from a model file.</summary>
        private static Mesh BakedMesh(string style)
        {
            string path = "Assets/CityX/Character/Hair/Hair1Mesh.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                Assert.Ignore($"{path} is made by the scene builders; not built yet in this checkout.");
            }
            return mesh;
        }

        [TestCase(CharacterHair.Hair1)]
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
        public void ARealModelHairLandsOnTheTopOfTheHeadNotHalfwayDownIt(string style)
        {
            Mesh mesh = BakedMesh(style);

            CharacterHair.Fit fit = CharacterHair.DefaultFit(style, mesh.bounds);
            Bounds placed = Placed(mesh.bounds, fit);

            Assert.Greater(placed.max.y, FusedBodyShape.HeadRadius, $"{style}: the hair does not reach the crown of the head");
            Assert.Less(placed.max.y, FusedBodyShape.HeadRadius * 1.3f, $"{style}: the hair floats well above the head");
            Assert.Greater(placed.min.y, -FusedBodyShape.HeadRadius * 1.6f, $"{style}: the hair hangs down past the chin and neck");
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

        // ---- the button row ----

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void TheHairButtonsFitTheColumnAndNeverOverlap(int count)
        {
            float width = UI.HairButtonRow.ButtonWidth(count);
            float halfColumn = UI.HairButtonRow.Available * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float centre = UI.HairButtonRow.CentreX(i, count);
                Assert.LessOrEqual(Mathf.Abs(centre) + width * 0.5f, halfColumn + 0.001f, $"{count} buttons: button {i} hangs off the panel");
                if (i > 0)
                {
                    float gap = centre - UI.HairButtonRow.CentreX(i - 1, count) - width;
                    Assert.GreaterOrEqual(gap, 0.0f, $"{count} buttons: buttons {i - 1} and {i} overlap");
                }
            }
        }

        [Test]
        public void ThreeHairButtonsKeepTheirOldWidth()
        {
            Assert.AreEqual(58.0f, UI.HairButtonRow.ButtonWidth(3), 1.0f);
            Assert.Less(UI.HairButtonRow.ButtonWidth(4), UI.HairButtonRow.ButtonWidth(3));
        }
    }
}
