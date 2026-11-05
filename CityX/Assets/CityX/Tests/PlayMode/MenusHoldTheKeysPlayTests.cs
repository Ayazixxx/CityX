using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// A menu opened in mid-air does not hang the body there, with the real keyboard on the real body. Each menu switched the reader off,
    /// and the reader is the only thing that ticks the body: it hung in the air until the menu closed.
    /// </summary>
    public sealed class MenusHoldTheKeysPlayTests : InputTestFixture
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

        private PlayerChat Chat(PlayerInputReader reader)
        {
            var chatObject = new GameObject("Chat");
            _made.Add(chatObject);
            var chat = chatObject.AddComponent<PlayerChat>();
            chat.Panel = new GameObject("Panel");
            chat.Panel.transform.SetParent(chatObject.transform, false);
            chat.Panel.SetActive(false);
            chat.InputReader = reader;
            return chat;
        }

        [UnityTest]
        public IEnumerator ABodyFallsWithTheChatOpen()
        {
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(100.0f, 1.0f, 100.0f);
            _made.Add(ground);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 12.0f, 0.0f), Quaternion.identity);
            _made.Add(player);
            var reader = player.GetComponent<PlayerInputReader>();
            Assert.IsTrue(reader.enabled, "setup: the player's reader is off");
            yield return null;
            float before = player.transform.position.y;

            PlayerChat chat = Chat(reader);
            chat.Open();
            Assert.IsTrue(chat.IsOpen, "setup: the chat did not open");
            for (int i = 0; i < 90; i++)
            {
                yield return null;
            }
            Assert.Less(player.transform.position.y, before - 4.0f, "the body hung in the air while the chat was open");
            Assert.IsTrue(PlayerBodies.IsPlayer(player), "with the chat open the body counts as nobody's: enemies stop coming for it");
            chat.Close();
#else
            yield break;
#endif
        }
    }
}
