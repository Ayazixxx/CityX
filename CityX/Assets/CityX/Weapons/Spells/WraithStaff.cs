using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE WRAITH STAFF (owner, 2026-11-08: "make an item called "Wraith Staff" it will be a rare drop from the wraiths like a 3% drop chance, and
    /// it summons 1 wraith that follows you around and fights for you, when he dies you can summon another"; docs/WRAITH_STAFF.md): every
    /// staff's hold; a click summons the Wraith (WraithSummonCaster). The owner's Meshy model, its orb glowing the red of the Wraith's eyes
    /// (WraithStaffBuilder).
    /// </summary>
    public sealed class WraithStaff : StaffItem
    {
        protected override IStaffCaster CasterOn(GameObject body)
        {
            return body != null ? body.GetComponent<WraithSummonCaster>() : null;
        }
    }
}
