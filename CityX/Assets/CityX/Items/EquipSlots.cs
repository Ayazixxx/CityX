namespace CityX.Items
{
    /// <summary>
    /// The equipment slots, by the keys Inventory keeps them under and each item registers against (ItemDefinition.EquipSlot). One place spells
    /// them: the inventory's equipped row and the loadout panel used to keep lists of their own, and the inventory's (Godot's hat, hands, feet)
    /// never matched the keys items were registered under, so a hat or a jetpack worn never showed there (2026-10-15, with the owner's ask for
    /// shoes, gloves and back slots in the inventory).
    ///
    /// The weapon keys are the old "right hand" and "left hand" ones: saves, the registry and the tests all know them by those names.
    /// </summary>
    public static class EquipSlots
    {
        public const string Helmet = "helmet";
        public const string Mask = "mask";
        public const string Torso = "top";
        public const string Gloves = "gloves";
        public const string Legs = "legs";
        public const string Shoes = "shoes";
        public const string Back = "back";

        /// <summary>The off hand's shield, held in the left hand beside whatever the right hand holds.</summary>
        public const string Shield = "shield";
        public const string Primary = "hand_r";
        public const string Secondary = "hand_l";

        /// <summary>The left-hand pistol paired with the primary's, and with the secondary's (Dual Pistols: docs/DUAL_PISTOLS.md).</summary>
        public const string PrimaryOffHand = "hand_r_pair";
        public const string SecondaryOffHand = "hand_l_pair";
    }
}
