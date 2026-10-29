using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>The Staff of Healing (docs/STAFFS.md): held, a gold beam that ties itself to the ally under the crosshair and heals them (HealingBeamCaster).</summary>
    public sealed class HealingStaff : StaffItem
    {
        protected override IStaffCaster CasterOn(GameObject body) => body != null ? body.GetComponent<HealingBeamCaster>() : null;
    }
}
