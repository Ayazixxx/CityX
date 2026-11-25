using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    public sealed partial class Inventory
    {
        /// <summary>
        /// Takes one thing out of the inventory altogether (a shirt laid in the heat press, docs/HEAT_PRESS.md): the one worn in
        /// <paramref name="wornSlot"/>, or the one in storage slot <paramref name="storageIndex"/> when no slot is given. Its state comes out
        /// with it. False, and nothing changed, when it is not there, or it is worn and there is no room to take it off.
        /// </summary>
        public bool TakeOut(string itemId, string wornSlot, int storageIndex, GameObject player, out ItemInstanceData data)
        {
            data = ItemInstanceData.None;
            if (!string.IsNullOrEmpty(wornSlot))
            {
                if (EquippedItemId(wornSlot) != itemId)
                {
                    return false;
                }
                // Off into storage the usual way (the coat over it, the layers, what others see), then out of the slot it went into.
                var emptyBefore = new bool[Storage.Length];
                for (int i = 0; i < Storage.Length; i++) emptyBefore[i] = Storage[i].IsEmpty;
                if (!UnequipToSlot(wornSlot, player))
                {
                    return false;
                }
                for (int i = 0; i < Storage.Length; i++)
                {
                    if (emptyBefore[i] && Storage[i].ItemId == itemId)
                    {
                        storageIndex = i;
                        break;
                    }
                }
            }
            if (storageIndex < 0 || storageIndex >= Storage.Length || Storage[storageIndex].ItemId != itemId)
            {
                return false;
            }
            data = Storage[storageIndex].InstanceData;
            Storage[storageIndex].Count--;
            if (Storage[storageIndex].Count <= 0)
            {
                Storage[storageIndex] = default;
            }
            return true;
        }
    }
}
