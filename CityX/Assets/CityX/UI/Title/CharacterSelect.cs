using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// Choosing, making and deleting characters, and entering the world.
    ///
    /// ONE COMPONENT FOR ALL THREE, deliberately. They are the same screen with
    /// different rows on it: making a character is what the empty list offers,
    /// and deleting is a mode the list goes into. Splitting them would mean
    /// three scripts passing the same list between each other and agreeing on
    /// who owns it.
    ///
    /// It asks the server played on (<see cref="CharacterSources.Current"/>:
    /// this PC's own, or the one joined) on every change rather than holding an
    /// unsaved list, so backing out of the screen can never lose a character
    /// and there is no "are you sure you want to discard" to get wrong.
    /// </summary>
    public sealed class CharacterSelect : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>The list, with a row per character.</summary>
            List,

            /// <summary>Typing a name for a new one.</summary>
            Creating,

            /// <summary>Confirming a delete.</summary>
            Confirming,
        }

        [Header("Wired by CharacterSelectBuilder")]
        public GameObject ListPanel;

        public GameObject CreatePanel;
        public GameObject ConfirmPanel;

        public InputField NameField;
        public Text ConfirmText;
        public Text EmptyNotice;

        /// <summary>
        /// How many pedestals stand in the row (owner, 2026-10-26: "make it display 5 character ... the back 2 on the right and left are dark just
        /// dark and shadowy figures"), and which of them is the centre one.
        /// </summary>
        public const int SlotCount = 5;

        public const int CentreSlot = 2;

        [Header("Carousel, wired by CharacterSelectBuilder")]
        [Tooltip("The pedestal anchors on the 3D stage, left to right as the camera sees them. A body is instantiated as a child of each one that has a character to show.")]
        public Transform[] PedestalAnchors = new Transform[SlotCount];

        [Tooltip("What the two furthest pedestals' bodies are drawn in: a dark, shadowy figure.")]
        public Material SilhouetteMaterial;

        [Tooltip("Cloned per pedestal slot, then stripped down to a static display body -- see StripWorldComponents.")]
        public GameObject CarouselBodyPrefab;

        [Tooltip("Loaded once by the builder -- runtime code has no AssetDatabase to load these itself. Same asset CharacterCreatorBuilder wires.")]
        public Mesh[] HairStyleMeshes = new Mesh[0];

        public Material HairMaterialAsset;

        [Tooltip("The clothes a pedestal body can wear, by item id. Loaded by the builder, as the hair is.")]
        public Inventory.ItemPrefabEntry[] ClothingPrefabs = new Inventory.ItemPrefabEntry[0];

        public Text NameLabel;
        public Text DetailLabel;
        public Text StatsLabel;
        public Text IndexLabel;

        [Tooltip("Swatches the creator offers. Chosen by CharacterSelectBuilder.")]
        public Image[] Swatches = new Image[0];

        [Tooltip("The scene the world lives in.")]
        public string WorldScene = "CityMassing";

        private readonly GameObject[] _pedestalBodies = new GameObject[SlotCount];
        private readonly int[] _shownAt = { -1, -1, -1, -1, -1 };
        private List<CharacterSave> _characters = new List<CharacterSave>();
        private Mode _mode = Mode.List;
        private int _selectedIndex;
        private string _pendingDeleteId;
        private Color _pendingSkin = CharacterSave.DefaultSkin;

        public Mode Showing => _mode;

        /// <summary>Which loaded character the centre pedestal is showing. Public so a test can read it.</summary>
        public int SelectedIndex => _selectedIndex;

        public IReadOnlyList<CharacterSave> Characters => _characters;

        /// <summary>The character the centre pedestal is on, or null with nothing saved yet.</summary>
        public CharacterSave Selected =>
            _selectedIndex >= 0 && _selectedIndex < _characters.Count ? _characters[_selectedIndex] : null;

        public Color PendingSkin => _pendingSkin;

        /// <summary>
        /// Paint handed over by the creator, waiting for a name.
        ///
        /// Held here rather than in the creator because the creator is torn
        /// down when it closes, and the paint has to outlive it long enough to
        /// be written into the save.
        /// </summary>
        public byte[] PendingPaint;

        /// <summary>The hairstyle chosen in the creator, for the same reason.</summary>
        public string PendingHair = "none";

        /// <summary>The hair colour chosen in the creator, held for the same reason.</summary>
        public Color PendingHairColour = CharacterSave.DefaultHairColour;

        /// <summary>
        /// Set by whoever shows a NEW character off before the world loads (the conveyor belt it was made on carries it away, BeltIntro):
        /// handed the loading of the world, to call when it is done. Null: the world loads at once.
        /// </summary>
        public System.Action<System.Action> Departure;

        private void OnEnable()
        {
            Refresh();
            Show(Mode.List);
        }

        /// <summary>Asks the server for the player's characters again and rebuilds the carousel.</summary>
        public void Refresh()
        {
            CharacterSources.Current.List(characters =>
            {
                if (this == null)
                {
                    return;   // a server's late answer, after the screen has gone
                }
                _characters = characters;
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _characters.Count - 1));
                UpdateCarousel();
            });
        }

        public void Show(Mode mode)
        {
            _mode = mode;
            if (ListPanel != null) ListPanel.SetActive(mode == Mode.List);
            if (CreatePanel != null) CreatePanel.SetActive(mode == Mode.Creating);
            if (ConfirmPanel != null) ConfirmPanel.SetActive(mode == Mode.Confirming);
        }

        /// <summary>
        /// Steps back one sub-mode, to the list.
        ///
        /// It does NOT hide itself when already at the list. Which panel is on
        /// screen is TitleScreen's to decide -- two things setting the same
        /// GameObject active is exactly the bug TitleScreen's enum exists to
        /// prevent.
        /// </summary>
        public void Back()
        {
            if (_mode != Mode.List)
            {
                Show(Mode.List);
            }
        }

        // --- making one ---

        public void BeginCreate()
        {
            _pendingSkin = CharacterSave.DefaultSkin;
            if (NameField != null)
            {
                NameField.text = string.Empty;
                NameField.ActivateInputField();
            }
            HighlightSwatches();
            Show(Mode.Creating);
        }

        /// <summary>Picks the nth offered skin colour.</summary>
        public void ChooseSkin(int index)
        {
            if (Swatches == null || index < 0 || index >= Swatches.Length
                || Swatches[index] == null)
            {
                return;
            }
            _pendingSkin = Swatches[index].color;
            HighlightSwatches();
        }

        private void HighlightSwatches()
        {
            if (Swatches == null)
            {
                return;
            }
            foreach (Image swatch in Swatches)
            {
                if (swatch == null)
                {
                    continue;
                }
                var outline = swatch.GetComponent<Outline>();
                if (outline != null)
                {
                    // Approximately, because the colour makes a round trip
                    // through a serialized Image and comes back very slightly
                    // different -- an exact comparison silently never matches.
                    outline.enabled = Approximately(swatch.color, _pendingSkin);
                }
            }
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.004f
                   && Mathf.Abs(a.g - b.g) < 0.004f
                   && Mathf.Abs(a.b - b.b) < 0.004f;
        }

        /// <summary>
        /// Has the server make the character and goes straight into the world with it --
        /// which is what somebody who just typed a name wants, rather than
        /// being returned to a list to find and click the thing they made.
        /// Returns the server's new character when it answered at once (this PC's own server), else null.
        /// </summary>
        public CharacterSave ConfirmCreate()
        {
            string name = NameField != null ? NameField.text : string.Empty;
            CharacterSave requested = CharacterSave.Create(name, _pendingSkin);
            requested.SetPaint(PendingPaint);
            requested.HairStyle = string.IsNullOrEmpty(PendingHair) ? "none" : PendingHair;
            requested.HairColour = PendingHairColour;
            PendingPaint = null;
            PendingHair = "none";
            PendingHairColour = CharacterSave.DefaultHairColour;
            CharacterSave answered = null;
            CharacterSources.Current.Create(requested, made =>
            {
                answered = made;
                if (this == null)
                {
                    return;
                }
                if (made == null)
                {
                    Refuse($"The server would not make that character (at most {CharacterService.MaxCharactersPerPlayer} each).");
                    return;
                }
                _characters.Insert(0, made);
                _selectedIndex = 0;
                UpdateCarousel();
                Play(made, isNew: true);
            });
            return answered;
        }

        private void Refuse(string why)
        {
            Debug.LogWarning($"CityX: {why}");
            if (DetailLabel != null)
            {
                DetailLabel.text = why;
            }
            Show(Mode.List);
        }

        // --- deleting one ---

        public void BeginDelete(string id)
        {
            _pendingDeleteId = id;
            CharacterSave character = _characters.Find(c => c.Id == id);
            if (ConfirmText != null)
            {
                ConfirmText.text = character == null
                    ? "Delete this character?"
                    : $"Delete {character.Name}, level {character.Level}?\nThis cannot be undone.";
            }
            Show(Mode.Confirming);
        }

        /// <summary>Deletes whichever character the centre pedestal is currently showing.</summary>
        public void BeginDeleteSelected()
        {
            CharacterSave character = Selected;
            if (character != null)
            {
                BeginDelete(character.Id);
            }
        }

        public void ConfirmDelete()
        {
            if (!string.IsNullOrEmpty(_pendingDeleteId))
            {
                CharacterSources.Current.Delete(_pendingDeleteId, _ =>
                {
                    if (this != null)
                    {
                        Refresh();
                    }
                });
                _pendingDeleteId = null;
            }
            Show(Mode.List);
        }

        public void CancelDelete()
        {
            _pendingDeleteId = null;
            Show(Mode.List);
        }

        // --- playing one ---

        public void Play(CharacterSave character, bool isNew)
        {
            if (character == null)
            {
                return;
            }

            // The server hands back the character as IT keeps it (and whether this player is an admin): that copy is the one played.
            CharacterSources.Current.Play(character.Id, (served, isAdmin) =>
            {
                if (this == null)
                {
                    return;
                }
                if (served == null)
                {
                    Refuse($"The server would not let {character.Name} be played.");
                    return;
                }
                PlaySession.Begin(served, isNew, isAdmin);
                if (isNew && Departure != null)
                {
                    Departure(LoadWorld);
                }
                else
                {
                    LoadWorld();
                }
            });
        }

        private void LoadWorld()
        {
            // Guarded because the scene has to be in the build settings for
            // this to work, and a missing entry throws an exception that reads
            // as the button being broken rather than as a project setting.
            if (Application.CanStreamedLevelBeLoaded(WorldScene))
            {
                SceneManager.LoadScene(WorldScene);
            }
            else
            {
                Debug.LogError($"CityX: the scene '{WorldScene}' is not in the build settings, " +
                               "so there is nothing to load. Add it under File > Build Profiles.");
            }
        }

        /// <summary>Plays whichever character the centre pedestal is currently showing.</summary>
        public void PlaySelected()
        {
            Play(Selected, isNew: false);
        }

        // --- the carousel ---

        /// <summary>
        /// Steps to the next character, wrapping back to the start -- a carousel is meant to be cycled, not a list that stops at its own ends.
        /// The next character stands on the RIGHT, so the right arrow brings it in from the right and the row slides left (owner, 2026-10-26:
        /// "make it so the direction it scrolls through the characters when you press the buttons on the right and left are inverted": it came
        /// in from the left, the stage's anchors having been laid out mirrored to the camera looking back at them).
        /// </summary>
        public void Next()
        {
            if (_characters.Count == 0)
            {
                return;
            }
            _selectedIndex = (_selectedIndex + 1) % _characters.Count;
            UpdateCarousel();
        }

        public void Previous()
        {
            if (_characters.Count == 0)
            {
                return;
            }
            _selectedIndex = (_selectedIndex - 1 + _characters.Count) % _characters.Count;
            UpdateCarousel();
        }

        /// <summary>Which character a pedestal (0 to SlotCount - 1, left to right) is showing, or -1 for none. Public so a test can read it.</summary>
        public int ShownAt(int slot) => slot >= 0 && slot < SlotCount ? _shownAt[slot] : -1;

        /// <summary>
        /// The character a pedestal shows, given how many there are, or -1 for none: the chosen one in the centre, the next ones to its right and
        /// the previous ones to its left (see Next). The pair beside the centre need two characters (with two, both show the other one); the
        /// furthest pair five, so nobody stands on the row twice.
        /// </summary>
        public static int CharacterFor(int slot, int selected, int count)
        {
            int offset = slot - CentreSlot;
            int reach = Mathf.Abs(offset);
            if (count == 0 || (reach == 1 && count < 2) || (reach == 2 && count < SlotCount))
            {
                return -1;
            }
            return ((selected + offset) % count + count) % count;
        }

        /// <summary>
        /// Repopulates the pedestals round _selectedIndex and refreshes the stat panel. Called on every
        /// selection change rather than animated into place -- with typically
        /// a handful of saves, instantiating up to five bodies is cheap
        /// enough to just redo outright, and it keeps this in step with
        /// Refresh() rebuilding the whole list the same way a delete or a
        /// new character already did before the carousel existed.
        /// </summary>
        private void UpdateCarousel()
        {
            DestroyPedestalBodies();

            if (EmptyNotice != null)
            {
                EmptyNotice.gameObject.SetActive(_characters.Count == 0);
            }

            if (_characters.Count == 0)
            {
                SetStatPanel(null);
                return;
            }

            for (int slot = 0; slot < SlotCount; slot++)
            {
                int character = CharacterFor(slot, _selectedIndex, _characters.Count);
                if (character >= 0)
                {
                    PopulateSlot(slot, character);
                }
            }

            SetStatPanel(_characters[_selectedIndex]);
        }

        private void SetStatPanel(CharacterSave character)
        {
            if (NameLabel != null)
            {
                NameLabel.text = character != null ? character.Name : string.Empty;
            }
            if (DetailLabel != null)
            {
                DetailLabel.text = character != null
                    ? $"Level {character.Level}   {character.PlaytimeText()}"
                    : string.Empty;
            }
            if (StatsLabel != null)
            {
                StatsLabel.text = character != null
                    ? CityX.Character.CharacterStats.Summary(character)
                    : string.Empty;
            }
            if (IndexLabel != null)
            {
                IndexLabel.text = character != null
                    ? $"{_selectedIndex + 1} / {_characters.Count}"
                    : string.Empty;
            }
        }

        private void DestroyPedestalBodies()
        {
            for (int i = 0; i < _pedestalBodies.Length; i++)
            {
                if (_pedestalBodies[i] != null)
                {
                    Destroy(_pedestalBodies[i]);
                    _pedestalBodies[i] = null;
                }
                _shownAt[i] = -1;
            }
        }

        /// <summary>Stands a static preview of _characters[characterIndex] on a pedestal slot (0 to SlotCount - 1, left to right).</summary>
        private void PopulateSlot(int slot, int characterIndex)
        {
            if (PedestalAnchors == null || slot >= PedestalAnchors.Length
                || PedestalAnchors[slot] == null || CarouselBodyPrefab == null)
            {
                return;
            }

            GameObject body = Instantiate(CarouselBodyPrefab, PedestalAnchors[slot]);
            body.name = $"Pedestal_{slot}";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;

            StripWorldComponents(body);
            ApplyCosmetics(body, _characters[characterIndex]);
            PutOnClothes(body, _characters[characterIndex]);

            // ONTO THE STAGE'S OWN LAYER, NOT WHATEVER Player.prefab SHIPS
            // ON. The anchors were put on the carousel's dedicated layer at
            // build time (CharacterSelectBuilder.BuildCarouselStage), but a
            // body cloned from the prefab at RUNTIME still carries the
            // prefab's own default layer -- invisible to a camera whose
            // cullingMask only shows the stage's layer, confirmed directly:
            // a render showed the pedestals lit and in frame with no body
            // on any of them, and every stripped-down component came back
            // present and active except the layer never having been
            // touched.
            int layer = PedestalAnchors[slot].gameObject.layer;
            foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;   // the clothes' own renderers too, so after they are put on
            }

            if (Mathf.Abs(slot - CentreSlot) == 2)
            {
                InShadow(body);
            }

            _pedestalBodies[slot] = body;
            _shownAt[slot] = characterIndex;
        }

        /// <summary>A body in the back row: every part of it (skin, hair, clothes) in the one dark silhouette material, its own colours cleared.</summary>
        private void InShadow(GameObject body)
        {
            if (SilhouetteMaterial == null)
            {
                return;
            }
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                var shadow = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < shadow.Length; i++)
                {
                    shadow[i] = SilhouetteMaterial;
                }
                renderer.sharedMaterials = shadow;
                renderer.SetPropertyBlock(null);
                for (int i = 0; i < shadow.Length; i++)
                {
                    renderer.SetPropertyBlock(null, i);
                }
            }
        }

        /// <summary>
        /// Leaves a still, undirected display body -- everything that would
        /// make it walk, swing, ragdoll or physically collide with the
        /// pedestal has to come off. The same order and the same list
        /// CharacterCreatorBuilder.BuildStage already uses for its own
        /// preview body, and for the same reason: PlayerLocomotion,
        /// PlayerClimbing and GrappleSwing all require the CharacterController
        /// and PlayerAnimationDriver requires MeleeAttacker, so the thing
        /// depended on has to go second or Unity silently refuses to remove
        /// the one that needs it.
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

            // THE REPORTED "WIND KEEPS PLAYING ON THE SELECT SCREEN AND
            // RESETS EVERY TIME YOU PICK A NEW CHARACTER" BUG. PlayerAudio's
            // own Awake() runs synchronously during Instantiate() --  before
            // this method ever gets a chance to strip it -- and immediately
            // AddComponent<AudioSource>()s and Play()s the looping city
            // ambience right there, fading it in. Destroying PlayerAudio
            // alone stops its per-frame fade logic but does nothing to the
            // AudioSource it already created and started, which keeps
            // looping on its own with nothing left managing it. Every
            // UpdateCarousel() rebuilds the pedestal bodies from scratch, so
            // a fresh one of these starts on top of the last every time the
            // selection changes -- the reported "resets." A pedestal is a
            // silent display prop; it has no legitimate reason to carry any
            // audio at all.
            Strip<PlayerAudio>(body);
            Strip<AudioSource>(body);

            // Held still rather than animated -- there is no locomotion
            // driving it, so an enabled Animator would just hold whichever
            // frame its default state happened to start on. UNLIKE the
            // creator's own body, this is not left at the bare bind pose:
            // that is a T-pose, arms straight out, which reads as broken on
            // a screen whose whole point is showing off the character
            // rather than exposing it for painting. Posed to a natural
            // arms-down stance instead, the same fixed rotations the
            // weapon builders' grip renders use.
            foreach (Animator animator in body.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }
            StillPose.ArmsDown(body.transform);
        }

        /// <summary>
        /// The character's saved clothes (owner, 2026-10-20: "make sure they have the items equipped on the character select screen (not
        /// weapons yet just wearables)"), put on the display body by each one's own OnEquip, which only dresses the body, the way the game
        /// puts them on. After the paint: painting recolours every renderer under the body.
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
                equippable.OnEquip(body, ItemInstanceData.None);
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

        private static void Strip<T>(GameObject root) where T : Component
        {
            foreach (T found in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                Destroy(found);
            }
        }

        /// <summary>
        /// Paints the body the way CharacterIdentity.Apply paints the live
        /// in-world one -- deliberately NOT by adding a CharacterIdentity
        /// component and calling it, though. That component also tracks
        /// playtime and autosaves back to the store on its own timer, and a
        /// character sitting on a pedestal being idly looked at is not being
        /// played -- attaching the real thing here would silently rack up
        /// playtime and touch LastPlayedUnixSeconds just from browsing the
        /// menu. This reimplements only the cosmetic half.
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
            }
        }
    }
}
