using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A short-sleeved tee, worn as a "top": Meshy's shirt cut off its mannequin and fitted to this body in Blender (TShirtBuilder), with a
    /// flat front and back so a design prints cleanly on it. Every printed shirt (the Blood Raven T-Shirt, ...) is this same shirt with its
    /// own texture and item.
    ///
    /// PRESSED DESIGNS (docs/HEAT_PRESS.md): any of them can carry a design pressed at the heat press, by id, in its instance data; it is drawn
    /// with the design over its own cloth (<see cref="PrintedShirt"/>), worn, on another player's copy, and lying folded on the ground. A design
    /// this machine does not have yet is fetched (DesignLibrary) and shows when it arrives.
    /// </summary>
    public sealed class TShirt : SkinnedGarment, IPrintable
    {
        [SerializeField] private string _itemId = ItemRegistry.TShirt;

        [Tooltip("Where the middle of the chest lies on the texture (ShirtPrintBaker.FitSide), for a pressed design.")]
        public Vector2 FrontMiddleUv;

        [Tooltip("Where the middle of the back lies on the texture.")]
        public Vector2 BackMiddleUv;

        [Tooltip("How far one metre along the cloth goes on the texture.")]
        public float UvPerMetre;

        public override string ItemId => _itemId;

        /// <summary>Makes this shirt the given item (a printed shirt's builder).</summary>
        public void SetItemId(string itemId) => _itemId = itemId;

        public string DesignId { get; private set; } = string.Empty;

        /// <summary>Whether the design on it is drawn (false while it is being fetched).</summary>
        public bool ShowsDesign => _printed != null;

        private Material _printed;

        protected override Material WornMaterial => _printed != null ? _printed : GarmentMaterial;

        protected override void TakeInstanceData(ItemInstanceData data) => ShowDesign(data.DesignId);

        public override ItemInstanceData GetInstanceData() =>
            DesignId.Length > 0 ? new ItemInstanceData { DesignId = DesignId } : ItemInstanceData.None;

        public void ShowDesign(string designId)
        {
            designId = designId ?? string.Empty;
            if (designId == DesignId)
            {
                return;
            }
            DesignId = designId;
            _printed = null;
            Redraw();
            if (designId.Length == 0)
            {
                return;
            }
            DesignLibrary.Get(designId, blob =>
            {
                if (this == null || DesignId != designId)
                {
                    return;   // taken off, or another design since
                }
                _printed = PrintedShirt.MaterialFor(this, designId, blob);
                Redraw();
            });
        }

        /// <summary>The worn skin, and the folded shirt on the ground, in the material it should have now.</summary>
        private void Redraw()
        {
            ShowWorn(GarmentMesh, WornMaterial);
            if (GroundPreview == null)
            {
                return;
            }
            foreach (Renderer renderer in GroundPreview.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = WornMaterial;
            }
        }
    }
}
