using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// Asking the server for three of the same thing off a body (a hobo's spare bottles) gets three: each answer is one ask's, and the asks
    /// left wait for theirs (bug sweep 2026-11-25: the first answer cleared them all, and two bottles vanished).
    /// </summary>
    public sealed class LootAsksTests
    {
        [Test]
        public void EachOfThreeAsksForTheSameThingIsAnsweredIntoTheBag()
        {
            var holder = new GameObject("Looter");
            try
            {
                var bag = holder.AddComponent<Inventory>();
                var asks = new LootAsks();
                for (int i = 0; i < 3; i++)
                {
                    asks.Ask("bottle", bag);
                }
                Assert.AreEqual(3, asks.Waiting("bottle"));
                for (int i = 0; i < 3; i++)
                {
                    Assert.AreSame(bag, asks.Answer("bottle"), $"answer {i + 1} of 3 went nowhere");
                }
                Assert.AreEqual(0, asks.Waiting("bottle"));
                Assert.IsNull(asks.Answer("bottle"), "a fourth answer found an ask nobody made");
                Assert.IsNull(asks.Answer("helmet"));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
