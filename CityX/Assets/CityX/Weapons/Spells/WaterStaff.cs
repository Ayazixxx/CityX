using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>The water staff (docs/STAFFS.md): held, a jet of water that shoves, throws limp and wears down whoever it is held on (WaterJetCaster).</summary>
    public sealed class WaterStaff : StaffItem
    {
        protected override IStaffCaster CasterOn(GameObject body) => body != null ? body.GetComponent<WaterJetCaster>() : null;
    }
}
