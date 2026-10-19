using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// The owner's Knight Helmet (KnightxHelmet.glb, 2026-10-18): a closed steel helmet worn over the whole head. Its model is baked by
    /// KnightHelmetBuilder with the head's centre at the origin, placed the way the owner fitted it in Blender, so it sits on the Head bone's
    /// pivot (the head sphere's centre) with no offset.
    ///
    /// Unlike the hats, other players see it: it is mounted on their copy of the wearer (ICosmeticMountable, NetworkedPlayerWiring).
    /// </summary>
    public sealed class KnightHelmet : HeadWearable, ICosmeticMountable
    {
        /// <summary>The child holding the mesh, tinted steel on the shared helmet material.</summary>
        public const string ModelName = "Model";

        public static readonly Color SteelColour = new Color(0.5f, 0.51f, 0.54f);

        public const float SteelMetallic = 0.85f;

        public const float SteelSmoothness = 0.5f;

        protected override string ItemId => ItemRegistry.KnightHelmet;

        protected override Vector3 HeadPosition => Vector3.zero;

        protected override void Awake()
        {
            base.Awake();
            WearableTint.Apply(transform.Find(ModelName)?.GetComponent<Renderer>(), SteelColour, SteelSmoothness, 0, SteelMetallic);
        }

        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.Head, HeadPosition, HeadRotationEuler, 1.0f);
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }
    }
}
