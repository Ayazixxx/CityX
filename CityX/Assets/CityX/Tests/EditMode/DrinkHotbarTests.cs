using CityX.Character;
using CityX.Items;
using CityX.UI;
using NUnit.Framework;

namespace CityX.Tests
{
    /// <summary>
    /// Drinks on the hotbar (owner, 2026-11-23: "make it so drinks can be equipped in the hotbar"; docs/PHIZZ_CAN.md): a drink goes in a slot, a weapon does not,
    /// the bar keeps its drinks through a save, and a drink's slot shows the last word of its name until icons are drawn.
    /// </summary>
    public sealed class DrinkHotbarTests
    {
        [Test]
        public void ADrinkGoesOnTheBarAndStaysThroughASaveAndAWeaponDoesNot()
        {
            var bar = new Hotbar();
            Assert.IsTrue(Hotbar.IsBarItem(ItemRegistry.PhizzCan));
            Assert.IsTrue(Hotbar.IsBarItem(ItemRegistry.PhizzBottle));
            Assert.IsFalse(Hotbar.IsBarItem(GlassBottleKinds.Soda), "an empty bottle is a weapon, not a drink");
            Assert.IsTrue(bar.Put(4, ItemRegistry.PhizzCan));
            Assert.IsFalse(bar.Put(5, GlassBottleKinds.Soda));
            Assert.AreEqual(ItemRegistry.PhizzCan, bar[4]);

            var loaded = new Hotbar();
            loaded.Load(bar.ToList());
            Assert.AreEqual(ItemRegistry.PhizzCan, loaded[4], "the drink was lost from the bar in the save");
        }

        [Test]
        public void ADrinksSlotShowsTheLastWordOfItsName()
        {
            Assert.AreEqual("S", SkillIcons.LetterFor(ItemRegistry.PhizzCan));
            Assert.AreEqual("B", SkillIcons.LetterFor(ItemRegistry.PhizzBottle));
        }
    }
}
