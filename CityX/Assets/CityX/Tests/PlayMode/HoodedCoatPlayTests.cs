using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The Hooded Coat's hood on a real body (docs/BROWN_COAT.md; owner, 2026-11-23: "hood off by default but if you type /hoodon in the text chat
    /// ... it turns into the hood on version"): put on, the hood is down; asked, it goes up and down and other players are told; their copy of
    /// the wearer does the same; a raised hood is told again for whoever comes late; with no coat on there is nothing to raise.
    /// </summary>
    public sealed class HoodedCoatPlayTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object made in _made)
            {
                if (made != null) Object.Destroy(made);
            }
            _made.Clear();
        }

#if UNITY_EDITOR
        private GameObject Body()
        {
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(_made.Count * 3.0f, 0.05f, 0.0f), Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            if (body.GetComponent<HoodSync>() == null) body.AddComponent<HoodSync>();   // the bare body: PlayerAbilityWiring gives the real players theirs
            _made.Add(body);
            return body;
        }

        private HoodedCoat Coat()
        {
            var coat = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/BrownCoat.prefab"),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity).GetComponent<HoodedCoat>();
            _made.Add(coat.gameObject);
            return coat;
        }
#endif

        /// <summary>
        /// In the game itself (owner, 2026-11-24: "/hoodon isnt workin", "or /hoodup"): the game's own scene, its player putting the coat on from
        /// the inventory, the words typed into its text chat.
        /// </summary>
        [UnityTest]
        public IEnumerator TypedInTheGamesChatTheHoodGoesUpAndDown()
        {
#if UNITY_EDITOR
            const string scenePath = "Assets/CityX/Scenes/CityMassing.unity";
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scenePath,
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return null;
            yield return null;
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            try
            {
                PlayerInputReader player = System.Array.Find(Object.FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None), r => r.enabled);
                Assert.IsNotNull(player, "no player in the game's scene");
                var chat = Object.FindFirstObjectByType<CityX.UI.PlayerChat>(FindObjectsInactive.Include);
                Assert.IsNotNull(chat, "no text chat in the game's scene");
                Assert.IsTrue(player.GetComponent<Inventory>().EquipItemId(ItemRegistry.BrownCoat, player.gameObject), "the coat would not go on");
                yield return null;
                HoodedCoat coat = player.GetComponentInChildren<HoodedCoat>(true);
                Assert.IsNotNull(coat, "the coat is not on the player");
                Assert.AreSame(chat.InputReader, player, "the chat speaks for another body");
                foreach ((string typed, bool up) in new[] { ("/hoodon", true), ("/hoodoff", false), ("/hoodup", true), ("/hooddown", false) })
                {
                    chat.Submit(typed);
                    yield return null;
                    Assert.AreEqual(up, coat.HoodUp, $"typing {typed} did not put the hood {(up ? "up" : "down")}");
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
                }
            }
            yield return null;
            LogAssert.ignoreFailingMessages = false;
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator TheHoodGoesUpAndDownHereAndOnAnotherPlayersCopy()
        {
#if UNITY_EDITOR
            GameObject wearer = Body(), copy = Body();
            var sync = wearer.GetComponent<HoodSync>();
            var copySync = copy.GetComponent<HoodSync>();
            Assert.IsFalse(sync.SetHood(true), "a hood went up with no coat on");

            HoodedCoat coat = Coat(), copyCoat = Coat();
            coat.OnEquip(wearer, ItemInstanceData.None);
            copyCoat.MountCosmetic(copy);
            yield return null;
            Assert.IsFalse(coat.HoodUp, "put on, the hood is not down");
            Assert.AreSame(coat, sync.Worn);

            var told = new List<AbilityEvent>();
            sync.Fired += told.Add;
            Assert.IsTrue(sync.SetHood(true));
            Assert.IsTrue(coat.HoodUp, "the hood did not go up");
            Assert.AreEqual(1, told.Count, "other players were not told");
            Assert.AreEqual(HoodSync.Up, told[0].Kind);
            copySync.PlayRemote(told[0]);
            Assert.IsTrue(copyCoat.HoodUp, "the copy's hood did not go up");
            yield return null;

            // Told again while it is up, for a player who was not there to hear it.
            sync.Tick(HoodSync.RepeatSeconds + 0.1f);
            Assert.AreEqual(2, told.Count, "a raised hood is not told again");
            Assert.AreEqual(HoodSync.Up, told[1].Kind);

            Assert.IsTrue(sync.SetHood(false));
            Assert.IsFalse(coat.HoodUp, "the hood did not come down");
            copySync.PlayRemote(told[told.Count - 1]);
            Assert.IsFalse(copyCoat.HoodUp, "the copy's hood did not come down");
            sync.Tick(HoodSync.RepeatSeconds + 0.1f);
            Assert.AreEqual(3, told.Count, "a lowered hood goes on being told");

            coat.OnUnequip(wearer);
            yield return null;
            Assert.IsNull(sync.Worn, "the coat taken off is still the body's hood");
            Assert.IsFalse(sync.SetHood(true));
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
        }
    }
}
