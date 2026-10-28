using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE ICE SHARD STAFF (owner, 2026-10-27: "when you click it shoots 1 iceshard but when you hold click it just shoots consecutively like an
    /// automatic gun"; docs/STAFFS.md): one shard a click, a stream of them while held, each paying mana.
    /// </summary>
    public sealed class IceShardStaff : StaffItem
    {
        protected override IStaffCaster CasterOn(GameObject body) => body != null ? body.GetComponent<IceShardCaster>() : null;
    }
}
