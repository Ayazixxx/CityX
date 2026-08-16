using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// What OldGuy needs from a building's front door (see
    /// CityX.View.BuildingDoor, the only implementation) to let himself in,
    /// without CityX.Character depending on CityX.View or vice versa --
    /// neither assembly's own .asmdef references the other. CityX.World is
    /// the one both CityX.Character and CityX.View already reference, so
    /// it is the shared home this interface has to live in, the same
    /// IEquippableWeapon-style inversion IDialogueBox already needed one
    /// layer boundary over.
    /// </summary>
    public interface IOpenableDoor
    {
        Vector3 Position { get; }
        void SetOpen(bool open);
    }
}
