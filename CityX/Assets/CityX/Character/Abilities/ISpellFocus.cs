namespace CityX.Character
{
    /// <summary>
    /// Something held that sways every spell cast through it (GAME_DESIGN.md: general staffs and wands boost every spell cast through them): the
    /// Magic Wand (docs/MAGIC_WAND.md). The body's PlayerVitals reads it at each cast: its Spell Power on top of the stat's, and its share of the
    /// spell's mana.
    /// </summary>
    public interface ISpellFocus
    {
        /// <summary>Spell Power points added to a spell cast through it (2% of its damage, or its healing, a point).</summary>
        float SpellPowerBonus { get; }

        /// <summary>What a spell's mana is multiplied by (below 1, cheaper).</summary>
        float ManaCostFactor { get; }
    }
}
