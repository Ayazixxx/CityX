using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>A C-menu icon: its Image is given the icon of <see cref="Kind"/> (MenuIcons) when it wakes, so no texture is saved in the scene.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class MenuIconImage : MonoBehaviour
    {
        public string Kind;

        private void Awake()
        {
            Draw();
        }

        /// <summary>Puts the icon on its Image now (PlayerMenuView does it for every icon as the menu wakes, the hidden ones too).</summary>
        public void Draw()
        {
            var image = GetComponent<Image>();
            if (image.sprite == null)
            {
                image.sprite = MenuIcons.Sprite(Kind);
            }
        }
    }
}
