using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// E PICKS UP the dropped item (DroppedItem) the player looks at most directly within reach, into their inventory. It acts only for the body
    /// this machine's player drives: another player's copy has its input reader switched off, and an NPC has none.
    /// </summary>
    public sealed class DroppedItemPicker : MonoBehaviour
    {
        /// <summary>As far as any pickup reaches, metres from the feet.</summary>
        public const float ReachMetres = 2.5f;

        /// <summary>The look cone's cosine: wider than a pickup at chest height (0.82), as a dropped item lies on the ground at the feet.</summary>
        public const float LookDot = 0.7f;

        /// <summary>Which drops: this machine's (a test gives each of its machines its own).</summary>
        public DroppedItems Record = DroppedItems.Shared;

        private PlayerInputReader _reader;
        private PlayerVitals _vitals;

        private void Awake()
        {
            _reader = GetComponent<PlayerInputReader>();
            _vitals = GetComponent<PlayerVitals>();
        }

        private void Update()
        {
            if (_reader == null || !_reader.enabled)
            {
                return;
            }
            if (InteractKey.Tapped)
            {
                TryPickUp();
            }
        }

        /// <summary>The dropped item the player would pick up now, or null. Public so a test can ask without a keyboard.</summary>
        public DroppedItem InReach()
        {
            DroppedItem best = null;
            float bestDot = float.MinValue;
            foreach (DroppedItem item in Record.Shown)
            {
                if (item == null || Record.IsAskedFor(item.Id))
                {
                    continue;
                }
                if (InteractionReach.CanReach(item.Centre, transform, ReachMetres, LookDot, out float dot) && dot > bestDot)
                {
                    best = item;
                    bestDot = dot;
                }
            }
            return best;
        }

        /// <summary>Picks up the item in reach (at once, or once the server says this player was first). False with nothing to pick up.</summary>
        public bool TryPickUp()
        {
            var inventory = GetComponent<Inventory>();
            if (inventory == null || (_vitals != null && _vitals.IsDead))
            {
                return false;
            }
            DroppedItem item = InReach();
            return item != null && Record.Take(item.Id, inventory);
        }
    }
}
