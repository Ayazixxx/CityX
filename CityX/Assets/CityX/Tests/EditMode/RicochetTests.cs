using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Where a turned-aside bullet goes (Ricochet; owner, 2026-10-27: "it can fly in a random direction but not if its going to hit an ally"):
    /// random, back the way it came within the cone; a way the trace refuses is passed over; with none safe, nowhere.
    /// </summary>
    public sealed class RicochetTests
    {
        [Test]
        public void ItFliesOffAtRandomBackTheWayItCameWithinTheCone()
        {
            var random = new System.Random(7);
            Vector3 incoming = Vector3.forward;   // fired along +z at the guard
            float leftMost = 0.0f, rightMost = 0.0f;
            for (int i = 0; i < 200; i++)
            {
                Assert.IsTrue(Ricochet.TryPick(Vector3.zero, incoming, random, AnyWay, out Vector3 way, out _));
                Vector3 flat = new Vector3(way.x, 0.0f, way.z);
                float off = Vector3.SignedAngle(Vector3.back, flat, Vector3.up);
                float pitch = Mathf.Asin(way.y) * Mathf.Rad2Deg;
                Assert.LessOrEqual(Mathf.Abs(off), Ricochet.ConeDegrees + 0.01f, "it flew outside the cone");
                Assert.GreaterOrEqual(pitch, Ricochet.LowestPitch - 0.01f);
                Assert.LessOrEqual(pitch, Ricochet.HighestPitch + 0.01f);
                leftMost = Mathf.Min(leftMost, off);
                rightMost = Mathf.Max(rightMost, off);
            }
            Assert.Less(leftMost, -40.0f, "it never went far to one side: not random");
            Assert.Greater(rightMost, 40.0f, "it never went far to the other side: not random");
        }

        [Test]
        public void AWayThatWouldHitSomeoneItMustNotIsPassedOver()
        {
            var random = new System.Random(11);
            for (int i = 0; i < 100; i++)
            {
                if (Ricochet.TryPick(Vector3.zero, Vector3.forward, random, OnlyRightOfBack, out Vector3 way, out _))
                {
                    Assert.Greater(way.x, 0.0f, "it took a way the trace refused");
                }
            }
        }

        [Test]
        public void WithNoSafeWayItGoesNowhere()
        {
            Assert.IsFalse(Ricochet.TryPick(Vector3.zero, Vector3.forward, new System.Random(3), NoWay, out Vector3 way, out Vector3 end));
            Assert.AreEqual(Vector3.zero, way);
            Assert.AreEqual(Vector3.zero, end);
        }

        private static bool AnyWay(Vector3 origin, Vector3 direction, out Vector3 end)
        {
            end = origin + direction * 10.0f;
            return true;
        }

        private static bool OnlyRightOfBack(Vector3 origin, Vector3 direction, out Vector3 end)
        {
            end = origin + direction * 10.0f;
            return direction.x > 0.0f;
        }

        private static bool NoWay(Vector3 origin, Vector3 direction, out Vector3 end)
        {
            end = origin;
            return false;
        }
    }
}
