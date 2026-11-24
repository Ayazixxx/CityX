using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A long open-fronted coat with a hood, worn as a "top" (docs/BROWN_COAT.md; owner, 2026-11-23: "try to make this brown coat item and make
    /// the hooded variant too. make it so it is hood off by default but if you type /hoodon in the text chat ... it turns into the hood on
    /// version"; 2026-11-24, with Meshy's two models of it: "put it into the game as a functional item called brown coat"). The same coat twice
    /// (SourceArt/BrownCoat), its hood lying on the back and its hood up round the head; putting the hood up swaps the one for the other
    /// (<see cref="HoodSync"/> tells other players). Each colour is its own item with its own pair of maps (the Brown Coat first).
    /// </summary>
    public sealed class HoodedCoat : SkinnedGarment, IHooded
    {
        [SerializeField] private string _itemId = ItemRegistry.BrownCoat;

        [Tooltip("The same coat with its hood up round the head (GarmentMesh is the hood-down one).")]
        public Mesh HoodUpMesh;

        [Tooltip("The hood-up coat's own paint (Meshy lays each model out its own way).")]
        public Material HoodUpMaterial;

        public override string ItemId => _itemId;

        /// <summary>Makes this coat the given item (each colour's builder).</summary>
        public void SetItemId(string itemId) => _itemId = itemId;

        public bool HoodUp { get; private set; }

        private HoodSync _sync;

        /// <summary>The hood up or down: the worn skin is the other coat. Hair is left as it is (the owner trims each style for the hood).</summary>
        public void SetHood(bool up)
        {
            if (HoodUp == up || HoodUpMesh == null)
            {
                return;
            }
            HoodUp = up;
            ShowWorn(up ? HoodUpMesh : GarmentMesh, up ? HoodUpMaterial : GarmentMaterial);
        }

        protected override void OnEquipped(GameObject player)
        {
            HoodUp = false;
            _sync = player.GetComponent<HoodSync>();
            if (_sync != null)
            {
                _sync.Wear(this);
            }
        }

        protected override void OnUnequipped()
        {
            if (_sync != null)
            {
                _sync.TakeOff(this);
            }
            _sync = null;
        }
    }
}
