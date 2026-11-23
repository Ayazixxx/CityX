using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// A pair of fingerless gloves, worn in the gloves slot (owner, 2026-11-23; docs/FINGERLESS_GLOVES.md). Worn, they are the wearer's own hands
    /// lifted a little off the skin and cut short (GloveShell), drawn with the body skin's own bones, so they bend with every finger. Each
    /// colourway (FingerlessGloveColourways) is this same component with its own item and material. Lying on the ground they are a laid-out
    /// pair (the builder's model).
    ///
    /// Other players see them on their copy of the wearer (ICosmeticMountable).
    /// </summary>
    public sealed class FingerlessGloves : GroundWearable, IEquippableItem, ICosmeticMountable
    {
        public const string WornName = "FingerlessGloves";

        [SerializeField] private string _itemId = FingerlessGloveColourways.BlackItemId;
        [SerializeField] private Material _material;
        [SerializeField] private GameObject _onGround;

        /// <summary>The gloves made for each body mesh (every body shares one): made once, worn by all.</summary>
        private static readonly Dictionary<Mesh, Mesh> Made = new Dictionary<Mesh, Mesh>();

        private GameObject _worn;

        protected override string ItemId => _itemId;

        public Material Material => _material;

        /// <summary>The gloves drawn on the wearer, while worn.</summary>
        public SkinnedMeshRenderer Worn => _worn != null ? _worn.GetComponent<SkinnedMeshRenderer>() : null;

        /// <summary>The builder's: which colourway this pair is, its leather and the model it lies on the ground as.</summary>
        public void Setup(string itemId, Material material, GameObject onGround)
        {
            _itemId = itemId;
            _material = material;
            _onGround = onGround;
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

        /// <summary>Worn by another player's copy of the wearer: on its hands as on the wearer's own.</summary>
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

        /// <summary>Plain gloves carry no per-instance state.</summary>
        public ItemInstanceData GetInstanceData() => ItemInstanceData.None;

        /// <summary>
        /// The gloves for the body <paramref name="skin"/> draws, made the first time any body of its mesh wears a pair. Its bones are known by
        /// name (BodyRig): the finger bones are not in the humanoid avatar, so an Animator cannot find them.
        /// </summary>
        public static Mesh GlovesFor(SkinnedMeshRenderer skin)
        {
            Mesh whole = WornBodyCover.Whole(skin);
            if (whole == null)
            {
                return null;
            }
            if (Made.TryGetValue(whole, out Mesh made) && made != null)
            {
                return made;
            }
            var humanNamed = new Dictionary<string, HumanBodyBones>();
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                if (bone.Human != HumanBodyBones.LastBone)
                {
                    humanNamed[bone.Name] = bone.Human;
                }
            }
            var boneOf = new Dictionary<int, HumanBodyBones>();
            Transform[] bones = skin.bones;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && humanNamed.TryGetValue(bones[i].name, out HumanBodyBones human))
                {
                    boneOf[i] = human;
                }
            }
            made = GloveShell.Build(whole, boneOf);
            Made[whole] = made;
            return made;
        }

        /// <summary>
        /// The gloves drawn on the body's hands with the body skin's own bones, and this root onto the body (so it goes with it).
        /// </summary>
        private void PutOn(GameObject body)
        {
            MarkEquipped();
            if (_onGround != null)
            {
                _onGround.SetActive(false);
            }
            Transform skinObject = body != null ? TransformSearch.FindDeep(body.transform, BodySurface.SkinName) : null;
            var skin = skinObject != null ? skinObject.GetComponent<SkinnedMeshRenderer>() : null;
            if (skin == null)
            {
                return;
            }
            transform.SetParent(body.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            TakeOff();
            _worn = new GameObject(WornName);
            _worn.layer = skin.gameObject.layer;
            _worn.transform.SetParent(skin.transform.parent, false);
            _worn.transform.localPosition = skin.transform.localPosition;
            _worn.transform.localRotation = skin.transform.localRotation;
            _worn.transform.localScale = skin.transform.localScale;
            var renderer = _worn.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = GlovesFor(skin);
            renderer.bones = skin.bones;
            renderer.rootBone = skin.rootBone;
            renderer.localBounds = skin.localBounds;
            renderer.updateWhenOffscreen = skin.updateWhenOffscreen;
            renderer.quality = skin.quality;
            renderer.sharedMaterial = _material;
        }

        private void TakeOff()
        {
            if (_worn != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_worn);
                }
                else
                {
                    DestroyImmediate(_worn);
                }
                _worn = null;
            }
        }

        /// <summary>The gloves are drawn beside the body's skin, not under this root: they go with it.</summary>
        private void OnDestroy()
        {
            TakeOff();
        }
    }
}
