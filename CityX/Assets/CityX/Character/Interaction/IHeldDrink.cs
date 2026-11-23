namespace CityX.Character
{
    /// <summary>
    /// A drink in the player's hand (Weapons' PhizzCan, docs/PHIZZ_CAN.md): a click drinks it. Nothing is swung, thrown or blocked with a drink in
    /// hand.
    /// </summary>
    public interface IHeldDrink
    {
        /// <summary>Begins drinking. False if it cannot (already drinking, nothing to drink it with).</summary>
        bool Drink();
    }
}
