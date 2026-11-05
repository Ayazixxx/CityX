using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>A player's speeds by the keys held (FreeMovement): backing up is the slow way, and the rest stay a full walk.</summary>
    public sealed class FreeMovementTests
    {
        [Test]
        public void BackingUpIsSlowerAndEveryOtherWayIsAFullWalk()
        {
            Assert.AreEqual(FreeMovement.BackwardSpeedShare, FreeMovement.SpeedShare(Vector2.down), 1e-4f);
            Assert.Less(FreeMovement.BackwardSpeedShare, 1.0f);
            foreach (Vector2 keys in new[] { Vector2.up, Vector2.left, Vector2.right, new Vector2(0.7071f, 0.7071f), Vector2.zero })
            {
                Assert.AreEqual(1.0f, FreeMovement.SpeedShare(keys), 1e-4f, $"keys {keys}");
            }
        }

        [Test]
        public void AGamepadStickTiltedBackGoesTheSameShareHoweverFarItIsPushed()
        {
            Assert.AreEqual(FreeMovement.SpeedShare(Vector2.down), FreeMovement.SpeedShare(new Vector2(0.0f, -0.3f)), 1e-4f);
        }

        [Test]
        public void ADiagonalBackIsBetweenABackwardWalkAndAFullOne()
        {
            float share = FreeMovement.SpeedShare(new Vector2(0.7071f, -0.7071f));
            Assert.Greater(share, FreeMovement.BackwardSpeedShare);
            Assert.Less(share, 1.0f);
        }

        /// <summary>A little weight, not GTA's: a player's body still has to answer the keys in a fight.</summary>
        [Test]
        public void APlayersWeightIsLight()
        {
            const float walk = 4.0f, step = 1.0f / 60.0f;
            Vector3 velocity = Vector3.zero;
            float seconds = 0.0f;
            while (velocity.magnitude < walk - 0.01f)
            {
                velocity = MovementWeight.Step(velocity, Vector3.forward * walk, FreeMovement.PlayerFootScale, step);
                seconds += step;
                Assert.Less(seconds, 1.0f, "it never reached a walk");
            }
            Assert.That(seconds, Is.InRange(0.12f, 0.3f), "reaching a walk");
            seconds = 0.0f;
            while (velocity.magnitude > 0.01f)
            {
                velocity = MovementWeight.Step(velocity, Vector3.zero, FreeMovement.PlayerFootScale, step);
                seconds += step;
                Assert.Less(seconds, 1.0f, "it never stopped");
            }
            Assert.That(seconds, Is.InRange(0.08f, 0.22f), "stopping from a walk");
        }
    }
}
