using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The hand-tuned placement of each hairstyle, saved as an asset so it
    /// survives leaving Play Mode.
    ///
    /// A style gets an automatic fit worked out from its mesh and the size of the
    /// head (see CharacterHair.DefaultFit). This is what overrides it: while the
    /// game runs, the worn hair is an ordinary object -- select it in the
    /// Hierarchy, drag it with the Scene view gizmos -- and the CharacterHair
    /// Inspector's Save button writes what it ends up at into here. The asset
    /// lives in a Resources folder, so every head that wears the style reads the
    /// same numbers with nothing to wire.
    /// </summary>
    public sealed class HairFitSettings : ScriptableObject
    {
        public const string ResourceName = "HairFits";

        /// <summary>Where the asset lives: a Resources folder, so it loads by name with nothing wired.</summary>
        public const string AssetPath = "Assets/CityX/Character/Resources/HairFits.asset";

        [System.Serializable]
        public struct Entry
        {
            public string Style;
            public CharacterHair.Fit Fit;
        }

        public List<Entry> Entries = new List<Entry>();

        private static HairFitSettings _current;

        /// <summary>
        /// The settings in force. The saved asset unless something (a test) has put its
        /// own in place -- so a test can tune a fit without touching the real one.
        /// </summary>
        public static HairFitSettings Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<HairFitSettings>(ResourceName);
                }
                return _current;
            }
            set => _current = value;
        }

        public bool TryGet(string style, out CharacterHair.Fit fit)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Style == style)
                {
                    fit = Entries[i].Fit;
                    return true;
                }
            }
            fit = default;
            return false;
        }

        public void Set(string style, CharacterHair.Fit fit)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Style == style)
                {
                    Entries[i] = new Entry { Style = style, Fit = fit };
                    return;
                }
            }
            Entries.Add(new Entry { Style = style, Fit = fit });
        }

        public bool Remove(string style)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Style == style)
                {
                    Entries.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
    }
}
