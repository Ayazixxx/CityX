using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What is in a container while it is looked into (docs/LOOTING.md): its items, the one picked out, and taking one or all of them. Taking
    /// removes from the container's own list, so what is left stays behind. Plain, so a test can drive it.
    /// </summary>
    public sealed class LootList
    {
        private readonly List<string> _items;

        public LootList(List<string> items)
        {
            _items = items ?? new List<string>();
            Clamp();
        }

        public IReadOnlyList<string> Items => _items;

        public int Selected { get; private set; }

        public bool IsEmpty => _items.Count == 0;

        /// <summary>Picks out the next (+1) or previous (-1) item, round from the last to the first.</summary>
        public void Move(int step)
        {
            if (_items.Count == 0)
            {
                Selected = 0;
                return;
            }
            Selected = ((Selected + step) % _items.Count + _items.Count) % _items.Count;
        }

        /// <summary>Hands the picked item to <paramref name="give"/>; it leaves the container only if it was taken (a full inventory keeps it there).</summary>
        public bool TakeSelected(Func<string, bool> give)
        {
            if (IsEmpty || !give(_items[Selected]))
            {
                return false;
            }
            _items.RemoveAt(Selected);
            Clamp();
            return true;
        }

        /// <summary>Hands over everything that will be taken, in order; what is not taken stays. How many were taken.</summary>
        public int TakeAll(Func<string, bool> give)
        {
            int taken = 0;
            for (int i = 0; i < _items.Count;)
            {
                if (give(_items[i]))
                {
                    _items.RemoveAt(i);
                    taken++;
                }
                else
                {
                    i++;
                }
            }
            Clamp();
            return taken;
        }

        private void Clamp()
        {
            Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, _items.Count - 1));
        }
    }
}
