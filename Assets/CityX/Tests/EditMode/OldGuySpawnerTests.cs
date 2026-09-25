using CityX.Character;
using CityX.Core;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// "he must never be standing there, fully visible, for the whole of
    /// the player's own fall" -- ported from main.gd's own
    /// _spawn_old_guy_when_playable. The round trip (does he actually
    /// appear once the intro cutscene really finishes) needs a live
    /// IntroCutscene run to completion to exercise honestly, which this
    /// suite does not attempt -- matching OldGuyTests' own established
    /// EditMode/PlayMode split for the same reason. What's pinned here is
    /// the reported bug itself: a new character must not see him spawn
    /// immediately.
    /// </summary>
    public sealed class OldGuySpawnerTests
    {
        private GameObject _prefab;
        private GameObject _spawnerObject;
        private GameObject _starterObject;

        [SetUp]
        public void SetUp()
        {
            // ACTIVE, deliberately -- Instantiate copies a prefab's own
            // active state onto the clone. A real prefab asset has no such
            // hidden gotcha since it isn't a live scene object to begin
            // with; this stand-in has to stay active to behave like one.
            _prefab = new GameObject("OldGuyPrefab");
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawnerObject != null)
            {
                // Destroy whatever THIS spawner made before the spawner
                // itself, not via a scene-wide GameObject.Find("OldGuy") --
                // see SpawnedInstance's own doc comment on why that lookup
                // can't be trusted to mean what it looks like it means.
                var spawner = _spawnerObject.GetComponent<OldGuySpawner>();
                if (spawner != null && spawner.SpawnedInstance != null)
                {
                    Object.DestroyImmediate(spawner.SpawnedInstance);
                }
                Object.DestroyImmediate(_spawnerObject);
            }
            if (_starterObject != null) Object.DestroyImmediate(_starterObject);
            Object.DestroyImmediate(_prefab);
            PlaySession.Character = null;
            PlaySession.IsNewCharacter = false;
        }

        /// <summary>
        /// PlaySession has to be set BEFORE AddComponent, not after -- a
        /// real running game captures it in Awake() (see that class's own
        /// doc comment on why). This EditMode test harness itself does NOT
        /// reliably run Awake() from a plain AddComponent call inside a
        /// synchronous [Test] method (confirmed directly -- it simply never
        /// fired here), which is exactly why OldGuySpawner also captures the
        /// same flag, guarded, inside Bind() itself: setting PlaySession
        /// before this call still matters, since Bind() is what actually
        /// ends up doing the capturing in this test context.
        /// </summary>
        private OldGuySpawner MakeSpawner()
        {
            _spawnerObject = new GameObject("Spawner");
            var spawner = _spawnerObject.AddComponent<OldGuySpawner>();
            spawner.OldGuyPrefab = _prefab;
            return spawner;
        }

        [Test]
        public void SpawnsImmediatelyWhenThereIsNoPlaySessionAtAll()
        {
            PlaySession.Character = null;
            OldGuySpawner spawner = MakeSpawner();

            spawner.Bind();

            Assert.IsNotNull(spawner.SpawnedInstance,
                "with no PlaySession at all, Old Guy should spawn immediately");
        }

        [Test]
        public void SpawnsImmediatelyForALoadedCharacter()
        {
            PlaySession.Character = new CharacterSave();
            PlaySession.IsNewCharacter = false;
            OldGuySpawner spawner = MakeSpawner();

            spawner.Bind();

            Assert.IsNotNull(spawner.SpawnedInstance, "a loaded character has no cutscene to stay out of");
        }

        /// <summary>THE ACTUAL REPORTED BUG: he must not be there for the fall.</summary>
        [Test]
        public void DoesNotSpawnImmediatelyForANewCharacterEnteringTheIntroCutscene()
        {
            PlaySession.Character = new CharacterSave();
            PlaySession.IsNewCharacter = true;
            OldGuySpawner spawner = MakeSpawner();

            _starterObject = new GameObject("Starter");
            spawner.IntroStarter = _starterObject.AddComponent<IntroCutsceneStarter>();

            spawner.Bind();

            Assert.IsNull(spawner.SpawnedInstance,
                "a new character's own falling entrance must not have Old Guy visible in it");
        }

        [Test]
        public void SpawnsImmediatelyForANewCharacterIfNoIntroStarterIsWired()
        {
            PlaySession.Character = new CharacterSave();
            PlaySession.IsNewCharacter = true;
            OldGuySpawner spawner = MakeSpawner();
            spawner.IntroStarter = null;

            spawner.Bind();

            Assert.IsNotNull(spawner.SpawnedInstance,
                "with nothing ever going to signal completion, spawning now beats never spawning at all");
        }
    }
}
