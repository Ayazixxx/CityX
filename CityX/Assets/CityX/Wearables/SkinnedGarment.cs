using CityX.Character;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// Base for garments that deform with the body across several joints at
    /// once -- the hooded cloak (the whole body) -- rather than the coarse rigid per-bone attachment the old
    /// t-shirt/hoodie used, which could not span a joint without tearing.
    ///
    /// Ported from wearables/hooded_cloak.gd's own wear_on(): SkinnedGarmentBuilder pre-bakes the garment mesh, in the
    /// editor, against this rig's own 14 named bones AT REST -- the exact
    /// bindpose convention FusedBodyBuilder uses for the body itself. The
    /// bake and the live pose agree exactly at rest, and deform together
    /// identically to the body from there, because both were bound the same
    /// way against the same rest geometry.
    ///
    /// PUTTING THE GARMENT ON DOES NOT BUILD ITS OWN RENDERER -- it registers
    /// with SkinnedGarmentCombiner instead, which owns ONE shared
    /// SkinnedMeshRenderer for whatever is currently worn. See that class's
    /// own doc comment for why: a real skin evaluation runs once per
    /// renderer, and this project expects "100s if not 1000s" of items
    /// across many players in one area eventually, so the per-character
    /// rendering cost has to stay bounded by slot count, not catalog size.
    ///
    /// OTHER PLAYERS SEE IT (2026-10-22): it is put on their copy of the
    /// wearer the same way (ICosmeticMountable), and keeps running there
    /// (ICosmeticKeepsRunning) for what it does to the body each frame.
    /// </summary>
    public abstract class SkinnedGarment : MonoBehaviour, IEquippableItem, ICosmeticMountable, ICosmeticKeepsRunning
    {
        public const float InteractRange = 2.5f;

        /// <summary>
        /// The 14 bones every garment mesh is baked against, in bind order --
        /// matches hooded_cloak.glb's own imported bone list exactly
        /// (confirmed by inspecting it). Bind ORDER is
        /// load-bearing: BoneWeight.boneIndexN are indices into this array,
        /// not names, so SkinnedGarmentBuilder and this file must agree on it.
        /// </summary>
        public static readonly HumanBodyBones[] Bones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg,
        };

        [Tooltip("Baked by SkinnedGarmentBuilder against this rig's own rest pose.")]
        public Mesh GarmentMesh;

        public Material GarmentMaterial;

        [Tooltip("The non-skinned, tipped-over ground pose. Hidden on equip -- " +
                 "left showing, it sits in its raw bind pose overlapping the live " +
                 "skin and the two z-fight into a flickering, wrong-shaded mess.")]
        public GameObject GroundPreview;

        [Tooltip("The body's triangles this garment covers, one bit each, not drawn while it is worn (BodyUnderGarment). Empty keeps the whole body.")]
        [HideInInspector] public byte[] HiddenBodyTriangles;

        [Tooltip("The body's triangle count HiddenBodyTriangles was made for: another body is drawn whole.")]
        public int HiddenFromBodyTriangles;

        public abstract string ItemId { get; }

        private SkinnedGarmentCombiner Combiner { get; set; }

        private Transform _nearbyPlayer;
        private bool _equipped;
        private SkinnedMeshRenderer _bodySkin;
        private Mesh _wholeBody;

        private void Update()
        {
            if (_equipped)
            {
                return;
            }
            TickPickup();
        }

        private void OnTriggerEnter(Collider other)
        {
            Transform body = InteractionReach.LocalPlayerBody(other);
            if (body != null)
            {
                _nearbyPlayer = body;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (InteractionReach.IsLeaving(other, _nearbyPlayer))
            {
                _nearbyPlayer = null;
            }
        }

        /// <summary>Proximity only -- no look-direction check, matching the ground truth.</summary>
        private void TickPickup()
        {
            if (_nearbyPlayer == null)
            {
                return;
            }
            if (!InteractKey.Tapped)
            {
                return;
            }

            Vector3 toSelf = transform.position - _nearbyPlayer.position;
            if (toSelf.sqrMagnitude > InteractRange * InteractRange)
            {
                return;
            }

            var inventory = _nearbyPlayer.GetComponentInParent<Inventory>();
            if (inventory == null)
            {
                return;
            }
            if (inventory.PickUp(ItemId))
            {
                Destroy(gameObject);
            }
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            PutOn(player);
        }

        /// <summary>Unregisters from the shared combiner, then destroys the (by then bare) root.</summary>
        public void OnUnequip(GameObject player)
        {
            TakeOff();
            Destroy(gameObject);
        }

        /// <summary>Worn by another player's copy of the wearer: drawn on its body as on the wearer's own.</summary>
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

        private void PutOn(GameObject player)
        {
            _equipped = true;
            // Left showing, the ground pose's own raw, unposed mesh sits
            // right where the live skin also renders and the two z-fight
            // into a flickering, wrongly-shaded mess -- confirmed by
            // rendering it before this fix, which looked like a rainbow
            // texture failure until the actual cause (two overlapping
            // copies of the same mesh) turned up.
            if (GroundPreview != null)
            {
                GroundPreview.SetActive(false);
            }

            var animator = player.GetComponentInChildren<Animator>();
            // GetBoneTransform THROWS rather than returning null when the
            // Animator has no avatar at all (as opposed to an avatar that
            // simply lacks one particular bone, which it tolerates) -- so
            // this has to be checked up front rather than only null-checked
            // after the fact.
            if (animator == null || animator.avatar == null || GarmentMesh == null)
            {
                return;
            }

            // Shared by every worn garment, not built per-item -- see
            // SkinnedGarmentCombiner's own class comment: a real GPU skin
            // evaluation runs once per SkinnedMeshRenderer, and with
            // "100s if not 1000s" of items eventually in the catalog that
            // cost has to stay bounded by how many SLOTS exist (at most a
            // top and legs go through this), not by catalog size.
            Combiner = player.GetComponent<SkinnedGarmentCombiner>();
            if (Combiner == null)
            {
                Combiner = player.AddComponent<SkinnedGarmentCombiner>();
            }
            Combiner.Register(ItemId, GarmentMesh, GarmentMaterial);
            CoverBody(player);
            OnEquipped(player);
        }

        /// <summary>The body given back whole and the worn skin unregistered.</summary>
        private void TakeOff()
        {
            OnUnequipped();
            if (_bodySkin != null && _wholeBody != null)
            {
                _bodySkin.sharedMesh = _wholeBody;
            }
            _bodySkin = null;
            _wholeBody = null;
            if (Combiner != null)
            {
                Combiner.Unregister(ItemId);
            }
            Combiner = null;
        }

        /// <summary>Draws the body without what this garment hides, keeping the whole body to put back on unequip.</summary>
        private void CoverBody(GameObject player)
        {
            if (HiddenBodyTriangles == null || HiddenBodyTriangles.Length == 0)
            {
                return;
            }
            Transform skin = TransformSearch.FindDeep(player.transform, BodySurface.SkinName);
            _bodySkin = skin != null ? skin.GetComponent<SkinnedMeshRenderer>() : null;
            if (_bodySkin != null)
            {
                _wholeBody = _bodySkin.sharedMesh;
                _bodySkin.sharedMesh = BodyCover.Cut(_wholeBody, HiddenBodyTriangles, HiddenFromBodyTriangles);
            }
        }

        /// <summary>
        /// Called once the live skin is built and showing. A plain garment
        /// has nothing further to do; HoodedCloak overrides this to hide
        /// the body's own skin underneath (see that class's own comment).
        /// </summary>
        protected virtual void OnEquipped(GameObject player)
        {
        }

        /// <summary>The reverse of OnEquipped, called before the skin is torn down.</summary>
        protected virtual void OnUnequipped()
        {
        }

        /// <summary>
        /// Shows or hides the live worn skin without tearing it down --
        /// HoodedCloak's own doc comment on why a sleeve mesh baked against
        /// ordinary locomotion's narrow arm range needs to disappear rather
        /// than be shown crumpled during a pose far outside that range.
        /// </summary>
        protected void SetSkinVisible(bool visible)
        {
            if (Combiner != null)
            {
                Combiner.SetVisible(ItemId, visible);
            }
        }

        /// <summary>Plain clothing carries no per-instance state.</summary>
        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}
