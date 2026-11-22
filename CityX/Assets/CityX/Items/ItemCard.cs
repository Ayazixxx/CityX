using System.Collections.Generic;

namespace CityX.Items
{
    /// <summary>One line of an item's card: a label, what it reads, and the number behind it when there is one to compare.</summary>
    public readonly struct CardRow
    {
        public readonly string Label;
        public readonly string Text;
        public readonly float Number;
        public readonly bool HasNumber;

        /// <summary>Whether a bigger number is the better item (damage) or a smaller one is (mana per cast, the time to draw).</summary>
        public readonly bool HigherIsBetter;

        public CardRow(string label, string text, float number, bool hasNumber, bool higherIsBetter)
        {
            Label = label;
            Text = text;
            Number = number;
            HasNumber = hasNumber;
            HigherIsBetter = higherIsBetter;
        }
    }

    /// <summary>
    /// WHAT AN ITEM SAYS ABOUT ITSELF when inspected (docs/ITEM_INSPECT.md; the owner's Sword Art Online appraisal card): its name and rarity,
    /// what kind of thing it is and how it is held, the style (skill tree) it belongs to, the numbers that matter for its kind (a blade's
    /// attack, speed and reach; a gun's attack, fire rate, magazine and range; a staff's spell, attack and mana), what stat makes it
    /// stronger, and a few notes (a knife's backstab, a wand's boost). No durability (the owner, 2026-10-30) and no armour yet (2026-10-31:
    /// "we will figure out what armor is gonna look like later"). Plain data: Weapons.ItemCards fills one from an item's prefab.
    /// </summary>
    public sealed class ItemCard
    {
        public readonly string ItemId;
        public readonly string Name;
        public readonly Rarity Rarity;

        /// <summary>What it is ("Knife", "Pistol", "Staff", "Top"), and how it is held ("One Hand", "Two Hands", "Worn").</summary>
        public string Kind = "Item";

        public string Hands = "One Hand";

        /// <summary>The style it belongs to (docs/GAME_DESIGN.md, the styles table), and the stat that raises its numbers.</summary>
        public string Style = "";

        public string ScalesWith = "";

        /// <summary>What its hits are (Weapons.HitTypes: "Slash (heavy stab: Pierce)"), shown on a line of its own under the rarity; empty for what does no damage.</summary>
        public string Hits = "";

        public readonly List<CardRow> Rows = new List<CardRow>();

        public readonly List<string> Notes = new List<string>();

        public ItemCard(string itemId, string name, Rarity rarity)
        {
            ItemId = itemId;
            Name = name;
            Rarity = rarity;
        }

        /// <summary>"Knife / One Hand", as the appraisal card prints it under the name.</summary>
        public string TypeLine => string.IsNullOrEmpty(Hands) ? Kind : Kind + " / " + Hands;

        public ItemCard Add(string label, string text)
        {
            Rows.Add(new CardRow(label, text, 0.0f, false, true));
            return this;
        }

        public ItemCard Add(string label, float number, string text, bool higherIsBetter = true)
        {
            Rows.Add(new CardRow(label, text, number, true, higherIsBetter));
            return this;
        }

        public ItemCard Note(string note)
        {
            Notes.Add(note);
            return this;
        }

        /// <summary>
        /// How this card's row <paramref name="row"/> stands against the same line of <paramref name="held"/> (the item in the hand): 1 when this
        /// is the better, -1 the worse, 0 when they are equal or there is nothing to compare (no such line on the other, or no number).
        /// </summary>
        public int Compare(int row, ItemCard held)
        {
            if (held == null || row < 0 || row >= Rows.Count || !Rows[row].HasNumber)
            {
                return 0;
            }
            CardRow mine = Rows[row];
            foreach (CardRow theirs in held.Rows)
            {
                if (theirs.Label != mine.Label || !theirs.HasNumber)
                {
                    continue;
                }
                float difference = mine.Number - theirs.Number;
                if (difference > -1e-4f && difference < 1e-4f)
                {
                    return 0;
                }
                return (difference > 0.0f) == mine.HigherIsBetter ? 1 : -1;
            }
            return 0;
        }
    }
}
