using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
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
    /// It reads and writes through <see cref="CharacterStore"/> on every
    /// change rather than holding an unsaved list, so backing out of the screen
    /// can never lose a character and there is no "are you sure you want to
    /// discard" to get wrong.
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

        [Header("Carousel, wired by CharacterSelectBuilder")]
        [Tooltip("Left/Centre/Right pedestal anchors on the 3D stage. A body is instantiated as a child of each one that has a character to show.")]
        public Transform[] PedestalAnchors = new Transform[3];

        [Tooltip("Cloned per pedestal slot, then stripped down to a static display body -- see StripWorldComponents.")]
        public GameObject CarouselBodyPrefab;

        [Tooltip("Loaded once by the builder -- runtime code has no AssetDatabase to load these itself. Same asset CharacterCreatorBuilder wires.")]
        public Mesh[] HairStyleMeshes = new Mesh[0];

        public Material HairMaterialAsset;

        public Text NameLabel;
        public Text DetailLabel;
        public Text StatsLabel;
        public Text IndexLabel;

        [Tooltip("Swatches the creator offers. Chosen by CharacterSelectBuilder.")]
        public Image[] Swatches = new Image[0];

        [Tooltip("The scene the world lives in.")]
        public string WorldScene = "CityMassing";

        private readonly GameObject[] _pedestalBodies = new GameObject[3];
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

        private void OnEnable()
        {
            Refresh();
            Show(Mode.List);
        }

        /// <summary>Re-reads the store and rebuilds the carousel.</summary>
        public void Refresh()
        {
            _characters = CharacterStore.Load();
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _characters.Count - 1));
            UpdateCarousel();
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
        /// Creates the character and goes straight into the world with it --
        /// which is what somebody who just typed a name wants, rather than
        /// being returned to a list to find and click the thing they made.
        /// </summary>
        public CharacterSave ConfirmCreate()
        {
            string name = NameField != null ? NameField.text : string.Empty;
            CharacterSave created = CharacterSave.Create(name, _pendingSkin);
            created.SetPaint(PendingPaint);
            created.HairStyle = string.IsNullOrEmpty(PendingHair) ? "none" : PendingHair;
            PendingPaint = null;
            PendingHair = "none";
            _characters = CharacterStore.Add(created);
            _selectedIndex = _characters.FindIndex(c => c.Id == created.Id);
            UpdateCarousel();
            Play(created, isNew: true);
            return created;
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
                _characters = CharacterStore.Delete(_pendingDeleteId);
                _pendingDeleteId = null;
                _selectedIndex = Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _characters.Count - 1));
                UpdateCarousel();
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

            PlaySession.Begin(character, isNew);

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
        /// Steps the centre pedestal one character to the right, wrapping back
        /// to the start -- a carousel is meant to be cycled, not a list that
        /// stops at its own ends.
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

        /// <summary>
        /// Repopulates the three pedestals from the character either side of
        /// _selectedIndex and refreshes the stat panel. Called on every
        /// selection change rather than animated into place -- with typically
        /// a handful of saves, instantiating up to three bodies is cheap
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

            PopulateSlot(1, _selectedIndex);
            if (_characters.Count > 1)
            {
                int leftIndex = (_selectedIndex - 1 + _characters.Count) % _characters.Count;
                int rightIndex = (_selectedIndex + 1) % _characters.Count;
                PopulateSlot(0, leftIndex);
                PopulateSlot(2, rightIndex);
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
                    ? $"VIT {character.Vitality}   END {character.Endurance}   STR {character.Strength}"
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
            }
        }

        /// <summary>Stands a static preview of _characters[characterIndex] on pedestal slot 0/1/2 (left/centre/right).</summary>
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
                t.gameObject.layer = layer;
            }

            StripWorldComponents(body);
            ApplyCosmetics(body, _characters[characterIndex]);

            _pedestalBodies[slot] = body;
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
            // arms-down stance instead, the same fixed rotations
            // PistolBuilder.PoseArmsDown already uses for its own grip
            // renders.
            foreach (Animator animator in body.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }
            PoseArmsDown(body.transform);
        }

        /// <summary>Same fixed pose PistolBuilder.PoseArmsDown uses -- arms relaxed at the sides rather than the bind pose's T-pose.</summary>
        private static void PoseArmsDown(Transform root)
        {
            SetLocalRotation(root, "arm_l", new Vector3(0.0f, 0.0f, 78.0f));
            SetLocalRotation(root, "forearm_l", new Vector3(0.0f, 0.0f, 12.0f));
            SetLocalRotation(root, "arm_r", new Vector3(0.0f, 0.0f, -78.0f));
            SetLocalRotation(root, "forearm_r", new Vector3(0.0f, 0.0f, -12.0f));
        }

        private static void SetLocalRotation(Transform root, string boneName, Vector3 euler)
        {
            Transform bone = FindDeep(root, boneName);
            if (bone != null)
            {
                bone.localRotation = Quaternion.Euler(euler);
            }
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
                    atlas = paint.Atlas;
                }
            }

            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
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

            Transform head = FindDeep(body.transform, "head");
            if (head != null && HairStyleMeshes != null && HairStyleMeshes.Length > 0)
            {
                var hair = body.AddComponent<CharacterHair>();
                hair.Head = head;
                hair.StyleMeshes = HairStyleMeshes;
                hair.HairMaterial = HairMaterialAsset;
                hair.SetStyle(string.IsNullOrEmpty(character.HairStyle) ? "none" : character.HairStyle);
            }
        }

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
    }
}
