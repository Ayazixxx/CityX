using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>GTA-style turning's numbers (MovementTurn): slower turns at speed, and a sharp turn braking.</summary>
    public sealed class MovementTurnTests
    {
        [Test]
        public void TheFasterTheBodyGoesTheSlowerItTurns()
        {
            float standing = MovementTurn.DegreesPerSecond(0.0f, 4.0f, 7.5f);
            float walking = MovementTurn.DegreesPerSecond(4.0f, 4.0f, 7.5f);
            float sprinting = MovementTurn.DegreesPerSecond(7.5f, 4.0f, 7.5f);
            Assert.Greater(standing, walking);
            Assert.Greater(walking, sprinting);
            Assert.AreEqual(MovementTurn.SprintDegreesPerSecond, MovementTurn.DegreesPerSecond(20.0f, 4.0f, 7.5f), 1e-3f, "past a sprint it turns no slower");
        }

        [Test]
        public void AGentleTurnKeepsItsSpeedAndAReversalBrakes()
        {
            Assert.AreEqual(1.0f, MovementTurn.SpeedShare(30.0f), 1e-4f);
            Assert.Less(MovementTurn.SpeedShare(120.0f), 1.0f);
            Assert.AreEqual(MovementTurn.BrakedSpeedShare, MovementTurn.SpeedShare(180.0f), 1e-4f);
        }
    }
}
