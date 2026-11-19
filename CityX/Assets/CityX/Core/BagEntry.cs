using System;

namespace CityX.Core
{
    /// <summary>
    /// ONE THING CARRIED, as a character's save keeps it (docs/SERVER_SAVING.md, step 3): where it is (a storage slot, or the equipment slot it
    /// is worn or held in), what it is, how many, and the state it carries (a gun's rounds, a bottle's cracks, a can's litres). The item ids and
    /// slot keys are the Items assembly's; this side only keeps them.
    /// </summary>
    [Serializable]
    public sealed class BagEntry
    {
        /// <summary>The equipment slot it is worn or held in (EquipSlots' keys); empty for a thing in storage.</summary>
        public string Slot = string.Empty;

        /// <summary>Which storage slot, for a thing in storage.</summary>
        public int Index;

        public string ItemId = string.Empty;

        public int Count = 1;

        public bool HasDurability;
        public int Durability;

        public bool HasAmmo;
        public int Magazine;
        public int Reserve;

        public bool HasContents;
        public float Contents;

        public bool InStorage => string.IsNullOrEmpty(Slot);
    }
}
