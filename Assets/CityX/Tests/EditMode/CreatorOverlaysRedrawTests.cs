using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// CreatorOverlays' redraw-only surface: SaveRedraw, and Discard's
    /// branch to OnCancelRedraw instead of Title.Show(CharacterSelect) when
    /// there is no title screen to go back to. See RedrawCreatorRuntime's
    /// own doc comment for why ReDraw builds this component itself rather
    /// than through CharacterCreatorBuilder (editor-only, cannot run during
    /// play) -- Title stays null for the whole life of a redraw session,
    /// which is exactly the signal Discard() branches on.
    ///
    /// Awake() never runs in this EditMode context (matches every other
    /// Awake-wired panel's own tests in this project), so CharacterCreator's
    /// own Initialise()/Disarm() never fires here -- harmless, since none of
    /// the methods under test touch Paint at all.
    /// </summary>
    public sealed class CreatorOverlaysRedrawTests
    {
        private GameObject _object;
        private CreatorOverlays _overlays;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Overlays");
            _overlays = _object.AddComponent<CreatorOverlays>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void SaveRedrawFiresOnConfirmRedrawWithTheCreator()
        {
            var creatorObject = new GameObject("Creator");
            CharacterCreator creator = creatorObject.AddComponent<CharacterCreator>();
            _overlays.Creator = creator;

            CharacterCreator received = null;
            _overlays.OnConfirmRedraw = c => received = c;

            _overlays.SaveRedraw();

            Assert.AreSame(creator, received);
            Object.DestroyImmediate(creatorObject);
        }

        [Test]
        public void SaveRedrawWithNoCreatorDoesNothing()
        {
            bool fired = false;
            _overlays.OnConfirmRedraw = _ => fired = true;

            _overlays.SaveRedraw();

            Assert.IsFalse(fired);
        }

        [Test]
        public void DiscardWithNoTitleFiresOnCancelRedrawInstead()
        {
            bool cancelled = false;
            _overlays.OnCancelRedraw = () => cancelled = true;

            _overlays.Discard();

            Assert.IsTrue(cancelled);
        }

        [Test]
        public void TryGoBackWithNoEditsGoesStraightToDiscardAndFiresOnCancelRedraw()
        {
            bool cancelled = false;
            _overlays.OnCancelRedraw = () => cancelled = true;

            _overlays.TryGoBack();

            Assert.IsTrue(cancelled,
                "with no Creator wired (so no edits that could be lost) TryGoBack should discard immediately");
        }
    }
}
