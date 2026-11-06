using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>Frames the HAIR tab's tile of the style the head wears in gold, the rest plain, and tints every picture's hair the hair colour (HairPage).</summary>
    public sealed class HairStyleTiles : MonoBehaviour
    {
        public static readonly Color Idle = CreatorPageKit.TileIdle;
        public static readonly Color Worn = CreatorPageKit.TileLit;

        public CharacterCreator Creator;

        [Tooltip("Each style's tile frame, in CharacterHair.ValidStyles order.")]
        public Image[] Frames;

        [Tooltip("Each style's hair layer over its picture (null for the bald head), in the same order.")]
        public RawImage[] Tints;

        private string _shown;
        private Color? _tinted;

        private void Update()
        {
            Refresh();
        }

        /// <summary>Lights the worn style's frame and tints the hair, if either changed. Public for the editor's screenshot tool, where Update does not run.</summary>
        public void Refresh()
        {
            Color colour = Creator != null ? Creator.HairColour : CharacterHair.HairColour;
            if (Tints != null && _tinted != colour)
            {
                _tinted = colour;
                foreach (RawImage tint in Tints)
                {
                    if (tint != null)
                    {
                        tint.color = colour;
                    }
                }
            }
            string style = Creator != null ? Creator.HairStyle : CharacterHair.None;
            if (style == _shown || Frames == null)
            {
                return;
            }
            _shown = style;
            for (int i = 0; i < Frames.Length && i < CharacterHair.ValidStyles.Length; i++)
            {
                if (Frames[i] != null)
                {
                    Frames[i].color = CharacterHair.ValidStyles[i] == style ? Worn : Idle;
                }
            }
        }
    }
}
