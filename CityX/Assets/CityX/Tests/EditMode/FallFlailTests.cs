using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// The intro's falling body lies flat on its back and the wind throws its arms and legs about (FallFlail; owner, 2026-10-22: "make it so
    /// you are laying flat horizontal during the fall cutscene ... make some amount of flailing in the air like there is wind").
    /// </summary>
    public sealed class FallFlailTests
    {
        [Test]
        public void TheFrontOfABodyIsTheWayItsChestFaces()
        {
            // Standing facing +z: hips under the neck, left shoulder at -x.
            Vector3 front = FallFlail.Front(Vector3.zero, Vector3.up, Vector3.left, Vector3.right);
            Assert.AreEqual(1.0f, Vector3.Dot(front, Vector3.forward), 1e-5f);
            // Tipped over backwards with its head to -z: the chest faces the sky.
            Vector3 lying = FallFlail.Front(Vector3.zero, Vector3.back, Vector3.left, Vector3.right);
            Assert.AreEqual(1.0f, Vector3.Dot(lying, Vector3.up), 1e-5f);
            // Fallen forwards on its face, head to +z: the chest faces the ground.
            Vector3 onItsFace = FallFlail.Front(Vector3.zero, Vector3.forward, Vector3.left, Vector3.right);
            Assert.AreEqual(-1.0f, Vector3.Dot(onItsFace, Vector3.up), 1e-5f);
        }

        [Test]
        public void AStandingBodyTipsOverOntoItsBack()
        {
            Vector3 front = Vector3.forward;
            Vector3 turn = FallFlail.Righting(front, Vector3.up, Vector3.zero);
            Assert.Greater(turn.magnitude, 1.0f, "a standing body is left standing");
            // Turning about that axis carries the chest up toward the sky.
            Assert.Greater(Vector3.Dot(Vector3.Cross(turn, front), Vector3.up), 0.0f, "it tips onto its front, not its back");
        }

        [Test]
        public void ABodyOnItsBackIsLeftThereAndItsSpinIsTakenOff()
        {
            Assert.Less(FallFlail.Righting(Vector3.up, Vector3.forward, Vector3.zero).magnitude, 1e-4f, "a body on its back is turned");
            var spin = new Vector3(0.0f, 0.0f, 3.0f);
            Vector3 turn = FallFlail.Righting(Vector3.up, Vector3.forward, spin);
            Assert.Less(Vector3.Dot(turn, spin), 0.0f, "its spin is not slowed");
        }

        [Test]
        public void ABodyFaceDownRollsOverAlongItsSpine()
        {
            Vector3 turn = FallFlail.Righting(Vector3.down, Vector3.forward, Vector3.zero);
            Assert.Greater(turn.magnitude, 1.0f, "a body face down is left face down");
            Assert.AreEqual(1.0f, Mathf.Abs(Vector3.Dot(turn.normalized, Vector3.forward)), 1e-4f, "it does not roll along its spine");
        }

        [Test]
        public void TheWindLiftsTheLimbsAndThrowsEachAboutItsOwnWay()
        {
            Assert.AreEqual(Vector3.zero, FallFlail.Wind(FallFlail.LeftArm, 3.0f, 0.0f), "wind on a body that is not falling");

            float speed = FallFlail.FullWindSpeed;
            Vector3 lift = Vector3.zero;
            float largest = 0.0f;
            for (int i = 0; i < 600; i++)
            {
                Vector3 wind = FallFlail.Wind(FallFlail.RightLeg, i / 60.0f, speed);
                lift += wind / 600.0f;
                largest = Mathf.Max(largest, wind.magnitude);
            }
            Assert.Greater(lift.y, FallFlail.WindLift * 0.5f, "the wind does not lift the limbs");
            Assert.LessOrEqual(largest, FallFlail.WindLift + FallFlail.GustAcceleration * 2.0f, "a gust that would tear the limb off");

            Assert.Greater((FallFlail.Wind(FallFlail.LeftArm, 2.0f, speed) - FallFlail.Wind(FallFlail.RightArm, 2.0f, speed)).magnitude, 1.0f,
                "both arms thrown about alike");
            Assert.Greater((FallFlail.Wind(FallFlail.LeftArm, 2.0f, speed) - FallFlail.Wind(FallFlail.LeftArm, 2.5f, speed)).magnitude, 1.0f,
                "an arm held still by the gusts");
            Assert.Less(FallFlail.Wind(FallFlail.LeftArm, 2.0f, speed * 0.2f).magnitude, FallFlail.Wind(FallFlail.LeftArm, 2.0f, speed).magnitude,
                "the wind no weaker at the start of the fall");
        }
    }
}
