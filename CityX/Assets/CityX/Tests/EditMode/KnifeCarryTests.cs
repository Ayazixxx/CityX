using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>The carried knife's forearm roll (KnifeCarry): the blade brought round toward where it should point, within a forearm's turn.</summary>
    public sealed class KnifeCarryTests
    {
        [Test]
        public void TheRollBringsTheBladeRoundToThePointAboutTheForearm()
        {
            Vector3 forearm = Vector3.down;
            Vector3 blade = new Vector3(-1.0f, -0.3f, 0.0f);   // down and in across the thigh
            Vector3 wanted = Vector3.right;
            float roll = KnifeCarry.RollDegrees(forearm, blade, wanted, 180.0f);
            Vector3 turned = Quaternion.AngleAxis(roll, forearm) * blade;
            Assert.Greater(Vector3.Dot(turned.normalized, wanted), 0.9f, "the rolled blade does not point out");
        }

        [Test]
        public void TheRollIsNoMoreThanAForearmTurns()
        {
            float roll = KnifeCarry.RollDegrees(Vector3.down, Vector3.forward, Vector3.back + Vector3.right * 0.01f, KnifeCarry.MostRollDegrees);
            Assert.LessOrEqual(Mathf.Abs(roll), KnifeCarry.MostRollDegrees + 1e-3f);
        }

        [Test]
        public void ABladeAlongTheForearmIsNotRolled()
        {
            Assert.AreEqual(0.0f, KnifeCarry.RollDegrees(Vector3.down, Vector3.down, Vector3.right, KnifeCarry.MostRollDegrees), 1e-4f);
        }
    }
}
