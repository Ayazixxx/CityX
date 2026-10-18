using CityX.Character;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The prefab every joining player is spawned from. PlayerRigTests
    /// already checks the plain Player.prefab's Animator has a controller;
    /// nothing checked the DERIVED NetworkedPlayer.prefab, which is how it
    /// shipped with the controller overridden to null -- every spawned clone
    /// (a friend's body) played no animation whatsoever.
    /// </summary>
    public sealed class NetworkedPlayerRigTests
    {
        [Test]
        public void TheNetworkedPlayerKeepsItsAnimatorController()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/NetworkedPlayer.prefab");
            Assert.IsNotNull(prefab, "no NetworkedPlayer.prefab -- run CityX/Rebuild Networked Player");

            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator, "the networked player has no Animator at all");
            Assert.IsNotNull(animator.runtimeAnimatorController,
                "the networked player's Animator has no controller -- every spawned player would stand frozen " +
                "in its bind pose, and no synced animation (walk, jump, punch) could ever play on it");
        }

        /// <summary>
        /// Every body made from the player's walks, runs and jumps in Bloodrage as the player does. The joining player's body shipped with the
        /// Bloodrage walk and run blanked (found 2026-10-17): the rig builder deleted and re-made those two clips on every build, and a body
        /// saved while it still held the deleted ones kept nothing (ClipAsset now writes them in place).
        /// </summary>
        [TestCase("NetworkedPlayer")]
        [TestCase("Swordsman")]
        [TestCase("Shoota")]
        public void EveryBodyMadeFromThePlayersKeepsItsBloodrageClips(string body)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab").GetComponentInChildren<BloodrageAbility>(true);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Character/{body}.prefab");
            Assert.IsNotNull(prefab, $"no {body}.prefab");
            var rage = prefab.GetComponentInChildren<BloodrageAbility>(true);
            Assert.IsNotNull(rage, $"{body} has no Bloodrage");
            Assert.Greater(player.BloodrageClips.Length, 0, "the player has no Bloodrage clips to compare with");
            Assert.AreEqual(player.BloodrageClips.Length, rage.BloodrageClips.Length, $"{body}: a different number of Bloodrage clips");
            for (int i = 0; i < player.BloodrageClips.Length; i++)
            {
                Assert.IsNotNull(rage.BloodrageClips[i], $"{body}: Bloodrage clip {i} is blank, so that move plays its ordinary animation in Bloodrage");
                Assert.AreSame(player.BloodrageClips[i], rage.BloodrageClips[i], $"{body}: Bloodrage clip {i} is not the player's");
            }
        }
    }
}
