using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A thrown weapon with its own aim (the Giant Boomerang, docs/BOOMERANG.md): told every frame whether right-click's aim is held, so it can draw itself into
    /// the aim and put itself away again. The body faces where the camera looks while it aims (LocomotionInput.ThrowAiming).
    /// </summary>
    public interface IAimedThrow
    {
        void SetAiming(GameObject player, bool aiming);
    }
}
