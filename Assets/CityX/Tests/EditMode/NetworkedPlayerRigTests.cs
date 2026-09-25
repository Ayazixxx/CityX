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
    }
}
