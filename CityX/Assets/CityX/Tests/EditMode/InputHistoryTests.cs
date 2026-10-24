using CityX.UI;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>Up and Down walk what was typed before (owner, 2026-10-24), in the console and the chat, each its own.</summary>
    public sealed class InputHistoryTests
    {
        [Test]
        public void UpGoesBackThroughTheLinesAndDownComesBackToWhatWasBeingTyped()
        {
            var history = new InputHistory();
            history.Add("/spawn swordsman 3");
            history.Add("/heal");
            history.Add("/goto slope");

            Assert.AreEqual("/goto slope", history.Older("/eq"));
            Assert.AreEqual("/heal", history.Older("/goto slope"));
            Assert.AreEqual("/spawn swordsman 3", history.Older("/heal"));
            Assert.AreEqual("/spawn swordsman 3", history.Older("/spawn swordsman 3"), "past the oldest it stays on the oldest");
            Assert.AreEqual("/heal", history.Newer("/spawn swordsman 3"));
            Assert.AreEqual("/goto slope", history.Newer("/heal"));
            Assert.AreEqual("/eq", history.Newer("/goto slope"), "past the newest the half-typed line did not come back");
            Assert.AreEqual("/eq", history.Newer("/eq"));
        }

        [Test]
        public void EmptyAndRepeatedLinesAreNotKeptTwiceAndTheOldestGo()
        {
            var history = new InputHistory();
            history.Add("hi");
            history.Add("hi");
            history.Add("   ");
            Assert.AreEqual(1, history.Count);
            for (int i = 0; i < InputHistory.MaxLines + 5; i++)
            {
                history.Add("line " + i);
            }
            Assert.AreEqual(InputHistory.MaxLines, history.Count);
        }

        [Test]
        public void SendingALineOrReopeningEndsTheWalk()
        {
            var history = new InputHistory();
            history.Add("a");
            history.Add("b");
            Assert.AreEqual("b", history.Older(""));
            Assert.AreEqual("a", history.Older("b"));
            history.EndWalk();
            Assert.AreEqual("b", history.Older(""), "a reopened box did not start from the newest line");
            history.Add("c");
            Assert.AreEqual("c", history.Older(""));
        }

        [Test]
        public void WithNothingTypedYetTheArrowsLeaveTheBoxAlone()
        {
            var history = new InputHistory();
            Assert.AreEqual("typing", history.Older("typing"));
            Assert.AreEqual("typing", history.Newer("typing"));
        }
    }
}
