using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// What the HUD actually does with a body over real frames.
    ///
    /// Built by hand rather than by loading the prefab, so these test the
    /// component's own behaviour and fail for one reason. The prefab's wiring
    /// is PlayerHudTests' job.
    /// </summary>
    public class PlayerHudPlayTests
    {
        private GameObject _hudObject;
        private GameObject _bodyObject;
        private CursorLockMode _lockStateBefore;
        private bool _cursorVisibleBefore;

        [SetUp]
        public void SetUp()
        {
            _lockStateBefore = Cursor.lockState;
            _cursorVisibleBefore = Cursor.visible;
        }

        [TearDown]
        public void TearDown()
        {
            if (_hudObject != null) Object.DestroyImmediate(_hudObject);
            if (_bodyObject != null) Object.DestroyImmediate(_bodyObject);
            Cursor.lockState = _lockStateBefore;
            Cursor.visible = _cursorVisibleBefore;
        }

        private PlayerHud MakeHud(out PlayerVitals vitals)
        {
            _bodyObject = new GameObject("Body");
            vitals = _bodyObject.AddComponent<PlayerVitals>();

            _hudObject = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            var hud = _hudObject.AddComponent<PlayerHud>();

            hud.HealthFill = MakeChild("HealthFill", out Image healthImage);
            hud.HealthFillImage = healthImage;
            hud.StaminaFill = MakeChild("StaminaFill", out _);

            var label = new GameObject("Level", typeof(RectTransform));
            label.transform.SetParent(_hudObject.transform, false);
            hud.LevelLabel = label.AddComponent<Text>();

            hud.DeathScreen = new GameObject("DeathScreen");
            hud.DeathScreen.transform.SetParent(_hudObject.transform, false);
            hud.DeathScreen.SetActive(false);

            hud.Crosshair = new GameObject("Crosshair");
            hud.Crosshair.transform.SetParent(_hudObject.transform, false);

            var buttonObject = new GameObject("Respawn", typeof(RectTransform));
            buttonObject.transform.SetParent(_hudObject.transform, false);
            hud.RespawnButton = buttonObject.AddComponent<Button>();

            hud.Target = vitals;
            return hud;
        }

        private RectTransform MakeChild(string name, out Image image)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_hudObject.transform, false);
            image = go.AddComponent<Image>();
            return (RectTransform)go.transform;
        }

        [UnityTest]
        public IEnumerator TheHealthBarShrinksInProportionToDamage()
        {
            PlayerHud hud = MakeHud(out PlayerVitals vitals);
            yield return null;

            Assert.AreEqual(1.0f, hud.HealthFill.anchorMax.x, 0.001f,
                "a full health bar should span its whole well");

            vitals.TakeDamage(vitals.MaxHealth * 0.5f);
            yield return null;

            Assert.AreEqual(0.5f, hud.HealthFill.anchorMax.x, 0.001f,
                "half health should be half a bar");

            vitals.TakeDamage(vitals.MaxHealth);
            yield return null;

            Assert.AreEqual(0.0f, hud.HealthFill.anchorMax.x, 0.001f,
                "an empty bar should be empty, not negative or wrapped around");
        }

        [UnityTest]
        public IEnumerator TheBarChangesColourAsHealthDrops()
        {
            PlayerHud hud = MakeHud(out PlayerVitals vitals);
            yield return null;

            Assert.AreEqual(PlayerHud.HealthGreen, hud.HealthFillImage.color);

            vitals.TakeDamage(vitals.MaxHealth * 0.5f);
            yield return null;
            Assert.AreEqual(PlayerHud.HealthYellow, hud.HealthFillImage.color,
                "half health should read as yellow");

            vitals.TakeDamage(vitals.MaxHealth * 0.4f);
            yield return null;
            Assert.AreEqual(PlayerHud.HealthRed, hud.HealthFillImage.color,
                "a tenth of health left should read as red");
        }

        [UnityTest]
        public IEnumerator SprintingDrainsTheStaminaBar()
        {
            PlayerHud hud = MakeHud(out PlayerVitals vitals);
            yield return null;
            Assert.AreEqual(1.0f, hud.StaminaFill.anchorMax.x, 0.001f);

            // A real second of sprinting, stepped rather than measured in
            // frames -- batch mode runs far faster than realtime, so counting
            // frames would drain almost nothing.
            for (int i = 0; i < 60; i++)
            {
                vitals.TickStamina(true, 1.0f / 60.0f);
            }
            yield return null;

            float expected = (vitals.MaxStamina - PlayerVitals.StaminaDrainPerSecond) / vitals.MaxStamina;
            Assert.AreEqual(expected, hud.StaminaFill.anchorMax.x, 0.01f,
                "the stamina bar does not match what the body actually has left");
        }

        [UnityTest]
        public IEnumerator TheDeathScreenAppearsOnDeathAndClearsOnRespawn()
        {
            PlayerHud hud = MakeHud(out PlayerVitals vitals);
            yield return null;

            Assert.IsFalse(hud.DeathScreen.activeSelf, "the death screen should start hidden");

            Assert.IsTrue(hud.Crosshair.activeSelf, "the crosshair should be up while alive");

            vitals.TakeDamage(vitals.MaxHealth);
            yield return null;
            Assert.IsTrue(hud.DeathScreen.activeSelf, "dying did not raise the death screen");
            Assert.IsFalse(hud.Crosshair.activeSelf,
                "the crosshair stayed up over the death screen, between YOU DIED and the button");

            vitals.Revive();
            yield return null;
            Assert.IsFalse(hud.DeathScreen.activeSelf,
                "the death screen stayed up after respawning");
            Assert.IsTrue(hud.Crosshair.activeSelf,
                "respawning did not bring the crosshair back");
        }

        /// <summary>
        /// "MAKE IT SO WHEN YOU DIE YOU CAN CLICK RESPAWN" -- a real reported
        /// bug: the button itself was always genuinely wired (see
        /// PressingRespawnBringsTheBodyBack), but third-person play keeps the
        /// cursor locked and invisible the whole time, so there was no
        /// pointer on screen to actually click it with. Only the unlock-on-
        /// death direction is asserted -- Cursor.lockState does not reliably
        /// latch back to Locked under headless batch mode (no real window
        /// for the OS to confine the pointer to), the same gotcha
        /// PauseMenuViewTests' own class comment already documents.
        /// </summary>
        [UnityTest]
        public IEnumerator DyingFreesTheCursorSoTheRespawnButtonCanActuallyBeClicked()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            PlayerHud hud = MakeHud(out PlayerVitals vitals);
            yield return null;

            vitals.TakeDamage(vitals.MaxHealth);
            yield return null;

            Assert.AreEqual(CursorLockMode.None, Cursor.lockState,
                "dying should free the cursor so the respawn button can be clicked");
            Assert.IsTrue(Cursor.visible, "dying should make the cursor visible again");
        }

        [UnityTest]
        public IEnumerator PressingRespawnBringsTheBodyBack()
        {
            PlayerHud hud = MakeHud(out PlayerVitals vitals);
            yield return null;

            vitals.TakeDamage(vitals.MaxHealth);
            yield return null;
            Assert.IsTrue(vitals.IsDead);

            hud.RespawnButton.onClick.Invoke();
            yield return null;

            Assert.IsFalse(vitals.IsDead, "the respawn button did not revive the body");
            Assert.AreEqual(vitals.MaxHealth, vitals.Health, 0.001f,
                "respawning should restore full health");
            Assert.IsFalse(hud.DeathScreen.activeSelf);
        }

        /// <summary>
        /// The leak this guards is specific: a HUD that keeps listening to a
        /// body it no longer shows will raise its death screen when that other
        /// body dies, over a player who is perfectly alive.
        /// </summary>
        [UnityTest]
        public IEnumerator RepointingTheHudStopsItListeningToTheOldBody()
        {
            PlayerHud hud = MakeHud(out PlayerVitals first);
            yield return null;

            var secondObject = new GameObject("SecondBody");
            var second = secondObject.AddComponent<PlayerVitals>();
            try
            {
                hud.Target = second;
                yield return null;

                first.TakeDamage(first.MaxHealth);
                yield return null;

                Assert.IsTrue(first.IsDead, "the first body should have died");
                Assert.IsFalse(hud.DeathScreen.activeSelf,
                    "the HUD raised its death screen for a body it no longer shows");

                second.TakeDamage(second.MaxHealth);
                yield return null;
                Assert.IsTrue(hud.DeathScreen.activeSelf,
                    "the HUD did not react to the body it was repointed at");
            }
            finally
            {
                Object.DestroyImmediate(secondObject);
            }
        }

        [UnityTest]
        public IEnumerator TheLevelBadgeShowsTheBodysLevel()
        {
            PlayerHud hud = MakeHud(out PlayerVitals vitals);
            yield return null;

            Assert.AreEqual("1", hud.LevelLabel.text, "a fresh character should read as level 1");

            vitals.Level = 7;
            yield return null;
            Assert.AreEqual("7", hud.LevelLabel.text, "the badge did not follow the level");
        }

        /// <summary>
        /// A HUD with nothing to show must sit there, not throw. It is added to
        /// the scene before the player exists in every spawn path there will
        /// ever be.
        /// </summary>
        [UnityTest]
        public IEnumerator AHudWithNoTargetDoesNothingRatherThanThrowing()
        {
            PlayerHud hud = MakeHud(out PlayerVitals _);
            hud.Target = null;
            yield return null;
            yield return null;

            Assert.Pass("survived two frames with no body to read");
        }

        private Text MakeText(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_hudObject.transform, false);
            return go.AddComponent<Text>();
        }

        /// <summary>
        /// The mana bar (owner, 2026-10-14: "figure out a way to include a mana bar ... make it so the mana bar doesnt appear until you unlock a
        /// spell. but i guess for now just have it on screen from the beginning"): it shows the body's mana and its percentage, and it is on
        /// screen only while the body has mana at all.
        /// </summary>
        [UnityTest]
        public IEnumerator TheManaBarFollowsManaAndShowsOnlyWhileTheBodyHasMana()
        {
            PlayerHud hud = MakeHud(out PlayerVitals vitals);
            hud.ManaFill = MakeChild("ManaFill", out _);
            hud.ManaBar = hud.ManaFill.gameObject;
            hud.ManaText = MakeText("ManaText");
            yield return null;
            Assert.IsTrue(hud.ManaBar.activeSelf, "for now the mana bar is on screen from the start");
            Assert.AreEqual(1.0f, hud.ManaFill.anchorMax.x, 0.001f);
            Assert.AreEqual("100%", hud.ManaText.text);

            Assert.IsTrue(vitals.TrySpendMana(vitals.MaxMana * 0.5f));
            yield return null;
            Assert.AreEqual(0.5f, hud.ManaFill.anchorMax.x, 0.001f, "the mana bar does not show what is left");
            Assert.AreEqual("50%", hud.ManaText.text);

            vitals.ManaUnlocked = false;
            yield return null;
            Assert.IsFalse(hud.ManaBar.activeSelf, "the mana bar is on screen for a body with no mana yet");
            vitals.ManaUnlocked = true;
            yield return null;
            Assert.IsTrue(hud.ManaBar.activeSelf, "the mana bar did not come back once the body had mana");
        }

        [UnityTest]
        public IEnumerator TheHealthBarShowsItsPercentageAndTheFrameTheCharactersName()
        {
            PlayerHud hud = MakeHud(out PlayerVitals vitals);
            hud.HealthText = MakeText("HealthText");
            hud.NameLabel = MakeText("NameLabel");
            yield return null;
            Assert.AreEqual("100%", hud.HealthText.text);
            Assert.AreEqual(PlayerHud.DefaultName, hud.NameLabel.text, "a body that never went through the creator shows the default name");

            vitals.TakeDamage(vitals.MaxHealth * 0.25f, false);
            yield return null;
            Assert.AreEqual("75%", hud.HealthText.text);

            var identity = _bodyObject.AddComponent<CharacterIdentity>();
            identity.Apply(CharacterSave.Create("Flours", Color.white));
            yield return null;
            Assert.AreEqual("Flours", hud.NameLabel.text, "the frame does not show the character's own name");
        }
    }
}
