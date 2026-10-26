using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A pair of shoes, worn on the feet (the White Sneaker first, owner 2026-10-25). Each shoe is rigid on its foot bone, as a helmet is on
    /// the head: the body's feet are one bone each, with nothing to bend a sole. The models are baked in their foot bone's space at rest
    /// (WhiteSneakerBuilder), so worn each hangs on its bone with no offset; lying on the ground they stand side by side as worn. The feet
    /// under them, and the ankles the collars cover, are not drawn (WornBodyCover), so no skin comes through.
    ///
    /// Other players see them on their copy of the wearer (ICosmeticMountable).
    /// </summary>
    public sealed class Shoes : GroundWearable, IEquippableItem, ICosmeticMountable
    {
        [SerializeField] private string _itemId = ItemRegistry.WhiteSneaker;

        [SerializeField] private Transform _left;
        [SerializeField] private Transform _right;

        /// <summary>The body triangles the pair hides (one bit each, BodyUnderGarment), and the body triangle count they were made for.</summary>
        [HideInInspector] public byte[] HiddenBodyTriangles;

        public int HiddenFromBodyTriangles;

        private SkinnedMeshRenderer _bodySkin;

        protected override string ItemId => _itemId;

        /// <summary>The left shoe's model (worn: on the left foot bone).</summary>
        public Transform Left => _left;

        public Transform Right => _right;

        /// <summary>The builder's: which item this pair is and its two models.</summary>
        public void Setup(string itemId, Transform left, Transform right)
        {
            _itemId = itemId;
            _left = left;
            _right = right;
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            PutOn(player);
        }

        public void OnUnequip(GameObject player)
        {
            TakeOff();
            Destroy(gameObject);
        }

        /// <summary>Worn by another player's copy of the wearer: on its feet as on the wearer's own.</summary>
        public void MountCosmetic(GameObject body)
        {
            PutOn(body);
        }

        /// <summary>Off the copy; NetworkedEquipmentSync destroys the model.</summary>
        public void UnmountCosmetic(GameObject body)
        {
            TakeOff();
        }

        public void SetCosmeticActive(bool active)
        {
        }

        /// <summary>Plain shoes carry no per-instance state.</summary>
        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        /// <summary>
        /// Each shoe onto its foot bone, the pair's root onto the body (so it goes with it), the feet under them hidden. An Animator with no avatar
        /// is tolerated: GetBoneTransform throws on one.
        /// </summary>
        private void PutOn(GameObject body)
        {
            MarkEquipped();
            var animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            if (animator == null || animator.avatar == null)
            {
                return;
            }
            transform.SetParent(body.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            Hang(_left, animator.GetBoneTransform(HumanBodyBones.LeftFoot));
            Hang(_right, animator.GetBoneTransform(HumanBodyBones.RightFoot));
            if (HiddenBodyTriangles == null || HiddenBodyTriangles.Length == 0)
            {
                return;
            }
            Transform skin = TransformSearch.FindDeep(body.transform, CityX.Character.BodySurface.SkinName);
            _bodySkin = skin != null ? skin.GetComponent<SkinnedMeshRenderer>() : null;
            if (_bodySkin != null)
            {
                WornBodyCover.Hide(_bodySkin, this, HiddenBodyTriangles, HiddenFromBodyTriangles);
            }
        }

        private static void Hang(Transform shoe, Transform foot)
        {
            if (shoe == null || foot == null)
            {
                return;
            }
            shoe.SetParent(foot, false);
            shoe.localPosition = Vector3.zero;
            shoe.localRotation = Quaternion.identity;
            shoe.localScale = Vector3.one;
        }

        /// <summary>The feet drawn again.</summary>
        private void TakeOff()
        {
            WornBodyCover.Show(_bodySkin, this);
            _bodySkin = null;
        }

        /// <summary>The shoes hang on the feet, not under this root: they go with it (taken off, or the copy wearing them gone).</summary>
        private void OnDestroy()
        {
            TakeOff();
            foreach (Transform shoe in new[] { _left, _right })
            {
                if (shoe != null && !shoe.IsChildOf(transform))
                {
                    Destroy(shoe.gameObject);
                }
            }
        }
    }
}
