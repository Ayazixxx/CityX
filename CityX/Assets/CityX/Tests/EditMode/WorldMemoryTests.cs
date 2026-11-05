using CityX.World;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// A new world begins with nothing a player did in the last one (WorldMemory): the lights switched, the doors opened and Old Guy's
    /// house are kept in memory while the program runs, and nothing but tests ever cleared them.
    /// </summary>
    public sealed class WorldMemoryTests
    {
        [TearDown]
        public void TearDown()
        {
            WorldMemory.Forget();
        }

        [Test]
        public void ANewWorldForgetsWhatWasDoneInTheLast()
        {
            var room = new RoomKey(7, 2, 0);
            RoomLights.Set(room, true);
            PowerGrid.Restore(7);
            BalconyDoorState.Shared.Set(7, 1, 3, true);
            TutorialHouseDamage.Shared.Break(0);
            TutorialHousePower.Shared.TurnOn();
            Assert.IsTrue(RoomLights.IsOn(room) && BalconyDoorState.Shared.IsOpen(7, 1, 3) && TutorialHouseDamage.Shared.IsBroken(0)
                          && TutorialHousePower.Shared.IsOn, "setup: nothing was changed");

            WorldMemory.Forget();

            Assert.IsEmpty(RoomLights.AllOn(), "a light switched on in the last world is on in the new one");
            Assert.IsEmpty(BalconyDoorState.Shared.AllOpen(), "a balcony door opened in the last world is open in the new one");
            Assert.IsEmpty(TutorialHouseDamage.Shared.All(), "Old Guy's house is already broken into");
            Assert.IsFalse(TutorialHousePower.Shared.IsOn, "Old Guy's house is already lit");
        }
    }
}
