using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Wearables
{
    /// <summary>
    /// Base for garments that deform with the body across several joints at
    /// once -- skelli jeans (hip and knee) and the hooded cloak (the whole
    /// body) -- rather than the coarse rigid per-bone attachment the old
    /// t-shirt/hoodie used, which could not span a joint without tearing.
    ///
    /// Ported from wearables/skelli_jeans.gd's and hooded_cloak.gd's own
    /// wear_on(): SkinnedGarmentBuilder pre-bakes the garment mesh, in the
    /// editor, against this rig's own 14 named bones AT REST -- the exact
    /// bindpose convention FusedBodyBuilder uses for the body itself. Putting
    /// the garment on a character at runtime is then only building a fresh
    /// SkinnedMeshRenderer whose bones[] point at THAT character's own live
    /// bone transforms: the bake and the live pose agree exactly at rest, and
    /// deform together identically to the body from there, because both were
    /// bound the same way against the same rest geometry.
    /// </summary>
    public abstract class SkinnedGarment : MonoBehaviour, IEquippableItem
    {
        public const float InteractRange = 2.5f;

        /// <summary>
        /// The 14 bones every garment mesh is baked against, in bind order --
        /// matches skelli_jeans.glb's and hooded_cloak.glb's own imported
        /// bone list exactly (confirmed by inspecting both). Bind ORDER is
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

        public abstract string ItemId { get; }

        private GameObject _skinInstance;
        private Transform _nearbyPlayer;
        private bool _equipped;

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
            if (other.GetComponentInParent<PlayerVitals>() != null)
            {
                _nearbyPlayer = other.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform == _nearbyPlayer || (other.GetComponentInParent<PlayerVitals>() != null))
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
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
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
            if (inventory.AddItem(ItemId, 1))
            {
                Destroy(gameObject);
            }
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
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

            var boneTransforms = new Transform[Bones.Length];
            for (int i = 0; i < Bones.Length; i++)
            {
                boneTransforms[i] = animator.GetBoneTransform(Bones[i]);
            }

            var holder = new GameObject(ItemId + "Skin");
            holder.transform.SetParent(player.transform, false);

            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = GarmentMesh;
            renderer.bones = boneTransforms;
            renderer.rootBone = boneTransforms[0];   // Hips
            renderer.sharedMaterial = GarmentMaterial;
            // Four bones per vertex, explicitly -- matches FusedBodyBuilder's
            // own body renderer, since a garment sharing a joint with the
            // body has to deform by the same rule or the two visibly diverge
            // wherever more than one bone influences a vertex.
            renderer.quality = SkinQuality.Bone4;
            renderer.updateWhenOffscreen = true;

            _skinInstance = holder;
            OnEquipped(player);
        }

        /// <summary>Destroys the live skin instance, then the (by then bare) root.</summary>
        public void OnUnequip(GameObject player)
        {
            OnUnequipped(player);
            if (_skinInstance != null)
            {
                Destroy(_skinInstance);
            }
            _skinInstance = null;
            Destroy(gameObject);
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
        protected virtual void OnUnequipped(GameObject player)
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
            if (_skinInstance != null)
            {
                _skinInstance.SetActive(visible);
            }
        }

        /// <summary>Plain clothing carries no per-instance state.</summary>
        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}
