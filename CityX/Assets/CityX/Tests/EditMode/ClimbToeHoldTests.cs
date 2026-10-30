using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// A climbing foot on its toes (owner, 2026-10-30: "the bottom of the feet is pressed flat on the wall instead of it being like the tip of the toes"): it points
    /// into the wall, its sole does not face it, the front of the toe is the part nearest the wall, and each foot turns out to its own side.
    /// </summary>
    public sealed class ClimbToeHoldTests
    {
        private static readonly Vector3[] Normals = { Vector3.back, Vector3.right, new Vector3(0.6f, 0.0f, 0.8f), new Vector3(-0.3f, 0.15f, -0.95f).normalized };

        /// <summary>The foot's outline in its own frame: its toe and heel rounds at the sole and at the top of the foot.</summary>
        private static Vector3[] FootOutline()
        {
            var points = new System.Collections.Generic.List<Vector3>();
            float sole = -BodyRig.FootY;
            float top = sole + FusedBodyShape.FootHeight - FusedBodyShape.FootTopRounding;
            foreach (float y in new[] { sole, top })
            {
                for (int i = 0; i < 36; i++)
                {
                    float a = i * 10.0f * Mathf.Deg2Rad;
                    var round = new Vector3(Mathf.Sin(a), 0.0f, Mathf.Cos(a));
                    points.Add(new Vector3(0.0f, y, FusedBodyShape.FootToeCentreForward) + round * FusedBodyShape.FootToeRadius);
                    points.Add(new Vector3(0.0f, y, FusedBodyShape.FootHeelCentreForward) + round * FusedBodyShape.FootHeelRadius);
                }
            }
            return points.ToArray();
        }

        [Test]
        public void TheFootPointsIntoTheWallAndItsSoleDoesNotFaceIt()
        {
            foreach (Vector3 normal in Normals)
            {
                foreach (bool left in new[] { true, false })
                {
                    Quaternion foot = ClimbToeHold.Rotation(normal, left);
                    float intoWall = Vector3.Dot(foot * Vector3.forward, -normal);
                    Assert.Greater(intoWall, Mathf.Cos(40.0f * Mathf.Deg2Rad), $"the toes point {Mathf.Acos(intoWall) * Mathf.Rad2Deg:F0} degrees off the wall");
                    float soleToWall = Vector3.Dot(foot * Vector3.down, -normal);
                    Assert.Less(soleToWall, 0.5f, "the sole faces the wall: pressed flat on it again");
                }
            }
        }

        [Test]
        public void EachFootTurnsOutToItsOwnSide()
        {
            foreach (Vector3 normal in Normals)
            {
                Vector3 across = ClimbLimbStepper.Across(normal);   // the body's right, facing the wall
                Assert.Less(Vector3.Dot(ClimbToeHold.Rotation(normal, true) * Vector3.forward, across), -0.1f, "the left foot does not turn out to the left");
                Assert.Greater(Vector3.Dot(ClimbToeHold.Rotation(normal, false) * Vector3.forward, across), 0.1f, "the right foot does not turn out to the right");
            }
        }

        [Test]
        public void TheToeLandsOnItsHoldAndNoOtherPartOfTheFootIsNearerTheWall()
        {
            Vector3[] outline = FootOutline();
            foreach (Vector3 normal in Normals)
            {
                foreach (bool left in new[] { true, false })
                {
                    foreach (float scale in new[] { 1.0f, 1.6f })
                    {
                        Quaternion foot = ClimbToeHold.Rotation(normal, left);
                        Vector3 hold = new Vector3(0.3f, 2.0f, 1.0f);
                        Vector3 ankle = ClimbToeHold.AnkleFor(hold, foot, normal, scale);
                        float holdDepth = Vector3.Dot(hold, normal);
                        foreach (Vector3 p in outline)
                        {
                            float depth = Vector3.Dot(ankle + foot * (p * scale), normal);
                            Assert.GreaterOrEqual(depth, holdDepth - 0.002f, "a part of the foot other than the toe's front reaches nearer the wall than the toe");
                        }
                        float standOff = Vector3.Dot(ankle - hold, normal);
                        Assert.Greater(standOff, 0.15f * scale, $"the ankle stands only {standOff:F2} m off the toe's hold: the foot is not pointing into the wall");
                    }
                }
            }
        }
    }
}
