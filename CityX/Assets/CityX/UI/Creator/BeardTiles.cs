using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>The FACE tab's beards (FacePage): the one the body wears framed in gold, as the hairstyles are (HairStyleTiles).</summary>
    public sealed class BeardTiles : MonoBehaviour
    {
        public CharacterCreator Creator;

        [Tooltip("Each beard's tile frame, in CharacterHair.ValidBeards order.")]
        public Image[] Frames;

        private string _shown;

        private void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            string beard = Creator != null ? Creator.Beard : CharacterHair.None;
            if (beard == _shown || Frames == null)
            {
                return;
            }
            _shown = beard;
            for (int i = 0; i < Frames.Length && i < CharacterHair.ValidBeards.Length; i++)
            {
                if (Frames[i] != null)
                {
                    Frames[i].color = CharacterHair.ValidBeards[i] == beard ? CreatorPageKit.TileLit : CreatorPageKit.TileIdle;
                }
            }
        }
    }
}
