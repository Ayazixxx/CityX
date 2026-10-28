using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// The body's spell a staff shoots (docs/STAFFS.md): the Arcane Staff's is ArcaneOrbCaster, the Fireball Staff's FireballCaster. The staff
    /// is the model in the hand and the click; the caster on the body charges, pays the mana, shoots and tells other players, so a staff's shots
    /// replicate as its spell's do.
    /// </summary>
    public interface IStaffCaster
    {
        /// <summary>
        /// One frame of the staff's click: <paramref name="held"/> the button, <paramref name="tip"/> where the shot leaves from,
        /// <paramref name="eye"/>/<paramref name="look"/> the camera (the crosshair).
        /// </summary>
        void TickStaff(bool held, Transform tip, Vector3 eye, Vector3 look, float deltaTime);

        /// <summary>Drops a charge in progress without shooting.</summary>
        void CancelStaffCharge();
    }
}
