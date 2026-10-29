using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A ranged weapon that is DRAWN while its trigger is held and shoots when it is let go (the bow, docs/ARCHERY.md), unlike a gun that fires
    /// on the press. PlayerInputReader tells it every frame whether left-click is down, with where the shot would leave from and the way the
    /// camera looks; the weapon keeps its own draw and looses on the release. It is still an <see cref="IEquippableGun"/> (right-click aims it,
    /// the HUD shows its ammo text), but the gun's own arm pose stays off: the bow poses both arms itself.
    /// </summary>
    public interface IDrawnWeapon
    {
        /// <summary>Every frame while equipped: the trigger held or not, the shot's origin and the camera's way, aiming (right-click) or not.</summary>
        void Draw(bool held, Vector3 origin, Vector3 direction, bool aiming, GameObject wielder, float deltaTime);

        /// <summary>How far drawn, 0 (not at all) to 1 (fully).</summary>
        float DrawShare { get; }
    }
}
