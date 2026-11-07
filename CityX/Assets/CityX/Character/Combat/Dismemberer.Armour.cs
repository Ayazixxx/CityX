using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    // Armour and what is worn, against dismemberment (docs/ARMOUR.md; owner, 2026-11-06: "probably having armor makes you harder to be
    // dismembered and if you do still get dismembered badly we could have the armor just come off the body and turn into its object form").
    public sealed partial class Dismemberer
    {
        /// <summary>The heaviest armour covering <paramref name="part"/>, as its tier's share (0 bare, 1 heavy: Items.ArmourRatings).</summary>
        public float ArmourOn(BodyPart part)
        {
            float best = 0.0f;
            foreach (IWornItem worn in GetComponentsInChildren<IWornItem>())
            {
                if (worn.IsWorn && ItemRegistry.TryGet(worn.WornItemId, out ItemDefinition definition) && DismemberRules.Covers(definition.EquipSlot, part))
                {
                    best = Mathf.Max(best, ArmourRatings.TierShare(ArmourRatings.TierOf(worn.WornItemId)));
                }
            }
            return best;
        }

        /// <summary>
        /// What is worn on a part that comes off (a helmet on the head) falls off the body as the item itself, for anyone to pick up, instead of
        /// going with the part or bursting with it. The machine that decides the body drops it (for everyone in a session: DroppedItems); every
        /// machine takes it off its own copy of the body. A player's own gear stays theirs (their inventory still holds it).
        /// </summary>
        private void PopWornOff(Transform under, Vector3 away)
        {
            if (PlayerBodies.IsPlayer(gameObject))
            {
                return;
            }
            var loot = GetComponent<CorpseLoot>();
            foreach (IWornItem worn in under.GetComponentsInChildren<IWornItem>())
            {
                if (!worn.IsWorn || string.IsNullOrEmpty(worn.WornItemId) || !(worn is Component component))
                {
                    continue;
                }
                string itemId = worn.WornItemId;
                if (!IsCopy)
                {
                    var pose = new Pose(component.transform.position, component.transform.rotation);
                    Vector3 fling = away * BlownSpeed + Vector3.up * KnockOffLift;
                    DroppedItems.Shared.Drop(DroppedItem.LetGo(itemId, ItemInstanceData.None, pose, transform, Vector3.zero, fling),
                        shared: EnemySession.IsShared(gameObject), gameObject);
                }
                foreach (Renderer renderer in component.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;   // gone now: the part's own pieces are made this frame, before the item's object is destroyed
                }
                if (loot != null)
                {
                    loot.Lose(itemId);
                }
                if (worn is IEquippableItem equippable)
                {
                    equippable.OnUnequip(gameObject);
                }
                PiecesPoppedOff++;
            }
        }

        /// <summary>How many worn pieces have come off this body as items (for tests).</summary>
        public int PiecesPoppedOff { get; private set; }
    }
}
