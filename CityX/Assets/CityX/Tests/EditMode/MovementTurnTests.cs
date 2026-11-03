using CityX.Character;
using NUnit.Framework;
using UnityEngine;

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

        /// <summary>Turns from yaw 0 to <paramref name="wanted"/> at a steady speed, a frame at a time, and reports what the eye sees.</summary>
        private static void Turn(float wanted, float speed, out float secondsToNineTenths, out float firstFrame, out float mostInAFrame, out float furthest)
        {
            const float step = 1.0f / 60.0f;
            float yaw = 0.0f, velocity = 0.0f;
            secondsToNineTenths = -1.0f;
            firstFrame = 0.0f;
            mostInAFrame = 0.0f;
            furthest = 0.0f;
            for (int frame = 1; frame <= 180; frame++)
            {
                float next = MovementTurn.Step(yaw, wanted, ref velocity, speed, 4.0f, 7.5f, step);
                float moved = Mathf.Abs(Mathf.DeltaAngle(yaw, next));
                if (frame == 1) firstFrame = moved;
                mostInAFrame = Mathf.Max(mostInAFrame, moved);
                yaw = next;
                furthest = Mathf.Max(furthest, Mathf.Abs(yaw));
                if (secondsToNineTenths < 0.0f && Mathf.Abs(yaw) >= Mathf.Abs(wanted) * 0.9f)
                {
                    secondsToNineTenths = frame * step;
                }
            }
            Assert.AreEqual(wanted, yaw, 0.1f, "the turn never arrived");
        }

        [Test]
        public void GoingFromWToWPlusAAtAWalkEasesRoundInAboutAThirdOfASecond()
        {
            Turn(-45.0f, 4.0f, out float seconds, out float firstFrame, out float mostInAFrame, out float furthest);
            Assert.Greater(seconds, 0.2f, "it still snaps");
            Assert.Less(seconds, 0.4f, "it answers the keys too slowly");
            Assert.Less(firstFrame, 2.0f, "it does not ease in");
            Assert.Less(mostInAFrame, 5.0f, "some frame still jumps");
            Assert.Less(furthest, 45.5f, "it swung past and came back");
        }

        [Test]
        public void AnAboutFaceFromAStandStaysQuick()
        {
            Turn(180.0f, 0.0f, out float seconds, out _, out _, out _);
            Assert.Less(seconds, 0.3f);
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
