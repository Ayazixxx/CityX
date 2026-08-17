using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// STUB -- fleshed out in the Loadout build step. Placeholder so
    /// PlayerSubmenuView compiles and can wire its button now; Open() is a
    /// safe no-op until the real 8-slot equip panel exists.
    /// </summary>
    public sealed class LoadoutPanelView : MonoBehaviour
    {
        public bool IsOpen => false;

        public void Open()
        {
        }
    }
}
