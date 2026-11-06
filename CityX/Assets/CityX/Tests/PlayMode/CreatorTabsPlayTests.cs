using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The creator's PAINT, HAIR and FACE tabs (docs/CHARACTER_CREATOR.md), in the real title scene and with a real mouse: a tab puts up its own
    /// page and no other, the body takes paint only on PAINT (on the others a drag on it turns it, as a drag beside it does), the brush ring
    /// follows the mouse only there, and the wheel over the hairstyles leaves the camera alone while over the body it still zooms.
    ///
    /// THE SCENE IS OPENED INSIDE EACH TEST, NOT IN A [UnitySetUp]. That runs before InputTestFixture's own [SetUp], so the scene's event
    /// system would wake on the input system the fixture then swaps out, and never see this mouse.
    /// </summary>
    public sealed class CreatorTabsPlayTests : InputTestFixture
    {
        /// <summary>The tabs and their pages, in CreatorTabs order (Paint, Hair, Face).</summary>
        private static readonly string[] TabNames = { "PaintTab", "HairTab", "FaceTab" };
        private static readonly string[] PageNames = { "PaintPage", "HairPage", "FacePage" };

        /// <summary>One notch of the wheel as Windows reports it; the creator only reads which way it turned.</summary>
        private const float WheelNotch = 120.0f;

        private Mouse _mouse;
        private CharacterCreator _creator;
        private CreatorTabs _tabs;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
            // An event system left behind by another test reads the input system this fixture has just swapped out, and as the first one
            // switched on it would stay the current one: the title scene's own would never see this mouse.
            foreach (EventSystem leftover in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(leftover.gameObject);
            }
        }

        [UnityTearDown]
        public IEnumerator CloseTheCreator()
        {
            Scene title = SceneManager.GetSceneByPath(CharacterCreatorPlayTests.ScenePath);
            if (title.IsValid() && title.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(title);
            }
        }

        /// <summary>The title scene with the creator showing, opened as CharacterCreatorPlayTests opens it.</summary>
        private IEnumerator Open()
        {
            yield return CharacterCreatorPlayTests.LoadTitle();
            var title = Object.FindFirstObjectByType<TitleScreen>();
            Assert.IsNotNull(title, "the title scene has no TitleScreen");
            title.Show(TitleScreen.Panel.Creator);
            yield return null;

            _creator = Object.FindFirstObjectByType<CharacterCreator>();
            Assert.IsNotNull(_creator, "showing the creator did not switch its stage on");
            _tabs = Object.FindFirstObjectByType<CreatorTabs>();
            Assert.IsNotNull(_tabs, "the creator has no tabs");
            Assert.IsNotNull(EventSystem.current, "the title scene has no event system to click with");
            // The scene's module comes saved with its actions bound before this test's mouse existed; bound afresh, it reads that mouse.
            var module = EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Assert.IsNotNull(module, "the title scene's event system has no Input System module");
            module.AssignDefaultActions();
            yield return null;
        }

        /// <summary>A part of the creator's panel by name (the tabs sit on the panel itself).</summary>
        private Transform Find(string name)
        {
            Transform found = TransformSearch.FindDeep(_tabs.transform, name);
            Assert.IsNotNull(found, $"the creator has no {name}");
            return found;
        }

        /// <summary>
        /// The mouse moved to <paramref name="screen"/>, and two frames: the event system and the creator both read it in their Update, in no
        /// fixed order, so after one frame the creator may still see the interface that was under the mouse before.
        /// </summary>
        private IEnumerator MoveTo(Vector2 screen)
        {
            Move(_mouse.position, screen);
            yield return null;
            yield return null;
        }

        /// <summary>A left click on the middle of the creator's <paramref name="name"/>: an event a frame.</summary>
        private IEnumerator Click(string name)
        {
            yield return MoveTo(CharacterCreatorPlayTests.ScreenCentre((RectTransform)Find(name)));
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
            yield return null;
        }

        /// <summary>The left button pressed at <paramref name="from"/>, the mouse moved by <paramref name="by"/> with it held, then let go.</summary>
        private IEnumerator DragFrom(Vector2 from, Vector2 by)
        {
            yield return MoveTo(from);
            Press(_mouse.leftButton);
            yield return null;
            Move(_mouse.position, from + by, by);   // with its delta, which the body turns by
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
        }

        /// <summary>One notch of the wheel rolled away, which zooms in, wherever the mouse is.</summary>
        private IEnumerator WheelIn()
        {
            Set(_mouse.scroll, new Vector2(0.0f, WheelNotch));
            yield return null;
            yield return null;
        }

        /// <summary>The creator opens on PAINT; each tab, clicked, puts up its own page alone and lights its own caption.</summary>
        [UnityTest]
        public IEnumerator EachTabShowsOnlyItsOwnPageAndLightsItsLabel()
        {
            yield return Open();
            AssertUp(CreatorTabs.Paint, "on opening");
            foreach (int tab in new[] { CreatorTabs.Hair, CreatorTabs.Face, CreatorTabs.Paint })
            {
                yield return Click(TabNames[tab]);
                AssertUp(tab, $"after a click on {TabNames[tab]}");
            }
        }

        /// <summary>Only <paramref name="tab"/>'s page is seen and takes clicks, and only its caption is lit.</summary>
        private void AssertUp(int tab, string when)
        {
            Assert.AreEqual(tab, _tabs.Current, $"{when}, {TabNames[_tabs.Current]} is up");
            Color lit = Caption(tab).color;
            for (int i = 0; i < PageNames.Length; i++)
            {
                CanvasGroup page = Find(PageNames[i]).GetComponent<CanvasGroup>();
                Assert.IsNotNull(page, $"{PageNames[i]} has no CanvasGroup to be hidden by");
                bool up = i == tab;
                Assert.AreEqual(up ? 1.0f : 0.0f, page.alpha, $"{when}, {PageNames[i]} is {(up ? "hidden" : "showing as well")}");
                Assert.AreEqual(up, page.blocksRaycasts, $"{when}, {PageNames[i]} {(up ? "takes no clicks" : "still takes clicks")}");
                Assert.AreEqual(up, page.interactable, $"{when}, {PageNames[i]} {(up ? "cannot be used" : "can still be used")}");
                if (!up)
                {
                    Assert.AreNotEqual(lit, Caption(i).color, $"{when}, {TabNames[i]} is lit as well as {TabNames[tab]}");
                }
            }
        }

        private Text Caption(int tab)
        {
            Text caption = Find(TabNames[tab]).GetComponentInChildren<Text>(true);
            Assert.IsNotNull(caption, $"{TabNames[tab]} has no caption");
            return caption;
        }

        /// <summary>
        /// Off the PAINT tab the body takes no paint: a drag that starts on it turns it, as one beside it does, so turning the body round to see a
        /// hairstyle from behind leaves no mark on it -- and nothing to undo, or to be asked about on the way out.
        /// </summary>
        [UnityTest]
        public IEnumerator OffThePaintTabADragOnTheBodyTurnsItAndPaintsNothing([Values("HairTab", "FaceTab")] string tab)
        {
            yield return Open();
            yield return Click(tab);
            Assert.IsFalse(_creator.PaintEnabled, $"{tab} left the body to be painted");
            _creator.Colour = Color.red;
            _creator.BrushRadiusPixels = 6;
            Color32[] before = _creator.Paint.Atlas.GetPixels32();
            Quaternion facing = _creator.RotationPivot.localRotation;

            // The press and the drag straight to the creator, as MissingTheBodyTurnsItInstead does: a test mouse's movement does not reach its turn.
            Vector2 chest = CharacterCreatorPlayTests.ChestOnScreen(_creator);
            _creator.PressAt(chest);
            yield return null;
            _creator.Drag(chest, new Vector2(40.0f, 0.0f));
            yield return null;

            Assert.Greater(Quaternion.Angle(facing, _creator.RotationPivot.localRotation), 1.0f, $"on {tab}, a drag on the body did not turn it");
            Assert.IsFalse(_creator.HasEdits, $"on {tab}, a drag on the body left a stroke to undo");
            Color32[] after = _creator.Paint.Atlas.GetPixels32();
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i].r != after[i].r || before[i].g != after[i].g || before[i].b != after[i].b)
                {
                    Assert.Fail($"on {tab}, a drag on the body painted it (texel {i})");
                }
            }
        }

        /// <summary>Back on PAINT the body takes paint again, and a stroke on it does not turn it as well.</summary>
        [UnityTest]
        public IEnumerator BackOnThePaintTabADragOnTheBodyPaintsItAgain()
        {
            yield return Open();
            yield return Click("HairTab");
            yield return Click("PaintTab");
            Assert.IsTrue(_creator.PaintEnabled, "back on PAINT, the body still cannot be painted");
            _creator.Colour = Color.red;
            _creator.BrushRadiusPixels = 6;
            Quaternion facing = _creator.RotationPivot.localRotation;

            yield return DragFrom(CharacterCreatorPlayTests.ChestOnScreen(_creator), new Vector2(40.0f, 0.0f));

            Assert.IsTrue(_creator.HasEdits, "back on PAINT, a drag on the body painted nothing");
            Assert.AreEqual(0.0f, Quaternion.Angle(facing, _creator.RotationPivot.localRotation), 0.01f,
                "back on PAINT, a stroke on the body turned it as well");
        }

        /// <summary>The brush ring follows the mouse over the body on PAINT, where a press paints, and not on the tabs where a press turns it.</summary>
        [UnityTest]
        public IEnumerator TheBrushRingFollowsTheMouseOnlyOnThePaintTab()
        {
            yield return Open();
            Assert.AreEqual(CharacterCreator.Tool.Brush, _creator.Current, "the creator does not open on the brush");
            yield return MoveTo(CharacterCreatorPlayTests.ChestOnScreen(_creator));
            Assert.IsTrue(_creator.BrushRingVisible, "on PAINT, no brush ring under the mouse on the body, so this would prove nothing");

            foreach (string tab in new[] { "HairTab", "FaceTab" })
            {
                yield return Click(tab);
                yield return MoveTo(CharacterCreatorPlayTests.ChestOnScreen(_creator));
                Assert.IsFalse(_creator.BrushRingVisible, $"on {tab}, the brush ring still follows the mouse over the body");
            }

            yield return Click("PaintTab");
            yield return MoveTo(CharacterCreatorPlayTests.ChestOnScreen(_creator));
            Assert.IsTrue(_creator.BrushRingVisible, "back on PAINT, the brush ring did not come back");
        }

        /// <summary>
        /// The wheel over the hairstyles is theirs (the grid scrolls once there are more than it shows) and leaves the camera where it was; over
        /// the body it zooms, as it always has.
        /// </summary>
        [UnityTest]
        public IEnumerator TheWheelOverTheHairstylesLeavesTheCameraWhereItWasAndOverTheBodyStillZooms()
        {
            yield return Open();
            yield return Click("HairTab");
            Transform view = _creator.Camera.transform;

            Transform grid = Find("HairStyleGrid");
            Vector2 overTile = CharacterCreatorPlayTests.ScreenCentre((RectTransform)Find("Hair" + CharacterHair.Hair1 + "Button"));
            yield return MoveTo(overTile);
            GameObject under = CharacterCreatorPlayTests.InterfaceAt(overTile);
            Assert.IsTrue(under != null && under.transform.IsChildOf(grid), "the mouse is not over the hairstyles, so this would prove nothing");
            Vector3 home = view.localPosition;
            yield return WheelIn();
            Assert.Less(Vector3.Distance(home, view.localPosition), 1e-4f, "the wheel over the hairstyles zoomed the body behind them");

            Vector2 chest = CharacterCreatorPlayTests.ChestOnScreen(_creator);
            yield return MoveTo(chest);
            Assert.IsNull(CharacterCreatorPlayTests.InterfaceAt(chest), "the interface covers the body, so this would prove nothing");
            yield return WheelIn();
            Assert.Less(Mathf.Abs(view.localPosition.z), Mathf.Abs(home.z) - 0.1f, "the wheel over the body no longer zooms in");
        }
    }
}
