using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Open/Close don't touch anything Awake creates -- they only flip
    /// public fields and engine globals -- so this reaches them directly
    /// rather than needing a Bind() the way Radio's Awake-built AudioSource
    /// did.
    /// </summary>
    public sealed class PauseMenuViewTests
    {
        private GameObject _object;
        private PauseMenuView _view;
        private GameObject _dim;
        private float _timeScaleBefore;
        private CursorLockMode _lockStateBefore;
        private bool _cursorVisibleBefore;

        [SetUp]
        public void SetUp()
        {
            _timeScaleBefore = Time.timeScale;
            _lockStateBefore = Cursor.lockState;
            _cursorVisibleBefore = Cursor.visible;

            _object = new GameObject("PauseMenu");
            _view = _object.AddComponent<PauseMenuView>();
            _dim = new GameObject("Dim");
            _dim.SetActive(false);
            _view.Dim = _dim;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            Object.DestroyImmediate(_dim);
            Time.timeScale = _timeScaleBefore;
            Cursor.lockState = _lockStateBefore;
            Cursor.visible = _cursorVisibleBefore;
        }

        [Test]
        public void OpeningShowsTheOverlayAndFreezesTime()
        {
            _view.Open();

            Assert.IsTrue(_view.IsOpen);
            Assert.AreEqual(0.0f, Time.timeScale, 0.001f);
            Assert.IsTrue(Cursor.visible);
        }

        [Test]
        public void ClosingHidesTheOverlayAndUnfreezesTime()
        {
            _view.Open();

            _view.Close();

            Assert.IsFalse(_view.IsOpen);
            Assert.AreEqual(1.0f, Time.timeScale, 0.001f);
        }

        // NOT HEADLESSLY TESTABLE: whether Close() restores a Locked cursor.
        // Cursor.lockState does not actually latch to Locked under
        // -batchmode -nographics -- there is no real window for the OS to
        // confine the pointer to -- so a test that sets it to Locked and
        // checks it stuck would be asserting on this environment's own
        // limitation, not on PauseMenuView. The restore-conditionally logic
        // (Open remembers whether the cursor was captured, Close only
        // re-locks it if it was) needs a real windowed Play session to
        // verify, the same category of gap the intro cutscene's own "cannot
        // be verified headlessly" list already carries.

        [Test]
        public void ClosingLeavesAnAlreadyFreeCursorAlone()
        {
            Cursor.lockState = CursorLockMode.None;

            _view.Open();
            _view.Close();

            Assert.AreEqual(CursorLockMode.None, Cursor.lockState);
        }
    }
}
