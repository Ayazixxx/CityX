using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The hairstyles' pictures for the creator's HAIR tab: a plain white head wearing each style, baked by CityX/Bake Hair Icons
    /// (UIEditor.HairIconBaker) into Resources, so the title screen's creator and ReDraw's (built during play) both find them.
    /// </summary>
    public static class HairIcons
    {
        /// <summary>Under a Resources folder: UI/Resources/HairIcons/{style}.png.</summary>
        public const string Folder = "HairIcons";

        public const int Size = 128;

        /// <summary>The picture of <paramref name="style"/>; null where none has been baked.</summary>
        public static Texture2D For(string style) => Resources.Load<Texture2D>(Folder + "/" + style);
    }
}
