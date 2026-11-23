using System.Collections.Generic;
using CityX.Items;

namespace CityX.Character
{
    /// <summary>
    /// THE BAR: ten slots on the number keys 1 to 0, all equal (owner, 2026-10-14: "you dont think that 9 and 0 key should be usable"; no ultimate
    /// slot). A slot holds a skill id or nothing. A skill sits in one slot at most: putting it somewhere else moves it. Only skills that go on the
    /// bar (spells and weapon techniques, not movement) are accepted; whether the character has learnt it is PlayerSkills' check. Plain class.
    /// </summary>
    public sealed class Hotbar
    {
        public const int SlotCount = 10;

        private readonly string[] _slots = new string[SlotCount];

        public event System.Action Changed;

        /// <summary>What a fresh bar holds: the number keys as they were before the skill tree (<see cref="SkillCatalog.DefaultBar"/>).</summary>
        public static Hotbar WithDefaults()
        {
            var bar = new Hotbar();
            bar.Load(SkillCatalog.DefaultBar);
            return bar;
        }

        public string this[int slot] => slot >= 0 && slot < SlotCount ? _slots[slot] : null;

        /// <summary>The key printed on a slot: 1 to 9, then 0.</summary>
        public static string KeyLabel(int slot) => slot == SlotCount - 1 ? "0" : (slot + 1).ToString();

        public int SlotOf(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == id)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>A skill that goes on the bar (a spell, a weapon technique), or a drink (an item: its key takes one out of the bag and drinks it).</summary>
        public static bool GoesOnBar(string id)
        {
            SkillNode node = SkillCatalog.Find(id);
            return (node != null && node.GoesOnBar) || IsBarItem(id);
        }

        /// <summary>
        /// An item that goes on the bar: a drink (owner, 2026-11-23: "drinks can be equipped in the hotbar ant it appears and does the drinking animation
        /// immediately"; docs/PHIZZ_CAN.md).
        /// </summary>
        public static bool IsBarItem(string id) =>
            !string.IsNullOrEmpty(id) && ItemRegistry.TryGet(id, out ItemDefinition definition) && definition.Category == ItemRegistry.DrinkCategory;

        /// <summary>Puts a skill in a slot, taking it out of any other (null empties the slot). False when it is not a skill that goes on the bar.</summary>
        public bool Put(int slot, string id)
        {
            if (slot < 0 || slot >= SlotCount)
            {
                return false;
            }
            if (string.IsNullOrEmpty(id))
            {
                return Clear(slot);
            }
            if (!GoesOnBar(id))
            {
                return false;
            }
            int old = SlotOf(id);
            if (old == slot)
            {
                return true;
            }
            if (old >= 0)
            {
                _slots[old] = null;
            }
            _slots[slot] = id;
            Changed?.Invoke();
            return true;
        }

        public bool Clear(int slot)
        {
            if (slot < 0 || slot >= SlotCount || _slots[slot] == null)
            {
                return false;
            }
            _slots[slot] = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes a skill off the bar wherever it is (when it is forgotten).</summary>
        public void Remove(string id)
        {
            int slot = SlotOf(id);
            if (slot >= 0)
            {
                Clear(slot);
            }
        }

        /// <summary>Puts a newly learnt skill in the first empty slot, as WoW does; -1 when it does not go on the bar or the bar is full.</summary>
        public int PlaceInFirstEmpty(string id)
        {
            if (SlotOf(id) >= 0)
            {
                return SlotOf(id);
            }
            SkillNode node = SkillCatalog.Find(id);
            if (node == null || !node.GoesOnBar)
            {
                return -1;
            }
            for (int i = 0; i < SlotCount; i++)
            {
                if (_slots[i] == null)
                {
                    Put(i, id);
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Fills the bar from a list (a save, or the defaults); empty strings and anything that does not go on the bar are empty slots.</summary>
        public void Load(IList<string> ids)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _slots[i] = null;
            }
            if (ids != null)
            {
                for (int i = 0; i < SlotCount && i < ids.Count; i++)
                {
                    if (GoesOnBar(ids[i]) && SlotOf(ids[i]) < 0)
                    {
                        _slots[i] = ids[i];
                    }
                }
            }
            Changed?.Invoke();
        }

        /// <summary>The ten slots for a save: an empty slot is an empty string (JsonUtility keeps a list of strings, not nulls).</summary>
        public List<string> ToList()
        {
            var list = new List<string>(SlotCount);
            for (int i = 0; i < SlotCount; i++)
            {
                list.Add(_slots[i] ?? string.Empty);
            }
            return list;
        }
    }
}
