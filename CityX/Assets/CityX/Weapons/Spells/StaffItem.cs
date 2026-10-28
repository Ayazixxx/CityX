using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A MAGIC STAFF in the right hand (docs/STAFFS.md; GAME_DESIGN.md: a staff's attack is its spell on the click, every shot paying mana).
    /// Every staff is held the same way: its shaft through the fist past the thumb, where a one-handed sword's handle runs, the body standing
    /// it upright or thrusting it out (PlayerLocomotion.Staff.cs). Left-click is the staff's (PlayerInputReader); what a click and a hold do is
    /// the spell's, on the body (<see cref="IStaffCaster"/>), so the shots reach other players as that spell's do. Another player's copy holds a
    /// model of it the same way. The models are drawn in code (StaffBuilder) until the owner's own.
    /// </summary>
    public abstract class StaffItem : MonoBehaviour, IEquippableItem, IEquippableStaff, ICosmeticMountable
    {
        /// <summary>The model's child at the head's middle, where shots leave from.</summary>
        public const string TipName = "Tip";

        [Tooltip("Where shots leave from: the middle of the staff's head.")]
        public Transform Tip;

        private PlayerLocomotion _locomotion;

        /// <summary>The staff's root in the right-hand bone's frame: the shaft's grip on the handle's line under the palm.</summary>
        public static Vector3 GripPosition => new Vector3(PlayerLocomotion.PalmOffsetMetres, -SwordWeapon.OneHandHandleBelowPalm, 0.0f);

        /// <summary>The shaft (the model's +Y) along the hand's +Z, out past the thumb.</summary>
        public static Quaternion GripRotation => Quaternion.FromToRotation(Vector3.up, Vector3.forward);

        /// <summary>The spell on <paramref name="body"/> this staff shoots.</summary>
        protected abstract IStaffCaster CasterOn(GameObject body);

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            Hold(player);
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedStaff = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            CancelCharge(player);
            var input = player != null ? player.GetComponent<PlayerInputReader>() : null;
            if (input != null && ReferenceEquals(input.EquippedStaff, this))
            {
                input.EquippedStaff = null;
            }
            Release();
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            Hold(body);
        }

        public void UnmountCosmetic(GameObject body)
        {
            Release();
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        public void Tick(bool triggerHeld, Vector3 eye, Vector3 look, GameObject wielder, float deltaTime)
        {
            CasterOn(wielder)?.TickStaff(triggerHeld, Tip != null ? Tip : transform, eye, look, deltaTime);
        }

        public void CancelCharge(GameObject wielder)
        {
            if (wielder != null)
            {
                CasterOn(wielder)?.CancelStaffCharge();
            }
        }

        private void OnDestroy()
        {
            Release();
        }

        /// <summary>Into the right hand (whose fingers then close on it), the body told it carries a staff (it poses the arm from here on).</summary>
        private void Hold(GameObject body)
        {
            var animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            Transform hand = animator != null && animator.avatar != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = GripPosition;
                transform.localRotation = GripRotation;
                transform.localScale = Vector3.one;
            }
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            _locomotion = body != null ? body.GetComponent<PlayerLocomotion>() : null;
            if (_locomotion != null)
            {
                _locomotion.Staff = transform;
            }
        }

        private void Release()
        {
            if (_locomotion != null && _locomotion.Staff == transform)
            {
                _locomotion.Staff = null;
            }
        }
    }
}
