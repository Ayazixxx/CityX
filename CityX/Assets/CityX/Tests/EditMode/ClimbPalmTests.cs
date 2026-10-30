using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A climbing hand holds the wall (2026-10-30, after the owner's climbing video): its palm flat on the face, its fingers up the wall and leaning out, its thumb
    /// toward the body. The hand bones are the rig's own: the fingers run +X on the right hand and -X on the left, the palm is -Y, the thumb +Z.
    /// </summary>
    public sealed class ClimbPalmTests
    {
        private static readonly Vector3[] Normals = { Vector3.back, Vector3.right, new Vector3(0.6f, 0.0f, 0.8f), new Vector3(-0.3f, 0.15f, -0.95f).normalized };

        [Test]
        public void ThePalmIsFlatOnTheWallTheFingersUpItTheThumbInward()
        {
            foreach (Vector3 normal in Normals)
            {
                Vector3 across = ClimbLimbStepper.Across(normal);   // the body's right, facing the wall
                foreach (bool left in new[] { true, false })
                {
                    Quaternion hand = PlayerLocomotion.PalmOnWall(normal, left);
                    Assert.Greater(Vector3.Dot(hand * Vector3.down, -normal), 0.99f, "the palm does not face the wall");
                    Vector3 fingers = hand * (left ? Vector3.left : Vector3.right);
                    Assert.Greater(Vector3.Dot(fingers, Vector3.up), Mathf.Cos(30.0f * Mathf.Deg2Rad), "the fingers do not point up the wall");
                    float outward = Vector3.Dot(fingers, across) * (left ? -1.0f : 1.0f);
                    Assert.Greater(outward, 0.1f, "the fingers do not lean out to the hand's own side");
                    float thumbInward = Vector3.Dot(hand * Vector3.forward, across) * (left ? 1.0f : -1.0f);
                    Assert.Greater(thumbInward, 0.8f, "the thumb does not point toward the body");
                }
            }
        }
    }
}
