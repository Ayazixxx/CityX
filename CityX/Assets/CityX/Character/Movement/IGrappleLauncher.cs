using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What GrappleSwing needs from the equipped launcher item, without
    /// depending on the Weapons assembly -- same layering reason
    /// IEquippableWeapon exists: Character depends on nothing, Weapons
    /// depends on Character, never the other way round. GrapplingHookItem
    /// (in CityX.Weapons) implements this.
    /// </summary>
    public interface IGrappleLauncher
    {
        /// <summary>World geometry only -- see GrapplingHookItem's own "not other characters" rule.</summary>
        bool FindAnchor(Vector3 origin, Vector3 direction, float maxRange, out Vector3 anchor);

        void ShowRope(Vector3 anchor);
        void UpdateRope(Vector3 anchor);
        void HideRope();
    }
}
