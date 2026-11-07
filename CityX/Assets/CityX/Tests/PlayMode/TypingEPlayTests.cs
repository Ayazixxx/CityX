using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// An E typed into the chat or the console is a letter (bug hunt, 2026-11-06): it picked up whatever lay near, as every pickup, the carry
    /// and the search read E through InteractKey. With a real keyboard and a real focused text box, E taps the key only when no text box has it.
    /// </summary>
    public sealed class TypingEPlayTests : InputTestFixture
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
        }

        [TearDown]
        public void TearDownScene()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
        }

        /// <summary>Taps E (down one frame, up the next) and says whether InteractKey saw a tap, read every frame as its users read it.</summary>
        private IEnumerator TapE(System.Action<bool> tapped)
        {
            bool seen = false;
            seen |= InteractKey.Tapped;
            Press(Keyboard.current.eKey);
            yield return null;
            seen |= InteractKey.Tapped;
            Release(Keyboard.current.eKey);
            yield return null;
            seen |= InteractKey.Tapped;
            yield return null;
            seen |= InteractKey.Tapped;
            tapped(seen);
        }

        [UnityTest]
        public IEnumerator AnETypedIntoATextBoxIsNoTapOfTheInteractKey()
        {
            Assert.IsNotNull(InteractKey.IsTyping, "the UI never told the E key how to know a text box has the keyboard");
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                _made.Add(events);
            }
            var canvas = new GameObject("Canvas", typeof(Canvas));
            _made.Add(canvas);
            var fieldObject = new GameObject("Chat input", typeof(RectTransform), typeof(Image), typeof(InputField));
            fieldObject.transform.SetParent(canvas.transform, false);
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text));
            text.transform.SetParent(fieldObject.transform, false);
            text.GetComponent<Text>().font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var field = fieldObject.GetComponent<InputField>();
            field.textComponent = text.GetComponent<Text>();
            yield return null;

            bool tapped = false;
            yield return TapE(t => tapped = t);
            Assert.IsTrue(tapped, "setup: E outside any text box is no tap");

            EventSystem.current.SetSelectedGameObject(fieldObject);
            field.ActivateInputField();
            for (int i = 0; i < 3 && !field.isFocused; i++) yield return null;
            Assert.IsTrue(field.isFocused, "setup: the text box did not take the keyboard");
            Assert.IsTrue(InteractKey.Typing, "a focused text box is not typing to the E key");
            yield return TapE(t => tapped = t);
            Assert.IsFalse(tapped, "an E typed into a text box tapped the interact key: it would pick up what lies near");

            field.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            Assert.IsFalse(InteractKey.Typing);
        }
    }
}
