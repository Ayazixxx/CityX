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

        // ---- the anime hair itself ----

        [Test]
        public void TheAnimeHairMeshIsBakedAtTheSizeOfAHead()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/AnimeHair0Mesh.asset");
            if (mesh == null)
            {
                Assert.Ignore("AnimeHair0Mesh.asset is made by the scene builders; not built yet in this checkout.");
            }

            // The glb's node scales it by 1.74 and lifts it 1.1m. Unbaked, this was half a metre wide
            // and hovering above the head, and the fit -- which only measures bounds -- hid the mistake
            // for the size but not for the shape.
            Assert.That(mesh.bounds.size.x, Is.InRange(0.8f, 1.2f), "the model's node scale was not baked into the mesh");
            Assert.That(mesh.bounds.center.y, Is.GreaterThan(2.0f), "the model's node lift was not baked into the mesh");
        }

        [Test]
        public void TheRealAnimeHairLandsOnTheTopOfTheHeadNotHalfwayDownIt()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Hair/AnimeHair0Mesh.asset");
            if (mesh == null)
            {
                Assert.Ignore("AnimeHair0Mesh.asset is made by the scene builders; not built yet in this checkout.");
            }

            CharacterHair.Fit fit = CharacterHair.DefaultFit(CharacterHair.AnimeHair0, mesh.bounds);
            Bounds placed = Placed(mesh.bounds, fit);

            Assert.Greater(placed.max.y, FusedBodyShape.HeadRadius, "the hair does not reach the crown of the head");
            Assert.Less(placed.max.y, FusedBodyShape.HeadRadius * 1.3f, "the hair floats well above the head");
            Assert.Greater(placed.min.y, -FusedBodyShape.HeadRadius * 1.6f, "the hair hangs down past the chin and neck");
        }
    }
}
