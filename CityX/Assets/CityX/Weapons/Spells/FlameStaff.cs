using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>The flame staff (docs/STAFFS.md): held, a flamethrower's stream of fire from its head, paid in mana (FlameStreamCaster).</summary>
    public sealed class FlameStaff : StaffItem
    {
        protected override IStaffCaster CasterOn(GameObject body) => body != null ? body.GetComponent<FlameStreamCaster>() : null;
    }
}
