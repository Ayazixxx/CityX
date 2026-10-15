using CityX.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// Checks the HUD prefab is wired, for the same reason the rig is checked:
    /// every way it can be wrong is silent.
    ///
    /// A null RectTransform reference means a bar that never moves. A missing
    /// font means a label that renders nothing. A death screen left active in
    /// the prefab means a black overlay over the game from the first frame. All
    /// of them build, save and load without complaint.
    /// </summary>
    public class PlayerHudTests
    {
        private const string PrefabPath = "Assets/CityX/UI/PlayerHud.prefab";

        private static PlayerHud LoadHud()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"no HUD prefab at {PrefabPath} -- run CityX/Rebuild Player HUD");

            var hud = prefab.GetComponent<PlayerHud>();
            Assert.IsNotNull(hud, "the HUD prefab has no PlayerHud component");
            return hud;
        }

        [Test]
        public void EveryPieceTheHudDrivesIsWired()
        {
            PlayerHud hud = LoadHud();

            Assert.IsNotNull(hud.HealthFill, "no health fill -- the health bar would never move");
            Assert.IsNotNull(hud.StaminaFill, "no stamina fill -- the stamina bar would never move");
            Assert.IsNotNull(hud.HealthFillImage,
                "no health fill image -- the bar could not change colour as health drops");
            Assert.IsNotNull(hud.LevelLabel, "no level label");
            Assert.IsNotNull(hud.Crosshair, "no crosshair -- it could not be hidden on death");
            Assert.IsNotNull(hud.DeathScreen, "no death screen -- dying would show nothing");
            Assert.IsNotNull(hud.RespawnButton, "no respawn button -- death would be unrecoverable");
            Assert.IsNotNull(hud.PortraitImage,
                "no portrait image -- CapturePortrait would have nothing to display its RenderTexture on");
            // The World of Warcraft style frame (2026-10-14): the name, the percentages, the mana bar and the pieces HudArt paints.
            Assert.IsNotNull(hud.NameLabel, "no name over the bars");
            Assert.IsNotNull(hud.HealthText, "no health percentage");
            Assert.IsNotNull(hud.ManaBar, "no mana bar");
            Assert.IsNotNull(hud.ManaFill, "no mana fill -- the mana bar would never move");
            Assert.IsNotNull(hud.ManaText, "no mana percentage");
            Assert.IsNotNull(hud.PortraitRing, "no ring round the portrait");
            Assert.IsNotNull(hud.PortraitRimShadow, "no shadow under the portrait's rim");
            Assert.IsNotNull(hud.Plate, "no plate behind the name and the bars");
            Assert.IsNotNull(hud.LevelRing, "no ring round the level");
            Assert.AreEqual(3, hud.BarWells.Length, "health, mana and stamina each have a well");
            Assert.AreEqual(3, hud.BarFills.Length, "health, mana and stamina each have a fill");
            Assert.IsTrue(hud.ManaBar.transform.IsChildOf(hud.Plate.transform), "the mana bar is not in the plate's column, so hiding it would leave a gap");
            // PortraitCamera itself is wired by PlayerHudBuilder.AddToScene,
            // not the prefab -- it needs a real player Transform to sit
            // under (see AddToScene's own BuildPortraitCamera), which the
            // prefab alone does not have.
        }

        /// <summary>
        /// If the death screen ships enabled, the game opens with a black
        /// overlay and a YOU DIED across it. It is on in the editor exactly
        /// long enough to be wired up and must be off when saved.
        /// </summary>
        [Test]
        public void TheDeathScreenStartsHidden()
        {
            PlayerHud hud = LoadHud();
            Assert.IsFalse(hud.DeathScreen.activeSelf,
                "the death screen is active in the prefab -- the game would start behind it");
        }

        /// <summary>
        /// Every Text needs a font object. Without one it renders nothing at
        /// all -- not a fallback, not a box, nothing -- and the only symptom is
        /// an empty rectangle where the label should be.
        /// </summary>
        [Test]
        public void EveryLabelHasAFont()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Text[] labels = prefab.GetComponentsInChildren<Text>(true);

            Assert.Greater(labels.Length, 0, "the HUD has no text at all");
            foreach (Text label in labels)
            {
                Assert.IsNotNull(label.font,
                    $"'{label.name}' has no font, so it will render nothing");
            }
        }

        /// <summary>
        /// A screen-space canvas needs a raycaster before any button on it can
        /// be clicked, and a scaler before it holds its proportions at another
        /// resolution.
        /// </summary>
        [Test]
        public void TheCanvasCanBeClickedAndScales()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            var canvas = prefab.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "no Canvas on the HUD");
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode,
                "the HUD canvas is not screen-space, so it would render in the world");

            Assert.IsNotNull(prefab.GetComponent<GraphicRaycaster>(),
                "no GraphicRaycaster -- the respawn button could never be clicked");

            var scaler = prefab.GetComponent<CanvasScaler>();
            Assert.IsNotNull(scaler, "no CanvasScaler");
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode,
                "the HUD is sized in raw pixels, so it shrinks to nothing on a large display");
        }

        /// <summary>
        /// The colour thresholds, checked as a pure function rather than by
        /// reading a pixel. These are the numbers from hud.gd.
        /// </summary>
        [Test]
        public void TheHealthColourFollowsItsThresholds()
        {
            Assert.AreEqual(PlayerHud.HealthGreen, PlayerHud.ColourForHealth(1.0f),
                "full health should be green");
            Assert.AreEqual(PlayerHud.HealthGreen, PlayerHud.ColourForHealth(0.76f),
                "just above the yellow threshold should still be green");
            Assert.AreEqual(PlayerHud.HealthYellow, PlayerHud.ColourForHealth(0.75f),
                "exactly at the yellow threshold should be yellow");
            Assert.AreEqual(PlayerHud.HealthYellow, PlayerHud.ColourForHealth(0.26f),
                "just above the red threshold should still be yellow");
            Assert.AreEqual(PlayerHud.HealthRed, PlayerHud.ColourForHealth(0.25f),
                "exactly at the red threshold should be red");
            Assert.AreEqual(PlayerHud.HealthRed, PlayerHud.ColourForHealth(0.0f),
                "dead should be red");
        }

        [Test]
        public void TheBarPercentagesReadLikeWoWs()
        {
            // 100 only when full and 0 only when empty: a scratch reads 99 and a sliver 1, so neither looks like nothing happened or like death.
            Assert.AreEqual(100, PlayerHud.PercentFor(1.0f));
            Assert.AreEqual(99, PlayerHud.PercentFor(0.999f));
            Assert.AreEqual(50, PlayerHud.PercentFor(0.5f));
            Assert.AreEqual(1, PlayerHud.PercentFor(0.001f));
            Assert.AreEqual(0, PlayerHud.PercentFor(0.0f));
        }

        [Test]
        public void TheFrameArtIsPaintedOnEveryPiece()
        {
            PlayerHud prefabHud = LoadHud();
            GameObject instance = Object.Instantiate(prefabHud.gameObject);
            try
            {
                var hud = instance.GetComponent<PlayerHud>();
                HudArt.Paint(hud);
                Assert.IsNotNull(hud.PortraitRing.sprite, "the portrait's ring was not painted");
                Assert.IsNotNull(hud.LevelRing.sprite, "the level's ring was not painted");
                Assert.IsNotNull(hud.PortraitRimShadow.sprite);
                Assert.AreEqual(Image.Type.Sliced, hud.Plate.type, "the plate must stretch by its edges, not squash its corners");
                Assert.AreNotEqual(Vector4.zero, hud.Plate.sprite.border, "the plate's sprite has no slicing border");
                foreach (Image well in hud.BarWells)
                {
                    Assert.IsNotNull(well.sprite, "a bar's well was not painted");
                }
                foreach (Image fill in hud.BarFills)
                {
                    Assert.AreSame(HudArt.BarFill, fill.sprite, "a bar's fill was not painted");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
