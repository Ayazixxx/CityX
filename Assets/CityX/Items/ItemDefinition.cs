namespace CityX.Items
{
    /// <summary>
    /// One entry from items/item_registry.gd's ITEMS dictionary -- id,
    /// display name, category, the equipment slot it fills, and how many
    /// fit in a single storage slot.
    /// </summary>
    public readonly struct ItemDefinition
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Category;
        public readonly string EquipSlot;
        public readonly int StackSize;

        public ItemDefinition(string id, string displayName, string category, string equipSlot, int stackSize)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            EquipSlot = equipSlot;
            StackSize = stackSize;
        }
    }
}
