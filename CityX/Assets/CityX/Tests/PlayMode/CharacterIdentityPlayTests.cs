using System.Collections;
using System.IO;
using CityX.Character;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The chosen character actually reaching the body in the world, and what
    /// they earned getting back to disk.
    ///
    /// This is the join between the menu and the game, and it is the kind of
    /// seam that looks fine in both halves and does nothing in the middle: a
    /// character that loads with the right name and the wrong level, or a
    /// session whose playtime never gets written, both look exactly like
    /// working software until somebody checks.
    /// </summary>
    public class CharacterIdentityPlayTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CityXPlayTests",
                Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            CharacterStore.DirectoryOverride = _directory;
            PlaySession.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            PlaySession.Clear();
            CharacterStore.DirectoryOverride = ScratchDataFixture.Folder;
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        private static GameObject MakeBody()
        {
            var body = new GameObject("Body", typeof(PlayerVitals), typeof(CharacterIdentity));
            return body;
        }

        [UnityTest]
        public IEnumerator ChoosingACharacterGivesTheBodyItsLevelAndStats()
        {
            CharacterSave chosen = CharacterSave.Create("Ayaz", Color.red);
            chosen.Level = 9;
            chosen.Vitality = 4;
            chosen.Endurance = 2;
            chosen.Strength = 6;
            CharacterStore.Add(chosen);
            PlaySession.Begin(chosen, isNew: false);

            GameObject body = MakeBody();
            yield return null;

            var vitals = body.GetComponent<PlayerVitals>();
            Assert.AreEqual(9, vitals.Level);
            Assert.AreEqual(4, vitals.Vitality);
            Assert.AreEqual(6, vitals.Strength);

            Object.Destroy(body);
        }

        /// <summary>
        /// STAT POINTS MOVE THE CEILINGS, so the bars have to be refilled
        /// against the new maximums. Without that a character with points in
        /// Vitality spawns on a fraction of their health bar, which reads as
        /// arriving in the world already hurt.
        /// </summary>
        [UnityTest]
        public IEnumerator ACharacterWithStatPointsSpawnsOnFullHealthNotAFraction()
        {
            CharacterSave tough = CharacterSave.Create("Tough", Color.white);
            tough.Vitality = 10;
            PlaySession.Begin(tough, isNew: false);

            GameObject body = MakeBody();
            yield return null;

            var vitals = body.GetComponent<PlayerVitals>();
            Assert.Greater(vitals.MaxHealth, PlayerVitals.BaseMaxHealth,
                "the stat points did not raise the ceiling at all");
            Assert.AreEqual(vitals.MaxHealth, vitals.Health, 0.001f,
                "the character arrived already hurt");

            Object.Destroy(body);
        }

        /// <summary>
        /// Opening the world directly is how it is worked on nearly all of the
        /// time. A component that needs a chosen character would break that.
        /// </summary>
        [UnityTest]
        public IEnumerator WithNoChosenCharacterTheBodyStillWorksAndNothingIsSaved()
        {
            GameObject body = MakeBody();
            yield return null;

            var identity = body.GetComponent<CharacterIdentity>();
            Assert.IsNull(identity.Character);
            Assert.IsFalse(identity.Write(), "it saved something with nobody playing");
            Assert.IsEmpty(CharacterStore.Load());

            Object.Destroy(body);
        }

        [UnityTest]
        public IEnumerator PlayingAccruesPlaytimeAndWritesItBack()
        {
            CharacterSave chosen = CharacterSave.Create("Clocker", Color.white);
            CharacterStore.Add(chosen);
            PlaySession.Begin(chosen, isNew: false);

            GameObject body = MakeBody();
            var identity = body.GetComponent<CharacterIdentity>();

            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.Greater(identity.Character.PlaytimeSeconds, 0.0,
                "no playtime accrued over ten frames");

            Assert.IsTrue(identity.Write());
            Assert.Greater(CharacterStore.Load()[0].PlaytimeSeconds, 0.0,
                "the playtime never reached disk");

            Object.Destroy(body);
        }

        /// <summary>
        /// Levelling up mid-session has to survive the session. Writing back
        /// only what the menu knew would silently discard everything earned.
        /// </summary>
        [UnityTest]
        public IEnumerator LevellingUpDuringPlayIsWrittenBack()
        {
            CharacterSave chosen = CharacterSave.Create("Climber", Color.white);
            CharacterStore.Add(chosen);
            PlaySession.Begin(chosen, isNew: false);

            GameObject body = MakeBody();
            yield return null;

            body.GetComponent<PlayerVitals>().Level = 15;
            body.GetComponent<CharacterIdentity>().Write();

            Assert.AreEqual(15, CharacterStore.Load()[0].Level);

            Object.Destroy(body);
        }

        [UnityTest]
        public IEnumerator TheSessionSurvivesBeingReadTwiceAndClearsOnDemand()
        {
            CharacterSave chosen = CharacterSave.Create("Persistent", Color.white);
            PlaySession.Begin(chosen, isNew: true);

            Assert.AreSame(chosen, PlaySession.Character);
            Assert.IsTrue(PlaySession.IsNewCharacter);

            PlaySession.Clear();
            Assert.IsNull(PlaySession.Character);
            Assert.IsFalse(PlaySession.IsNewCharacter);
            yield return null;
        }
    }
}
