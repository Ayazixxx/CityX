using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Something that cares whether it is standing inside a building.
    ///
    /// IN WORLD RATHER THAN VIEW, for the same reason IBreakableSurface is:
    /// the thing that raises it is a piece of city geometry and the thing that
    /// answers it is a piece of character behaviour, and Character does not
    /// reference View. This interface is the whole of what they share.
    ///
    /// THE VOLUME COMES ALONG, not just "you are indoors now". Godot passes its
    /// own Area3D for the same reason: claiming a building needs to know WHICH
    /// building, so the footprint has to survive the call.
    /// </summary>
    public interface IIndoorAware
    {
        void EnterBuilding(Transform volume);

        void ExitBuilding(Transform volume);
    }
}
