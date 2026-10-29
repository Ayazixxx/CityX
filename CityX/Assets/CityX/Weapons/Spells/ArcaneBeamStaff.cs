using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>The Arcane Beam Staff (docs/STAFFS.md): held, a magenta beam that burns whatever it is held on, paid in mana (ArcaneBeamCaster).</summary>
    public sealed class ArcaneBeamStaff : StaffItem
    {
        protected override IStaffCaster CasterOn(GameObject body) => body != null ? body.GetComponent<ArcaneBeamCaster>() : null;
    }
}
