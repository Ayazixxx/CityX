using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The player's characters standing in a row on the conveyor belt the creator makes them on (docs/CHARACTER_SELECT.md): each one dressed,
    /// painted and idling (<see cref="LineupFigure"/>), carried by the belt however far it has run (<see cref="ConveyorBelt.Travel"/>), whoever
    /// runs it: the character list rolling one to the middle, or the creator rolling a new body in behind them. On the creator's stage, with
    /// its belt, sky, lights and camera.
    /// </summary>
    public sealed class BeltLineup : MonoBehaviour
    {
        /// <summary>Half a body's width with its arms down, metres: how far past its middle it is still in sight, and can be clicked.</summary>
        public const float HalfWidth = 0.55f;

        [Header("Wired by CharacterSelectBuilder")]
        public ConveyorBelt Belt;

        [Tooltip("The creator's camera: the row is seen through it.")]
        public Camera View;

        [Tooltip("A player's body (Player.prefab), stripped down to a figure for each character.")]
        public GameObject BodyPrefab;

        [Tooltip("Loaded by the builder: runtime code has no AssetDatabase. The same hair the creator puts on.")]
        public Mesh[] HairStyleMeshes = new Mesh[0];

        public Material HairMaterialAsset;

        [Tooltip("The clothes a figure can wear, by item id. Loaded by the builder, as the hair is.")]
        public Inventory.ItemPrefabEntry[] ClothingPrefabs = new Inventory.ItemPrefabEntry[0];

        private readonly List<CharacterSave> _slots = new List<CharacterSave>();
        private readonly List<GameObject> _figures = new List<GameObject>();
        private Transform _row;

        /// <summary>How far the belt had run (<see cref="ConveyorBelt.Travel"/>) when slot 0 would have stood in the middle of the screen.</summary>
        public float Origin { get; set; }

        /// <summary>How far apart the figures stand, metres (the row's <see cref="BeltLine.Spacing"/>).</summary>
        public float Spacing { get; set; } = BeltLine.WideSpacing;

        /// <summary>How many slots the row has (an emptied one, a character just deleted, still counts).</summary>
        public int Count => _slots.Count;

        /// <summary>The figure in <paramref name="slot"/>, or null (empty, not made yet, or out of the row).</summary>
        public GameObject FigureAt(int slot) => slot >= 0 && slot < _figures.Count ? _figures[slot] : null;

        /// <summary>Stands <paramref name="characters"/> in a row on the belt, the first in slot 0; any figures there were go.</summary>
        public void Show(IReadOnlyList<CharacterSave> characters)
        {
            Clear();
            if (characters != null)
            {
                foreach (CharacterSave character in characters)
                {
                    _slots.Add(character);
                    _figures.Add(null);
                }
            }
            // Those in sight at once; the rest one a frame (MakeOneMissing), so opening a long row does not stall. Never under a stage that is
            // down: a figure would wake when it comes up, after it was stripped, and start the sounds its strip took away.
            for (int slot = 0; slot < _slots.Count && isActiveAndEnabled; slot++)
            {
                if (InSight(Along(slot)))
                {
                    Make(slot);
                }
            }
            Place();
        }

        /// <summary>Where <paramref name="slot"/> stands now, metres from the middle of the screen, positive to its right.</summary>
        public float Along(int slot)
        {
            float run = Belt != null ? Belt.Travel - Origin : -Origin;
            return slot * Spacing + run;
        }

        /// <summary>Whether something standing <paramref name="along"/> the belt is (at least partly) on the screen.</summary>
        public bool InSight(float along) => Mathf.Abs(along) - HalfWidth < BeltIntro.EdgeOfScreen(View);

        /// <summary>The character in <paramref name="slot"/> leaves the row (deleted): its slot stays, empty, so nobody else moves.</summary>
        public void Empty(int slot)
        {
            if (slot < 0 || slot >= _slots.Count)
            {
                return;
            }
            _slots[slot] = null;
            Remove(slot);
        }

        /// <summary>The figure in <paramref name="slot"/> made again for <paramref name="character"/> (its look changed: redrawn).</summary>
        public void Remake(int slot, CharacterSave character)
        {
            if (slot < 0 || slot >= _slots.Count)
            {
                return;
            }
            _slots[slot] = character;
            Remove(slot);
            Make(slot);
            Place();
        }

        /// <summary>Takes away every figure standing left of <paramref name="along"/> (out of sight: a new body is about to be rolled in there).</summary>
        public void ClearLeftOf(float along)
        {
            for (int slot = 0; slot < _slots.Count; slot++)
            {
                if (Along(slot) < along)
                {
                    _slots[slot] = null;
                    Remove(slot);
                }
            }
        }

        /// <summary>Where the leftmost figure in sight stands, metres from the middle; 0 with none in sight.</summary>
        public float LeftmostInSight()
        {
            float leftmost = 0.0f;
            for (int slot = 0; slot < _slots.Count; slot++)
            {
                float along = Along(slot);
                if (_slots[slot] != null && InSight(along))
                {
                    leftmost = Mathf.Min(leftmost, along);
                }
            }
            return leftmost;
        }

        /// <summary>The slot of the figure under <paramref name="screenPosition"/> (pixels), or -1.</summary>
        public int SlotUnder(Vector2 screenPosition)
        {
            if (View == null)
            {
                return -1;
            }
            for (int slot = 0; slot < _figures.Count; slot++)
            {
                GameObject figure = _figures[slot];
                if (figure == null)
                {
                    continue;
                }
                Transform at = figure.transform;
                Vector3 low = View.WorldToScreenPoint(at.TransformPoint(new Vector3(HalfWidth, 0.0f, 0.0f)));
                Vector3 high = View.WorldToScreenPoint(at.TransformPoint(new Vector3(-HalfWidth, BodyRig.StandingHeight, 0.0f)));
                if (low.z <= 0.0f || high.z <= 0.0f)
                {
                    continue;
                }
                Rect box = Rect.MinMaxRect(Mathf.Min(low.x, high.x), Mathf.Min(low.y, high.y), Mathf.Max(low.x, high.x), Mathf.Max(low.y, high.y));
                if (box.Contains(screenPosition))
                {
                    return slot;
                }
            }
            return -1;
        }

        /// <summary>Every figure gone and the row empty.</summary>
        public void Clear()
        {
            for (int slot = 0; slot < _figures.Count; slot++)
            {
                Remove(slot);
            }
            _slots.Clear();
            _figures.Clear();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void LateUpdate()
        {
            MakeOneMissing();
            Place();
        }

        /// <summary>The figure nearest the middle that is still to be made, if any: one a frame.</summary>
        private void MakeOneMissing()
        {
            int nearest = -1;
            for (int slot = 0; slot < _slots.Count; slot++)
            {
                if (_slots[slot] != null && _figures[slot] == null && (nearest < 0 || Mathf.Abs(Along(slot)) < Mathf.Abs(Along(nearest))))
                {
                    nearest = slot;
                }
            }
            if (nearest >= 0)
            {
                Make(nearest);
            }
        }

        /// <summary>Each figure where the belt has carried it. The stage's cameras look back along -Z, so the screen's right is its -X.</summary>
        private void Place()
        {
            for (int slot = 0; slot < _figures.Count; slot++)
            {
                if (_figures[slot] != null)
                {
                    _figures[slot].transform.localPosition = new Vector3(-Along(slot), 0.0f, 0.0f);
                }
            }
        }

        private void Remove(int slot)
        {
            if (slot < _figures.Count && _figures[slot] != null)
            {
                Destroy(_figures[slot]);
                _figures[slot] = null;
            }
        }

        // --- a figure ---

        /// <summary>Phase apart in the idle from one slot to the next, so neighbours never breathe together.</summary>
        private const float PhaseStep = 0.37f;

        private void Make(int slot)
        {
            CharacterSave character = _slots[slot];
            if (character == null || _figures[slot] != null || BodyPrefab == null)
            {
                return;
            }
            if (_row == null)
            {
                _row = new GameObject("Lineup") { layer = gameObject.layer }.transform;
                _row.SetParent(transform, false);
            }
            GameObject body = Instantiate(BodyPrefab, _row);
            body.name = $"Figure_{slot}_{character.Name}";
            body.transform.localPosition = new Vector3(-Along(slot), 0.0f, 0.0f);
            body.transform.localRotation = Quaternion.identity;
            StripWorldComponents(body);
            ApplyCosmetics(body, character);
            PutOnClothes(body, character);
            foreach (Transform part in body.GetComponentsInChildren<Transform>(true))
            {
                part.gameObject.layer = gameObject.layer;   // the clothes too: the stage's camera sees only its own layer
            }
            Animator animator = body.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                body.AddComponent<LineupFigure>().Begin(animator, slot * PhaseStep);
            }
            _figures[slot] = body;
        }

        /// <summary>
        /// Leaves a figure that only stands and idles: nothing that would walk it, swing, ragdoll, collide, count play time, save or make a
        /// sound. Dependants before what they depend on, or Unity refuses to remove a component another still needs: PlayerLocomotion,
        /// PlayerClimbing and GrappleSwing need the CharacterController, PlayerAnimationDriver needs MeleeAttacker.
        /// </summary>
        private static void StripWorldComponents(GameObject body)
        {
            Strip<PlayerInputReader>(body);
            Strip<PlayerLocomotion>(body);
            Strip<PlayerClimbing>(body);
            Strip<PlayerVaulting>(body);
            Strip<WallStick>(body);
            Strip<GrappleSwing>(body);
            Strip<CharacterController>(body);
            Strip<PlayerAnimationDriver>(body);
            Strip<MeleeAttacker>(body);
            Strip<Ragdoll>(body);
            Strip<CharacterIdentity>(body);
            Strip<PlayerVitals>(body);
            Strip<Joint>(body);
            Strip<Rigidbody>(body);
            Strip<Collider>(body);
            // PlayerAudio starts its looping city air in its own Awake, inside Instantiate: the source it made goes too.
            Strip<PlayerAudio>(body);
            Strip<AudioSource>(body);
        }

        private static void Strip<T>(GameObject root) where T : Component
        {
            foreach (T found in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                Destroy(found);
            }
        }

        /// <summary>
        /// The character's paint, skin and hair, as CharacterIdentity puts them on the body played, but not by that component: it also counts
        /// play time and saves, and a character looked at in a list is not being played.
        /// </summary>
        private void ApplyCosmetics(GameObject body, CharacterSave character)
        {
            byte[] painted = character.GetPaint();
            Texture2D atlas = null;
            if (painted != null)
            {
                var paint = new BodyPaint(character.Skin);
                if (paint.Decode(painted))
                {
                    paint.ShrinkForWearing();
                    atlas = paint.Atlas;
                }
            }

            foreach (Renderer renderer in BodySurface.Renderers(body.transform))
            {
                Material instance = renderer.material;
                if (instance == null)
                {
                    continue;
                }
                if (atlas != null)
                {
                    if (instance.HasProperty("_BaseMap")) instance.SetTexture("_BaseMap", atlas);
                    instance.mainTexture = atlas;
                    if (instance.HasProperty("_BaseColor")) instance.SetColor("_BaseColor", Color.white);
                    if (instance.HasProperty("_Color")) instance.SetColor("_Color", Color.white);
                }
                else
                {
                    if (instance.HasProperty("_BaseColor")) instance.SetColor("_BaseColor", character.Skin);
                    if (instance.HasProperty("_Color")) instance.SetColor("_Color", character.Skin);
                }
            }

            Transform head = TransformSearch.FindDeep(body.transform, "head");
            if (head != null && HairStyleMeshes != null && HairStyleMeshes.Length > 0)
            {
                var hair = body.AddComponent<CharacterHair>();
                hair.Head = head;
                hair.StyleMeshes = HairStyleMeshes;
                hair.HairMaterial = HairMaterialAsset;
                hair.SetStyle(string.IsNullOrEmpty(character.HairStyle) ? "none" : character.HairStyle);
                hair.SetColour(character.HairColour);
                hair.SetBeard(string.IsNullOrEmpty(character.Beard) ? CharacterHair.None : character.Beard);
            }
        }

        /// <summary>
        /// The character's saved clothes, each put on by its own OnEquip, which only dresses the body, as the game puts them on. After the
        /// paint: painting recolours every renderer under the body.
        /// </summary>
        private void PutOnClothes(GameObject body, CharacterSave character)
        {
            if (character.Wearing == null)
            {
                return;
            }
            foreach (string itemId in character.Wearing)
            {
                bool clothing = ItemRegistry.TryGet(itemId, out ItemDefinition definition) && definition.IsWearable;
                GameObject prefab = clothing ? ClothingPrefab(itemId) : null;
                if (prefab == null)
                {
                    continue;
                }
                GameObject worn = Instantiate(prefab, body.transform, false);
                var equippable = worn.GetComponent<IEquippableItem>();
                if (equippable == null)
                {
                    Destroy(worn);
                    continue;
                }
                equippable.OnEquip(body, Inventory.WornData(character, itemId));   // a pressed shirt with its print
                foreach (Collider pickup in worn.GetComponentsInChildren<Collider>(true))
                {
                    pickup.enabled = false;
                }
            }
        }

        private GameObject ClothingPrefab(string itemId)
        {
            foreach (Inventory.ItemPrefabEntry entry in ClothingPrefabs)
            {
                if (entry.ItemId == itemId)
                {
                    return entry.Prefab;
                }
            }
            return null;
        }
    }
}
