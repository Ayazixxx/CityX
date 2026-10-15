using System.Collections;
using System.Linq;
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
    /// THE SKILL MENU AND THE BAR (owner, 2026-10-14): one big map with every tree side by side like Skyrim's, moved by dragging and the wheel,
    /// wrapping round; learning a star spends a point; a learnt spell goes on the bar (keys 1 to 0), movement never does; the HUD shows the bar
    /// in the bottom-left corner.
    /// </summary>
    public sealed class SkillMenuPlayTests
    {
        private GameObject _body;
        private GameObject _hud;

        [TearDown]
        public void TearDown()
        {
            if (SkillMenuView.Instance != null)
            {
                SkillMenuView.Instance.Close();
                Object.DestroyImmediate(SkillMenuView.Instance.gameObject);
            }
            if (_hud != null) Object.DestroyImmediate(_hud);
            if (_body != null) Object.DestroyImmediate(_body);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>A body as a brand new character has it: vaulting, and the points given.</summary>
        private PlayerSkills NewCharacter(int points)
        {
            _body = new GameObject("Body");
            PlayerVitals vitals = _body.AddComponent<PlayerVitals>();
            PlayerSkills skills = PlayerSkills.On(_body);
            skills.Load(CharacterSave.Create("Tester", Color.white));
            vitals.SkillPoints = points;
            return skills;
        }

        [UnityTest]
        public IEnumerator EveryTreeIsOnTheMapAndLearningAStarSpendsAPoint()
        {
            PlayerSkills skills = NewCharacter(1);
            SkillMenuView menu = SkillMenuView.Show(skills, null, null);
            yield return null;

            Assert.IsTrue(menu.IsOpen);
            Assert.IsTrue(SkillMenuView.IsAnyOpen);
            foreach (SkillTree tree in SkillCatalog.Trees)
            {
                Assert.IsNotNull(menu.transform.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.text == tree.Name), $"{tree.Name} has no name on the map");
            }

            menu.Select(SkillCatalog.Dash);
            Assert.AreEqual(SkillBook.Refusal.None, menu.LearnSelected(), "a new character's point should buy Dash");
            Assert.IsTrue(skills.Knows(SkillCatalog.Dash));
            Assert.AreEqual(0, skills.Points);
            Assert.AreEqual(-1, skills.Bar.SlotOf(SkillCatalog.Dash), "movement does not go on the bar");

            menu.Select(SkillCatalog.Climb);
            Assert.AreEqual(SkillBook.Refusal.NoPoints, menu.LearnSelected(), "no points left");
            menu.Select(SkillCatalog.WallStick);
            Assert.AreEqual(SkillBook.Refusal.NeedsPrerequisite, skills.WhyNot(SkillCatalog.WallStick), "Wall Stick sits above Climb");
        }

        [UnityTest]
        public IEnumerator ALearntSpellGoesOnTheBarAndMovesFromSlotToSlot()
        {
            PlayerSkills skills = NewCharacter(3);
            SkillMenuView menu = SkillMenuView.Show(skills, null, null);
            yield return null;

            menu.Select(SkillCatalog.Fireball);
            Assert.AreEqual(SkillBook.Refusal.None, menu.LearnSelected());
            Assert.AreEqual(0, skills.Bar.SlotOf(SkillCatalog.Fireball), "a new spell goes in the first empty slot (key 1)");

            Assert.IsTrue(menu.PutSelectedOnBar(9), "key 0 takes it");
            Assert.AreEqual(9, skills.Bar.SlotOf(SkillCatalog.Fireball));
            Assert.IsNull(skills.Bar[0]);

            menu.ClickSlot(4);
            Assert.AreEqual(4, skills.Bar.SlotOf(SkillCatalog.Fireball), "clicking a slot with the spell chosen moves it there");
            menu.ClearSlot(4);
            Assert.AreEqual(-1, skills.Bar.SlotOf(SkillCatalog.Fireball));

            menu.Select(SkillCatalog.Dash);
            menu.LearnSelected();
            Assert.IsFalse(menu.PutSelectedOnBar(2), "movement keeps its own controls");
            Assert.IsNull(skills.Bar[2]);

            menu.Select(SkillCatalog.IceShards);
            Assert.IsFalse(menu.PutSelectedOnBar(2), "a spell not learnt yet cannot go on the bar");
        }

        [UnityTest]
        public IEnumerator DraggingALearntStarOntoASlotPutsItThere()
        {
            PlayerSkills skills = NewCharacter(1);
            skills.TryLearn(SkillCatalog.Bloodrage);
            SkillMenuView menu = SkillMenuView.Show(skills, null, null);
            yield return null;
            Canvas.ForceUpdateCanvases();

            RectTransform slot = menu.GetComponentsInChildren<SkillSlotHandle>(true).First(h => h.Slot == 6).transform.parent as RectTransform;
            Vector2 slotOnScreen = RectTransformUtility.WorldToScreenPoint(null, slot.TransformPoint(slot.rect.center));

            Assert.IsTrue(menu.BeginCarry(SkillCatalog.Bloodrage, new Vector2(400.0f, 300.0f)));
            Assert.IsTrue(menu.IsCarrying);
            menu.MoveCarry(slotOnScreen);
            menu.EndCarry(slotOnScreen);
            Assert.AreEqual(6, skills.Bar.SlotOf(SkillCatalog.Bloodrage), "dropped on key 7's slot");

            Assert.IsTrue(menu.BeginCarry(SkillCatalog.Bloodrage, slotOnScreen));
            menu.EndCarry(new Vector2(600.0f, 400.0f));
            Assert.AreEqual(6, skills.Bar.SlotOf(SkillCatalog.Bloodrage), "dropped on the sky: nothing changes");

            Assert.IsFalse(menu.BeginCarry(SkillCatalog.Vault, slotOnScreen), "movement cannot be carried to the bar");
        }

        [UnityTest]
        public IEnumerator TheRowWrapsRoundAndShowAllFitsEveryTree()
        {
            PlayerSkills skills = NewCharacter(0);
            SkillMenuView menu = SkillMenuView.Show(skills, null, null);
            yield return null;
            float width = menu.Layout.RowWidth;

            // Sliding a whole row's length, a notch at a time, comes back round to the same view.
            Vector2 before = menu.OnScreen(SkillCatalog.Fireball);
            float notches = width * menu.Zoom / SkillMenuView.SlidePerNotch;
            for (float done = 0.0f; done < notches; done += 1.0f)
            {
                menu.Slide(Mathf.Min(1.0f, notches - done));
            }
            Vector2 after = menu.OnScreen(SkillCatalog.Fireball);
            Assert.AreEqual(before.x, after.x, 0.5f, "a full turn of the row should bring the same stars back");
            Assert.That(menu.Scroll, Is.InRange(0.0f, width), "the scroll stays within one row");

            // Dragging moves the map with the pointer.
            float scroll = menu.Scroll;
            menu.Pan(new Vector2(-100.0f, 0.0f));
            Assert.AreEqual(menu.Layout.Wrap(scroll + 100.0f / menu.Zoom), menu.Scroll, 0.01f);

            // Show all: every tree across the view at once.
            menu.ShowAll();
            Assert.AreEqual(menu.MinZoom, menu.Zoom, 1e-4f);
            float halfView = ((RectTransform)menu.GetComponentInChildren<SkillMapDrag>().transform).rect.width * 0.5f;
            foreach (SkillTree tree in SkillCatalog.Trees)
            {
                SkillNode star = SkillCatalog.InTree(tree.Id).First();
                Assert.LessOrEqual(Mathf.Abs(menu.OnScreen(star.Id).x), halfView + 1.0f, $"{tree.Name} is off the view at Show all");
            }
        }

        [UnityTest]
        public IEnumerator TheBumpersStepFromTreeToTreeAndZoomKeepsThePointUnderThePointer()
        {
            PlayerSkills skills = NewCharacter(0);
            SkillMenuView menu = SkillMenuView.Show(skills, null, null);
            yield return null;

            string start = menu.Layout.TreeAt(menu.Scroll);
            menu.JumpTree(1);
            string next = menu.Layout.TreeAt(menu.Scroll);
            int i = menu.Layout.TreeOrder.ToList().IndexOf(start);
            Assert.AreEqual(menu.Layout.TreeOrder[(i + 1) % menu.Layout.TreeOrder.Count], next);
            Assert.AreEqual(0.0f, menu.Layout.Across(menu.Scroll, menu.Layout.TreeCentre(next)), 0.5f, "the tree is in the middle");
            menu.JumpTree(-1);
            Assert.AreEqual(start, menu.Layout.TreeAt(menu.Scroll));

            var pointer = new Vector2(200.0f, -80.0f);
            Vector2 mapPointBefore = new Vector2(menu.Scroll, menu.PanY) + pointer / menu.Zoom;
            menu.ZoomBy(1.3f, pointer);
            Vector2 mapPointAfter = new Vector2(menu.Scroll, menu.PanY) + pointer / menu.Zoom;
            Assert.AreEqual(menu.Layout.Wrap(mapPointBefore.x), menu.Layout.Wrap(mapPointAfter.x), 0.5f);
            Assert.AreEqual(mapPointBefore.y, mapPointAfter.y, 0.5f);

            Assert.AreEqual(SkillCatalog.Fireball, menu.StarNear(menu.OnScreen(SkillCatalog.Fireball), 10.0f), "the star at the middle is picked");
        }

        [UnityTest]
        public IEnumerator ResetNeedsTwoClicksAndGivesThePointsBack()
        {
            PlayerSkills skills = NewCharacter(2);
            skills.TryLearn(SkillCatalog.Climb);
            skills.TryLearn(SkillCatalog.Fireball);
            Assert.AreEqual(0, skills.Points);
            SkillMenuView menu = SkillMenuView.Show(skills, null, null);
            yield return null;

            Button reset = menu.GetComponentsInChildren<Button>(true).First(b => b.GetComponentInChildren<Text>().text == "Reset");
            reset.onClick.Invoke();
            Assert.IsTrue(skills.Knows(SkillCatalog.Climb), "one click only asks");
            Assert.AreEqual("Sure?", reset.GetComponentInChildren<Text>().text);
            reset.onClick.Invoke();
            Assert.IsFalse(skills.Knows(SkillCatalog.Climb));
            Assert.IsFalse(skills.Knows(SkillCatalog.Fireball));
            Assert.IsTrue(skills.Knows(SkillCatalog.Vault));
            Assert.AreEqual(2, skills.Points, "both points back");
            Assert.AreEqual(-1, skills.Bar.SlotOf(SkillCatalog.Fireball), "a forgotten spell leaves the bar");
        }

        [UnityTest]
        public IEnumerator OpeningRestsThePlayersControlsAndClosingGivesThemBack()
        {
            GameObject playerPrefab = UnityEditorLoad("Assets/CityX/Character/Player.prefab");
            if (playerPrefab == null)
            {
                Assert.Ignore("needs the editor");
            }
            _body = Object.Instantiate(playerPrefab, new Vector3(0.0f, 200.0f, 0.0f), Quaternion.identity);
            PlayerInputReader playerInput = _body.GetComponent<PlayerInputReader>();
            PlayerSkills bodySkills = _body.GetComponent<PlayerSkills>();
            Assert.IsNotNull(bodySkills, "the input reader gives every body its skills");
            Assert.IsTrue(bodySkills.Knows(SkillCatalog.Climb) && bodySkills.Knows(SkillCatalog.Fireball),
                "a body with no character knows what the game had before the skill tree");
            Assert.IsFalse(bodySkills.Knows(SkillCatalog.SuperJump),
                "...which did not include the charged jump: Space still jumps the instant it is pressed");
            yield return null;

            SkillMenuView menu = SkillMenuView.Show(bodySkills, playerInput, null);
            Assert.IsFalse(playerInput.enabled, "the player stands still while the menu is open");
            menu.Close();
            Assert.IsTrue(playerInput.enabled);
            Assert.IsFalse(SkillMenuView.IsAnyOpen);
            Assert.AreEqual(Time.frameCount, SkillMenuView.ClosedOnFrame);
        }

        [UnityTest]
        public IEnumerator TheHudShowsTheBarInTheBottomLeftWithKeysOneToZero()
        {
            PlayerSkills skills = NewCharacter(3);
            skills.TryLearn(SkillCatalog.Fireball);
            skills.TryLearn(SkillCatalog.SwordSpin);
            PlayerSkills.Local = skills;

            _hud = new GameObject("Hud", typeof(RectTransform), typeof(Canvas));
            _hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var hud = _hud.AddComponent<PlayerHud>();
            hud.Target = _body.GetComponent<PlayerVitals>();
            yield return null;
            yield return null;

            HotbarView bar = _hud.GetComponent<HotbarView>();
            Assert.IsNotNull(bar, "the HUD did not add its bar");
            Assert.AreEqual(Vector2.zero, bar.Bar.anchorMin, "anchored to the bottom-left corner");
            Assert.AreEqual(HotbarView.Corner, bar.Bar.anchoredPosition);
            Assert.AreEqual(Hotbar.SlotCount, bar.Slots.Length);
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" }, bar.Slots.Select(s => s.Key.text).ToArray());

            Assert.AreEqual(SkillCatalog.Fireball, bar.Slots[0].SkillId);
            Assert.IsNotNull(bar.Slots[0].Icon.sprite, "the fireball has an icon");
            Assert.AreEqual(HotbarView.IconColour, bar.Slots[0].Icon.color, "a spell is always usable");
            Assert.AreEqual(SkillCatalog.SwordSpin, bar.Slots[1].SkillId);
            Assert.AreEqual(HotbarView.UnusableIconColour, bar.Slots[1].Icon.color, "the sword spin is greyed with no sword in hand");
            Assert.IsNull(bar.Slots[2].SkillId);
            Assert.IsFalse(bar.Slots[2].Icon.enabled, "an empty slot shows no icon");

            skills.PutOnBar(9, SkillCatalog.Fireball);
            yield return null;
            Assert.AreEqual(SkillCatalog.Fireball, bar.Slots[9].SkillId, "the HUD follows the bar");
            Assert.IsNull(bar.Slots[0].SkillId);
        }

        private static GameObject UnityEditorLoad(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }
    }
}
