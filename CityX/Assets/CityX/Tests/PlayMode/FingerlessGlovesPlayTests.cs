using System.Collections;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Wearables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The fingerless gloves on the real player (docs/FINGERLESS_GLOVES.md): put on, they are drawn with the body skin's own bones in their
    /// colourway's leather; under a long-sleeved shirt they are the same whole gloves (the shirt hides the forearms' skin, not the cuffs); taken
    /// off, they go; lying on the ground they are not drawn on anyone.
    /// </summary>
    public sealed class FingerlessGlovesPlayTests
    {
        private GameObject _player;
        private SkinnedMeshRenderer _skin;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _skin = TransformSearch.FindDeep(_player.transform, BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
#endif
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_player);
            foreach (FingerlessGloves left in Object.FindObjectsByType<FingerlessGloves>(FindObjectsSortMode.None))
            {
                Object.Destroy(left.gameObject);
            }
        }

        private static FingerlessGloves Spawn(string itemId)
        {
#if UNITY_EDITOR
            return Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FingerlessGloveColourways.PrefabPath(itemId)),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity).GetComponent<FingerlessGloves>();
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator WornTheyAreDrawnWithTheBodysBonesInTheirLeatherAndGoWhenTakenOff()
        {
            foreach (FingerlessGloveColourways.Colourway colourway in FingerlessGloveColourways.All)
            {
                FingerlessGloves gloves = Spawn(colourway.ItemId);
                Assert.IsNull(gloves.Worn, "gloves lying on the ground are drawn on someone");
                gloves.OnEquip(_player, ItemInstanceData.None);
                yield return null;
                SkinnedMeshRenderer worn = gloves.Worn;
                Assert.IsNotNull(worn, $"{colourway.ItemId} is not drawn on the player");
                Assert.AreEqual(_skin.bones, worn.bones, "the gloves are not drawn with the body's bones");
                Assert.AreSame(gloves.Material, worn.sharedMaterial);
                Assert.IsFalse(gloves.GetComponentInChildren<MeshRenderer>(true).gameObject.activeSelf, "the pair lying on the ground still shows");
                Assert.AreEqual(0, BodySurface.Renderers(_player.transform).FindAll(r => r == worn).Count, "the body's paint would go on the gloves");

                gloves.OnUnequip(_player);
                yield return null;
                Assert.IsNull(TransformSearch.FindDeep(_player.transform, FingerlessGloves.WornName), $"{colourway.ItemId} stayed on after being taken off");
            }
        }

        [UnityTest]
        public IEnumerator UnderALongSleevedShirtTheyAreTheWholeGloves()
        {
#if UNITY_EDITOR
            Mesh bare = FingerlessGloves.GlovesFor(_skin);
            GameObject shirt = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/LongSleeveTShirt.prefab"),
                new Vector3(30.0f, 0.5f, 30.0f), Quaternion.identity);
            shirt.GetComponent<IEquippableItem>().OnEquip(_player, ItemInstanceData.None);
            yield return null;
            Assert.AreNotSame(WornBodyCover.Whole(_skin), _skin.sharedMesh, "the shirt does not hide any of the body: nothing is tested");
            FingerlessGloves gloves = Spawn(FingerlessGloveColourways.BlackItemId);
            gloves.OnEquip(_player, ItemInstanceData.None);
            yield return null;
            Assert.AreSame(bare, gloves.Worn.sharedMesh, "under the shirt the gloves were made from the cut body: their cuffs would be missing");
            Object.Destroy(shirt);
#else
            Assert.Ignore("needs the editor to load the prefabs.");
            yield break;
#endif
        }
    }
}
