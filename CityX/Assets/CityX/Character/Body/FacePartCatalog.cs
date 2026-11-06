using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The premade face parts a character can wear instead of drawing a face (docs/CHARACTER_CREATOR.md): the kinds of part, in the order
    /// the creator's FACE tab shows their chips, and each kind's parts. Data only: a part is added by adding an entry to the asset at
    /// <see cref="AssetPath"/>, which sits in a Resources folder so the title screen's creator (built in the editor) and ReDraw's (built
    /// during play) read the same one. Until that asset exists every kind is there with no parts. In Character rather than UI because a
    /// body being worn will look its parts up here too.
    /// </summary>
    [CreateAssetMenu(menuName = "CityX/Face Part Catalog", fileName = ResourceName)]
    public sealed class FacePartCatalog : ScriptableObject
    {
        public const string ResourceName = "FacePartCatalog";

        public const string AssetPath = "Assets/CityX/Character/Resources/FacePartCatalog.asset";

        [Serializable]
        public sealed class Category
        {
            [Tooltip("Saved with every character wearing one of its parts, so never renamed.")]
            public string Key;

            [Tooltip("On its chip.")]
            public string Label;

            [Tooltip("Its parts in a sentence: \"No premade {Noun} yet.\"")]
            public string Noun;

            public List<Part> Parts = new List<Part>();
        }

        [Serializable]
        public sealed class Part
        {
            [Tooltip("Saved with every character wearing it, so never renamed and never given to another part.")]
            public string Id;

            [Tooltip("Its picture on the FACE tab's tile.")]
            public Texture2D Icon;
        }

        public List<Category> Categories = Defaults();

        private static FacePartCatalog _current;

        /// <summary>The catalog in force: the asset, a test's own put in its place, or with no asset yet every kind and no parts.</summary>
        public static FacePartCatalog Current
        {
            get
            {
                if (_current == null)
                {
                    _current = Resources.Load<FacePartCatalog>(ResourceName);
                }
                if (_current == null)
                {
                    _current = CreateInstance<FacePartCatalog>();
                    _current.hideFlags = HideFlags.DontSave;
                }
                return _current;
            }
            set => _current = value;
        }

        /// <summary>The kinds the FACE tab offers, with no parts yet. A new asset starts with these.</summary>
        public static List<Category> Defaults()
        {
            return new List<Category>
            {
                new Category { Key = "eyes", Label = "EYES", Noun = "eyes" },
                new Category { Key = "brows", Label = "BROWS", Noun = "brows" },
                new Category { Key = "mouth", Label = "MOUTH", Noun = "mouths" },
                new Category { Key = "nose", Label = "NOSE", Noun = "noses" },
                new Category { Key = "extras", Label = "EXTRAS", Noun = "extras" },
            };
        }
    }
}
