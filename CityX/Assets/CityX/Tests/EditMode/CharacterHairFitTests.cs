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

        [Test]
        public void EmbermaneKeepsItsOwnLongStandingPlacement()
        {
            var bounds = new Bounds(new Vector3(0.0f, -0.3f, 0.0f), new Vector3(0.6f, 0.84f, 0.6f));
            CharacterHair.Fit fit = CharacterHair.DefaultFit(CharacterHair.Embermane, bounds);

            Assert.AreEqual(new Vector3(180.0f, 0.0f, 0.0f), fit.Euler, "embermane is no longer turned over the way Godot had it");
            Assert.AreEqual(Vector3.one, fit.Scale, "embermane was resized -- it never was");
        }

        // ---- the saved fit ----

        [Test]
        public void ASavedFitOverridesTheAutomaticOneAndResettingBringsItBack()
        {
            CharacterHair.Fit automatic = CharacterHair.CurrentFit(CharacterHair.AnimeHair0, AwkwardHair);
            Assert.IsFalse(CharacterHair.IsTuned(CharacterHair.AnimeHair0));

            var tuned = new CharacterHair.Fit(new Vector3(0.0f, 0.1f, -0.05f), new Vector3(0.0f, 10.0f, 0.0f), Vector3.one * 0.5f);
            CharacterHair.SaveFit(CharacterHair.AnimeHair0, tuned);

            Assert.IsTrue(CharacterHair.IsTuned(CharacterHair.AnimeHair0));
            CharacterHair.Fit now = CharacterHair.CurrentFit(CharacterHair.AnimeHair0, AwkwardHair);
            Assert.AreEqual(tuned.Position, now.Position);
            Assert.AreEqual(tuned.Euler, now.Euler);
            Assert.AreEqual(tuned.Scale, now.Scale);

            CharacterHair.ResetFit(CharacterHair.AnimeHair0);
            Assert.IsFalse(CharacterHair.IsTuned(CharacterHair.AnimeHair0));
            Assert.AreEqual(automatic.Position, CharacterHair.CurrentFit(CharacterHair.AnimeHair0, AwkwardHair).Position,
                "resetting did not bring the automatic fit back");
        }

        [Test]
        public void SavingOneStylesFitLeavesTheOthersAlone()
        {
            CharacterHair.SaveFit(CharacterHair.AnimeHair0, new CharacterHair.Fit(Vector3.up, Vector3.zero, Vector3.one));

            Assert.IsFalse(CharacterHair.IsTuned(CharacterHair.Embermane), "tuning one style tuned another");
        }

        [Test]
        public void SavingTheSameStyleTwiceKeepsOneEntry()
        {
            CharacterHair.SaveFit(CharacterHair.AnimeHair0, new CharacterHair.Fit(Vector3.up, Vector3.zero, Vector3.one));
            CharacterHair.SaveFit(CharacterHair.AnimeHair0, new CharacterHair.Fit(Vector3.down, Vector3.zero, Vector3.one));

            Assert.AreEqual(1, _scratch.Entries.Count, "saving twice piled up entries");
            Assert.AreEqual(Vector3.down, CharacterHair.CurrentFit(CharacterHair.AnimeHair0, AwkwardHair).Position,
                "the second save did not replace the first");
        }

        // ---- the model hairstyles themselves ----

        /// <summary>The baked mesh of each hairstyle that comes from a model file.</summary>
        private static Mesh BakedMesh(string style)
        {
            string path = style == CharacterHair.AnimeHair0 ? "Assets/CityX/Character/Hair/AnimeHair0Mesh.asset"
                : "Assets/CityX/Character/Hair/Hair1Mesh.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                Assert.Ignore($"{path} is made by the scene builders; not built yet in this checkout.");
            }
            return mesh;
        }

        [TestCase(CharacterHair.AnimeHair0)]
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

        [TestCase(CharacterHair.AnimeHair0)]
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
