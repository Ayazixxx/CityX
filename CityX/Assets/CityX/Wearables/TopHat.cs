using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A classic black top hat, worn on the head. Ported from wearables/top_hat.gd: three procedural cylinders (TopHatBuilder) worn on the
    /// Head bone (HeadWearable).
    /// </summary>
    public sealed class TopHat : HeadWearable
    {
        /// <summary>
        /// NOT a direct copy of top_hat.gd's own 0.68: this rig's Head bone pivot sits at the head sphere's CENTRE, not at Godot's "neck-top"
        /// convention (a first attempt floated the hat a whole head-height above the scalp). HeadRadius (0.35) reaches the top of the head
        /// from here; Godot's own small embed below the top (0.70 - 0.68 = 0.02) is kept: 0.35 - 0.02.
        /// </summary>
        private static readonly Vector3 HatOnHead = new Vector3(0.0f, 0.33f, 0.0f);

        /// <summary>
        /// Applied at runtime rather than baked into a Material per piece, so Brim/Crown/Band all share ONE "Helmet" category material
        /// (CharacterSkin.EnsureSharedCategory) while still looking like a black hat with a dark red band.
        /// </summary>
        public static readonly Color HatColour = new Color(0.03f, 0.03f, 0.035f);
        public static readonly Color BandColour = new Color(0.09f, 0.02f, 0.02f);

        protected override string ItemId => ItemRegistry.TopHat;

        protected override Vector3 HeadPosition => HatOnHead;

        protected override void Awake()
        {
            base.Awake();
            ApplyTints();
        }

        /// <summary>
        /// Re-applied every time this component wakes up (ground pickup, equipped instance, editor capture) rather than trusted to survive
        /// PrefabUtility.SaveAsPrefabAsset: see WearableTint's class comment.
        /// </summary>
        private void ApplyTints()
        {
            WearableTint.Apply(transform.Find("Brim")?.GetComponent<Renderer>(), HatColour, 0.4f);
            WearableTint.Apply(transform.Find("Crown")?.GetComponent<Renderer>(), HatColour, 0.4f);
            WearableTint.Apply(transform.Find("Band")?.GetComponent<Renderer>(), BandColour, 0.5f);
        }
    }
}
