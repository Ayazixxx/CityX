using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>The Frost Staff (docs/FROST.md): held, a cloud of frost that hurts a little, slows and then freezes whoever is in it (FrostSprayCaster).</summary>
    public sealed class FrostStaff : StaffItem
    {
        protected override IStaffCaster CasterOn(GameObject body) => body != null ? body.GetComponent<FrostSprayCaster>() : null;
    }
}
