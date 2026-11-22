using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A BOX OF ROUNDS lying on the ground (owner, 2026-11-21: "make a basic ammunition model for each type of gun that is an in game right now
    /// as an item spawn you can spawn and pick up into your inventory and press r to reload into your guns"). E puts its rounds in the bag as a
    /// stack of its ammunition (ItemRegistry); what does not fit stays in the box. A gun reloads from the bag (PistolWeapon.SpareRounds). Built
    /// by AmmoBuilder, one per kind, colour-coded as games mark their ammo.
    /// </summary>
    public sealed class AmmoBox : MonoBehaviour
    {
        /// <summary>How near the player stands to pick it up, metres: as near as to a garment on the ground.</summary>
        public const float PickupRange = 2.5f;

        [Tooltip("The ammunition this box holds (ItemRegistry). Set by AmmoBuilder.")]
        [SerializeField] private string _itemId;

        [Tooltip("How many rounds a box holds.")]
        [SerializeField] private int _rounds;

        [Tooltip("The guns that load it, for its card.")]
        [SerializeField] private string _forGuns;

        private readonly GroundPickup _pickup = new GroundPickup();

        public string ItemId => _itemId;
        public int Rounds => _rounds;
        public string ForGuns => _forGuns;

        /// <summary>What the box holds (AmmoBuilder).</summary>
        public void Fill(string itemId, int rounds, string forGuns)
        {
            _itemId = itemId;
            _rounds = rounds;
            _forGuns = forGuns;
        }

        private void Update()
        {
            if (_rounds > 0 && _pickup.TappedWithin(transform.position, PickupRange))
            {
                _rounds = GroundPickup.TakeStack(_pickup.Player, gameObject, _itemId, _rounds);
            }
        }

        private void OnTriggerEnter(Collider other) => _pickup.Entered(other);

        private void OnTriggerExit(Collider other) => _pickup.Left(other);
    }
}
