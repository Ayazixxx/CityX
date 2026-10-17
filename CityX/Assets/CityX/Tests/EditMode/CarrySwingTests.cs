using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The carried sword hand swings with the walk (owner, 2026-10-17: "the right hand appears frozen"): CarrySwing on its own.</summary>
    public sealed class CarrySwingTests
    {
        private const float Dt = 1.0f / 60.0f;

        [Test]
        public void AHandSwingingBackAndForthIsGivenBackNearlyInFull()
        {
            var swing = new CarrySwing();
            Vector3 middle = new Vector3(0.1f, -0.7f, 0.05f);
            float most = 0.0f, least = 0.0f;
            for (int i = 0; i < 600; i++)
            {
                float forward = 0.15f * Mathf.Sin(2.0f * Mathf.PI * 1.0f * i * Dt);   // a step a second, 15 cm each way
                Vector3 offset = swing.Step(middle + Vector3.forward * forward, Dt);
                if (i > 300)
                {
                    most = Mathf.Max(most, offset.z);
                    least = Mathf.Min(least, offset.z);
                }
            }
            Assert.Greater(most, 0.13f, "the forward swing is lost");
            Assert.Less(least, -0.13f, "the backward swing is lost");
        }

        [Test]
        public void AStillHandStaysStillAndTheFirstFrameDoesNotJump()
        {
            var swing = new CarrySwing();
            Vector3 hand = new Vector3(0.1f, -0.7f, 0.05f);
            Assert.AreEqual(Vector3.zero, swing.Step(hand, Dt));
            for (int i = 0; i < 120; i++) swing.Step(hand, Dt);
            Assert.Less(swing.Step(hand, Dt).magnitude, 1e-4f);
        }

        [Test]
        public void ASuddenJumpOfTheClipHandIsCappedAndFadesAway()
        {
            var swing = new CarrySwing();
            swing.Step(Vector3.zero, Dt);
            Vector3 jump = swing.Step(new Vector3(0.0f, 0.0f, 2.0f), Dt);
            Assert.LessOrEqual(jump.magnitude, CarrySwing.MaxSwingMetres + 1e-4f);
            Vector3 later = Vector3.zero;
            for (int i = 0; i < 240; i++) later = swing.Step(new Vector3(0.0f, 0.0f, 2.0f), Dt);
            Assert.Less(later.magnitude, 0.01f, "a moved hand is not settled into the new middle");
        }
    }
}
