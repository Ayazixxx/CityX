using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// What a hobo carries, told to the server by the machine that runs him (NetworkedCorpseLoot): only ever less of what he had, so a client
    /// cannot put new things on a body to loot.
    /// </summary>
    public sealed class CarriedLootSyncTests
    {
        private static readonly List<string> ThreeBottles = new List<string> { "bottle", "bottle", "bottle" };

        [Test]
        public void WhatHeCarriesOnlyEverGoesDown()
        {
            Assert.IsTrue(NetworkedCorpseLoot.CarriesNoMoreThan(new[] { "bottle", "bottle" }, ThreeBottles), "one thrown");
            Assert.IsTrue(NetworkedCorpseLoot.CarriesNoMoreThan(new string[0], ThreeBottles), "all thrown");
            Assert.IsFalse(NetworkedCorpseLoot.CarriesNoMoreThan(new[] { "bottle", "bottle", "bottle", "bottle" }, ThreeBottles), "a fourth bottle appeared");
            Assert.IsFalse(NetworkedCorpseLoot.CarriesNoMoreThan(new[] { "ak47" }, ThreeBottles), "something he never had appeared");
            Assert.IsFalse(NetworkedCorpseLoot.CarriesNoMoreThan(new string[] { null }, ThreeBottles));
        }
    }
}
