using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// What PlayerInputReader needs from a staff in hand, without depending on the Weapons assembly (ArcaneStaff implements it). Left-click is
    /// the staff's: held, it charges; let go, it shoots (a click is an uncharged shot). Ticked every frame like the flamethrower.
    /// </summary>
    public interface IEquippableStaff
    {
        /// <summary>
        /// <paramref name="triggerHeld"/>: left-click is down. <paramref name="eye"/>/<paramref name="look"/>: the camera's position and facing,
        /// where the crosshair is. <paramref name="wielder"/>: the body holding it.
        /// </summary>
        void Tick(bool triggerHeld, Vector3 eye, Vector3 look, GameObject wielder, float deltaTime);

        /// <summary>Drops a charge in progress without shooting (the staff put away, the hands taken by something else).</summary>
        void CancelCharge(GameObject wielder);
    }
}
