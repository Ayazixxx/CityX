using CityX.View;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// E works ONE thing, the one nearest the crosshair (View.InteractionFocus): a door and the light switch beside it used to both answer the
    /// same press (owner, 2026-10-09: "I keep accidentally opening and closing the door when I'm only trying to do the light switch").
    /// </summary>
    public sealed class InteractionFocusTests
    {
        [TearDown]
        public void TearDown()
        {
            InteractionFocus.Resolve();
        }

        [Test]
        public void OnlyTheThingNearestTheCrosshairActs()
        {
            int door = 0, light = 0;
            InteractionFocus.Offer(24.0f, () => door++);   // the door, off to one side
            InteractionFocus.Offer(3.0f, () => light++);   // the switch, under the crosshair
            InteractionFocus.Resolve();
            Assert.AreEqual(0, door, "the door moved while the player was aiming at the switch");
            Assert.AreEqual(1, light);

            InteractionFocus.Offer(2.0f, () => door++);
            InteractionFocus.Offer(19.0f, () => light++);
            InteractionFocus.Resolve();
            Assert.AreEqual(1, door);
            Assert.AreEqual(1, light, "the switch flipped while the player was aiming at the door");
        }

        [Test]
        public void APressActsOnceAndOnlyOnce()
        {
            int door = 0;
            InteractionFocus.Offer(5.0f, () => door++);
            InteractionFocus.Resolve();
            InteractionFocus.Resolve();
            Assert.AreEqual(1, door);
        }
    }
}
