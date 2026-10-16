using System.Collections;
using System.Linq;
using CityX.Character;
using CityX.Core;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The skill menu with a real mouse and keyboard (owner, 2026-10-15: "click the skill you want and then it gets highlighted and then you press
    /// the number key you want it assigned to and it goes there"): a click on a learnt spell's star chooses it and lights it up, and a number
    /// key puts it in that slot of the bar.
    /// </summary>
    public sealed class SkillMenuRealInputPlayTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Mouse _mouse;
        private GameObject _body;

        public override void Setup()
        {
            base.Setup();
            // A leftover event system from the test before (the menu's own, Destroy'd at the end of ITS frame, which is this one) would be
            // found by the menu, which then makes none of its own -- and the click below goes to nobody.
            foreach (UnityEngine.EventSystems.EventSystem leftover in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(leftover.gameObject);
            }
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        public override void TearDown()
        {
            if (SkillMenuView.Instance != null)
            {
                SkillMenuView.Instance.Close();
                Object.DestroyImmediate(SkillMenuView.Instance.gameObject);
            }
            if (_body != null) Object.DestroyImmediate(_body);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            base.TearDown();
        }

        private PlayerSkills NewCharacter(int points)
        {
            _body = new GameObject("Body");
            PlayerVitals vitals = _body.AddComponent<PlayerVitals>();
            PlayerSkills skills = PlayerSkills.On(_body);
            skills.Load(CharacterSave.Create("Tester", Color.white));
            vitals.SkillPoints = points;
            return skills;
        }

        /// <summary>Where a star is on the screen, in pixels.</summary>
        private static Vector2 StarOnScreen(SkillMenuView menu, string id)
        {
            RectTransform star = (RectTransform)menu.GetComponentsInChildren<SkillStarHandle>(true).First(h => h.SkillId == id).transform;
            return RectTransformUtility.WorldToScreenPoint(null, star.TransformPoint(star.rect.center));
        }

        private IEnumerator Click(Vector2 at)
        {
            Move(_mouse.position, at);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
        }

        private IEnumerator Tap(KeyControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClickingALearntSpellAndPressingANumberPutsItInThatSlot()
        {
            PlayerSkills skills = NewCharacter(3);
            Assert.AreEqual(SkillBook.Refusal.None, skills.TryLearn(SkillCatalog.Fireball));
            Assert.AreEqual(SkillBook.Refusal.None, skills.TryLearn(SkillCatalog.IceShards));
            SkillMenuView menu = SkillMenuView.Show(skills, null, null);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();

            // Click the Ice Shards star: it is chosen.
            menu.ShowAll();
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return Click(StarOnScreen(menu, SkillCatalog.IceShards));
            Assert.AreEqual(SkillCatalog.IceShards, menu.Selected, "a click on the star did not choose it");
            Assert.IsTrue(menu.IsHighlighted(SkillCatalog.IceShards), "the chosen star is not lit up");
            Assert.IsTrue(menu.BarHintShown, "no hint over the bar says to press a number key");

            // 3 puts it in the third slot; 0 then moves it to the tenth.
            yield return Tap(_keyboard.digit3Key);
            Assert.AreEqual(2, skills.Bar.SlotOf(SkillCatalog.IceShards), "pressing 3 did not put it in slot 3");
            yield return Tap(_keyboard.digit0Key);
            Assert.AreEqual(9, skills.Bar.SlotOf(SkillCatalog.IceShards), "pressing 0 did not move it to slot 0");
            Assert.IsNull(skills.Bar[2], "it is still in slot 3 as well");

            // Another spell into a slot that holds one: the new one takes it.
            yield return Click(StarOnScreen(menu, SkillCatalog.Fireball));
            Assert.AreEqual(SkillCatalog.Fireball, menu.Selected);
            yield return Tap(_keyboard.digit0Key);
            Assert.AreEqual(9, skills.Bar.SlotOf(SkillCatalog.Fireball), "pressing 0 did not put Fireball in slot 0");

            // A star not learnt yet: a number key does nothing (and the card says why).
            yield return Click(StarOnScreen(menu, SkillCatalog.PoisonBreath));
            Assert.AreEqual(SkillCatalog.PoisonBreath, menu.Selected);
            yield return Tap(_keyboard.digit5Key);
            Assert.AreEqual(-1, skills.Bar.SlotOf(SkillCatalog.PoisonBreath));
            Assert.IsNull(skills.Bar[4]);
        }
    }
}
