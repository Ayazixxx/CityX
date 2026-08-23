using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE SELF-OVERLAP REGRESSION. Bind() puts the interact trigger
    /// SphereCollider on the same GameObject the CharacterController
    /// RequireComponent demands, and with no Rigidbody grouping the two,
    /// Unity treats them as separate bodies that collide with each other --
    /// confirmed live via CityX-NPC-DIAG log output showing OnTriggerEnter
    /// firing with `other` equal to the NPC itself, at distance 0.00,
    /// forever (self-distance can never exceed InteractRange, so the
    /// stale-reference self-heal in HandleInteractInput never kicks in
    /// either). OnTriggerEnter now excludes its own Controller by identity
    /// rather than a bare CharacterController-anywhere null check. Needs
    /// PlayMode, not EditMode -- OnTriggerEnter/Exit do not fire without a
    /// real physics step (see OldGuyTests's own note on the same split).
    /// </summary>
    public sealed class UniqueNpcPlayTests
    {
        private GameObject _npcObject;
        private UniqueNpc _npc;

        [SetUp]
        public void SetUp()
        {
            _npcObject = new GameObject("Npc", typeof(CharacterController));
            _npc = _npcObject.AddComponent<UniqueNpc>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_npcObject);
        }

        [UnityTest]
        public IEnumerator AloneInTheSceneNeverTreatsItselfAsANearbyPlayer()
        {
            for (int step = 0; step < 10; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsFalse(_npc.HasNearbyPlayer,
                "an NPC's own CharacterController overlaps its own interact trigger every single time -- " +
                "OnTriggerEnter has to exclude it by identity, not just null-check for *a* CharacterController");
        }

        [UnityTest]
        public IEnumerator ARealPlayerWalkingUpAndAwayIsTrackedCorrectly()
        {
            var player = new GameObject("Player", typeof(CharacterController));
            player.transform.position = new Vector3(0.0f, 0.0f, -6.0f);
            CharacterController controller = player.GetComponent<CharacterController>();

            Vector3 towardNpc = (_npcObject.transform.position - player.transform.position).normalized;
            for (int step = 0; step < 400 && !_npc.HasNearbyPlayer; step++)
            {
                controller.Move(towardNpc * (4.0f * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
            }
            Assert.IsTrue(_npc.HasNearbyPlayer,
                "walking a real CharacterController into the real trigger should still register a real player");

            for (int step = 0; step < 400 && _npc.HasNearbyPlayer; step++)
            {
                controller.Move(-towardNpc * (4.0f * Time.fixedDeltaTime));
                yield return new WaitForFixedUpdate();
            }
            Assert.IsFalse(_npc.HasNearbyPlayer, "walking back out of range should clear it, same as before this fix");

            Object.DestroyImmediate(player);
        }
    }
}
