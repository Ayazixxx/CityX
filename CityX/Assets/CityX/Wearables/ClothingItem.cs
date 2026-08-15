using CityX.Character;
using CityX.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Wearables
{
    /// <summary>
    /// Base for torso-slot clothing, ported from wearables/clothing.gd.
    /// Several separate rigid pieces, each parented onto its own body
    /// bone (torso onto the spine, sleeves onto the arm bones, a hood onto
    /// the neck) -- not one skinned mesh, not one shared parent. Godot's
    /// own accepted limitation (a visible seam at the shoulder mid-swing)
    /// carries over unchanged; true skinning is what skelli_jeans needed
    /// and is out of scope for t-shirt/hoodie.
    /// </summary>
    public abstract class ClothingItem : MonoBehaviour, IEquippableItem
    {
        public const float InteractRange = 2.5f;

        [System.Serializable]
        public struct BonePiece
        {
            public HumanBodyBones Bone;
            public Transform Piece;
            public Vector3 LocalPosition;
            public Vector3 LocalEulerAngles;
        }

        public BonePiece[] Pieces;

        public abstract string ItemId { get; }

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

        /// <summary>Proximity only -- no look-direction check, unlike the bottle.</summary>
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
            var animator = player.GetComponentInChildren<Animator>();
            if (animator == null || Pieces == null)
            {
                return;
            }

            foreach (BonePiece piece in Pieces)
            {
                if (piece.Piece == null)
                {
                    continue;
                }
                Transform bone = animator.GetBoneTransform(piece.Bone);
                if (bone == null)
                {
                    continue;
                }
                piece.Piece.SetParent(bone, false);
                // EXPLICITLY RE-APPLIED, NOT TRUSTED FROM THE PREFAB --
                // matches FusedBodyBuilder.AttachHead's own parent-then-set
                // order, since SetParent(worldPositionStays: false) alone
                // does not guarantee the exact baked offset survives.
                piece.Piece.localPosition = piece.LocalPosition;
                piece.Piece.localRotation = Quaternion.Euler(piece.LocalEulerAngles);
            }
        }

        /// <summary>
        /// Destroys every piece individually, then the (by then empty)
        /// root -- a plain Destroy(gameObject) on the root alone would
        /// leak every piece scattered onto the body's own bones.
        /// </summary>
        public void OnUnequip(GameObject player)
        {
            if (Pieces != null)
            {
                foreach (BonePiece piece in Pieces)
                {
                    if (piece.Piece != null)
                    {
                        Destroy(piece.Piece.gameObject);
                    }
                }
            }
            Destroy(gameObject);
        }

        /// <summary>Plain clothing carries no per-instance state.</summary>
        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}
