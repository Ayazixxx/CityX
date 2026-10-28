using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE ARCANE STAFF (owner, 2026-10-27; docs/ARCANE_ORB.md): Arcane Orb on the click. A click shoots a ball that leaves one cursed orb;
    /// held, the shot charges to two orbs and then three, on Shadow Flare's timing; every shot pays mana by its orbs, with no cooldown but the
    /// staff's own pace.
    /// </summary>
    public sealed class ArcaneStaff : StaffItem
    {
        protected override IStaffCaster CasterOn(GameObject body) => body != null ? body.GetComponent<ArcaneOrbCaster>() : null;
    }
}
