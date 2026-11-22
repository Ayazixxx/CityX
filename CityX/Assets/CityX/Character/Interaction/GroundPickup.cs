using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Something lying on the ground that this machine's player picks up with E (a weapon, a tool, a garment). Its trigger sphere says who
    /// has walked up (<see cref="Entered"/>, <see cref="Left"/>): only this machine's own player counts, and only their body leaving
    /// forgets them (<see cref="InteractionReach"/>). <see cref="Take"/> puts it in their inventory and takes its model off the ground.
    /// </summary>
    public sealed class GroundPickup
    {
        /// <summary>This machine's player, standing in the trigger; null with nobody there.</summary>
        public Transform Player { get; private set; }

        public void Entered(Collider other)
        {
            Transform body = InteractionReach.LocalPlayerBody(other);
            if (body != null)
            {
                Player = body;
            }
        }

        public void Left(Collider other)
        {
            if (InteractionReach.IsLeaving(other, Player))
            {
                Player = null;
            }
        }

        /// <summary>The player in the trigger tapped E this frame, within <paramref name="range"/> metres of <paramref name="item"/>.</summary>
        public bool TappedWithin(Vector3 item, float range) =>
            Player != null && InteractKey.Tapped && Vector3.Distance(item, Player.position) <= range;

        /// <summary>Into <paramref name="player"/>'s inventory; taken, the <paramref name="model"/> on the ground goes. False with no inventory or no room in it.</summary>
        public static bool Take(Transform player, GameObject model, string itemId, ItemInstanceData instanceData = default)
        {
            var inventory = player.GetComponentInParent<Inventory>();
            if (inventory == null || !inventory.PickUp(itemId, instanceData))
            {
                return false;
            }
            Object.Destroy(model);
            MarkGiven();
            return true;
        }

        /// <summary>
        /// Up to <paramref name="count"/> of a stackable <paramref name="itemId"/> (a box of rounds) into <paramref name="player"/>'s bag, as many as fit;
        /// the <paramref name="model"/> goes once all are taken. Returns how many are left in it.
        /// </summary>
        public static int TakeStack(Transform player, GameObject model, string itemId, int count)
        {
            var inventory = player.GetComponentInParent<Inventory>();
            int taken = inventory != null ? inventory.AddStacked(itemId, count) : 0;
            if (taken > 0)
            {
                MarkGiven();
            }
            if (taken >= count)
            {
                Object.Destroy(model);
            }
            return count - taken;
        }

        /// <summary>
        /// A thing lying only on this machine (put there by the console, or the tutorial house's): no server saw it given. An admin's next save
        /// says so, so a server on another machine lets it into the bag (docs/SERVER_SAVING.md, step 3b); anyone else's is theirs alone.
        /// </summary>
        private static void MarkGiven()
        {
            if (PlaySession.IsAdmin)
            {
                PlaySession.AdminEdited = true;
            }
        }
    }
}
