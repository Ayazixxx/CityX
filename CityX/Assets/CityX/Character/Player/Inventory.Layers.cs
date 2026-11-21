namespace CityX.Character
{
    /// <summary>The top slot's second place: a shirt under a coat (TopLayers; docs/FUR_HOODED_PARKA.md, "A shirt under it").</summary>
    public sealed partial class Inventory
    {
        /// <summary>The shirt worn under the coat in the top slot (empty: none, or another slot).</summary>
        public string UnderItemId(string slot) => slot == TopLayers.Top ? EquippedItemId(TopLayers.Under) : string.Empty;

        /// <summary>The second thing a slot holds, which the menus show beside the first: a pistol paired in a weapon slot, a shirt under a coat.</summary>
        public string SecondItemId(string slot)
        {
            string paired = PairedItemId(slot);
            return string.IsNullOrEmpty(paired) ? UnderItemId(slot) : paired;
        }

        /// <summary>Where a top goes (TopLayers.SlotFor); a coat going on over the shirt worn moves the shirt under it first.</summary>
        private string WearOnTop(string itemId)
        {
            string slot = TopLayers.SlotFor(itemId, EquippedItemId, out bool shirtGoesUnder);
            if (shirtGoesUnder)
            {
                MoveWorn(TopLayers.Top, TopLayers.Under);
            }
            return slot;
        }

        /// <summary>The coat taken off (or not put on after all): the shirt under it is the top again.</summary>
        private void RaiseShirt()
        {
            if (string.IsNullOrEmpty(EquippedItemId(TopLayers.Top)) && !string.IsNullOrEmpty(EquippedItemId(TopLayers.Under)))
            {
                MoveWorn(TopLayers.Under, TopLayers.Top);
            }
        }

        /// <summary>What is worn in one place, kept under another: it stays on the body, only the place it is kept under changes.</summary>
        private void MoveWorn(string from, string to)
        {
            _equipment[to] = EquippedItemId(from);
            _equippedInstances[to] = EquippedInstance(from);
            _equipment[from] = string.Empty;
            _equippedInstances[from] = null;
        }
    }
}
