using CityX.Items;

namespace CityX.Character
{
    /// <summary>
    /// Rounds kept in the bag (ItemRegistry's ammunition; owner, 2026-11-21: "pick up into your inventory and press r to reload into your guns"):
    /// counted and taken across every stack of a kind, and added stack by stack without a stack going past its size (the server refuses a bag
    /// with one that does, BagRules).
    /// </summary>
    public sealed partial class Inventory
    {
        /// <summary>How many of <paramref name="itemId"/> the bag holds, every stack together.</summary>
        public int CountInStorage(string itemId)
        {
            int count = 0;
            for (int i = 0; i < Storage.Length; i++)
            {
                if (!Storage[i].IsEmpty && Storage[i].ItemId == itemId)
                {
                    count += Storage[i].Count;
                }
            }
            return count;
        }

        /// <summary>Takes up to <paramref name="most"/> of <paramref name="itemId"/> out of the bag, from as many stacks as it needs; returns how many.</summary>
        public int TakeFromStorage(string itemId, int most)
        {
            int taken = 0;
            for (int i = 0; i < Storage.Length && taken < most; i++)
            {
                if (Storage[i].IsEmpty || Storage[i].ItemId != itemId)
                {
                    continue;
                }
                int here = System.Math.Min(Storage[i].Count, most - taken);
                Storage[i].Count -= here;
                taken += here;
                if (Storage[i].Count <= 0)
                {
                    Storage[i] = default;
                }
            }
            return taken;
        }

        /// <summary>
        /// Puts up to <paramref name="count"/> of a plain stackable <paramref name="itemId"/> into the bag: topping up its stacks to their size,
        /// then filling empty slots. Returns how many went in (fewer when the bag is full).
        /// </summary>
        public int AddStacked(string itemId, int count)
        {
            if (count <= 0 || !ItemRegistry.TryGet(itemId, out ItemDefinition definition))
            {
                return 0;
            }
            int size = System.Math.Max(1, definition.StackSize);
            int added = 0;
            for (int i = 0; i < Storage.Length && added < count; i++)
            {
                if (!Storage[i].IsEmpty && Storage[i].ItemId == itemId && Storage[i].InstanceData.IsEmpty && Storage[i].Count < size)
                {
                    int here = System.Math.Min(size - Storage[i].Count, count - added);
                    Storage[i].Count += here;
                    added += here;
                }
            }
            for (int i = 0; i < Storage.Length && added < count; i++)
            {
                if (Storage[i].IsEmpty)
                {
                    int here = System.Math.Min(size, count - added);
                    Storage[i] = new StorageSlot { ItemId = itemId, Count = here };
                    added += here;
                }
            }
            return added;
        }
    }
}
