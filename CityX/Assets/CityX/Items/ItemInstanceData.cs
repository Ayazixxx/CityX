namespace CityX.Items
{
    /// <summary>
    /// Per-instance state an equippable item carries across pickup, storage,
    /// and re-equip. Ported from the "instance_data" dictionary
    /// items/item_registry.gd's inventory hands around -- most items carry
    /// none (Durability unused), the glass bottle is the one item this pass
    /// that does.
    /// </summary>
    public struct ItemInstanceData
    {
        public bool HasDurability;
        public int Durability;

        public static readonly ItemInstanceData None = default;

        public bool IsEmpty => !HasDurability;
    }
}
