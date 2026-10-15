using System.Collections.Generic;
using CityX.Core;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The points a character has put into each stat (StatCatalog), and what they come to. Plain class, tested without a scene; PlayerVitals
    /// holds one, and every system a stat touches asks it (<see cref="Effect"/>, or a multiplier built from it).
    /// </summary>
    [System.Serializable]
    public sealed class StatSheet
    {
        [SerializeField]
        private int[] _points = new int[StatCatalog.Count];

        /// <summary>Raised when any stat's points change.</summary>
        public event System.Action Changed;

        public int this[StatId id]
        {
            get
            {
                Grow();
                return _points[(int)id];
            }
            set
            {
                Grow();
                int clamped = Mathf.Max(0, value);
                if (_points[(int)id] == clamped)
                {
                    return;
                }
                _points[(int)id] = clamped;
                Changed?.Invoke();
            }
        }

        /// <summary>What the points in a stat add (StatDefinition.Effect).</summary>
        public float Effect(StatId id) => StatCatalog.Get(id).Effect(this[id]);

        /// <summary>1 plus the stat's share: the factor something grows by (1.06 for +6%).</summary>
        public float Increase(StatId id) => 1.0f + Effect(id);

        /// <summary>1 minus the stat's share: the factor something shrinks by (0.94 for 6% less).</summary>
        public float Decrease(StatId id) => Mathf.Max(0.0f, 1.0f - Effect(id));

        /// <summary>Every point spent, all stats together.</summary>
        public int Total
        {
            get
            {
                Grow();
                int total = 0;
                foreach (int points in _points)
                {
                    total += points;
                }
                return total;
            }
        }

        /// <summary>Every stat with points in it, for a save.</summary>
        public List<StatEntry> ToList()
        {
            Grow();
            var list = new List<StatEntry>();
            foreach (StatDefinition stat in StatCatalog.All)
            {
                int points = _points[(int)stat.Id];
                if (points > 0)
                {
                    list.Add(new StatEntry(stat.Key, points));
                }
            }
            return list;
        }

        /// <summary>Takes the points from a save: unknown keys (a stat since removed) are dropped.</summary>
        public void Load(IEnumerable<StatEntry> entries)
        {
            Grow();
            for (int i = 0; i < _points.Length; i++)
            {
                _points[i] = 0;
            }
            if (entries != null)
            {
                foreach (StatEntry entry in entries)
                {
                    StatDefinition stat = StatCatalog.FindByKey(entry.Key);
                    if (stat != null)
                    {
                        _points[(int)stat.Id] += Mathf.Max(0, entry.Points);
                    }
                }
            }
            Changed?.Invoke();
        }

        /// <summary>A sheet saved before a stat was added has a shorter array: it grows to fit, the new stats at zero.</summary>
        private void Grow()
        {
            if (_points == null)
            {
                _points = new int[StatCatalog.Count];
            }
            else if (_points.Length < StatCatalog.Count)
            {
                System.Array.Resize(ref _points, StatCatalog.Count);
            }
        }
    }
}
