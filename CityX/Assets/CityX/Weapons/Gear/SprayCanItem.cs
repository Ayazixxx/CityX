using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A CAN OF SPRAY PAINT (owner, 2026-10-15: "add a graffiti spray paint can item ... go up to walls and right click with it equip in hand and
    /// it opens some free draw menu"). In the right hand, held as a bottle is; right-click at a wall opens the graffiti canvas
    /// (PlayerInputReader.SprayRequested, answered by the UI's GraffitiMenuView). The can remembers the last colour and brush size painted with,
    /// and its cap takes that colour. Other players see it in your hand (ICosmeticMountable); the graffiti itself reaches them through
    /// GraffitiNetwork. The model is drawn in code by SprayCanBuilder.
    /// </summary>
    public sealed class SprayCanItem : MonoBehaviour, IEquippableItem, ICosmeticMountable, ISprayCan
    {
        /// <summary>The can in the hand: upright in the fist, the nozzle up and forward, as the bottle is held.</summary>
        public static readonly Vector3 GripHandPosition = new Vector3(0.0f, -0.04f, 0.06f);

        public static readonly Vector3 GripHandRotationEuler = new Vector3(-15.0f, 10.0f, 5.0f);

        [Tooltip("The cap: shows the colour last painted with.")]
        public Renderer Cap;

        [SerializeField] private Color _colour = new Color(0.95f, 0.15f, 0.2f);
        [SerializeField] private int _brush = 6;

        private MaterialPropertyBlock _block;

        public Color Colour
        {
            get => _colour;
            set
            {
                _colour = value;
                ShowColour();
            }
        }

        public int Brush
        {
            get => _brush;
            set => _brush = Mathf.Max(1, value);
        }

        private void Awake()
        {
            ShowColour();
        }

        private void ShowColour()
        {
            if (Cap == null)
            {
                return;
            }
            _block ??= new MaterialPropertyBlock();
            Cap.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", _colour);
            Cap.SetPropertyBlock(_block);
        }

        public void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            var animator = player.GetComponentInChildren<Animator>();
            Transform hand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (hand != null)
            {
                transform.SetParent(hand, false);
                transform.localPosition = GripHandPosition;
                transform.localEulerAngles = GripHandRotationEuler;
                transform.localScale = Vector3.one;
            }
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            var input = player.GetComponent<PlayerInputReader>();
            if (input != null)
            {
                input.EquippedSprayCan = this;
            }
        }

        public void OnUnequip(GameObject player)
        {
            var input = player != null ? player.GetComponent<PlayerInputReader>() : null;
            if (input != null && ReferenceEquals(input.EquippedSprayCan, this))
            {
                input.EquippedSprayCan = null;
            }
            Destroy(gameObject);
        }

        public void MountCosmetic(GameObject body)
        {
            CosmeticMount.ToBone(transform, body, HumanBodyBones.RightHand, GripHandPosition, GripHandRotationEuler, 1.0f);
            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
        }

        public void UnmountCosmetic(GameObject body)
        {
        }

        public void SetCosmeticActive(bool active)
        {
        }

        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;
    }
}
