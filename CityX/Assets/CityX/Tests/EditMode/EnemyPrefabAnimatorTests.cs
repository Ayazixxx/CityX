using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Every enemy built on the player's body plays the player's animation (owner, 2026-10-17: "make the npc have all the movement animations
    /// that i do"). Their builders copy Player.prefab while the player rig is rebuilt in the same run, and the Animator's controller came out
    /// empty: the Shoota had been standing in his bind pose with nothing playing, and the swordsman did too until this was caught.
    /// </summary>
    public sealed class EnemyPrefabAnimatorTests
    {
        private const string ControllerPath = "Assets/CityX/Character/PlayerAnimator.controller";

        [TestCase("Assets/CityX/Character/Swordsman.prefab")]
        [TestCase("Assets/CityX/Character/Shoota.prefab")]
        public void TheEnemyPlaysThePlayersAnimation(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, $"{path} is missing -- rebuild the scene");
            var animator = prefab.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator, $"{path} has no Animator");
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            Assert.IsNotNull(animator.runtimeAnimatorController, $"{path}'s Animator has no controller: he stands still with nothing playing");
            Assert.AreEqual(controller, animator.runtimeAnimatorController, $"{path} plays another controller than the player's");
            Assert.IsNotNull(animator.avatar, $"{path}'s Animator has no avatar");
        }
    }
}
