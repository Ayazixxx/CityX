using System.Linq;
using CityX.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CityX.Tests
{
    /// <summary>
    /// The title screen's rain, heard and seen: full on the menu, nothing on the
    /// character list and the creator (the belt in the sky).
    /// The muffled murmur (Indoors) still works, for whichever panel wants it.
    /// </summary>
    public sealed class TitleAmbienceTests
    {
        private const string ScenePath = "Assets/CityX/Scenes/Title.unity";

        private GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null)
            {
                Object.DestroyImmediate(_host);
            }
        }

        private TitleAmbience MakeAmbience(out AudioSource sound, out AudioLowPassFilter filter, out TitleRainLayer rain)
        {
            _host = new GameObject("Ambience", typeof(RectTransform));
            sound = _host.AddComponent<AudioSource>();
            sound.volume = TitleAmbience.OutdoorsVolume;
            filter = _host.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = TitleAmbience.OutdoorsCutoffHz;
            rain = new GameObject("Rain", typeof(RectTransform)).AddComponent<TitleRainLayer>();
            rain.transform.SetParent(_host.transform, false);

            var ambience = _host.AddComponent<TitleAmbience>();
            ambience.Sound = sound;
            ambience.Filter = filter;
            ambience.Rain = rain;
            return ambience;
        }

        // ---- which panel sounds like what ----

        [Test]
        public void EachPanelGetsItsOwnMood()
        {
            Assert.AreEqual(TitleAmbience.Mood.Outdoors, TitleAmbience.MoodFor(TitleScreen.Panel.Menu));
            Assert.AreEqual(TitleAmbience.Mood.Outdoors, TitleAmbience.MoodFor(TitleScreen.Panel.Settings));
            Assert.AreEqual(TitleAmbience.Mood.Outdoors, TitleAmbience.MoodFor(TitleScreen.Panel.Multiplayer));
            Assert.AreEqual(TitleAmbience.Mood.Silent, TitleAmbience.MoodFor(TitleScreen.Panel.CharacterSelect),
                "the character list, on the belt in the sky with the creator, still has weather in it");
            Assert.AreEqual(TitleAmbience.Mood.Silent, TitleAmbience.MoodFor(TitleScreen.Panel.Creator),
                "the creator still has weather in it");
        }

        [Test]
        public void IndoorsIsQuieterAndDullerThanOutdoorsButStillRain()
        {
            Assert.Less(TitleAmbience.VolumeFor(TitleAmbience.Mood.Indoors), TitleAmbience.VolumeFor(TitleAmbience.Mood.Outdoors),
                "indoors is not quieter");
            Assert.Greater(TitleAmbience.VolumeFor(TitleAmbience.Mood.Indoors), 0.1f, "indoors is silent, not muffled");
            Assert.Less(TitleAmbience.CutoffFor(TitleAmbience.Mood.Indoors), 2000.0f, "indoors is not muffled enough to notice");
            Assert.AreEqual(TitleAmbience.OutdoorsCutoffHz, TitleAmbience.CutoffFor(TitleAmbience.Mood.Outdoors),
                "the outdoor rain is being filtered");
            Assert.AreEqual(0.0f, TitleAmbience.VolumeFor(TitleAmbience.Mood.Silent));
        }

        // ---- easing there ----

        [Test]
        public void SteppingIndoorsFadesTheVolumeAndTheMuffleInInsteadOfSnapping()
        {
            TitleAmbience ambience = MakeAmbience(out AudioSource sound, out AudioLowPassFilter filter, out _);

            ambience.SetMood(TitleAmbience.Mood.Indoors);
            ambience.Advance(TitleAmbience.CoverFadeSeconds * 0.5f);

            Assert.Less(sound.volume, TitleAmbience.OutdoorsVolume, "the fade has not started");
            Assert.Greater(sound.volume, TitleAmbience.IndoorsVolume, "the volume jumped straight to indoors");

            // A cutoff is heard in octaves: halfway is the geometric middle, not the average in hertz.
            float geometricMiddle = Mathf.Sqrt(TitleAmbience.OutdoorsCutoffHz * TitleAmbience.IndoorsCutoffHz);
            Assert.AreEqual(geometricMiddle, filter.cutoffFrequency, geometricMiddle * 0.02f,
                "the muffle is not eased in octaves");

            ambience.Advance(TitleAmbience.CoverFadeSeconds);
            Assert.AreEqual(TitleAmbience.IndoorsVolume, sound.volume, 0.0001f);
            Assert.AreEqual(TitleAmbience.IndoorsCutoffHz, filter.cutoffFrequency, 0.5f);
        }

        [Test]
        public void EnteringTheCreatorFadesTheSoundOutToNothing()
        {
            TitleAmbience ambience = MakeAmbience(out AudioSource sound, out _, out _);
            ambience.SetMood(TitleAmbience.Mood.Indoors);
            ambience.Advance(TitleAmbience.CoverFadeSeconds);

            ambience.SetMood(TitleAmbience.Mood.Silent);
            ambience.Advance(TitleAmbience.SilenceFadeSeconds * 0.5f);
            Assert.Greater(sound.volume, 0.0f, "the sound cut out instead of fading");
            Assert.Less(sound.volume, TitleAmbience.IndoorsVolume, "the fade has not started");

            ambience.Advance(TitleAmbience.SilenceFadeSeconds);
            Assert.AreEqual(0.0f, sound.volume, 0.0001f, "the sound never finishes fading out");
        }

        [Test]
        public void TheRainComesBackWhenYouLeaveTheCreator()
        {
            TitleAmbience ambience = MakeAmbience(out AudioSource sound, out AudioLowPassFilter filter, out _);
            ambience.SetMood(TitleAmbience.Mood.Silent);
            ambience.Advance(TitleAmbience.SilenceFadeSeconds * 2.0f);

            ambience.SetMood(TitleAmbience.Mood.Outdoors);
            ambience.Advance(TitleAmbience.CoverFadeSeconds * 2.0f);

            Assert.AreEqual(TitleAmbience.OutdoorsVolume, sound.volume, 0.0001f, "the rain did not come back");
            Assert.AreEqual(TitleAmbience.OutdoorsCutoffHz, filter.cutoffFrequency, 1.0f, "the rain is still muffled");
        }

        [Test]
        public void TheStreaksAreHiddenOnlyWhileSilent()
        {
            TitleAmbience ambience = MakeAmbience(out _, out _, out TitleRainLayer rain);
            Assert.IsTrue(rain.enabled);

            ambience.SetMood(TitleAmbience.Mood.Silent);
            Assert.IsFalse(rain.enabled, "the rain is still drawn over the creator");

            ambience.SetMood(TitleAmbience.Mood.Indoors);
            Assert.IsTrue(rain.enabled, "the character list lost its rain");
        }

        // ---- the built scene ----

        private Scene OpenTitle()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            Assert.IsTrue(scene.IsValid(), $"no title scene at {ScenePath}");
            return scene;
        }

        private static T FindIn<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault();
        }

        [Test]
        public void TheTitleScreenDrivesTheAmbienceFromItsPanels()
        {
            Scene scene = OpenTitle();
            try
            {
                var title = FindIn<TitleScreen>(scene);
                Assert.IsNotNull(title.Ambience, "the title screen has no ambience to drive");
                Assert.IsNotNull(title.Ambience.Sound, "the ambience has no rain sound");
                Assert.IsNotNull(title.Ambience.Sound.clip, "the rain sound has no recording");
                Assert.IsNotNull(title.Ambience.Filter, "the ambience has nothing to muffle with");
                Assert.IsNotNull(title.Ambience.Rain, "the ambience cannot hide the streaks");

                title.Show(TitleScreen.Panel.CharacterSelect);
                Assert.AreEqual(TitleAmbience.Mood.Silent, title.Ambience.Current);
                Assert.IsFalse(title.Ambience.Rain.enabled, "the character list, on the belt in the sky, still draws rain");

                title.Show(TitleScreen.Panel.Creator);
                Assert.AreEqual(TitleAmbience.Mood.Silent, title.Ambience.Current);
                Assert.IsFalse(title.Ambience.Rain.enabled, "the creator still draws rain");

                title.Show(TitleScreen.Panel.Menu);
                Assert.AreEqual(TitleAmbience.Mood.Outdoors, title.Ambience.Current);
                Assert.IsTrue(title.Ambience.Rain.enabled);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
