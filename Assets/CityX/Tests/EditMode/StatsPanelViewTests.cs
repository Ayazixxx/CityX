using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// StatsPanelView driven through its own public Open/Close, the same
    /// reasoning LoadoutPanelViewTests already uses -- Awake wires the "+"
    /// button listeners against whatever Button fields are assigned AT THE
    /// MOMENT AddComponent runs (none, here, since the real buttons are
    /// assigned afterward), so a simulated click would silently reach no
    /// listener. Spend-on-click is exercised at the PlayerVitals level
    /// instead (see PlayerVitalsProgressionTests). What IS tested here is
    /// Open/Close's own state transitions and the Refresh() read path
    /// Open() drives.
    /// </summary>
    public sealed class StatsPanelViewTests
    {
        private GameObject _bodyObject;
        private PlayerVitals _vitals;
        private PlayerInputReader _inputReader;
        private GameObject _cameraObject;
        private ThirdPersonCamera _orbitCamera;
        private GameObject _panelRoot;
        private StatsPanelView _stats;

        [SetUp]
        public void SetUp()
        {
            _bodyObject = new GameObject("Player", typeof(CharacterController));
            _inputReader = _bodyObject.AddComponent<PlayerInputReader>();
            _vitals = _bodyObject.AddComponent<PlayerVitals>();

            _cameraObject = new GameObject("Camera");
            _orbitCamera = _cameraObject.AddComponent<ThirdPersonCamera>();
            _orbitCamera.Target = _bodyObject.transform;

            _panelRoot = new GameObject("StatsRoot", typeof(RectTransform));
            // Same reasoning as PlayerMenuViewTests' own menuRoot -- a
            // freshly created GameObject defaults to active, and Awake's
            // own Close() call is a no-op here (Root is still null at the
            // moment AddComponent runs it), so this stands in for it.
            _panelRoot.SetActive(false);
            _stats = _panelRoot.AddComponent<StatsPanelView>();
            _stats.Root = _panelRoot;
            _stats.Vitals = _vitals;
            _stats.InputReader = _inputReader;
            _stats.OrbitCamera = _orbitCamera;

            _stats.LevelLabel = MakeText();
            _stats.ExperienceLabel = MakeText();
            _stats.PointsLabel = MakeText();
            _stats.VitalityLabel = MakeText();
            _stats.EnduranceLabel = MakeText();
            _stats.StrengthLabel = MakeText();

            var barFill = new GameObject("Fill", typeof(RectTransform));
            barFill.transform.SetParent(_panelRoot.transform, false);
            _stats.ExperienceBarFill = (RectTransform)barFill.transform;
        }

        private Text MakeText()
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(_panelRoot.transform, false);
            return go.AddComponent<Text>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_bodyObject);
            Object.DestroyImmediate(_cameraObject);
            Object.DestroyImmediate(_panelRoot);
        }

        [Test]
        public void OpenShowsTheRootAndDisablesInputAndOrbitCamera()
        {
            _stats.Open();

            Assert.IsTrue(_stats.IsOpen);
            Assert.IsFalse(_inputReader.enabled);
            Assert.IsFalse(_orbitCamera.enabled);
        }

        [Test]
        public void CloseHidesTheRootAndReenablesInputAndOrbitCamera()
        {
            _stats.Open();
            _stats.Close();

            Assert.IsFalse(_stats.IsOpen);
            Assert.IsTrue(_inputReader.enabled);
            Assert.IsTrue(_orbitCamera.enabled);
        }

        [Test]
        public void OpeningTwiceInARowIsHarmlessAndDoesNotReenableInputEarly()
        {
            _stats.Open();
            _inputReader.enabled = false; // simulate whatever Open() already set
            _stats.Open();

            Assert.IsTrue(_stats.IsOpen);
            Assert.IsFalse(_inputReader.enabled, "a redundant Open() must not disturb the already-disabled input");
        }

        [Test]
        public void OpenRefreshesLevelAndStatLabelsFromVitals()
        {
            _vitals.AddXp(PlayerVitals.XpPerLevelBase);
            _vitals.SpendStatPoint("strength");

            _stats.Open();

            StringAssert.Contains("2", _stats.LevelLabel.text);
            StringAssert.Contains("1", _stats.StrengthLabel.text);
        }

        [Test]
        public void OpenRefreshesThePointsLabelFromStatPoints()
        {
            _vitals.AddXp(PlayerVitals.XpPerLevelBase);

            _stats.Open();

            StringAssert.Contains(PlayerVitals.StatPointsPerLevel.ToString(), _stats.PointsLabel.text);
        }

        [Test]
        public void OpenScalesTheExperienceBarToTheCurrentFraction()
        {
            _vitals.AddXp(PlayerVitals.XpPerLevelBase / 2);

            _stats.Open();

            Assert.AreEqual(0.5f, _stats.ExperienceBarFill.anchorMax.x, 0.001f);
        }
    }
}
