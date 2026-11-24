using CityX.Character;
using CityX.Items;
using CityX.UI;
using CityX.Wearables;
using CityX.WearablesEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The Hooded Coat (docs/BROWN_COAT.md; owner, 2026-11-23: "try to make this brown coat item and make the hooded variant too. make it so it is
    /// hood off by default but if you type /hoodon in the text chat ... it turns into the hood on version ... make it brown but we can make multiple
    /// colorways of easily"; 2026-11-24, from Meshy's two models: "a functional item called brown coat"): the item, the coat with its two hoods
    /// and their paint, and the chat's words.
    /// </summary>
    public sealed class HoodedCoatTests
    {
        /// <summary>A coat's triangles at most (tools/model_qa.py --kind coat).</summary>
        private const int TriangleBudget = 15000;

        private static HoodedCoat Coat(string itemId)
        {
            GameObject networked = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            GameObject prefab = networked.GetComponent<Inventory>().PrefabForItem(itemId);
            Assert.IsNotNull(prefab, $"a copy of another player cannot build {itemId} (NetworkedPlayerWiring.ItemPrefabPaths)");
            return prefab.GetComponent<HoodedCoat>();
        }

        [Test]
        public void EveryColourIsACoatOfItsOwnWithBothHoods()
        {
            foreach (HoodedCoatBuilder.Colourway colourway in HoodedCoatBuilder.Colourways)
            {
                Assert.IsTrue(ItemRegistry.TryGet(colourway.ItemId, out ItemDefinition item), colourway.ItemId);
                Assert.AreEqual(EquipSlots.Torso, item.EquipSlot);
                Assert.IsTrue(ItemRegistry.IsCoat(colourway.ItemId), "not a coat: a shirt could not go under it");
                HoodedCoat coat = Coat(colourway.ItemId);
                Assert.IsNotNull(coat, colourway.Name + " is not a hooded coat");
                Assert.AreEqual(colourway.ItemId, coat.ItemId);
                Assert.IsNotNull(coat.GarmentMesh, "no coat with its hood down");
                Assert.IsNotNull(coat.HoodUpMesh, "no coat with its hood up");
                Assert.Less(coat.GarmentMesh.triangles.Length / 3, TriangleBudget);
                Assert.Less(coat.HoodUpMesh.triangles.Length / 3, TriangleBudget);
                // Hood down it stops under the head's middle; hood up it goes over the head's top (2.6 m).
                Assert.Less(coat.GarmentMesh.bounds.max.y, BodyRig.StandingHeight - 0.3f, "hood down, the coat goes over the head");
                Assert.Greater(coat.HoodUpMesh.bounds.max.y, BodyRig.StandingHeight, "hood up, the hood does not cover the head's top");
                Assert.That(coat.GarmentMesh.bounds.min.y, Is.InRange(0.5f, 0.75f), "the skirt does not hang to the thighs");
                Assert.Greater(coat.HiddenFromBodyTriangles, 0, "the body would show through the coat");
                Assert.IsNotNull(coat.GarmentMaterial.GetTexture("_BaseMap"), "no paint on it");
                Assert.IsNotNull(coat.HoodUpMaterial, "the hood-up coat has no paint of its own");
                Assert.IsNotNull(coat.HoodUpMaterial.GetTexture("_BaseMap"), "no paint on the hood-up coat");
                Assert.LessOrEqual(coat.GarmentMaterial.GetTexture("_BaseMap").width, 1024);
            }
            Assert.AreEqual("Brown Coat", ItemRegistry.TryGet(ItemRegistry.BrownCoat, out ItemDefinition brown) ? brown.DisplayName : null);
        }

        [Test]
        public void TheChatPutsTheHoodUpAndDown()
        {
            Assert.IsTrue(PlayerChat.IsHoodCommand("/hoodon", out bool up) && up);
            Assert.IsTrue(PlayerChat.IsHoodCommand("  /HoodOff ", out up) && !up);
            Assert.IsFalse(PlayerChat.IsHoodCommand("/hood", out _));
            Assert.IsFalse(PlayerChat.IsHoodCommand("hoodon", out _));
            Assert.IsFalse(PlayerChat.IsHoodCommand(null, out _));
        }

        [Test]
        public void ThePlayersBodyCanTellItsHood()
        {
            // (The networked body is every online player and their copies; the scene's own player is wired by the same PlayerAbilityWiring.)
            const string path = "Assets/CityX/Character/NetworkedPlayer.prefab";
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<HoodSync>(), path + " has no HoodSync: rebuild the scene");
        }
    }
}
