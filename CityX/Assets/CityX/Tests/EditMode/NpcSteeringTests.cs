using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// NpcSteering goes round what is in its way, but never round what it is coming for (owner, 2026-11-08, of the Crimson Claw: "it looks
    /// right and left super rapidly like 20 times per second": the player it chased read as a wall, and it sidestepped one way and the other).
    /// </summary>
    public sealed class NpcSteeringTests
    {
        private GameObject _body;
        private GameObject _ahead;

        [SetUp]
        public void SetUp()
        {
            _body = new GameObject("Walker");
            _ahead = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _ahead.transform.position = new Vector3(0.0f, 1.0f, 3.0f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_ahead);
        }

        [Test]
        public void WhatItComesForIsNotInItsWay()
        {
            var steering = new NpcSteering();
            Vector3 way = steering.Steer(_body.transform, Vector3.forward, 0.02f, _ahead.transform);
            Assert.Less(Vector3.Angle(way, Vector3.forward), 0.1f, "it steered round what it was coming for");
            Assert.IsFalse(steering.IsAvoiding);
        }

        [Test]
        public void AnythingElseInTheWayIsSteeredRound()
        {
            var steering = new NpcSteering();
            Vector3 way = steering.Steer(_body.transform, Vector3.forward, 0.02f);
            Assert.Greater(Vector3.Angle(way, Vector3.forward), 30.0f, "it walked straight into something in its way");
            Assert.IsTrue(steering.IsAvoiding);
        }
    }
}
