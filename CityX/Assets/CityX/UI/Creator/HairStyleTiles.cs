using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>Frames the HAIR tab's tile of the style the head wears in gold, the rest plain (HairPage).</summary>
    public sealed class HairStyleTiles : MonoBehaviour
    {
        public static readonly Color Idle = CreatorPageKit.TileIdle;
        public static readonly Color Worn = CreatorPageKit.TileLit;

        public CharacterCreator Creator;

        [Tooltip("Each style's tile frame, in CharacterHair.ValidStyles order.")]
        public Image[] Frames;

        private string _shown;

        private void Update()
        {
            Refresh();
        }

        /// <summary>Lights the worn style's frame if that changed. Public for the editor's screenshot tool, where Update does not run.</summary>
        public void Refresh()
        {
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
