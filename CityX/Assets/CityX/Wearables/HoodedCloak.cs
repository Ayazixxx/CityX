using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A full-length hooded robe, and the first Unity wearable that is a
    /// single continuous skinned drape rather than a set of rigid pieces.
    /// Ported from wearables/hooded_cloak.gd. Covers the whole body in one
    /// mesh, so unlike SkelliJeans there is no rigid-piece seam to hide.
    ///
    /// SWAPS THE BODY FOR A STUB MESH UNDERNEATH, the way the Godot
    /// original's set_body_stubs_only(true) does -- that class's own
    /// comment names exactly why this is not optional for a full-coverage
    /// garment: "rigid cloth over a mesh that moves on its own will always
    /// let a limb through somewhere, and no amount of fitting fixes that."
    /// The fix that holds is having no independently-posed body there to
    /// poke through: FusedBodyBuilder.BuildStub bakes a filtered copy that
    /// keeps only hands, feet and a collar under the chin -- exactly what
    /// showed anyway -- and this swaps the body's own renderer to it while
    /// worn, restoring the full mesh on unequip.
    /// </summary>
    public sealed class HoodedCloak : SkinnedGarment
    {
        public override string ItemId => ItemRegistry.HoodedCloak;

        [Tooltip("Baked by FusedBodyBuilder -- hands, feet and a chest collar only. Set by SkinnedGarmentBuilder.BuildCloak.")]
        public Mesh BodyStubMesh;

        /// <summary>
        /// hooded_cloak.gd's own SLEEVE_ARM_ROTATION_DEGREES.Z -- a pure
        /// spread, arrived at there by tuning it live against the real mesh
        /// and pasted in once it looked right, not derived from anything
        /// measurable here. THE PIECE THIS PORT WAS MISSING ENTIRELY: the
        /// sleeve mesh is baked (build_cloak.py) against the character's
        /// rest pose, but hooded_cloak.gd never LEAVES the arms at rest
        /// while worn -- character.gd's own set_worn_arm_pose() folds this
        /// spread into the body's skin for as long as the cloak stays on,
        /// which is what actually opens the sleeve tube enough for the real,
        /// animated arm to move through it without visibly poking through
        /// the cloth or crumpling the tube's own geometry against a limb
        /// sitting closer to the body than the sleeve was authored for.
        /// Without it, the arms swing at their normal, tighter locomotion
        /// angles while the sleeves stay shaped for a wider stance -- which
        /// is exactly what "sleeves are fucked up" looks like.
        /// </summary>
        private const float SleeveArmSpreadDegrees = 15.0f;

        private SkinnedMeshRenderer _bodySkin;
        private Mesh _fullBodyMesh;
        private Transform _leftUpperArm;
        private Transform _rightUpperArm;

        protected override void OnEquipped(GameObject player)
        {
            Transform skin = FindDeep(player.transform, "Skin");
            if (skin != null)
            {
                _bodySkin = skin.GetComponent<SkinnedMeshRenderer>();
            }
            if (_bodySkin != null)
            {
                if (BodyStubMesh != null)
                {
                    _fullBodyMesh = _bodySkin.sharedMesh;
                    _bodySkin.sharedMesh = BodyStubMesh;
                }
                else
                {
                    // No stub baked (BuildCloak never ran, or the prefab
                    // predates it) -- fall back to hiding the whole body
                    // rather than leaving bare skin visible under the cloak.
                    _bodySkin.gameObject.SetActive(false);
                }
            }

            var animator = player.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                _leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                _rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
        }

        /// <summary>
        /// Re-applied every frame in LateUpdate, AFTER the Animator's own
        /// evaluation for that frame (Unity runs Animator updates between
        /// Update and LateUpdate) -- PistolWeapon.UpdateHeldPose's own
        /// comment covers the same reasoning for the same reason: a
        /// MonoBehaviour cannot set a bone's rotation once at equip time and
        /// have it stick, because the very next Animator update overwrites
        /// it with whatever the current locomotion clip says.
        /// </summary>
        private void LateUpdate()
        {
            ApplySleeveArmSpread();
        }

        /// <summary>
        /// PUBLIC BECAUSE LATEUPDATE IS NOT AVAILABLE OUTSIDE PLAY MODE --
        /// same reasoning PlayerLocomotion.Bind's own doc comment gives:
        /// SkinnedGarmentBuilder's capture tool equips a cloak in the editor,
        /// never entering play mode, so nothing would ever call LateUpdate
        /// and this correction would silently never run there, which is
        /// exactly the gap that let a rigging bug like this one go
        /// unnoticed. Signs are opposite between the two arms -- matching
        /// PistolBuilder's own PoseArmsDown, which needs +78/-78 for the two
        /// sides to hang symmetrically down from a T-pose, so a spread in
        /// the opposite direction along that same axis needs the opposite
        /// pair of signs too.
        /// </summary>
        public void ApplySleeveArmSpread()
        {
            if (_leftUpperArm != null)
            {
                _leftUpperArm.localRotation *= Quaternion.Euler(0.0f, 0.0f, -SleeveArmSpreadDegrees);
            }
            if (_rightUpperArm != null)
            {
                _rightUpperArm.localRotation *= Quaternion.Euler(0.0f, 0.0f, SleeveArmSpreadDegrees);
            }
        }

        /// <summary>
        /// A plain Transform.Find("Skin") only checks DIRECT children, and
        /// FusedBodyBuilder.BuildBody nests it two deep (Player/Body/Skin,
        /// same for the civilian/OldGuy roots) -- a shallow Find silently
        /// returned null here and never hid anything, which is why a
        /// mid-stride pose still showed bare skin through the hood and
        /// wherever a limb kicked past the cloak's own coverage. Confirmed
        /// by rendering CaptureWearables' own stride shot before this fix.
        /// </summary>
        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        protected override void OnUnequipped(GameObject player)
        {
            if (_bodySkin != null)
            {
                if (_fullBodyMesh != null)
                {
                    _bodySkin.sharedMesh = _fullBodyMesh;
                }
                else
                {
                    _bodySkin.gameObject.SetActive(true);
                }
            }
            _bodySkin = null;
            _fullBodyMesh = null;
            // Stops LateUpdate from applying one more frame of spread after
            // unequip has already started -- clear_worn_top()'s own
            // "restores the normal stance" from the Godot original.
            _leftUpperArm = null;
            _rightUpperArm = null;
        }
    }
}
