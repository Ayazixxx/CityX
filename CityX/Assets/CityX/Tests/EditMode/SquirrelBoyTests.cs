using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Squirrel Boy's make (owner, 2026-11-25: "uses my squirrel boy character i made and the outfit he is wearing now. the sb shirt i designed, the
    /// squirrel mask, the brown cape and gloves, and khakis with bwsneakers"): his outfit, his shirt's design carried in the prefab, seen by
    /// every player in a session (a networked twin whose brain runs on its owner alone), and /spawn squirrelboy.
    /// </summary>
    public sealed class SquirrelBoyTests
    {
        private static SquirrelBoyBrain Brain()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/SquirrelBoy.prefab");
            Assert.IsNotNull(prefab, "no SquirrelBoy.prefab: rebuild the scene");
            var brain = prefab.GetComponent<SquirrelBoyBrain>();
            Assert.IsNotNull(brain);
            return brain;
        }

        [Test]
        public void HeWearsTheOwnersOutfit()
        {
            SquirrelBoyBrain brain = Brain();
            Inventory items = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab").GetComponent<Inventory>();
            foreach (string id in new[] { ItemRegistry.TShirt, ItemRegistry.SquirrelMask, CapeColourways.BrownItemId, FingerlessGloveColourways.BrownItemId,
                         ItemRegistry.Khakis, ItemRegistry.BWSneakers })
            {
                GameObject piece = items.PrefabForItem(id);
                Assert.IsNotNull(piece, $"no prefab for {id}");
                Assert.Contains(piece, brain.Wearing, $"he does not wear {id}");
            }
            Assert.AreEqual(6, brain.Wearing.Length, "he wears more than his outfit");
            Assert.IsNotNull(brain.ShirtDesign, "the SB design is not in his prefab");
        }

        [Test]
        public void EveryPlayerInASessionSeesHim()
        {
            var networked = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedSquirrelBoy.prefab");
            Assert.IsNotNull(networked, "no networked Squirrel Boy: other players would not see him");
            var enemy = networked.GetComponent<NetworkedEnemy>();
            Assert.IsNotNull(enemy);
            Assert.Contains(networked.GetComponent<SquirrelBoyBrain>(), enemy.OwnerOnly, "his brain runs on every machine, not his owner's alone");
        }
    }
}
