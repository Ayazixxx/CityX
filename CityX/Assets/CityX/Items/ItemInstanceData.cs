namespace CityX.Items
{
    /// <summary>
    /// Per-instance state an equippable item carries across pickup, storage,
    /// and re-equip. Ported from the "instance_data" dictionary
    /// items/item_registry.gd's inventory hands around -- most items carry
    /// none (Durability unused), the glass bottle is the one item this pass
    /// that does. A gun carries its rounds: changing weapons puts its model
    /// away and makes a new one when it is drawn again.
    /// </summary>
    public struct ItemInstanceData
    {
        public bool HasDurability;
        public int Durability;

        /// <summary>A gun's rounds, in its magazine and spare, as it was put away or let go of.</summary>
        public bool HasAmmo;

        public int Magazine;
        public int Reserve;

        /// <summary>What is left in a thing that is used up, in its own measure (a jerry can's litres), as it was put away or let go of.</summary>
        public bool HasContents;

        public float Contents;

        public static readonly ItemInstanceData None = default;

        public bool IsEmpty => !HasDurability && !HasAmmo && !HasContents;
    }
}
