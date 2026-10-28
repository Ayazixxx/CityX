using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE FIREBALL STAFF (owner, 2026-10-27: "when you hold click with that one it does a charged shot the explodes into a bigger ball of fire the
    /// longer you charge it"; docs/STAFFS.md): a click shoots a fireball; held, the shot grows for up to <see cref="FireballCaster.StaffFullChargeSeconds"/>
    /// into a bigger ball that bursts over an area. Every shot pays mana by its charge.
    /// </summary>
    public sealed class FireballStaff : StaffItem
    {
        protected override IStaffCaster CasterOn(GameObject body) => body != null ? body.GetComponent<FireballCaster>() : null;
    }
}
