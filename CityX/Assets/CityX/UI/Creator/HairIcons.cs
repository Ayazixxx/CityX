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

        /// <summary>A style's hair layer is its picture's name with this after it.</summary>
        public const string HairSuffix = "_hair";

        /// <summary>The picture of <paramref name="style"/>, its hair black; null where none has been baked.</summary>
        public static Texture2D For(string style) => Resources.Load<Texture2D>(Folder + "/" + style);

        /// <summary>
        /// Just the hair of <paramref name="style"/>'s picture, white and shaded, see-through elsewhere: laid over the picture and tinted with
        /// the hair colour (HairStyleTiles). Null for the bald head, or where none has been baked.
        /// </summary>
        public static Texture2D HairOf(string style) => Resources.Load<Texture2D>(Folder + "/" + style + HairSuffix);
    }
}
