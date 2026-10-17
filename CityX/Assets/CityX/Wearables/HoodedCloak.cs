using CityX.Character;
using CityX.Core;
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
        private Animator _animator;
        private Quaternion _leftUpperArmBase = Quaternion.identity;
        private Quaternion _rightUpperArmBase = Quaternion.identity;
        private GrappleSwing _swing;
        private bool _hiddenForSwing;

        protected override void OnEquipped(GameObject player)
        {
            Transform skin = TransformSearch.FindDeep(player.transform, "Skin");
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

            _animator = player.GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
            _swing = player.GetComponent<GrappleSwing>();
        }

        /// <summary>
        /// Re-applied every frame in LateUpdate, AFTER the Animator's own
        /// evaluation for that frame (Unity runs Animator updates between
        /// Update and LateUpdate) -- PistolWeapon.UpdateHeldPose's own
        /// comment covers the same reasoning for the same reason: a
        /// MonoBehaviour cannot set a bone's rotation once at equip time and
        /// have it stick, because the very next Animator update overwrites
        /// it with whatever the current locomotion clip says.
        ///
        /// SKIPPED ENTIRELY WHILE A GRAPPLE SWING OWNS THE ARM -- see
        /// UpdateSwingHiding's own doc comment for why the cloak has nothing
        /// to show during a swing at all, let alone a 15 degree spread on
        /// top of whatever GrappleSwing's own reach pose just set.
        /// </summary>
        private void LateUpdate()
        {
            UpdateSwingHiding();
            if (_swing == null || !_swing.IsActive)
            {
                ApplySleeveArmSpread();
            }
        }

        /// <summary>
        /// PUBLIC FOR THE SAME REASON ApplySleeveArmSpread IS -- LateUpdate
        /// never runs outside Play Mode, and GrapplingHookBuilder's own
        /// CaptureSwingWithCloak (an EditMode batch render) needs to drive
        /// this by hand to actually show whether the hide-while-swinging fix
        /// works, the same gap that let the sleeve-crumple bug this exists
        /// to fix go unnoticed in the first place.
        ///
        /// THE SLEEVE MESH CANNOT FOLLOW A GRAPPLE SWING'S OWN REACH POSE --
        /// confirmed directly by rendering one (GrapplingHookBuilder's own
        /// CaptureSwingWithCloak): baked and weighted against ordinary
        /// locomotion's narrow arm range (plus this file's own fixed 15
        /// degree spread), the tube crumples into a flat, wing-shaped mess
        /// at the shoulder when the arm reaches GrappleSwing's own ~150
        /// degrees for the rope -- a real bug report ("the body stays still
        /// and starts stretching") that survived a full audit of the
        /// swing's own pose code because the bug was never in the body at
        /// all. No amount of re-weighting fixes a rigid cone stretched that
        /// far without real cloth simulation, so this hides the cloak's own
        /// worn skin for the swing's duration instead -- the same
        /// stub-body/full-body swap OnEquipped/OnUnequipped already do,
        /// just triggered by the swing starting or ending rather than by
        /// the garment itself going on or off.
        /// </summary>
        public void UpdateSwingHiding()
        {
            bool swinging = _swing != null && _swing.IsActive;
            if (swinging == _hiddenForSwing)
            {
                return;
            }
            _hiddenForSwing = swinging;

            SetSkinVisible(!swinging);
            if (_bodySkin != null && _fullBodyMesh != null && BodyStubMesh != null)
            {
                _bodySkin.sharedMesh = swinging ? _fullBodyMesh : BodyStubMesh;
            }
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
        ///
        /// COMPOSES OFF A BASE RE-CAPTURED ONLY WHILE THE ANIMATOR IS LIVE,
        /// not off the bone's own current value -- see
        /// PlayerLocomotion.ApplyCastingPose's own doc comment for the full
        /// mechanism, since this used to be the exact same bug: multiplying
        /// the spread onto arm.localRotation every frame is only safe
        /// because the Animator overwrites this same bone with the clean
        /// clip pose earlier in the very same frame, every frame, while it
        /// is enabled. EXPLICIT BUG REPORT: wearing the cloak while opening
        /// the dev console (which disables the Animator the same way
        /// Ragdoll.Activate and GrappleSwing.Attach already do, for its own
        /// unrelated reason -- see DevConsoleView.Open's own comment) left
        /// the arms compounding this spread every frame with nothing to
        /// reset them, spinning them away until the console closed and
        /// re-enabled the Animator -- "stretched out for a sec... until I
        /// left console," with no grapple and no fireball cast involved at
        /// all.
        /// </summary>
        public void ApplySleeveArmSpread()
        {
            bool animatorLive = _animator != null && _animator.enabled;
            if (_leftUpperArm != null)
            {
                if (animatorLive)
                {
                    _leftUpperArmBase = _leftUpperArm.localRotation;
                }
                _leftUpperArm.localRotation = _leftUpperArmBase * Quaternion.Euler(0.0f, 0.0f, -SleeveArmSpreadDegrees);
            }
            if (_rightUpperArm != null)
            {
                if (animatorLive)
                {
                    _rightUpperArmBase = _rightUpperArm.localRotation;
                }
                _rightUpperArm.localRotation = _rightUpperArmBase * Quaternion.Euler(0.0f, 0.0f, SleeveArmSpreadDegrees);
            }
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
            _animator = null;
            _swing = null;
            _hiddenForSwing = false;
        }
    }
}
