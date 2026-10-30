using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE MAGIC STAFF (owner, 2026-10-30: "there will probably need to be a basic non elemental magic staff as well that does the same spec of dust
    /// attack but maybe it does slightly more damage at a slightly lower firerate"; docs/MAGIC_WAND.md): every staff's hold, the wand's speck from
    /// its head, a little harder and a little slower (MagicSpeckCaster.Staff).
    /// </summary>
    public sealed class MagicStaff : StaffItem
    {
        protected override IStaffCaster CasterOn(GameObject body)
        {
            var caster = body != null ? body.GetComponent<MagicSpeckCaster>() : null;
            return caster != null ? caster.Staff : null;
        }
    }
}
