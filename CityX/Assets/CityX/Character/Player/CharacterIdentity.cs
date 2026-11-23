using System.Linq;
using CityX.Core;
using CityX.Items;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Makes the body in the world be the character that was chosen in the
    /// menu, and writes back what they earned.
    ///
    /// THE WHOLE POINT IS THAT IT WORKS WITH NO CHARACTER AT ALL. Pressing play
    /// on the city scene in the editor skips the menu entirely, and that is how
    /// the world actually gets worked on, so a component that needs a chosen
    /// character to function would break the normal way of using the project.
    /// With none, this leaves the default body exactly as it is and saves
    /// nothing.
    /// </summary>
    [RequireComponent(typeof(PlayerVitals))]
    public sealed class CharacterIdentity : MonoBehaviour
    {
        /// <summary>
        /// How often the character is written back, in seconds.
        ///
        /// Periodically rather than only on quit, because a crash or a killed
        /// process is exactly when somebody loses an evening's play, and that
        /// is not a rare way for a game under development to end.
        /// </summary>
        public float SaveIntervalSeconds = 30.0f;

        private PlayerVitals _vitals;
        private Inventory _inventory;
        private CharacterSave _character;
        private float _sinceSave;
        private bool _wroteOnce;
        private bool _heldBack;
        private bool _dressed;

        /// <summary>The character being played, or null when there is none.</summary>
        public CharacterSave Character => _character;

        /// <summary>
        /// The body on this machine that is the character being played: the one whose progress is saved. Null with no character, and in a
        /// session until it is known which body is this machine's.
        /// </summary>
        public static CharacterIdentity Ours { get; private set; }

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            if (!_heldBack)
            {
                Bind();
            }
        }

        private void Bind()
        {
            _character = PlaySession.Character;
            if (_character == null)
            {
                return;
            }
            Ours = this;
            Apply(_character);
        }

        /// <summary>
        /// IN A SESSION EVERY PLAYER'S BODY IS ON EVERY MACHINE, and only one of them is the character chosen on this one. Until a body is
        /// known to be this machine's (NetworkedPlayerIdentity) it is held back: not made that character, not dressed in their clothes,
        /// counting no playtime, saving nothing. Left to itself each body here added its own seconds to the one save: with a friend near,
        /// playtime ran double, the server refused the progress as impossible, and went on refusing it for the rest of the session.
        /// </summary>
        public void StandBy()
        {
            _heldBack = true;
            if (_inventory != null)
            {
                _inventory.EquipmentChanged -= SaveWhatIsWorn;
            }
            _character = null;
            if (Ours == this)
            {
                Ours = null;
            }
        }

        /// <summary>This machine's own body, now that the session has said so: it becomes the chosen character.</summary>
        public void BeOurs()
        {
            if (!_heldBack)
            {
                return;
            }
            _heldBack = false;
            Bind();
            Dress();
        }

        private void Start()
        {
            Dress();
        }

        /// <summary>
        /// The clothes the character had on are put back on, after the body has been painted (painting recolours every renderer under it,
        /// clothes too); from then on every change to what is worn is saved at once.
        /// </summary>
        private void Dress()
        {
            if (_character == null || _dressed)
            {
                return;
            }
            _inventory = GetComponent<Inventory>();
            if (_inventory == null)
            {
                return;
            }
            _dressed = true;
            PutOnBag(_character);
            _inventory.EquipmentChanged += SaveWhatIsWorn;
        }

        /// <summary>
        /// Everything <paramref name="character"/> carried back on this body, as the server last took it (docs/SERVER_SAVING.md, step 3), or, for a
        /// save from before the bag was kept, its clothes and primary weapon (<see cref="PutOnClothes"/>). Public so a test can drive it.
        /// </summary>
        public void PutOnBag(CharacterSave character)
        {
            if (_inventory == null)
            {
                _inventory = GetComponent<Inventory>();
            }
            if (_inventory != null && character != null && character.BagVersion >= CharacterSave.CurrentBagVersion)
            {
                _inventory.FromBag(character.Bag, character.WeaponInHand, gameObject);
            }
            else
            {
                PutOnClothes(character);
            }
        }

        /// <summary>
        /// Puts each piece of <paramref name="character"/>'s saved clothing on this body, and its primary weapon in its hands: all a body that only
        /// shows a character wears (the title's rooftop). Public so a test can drive it.
        /// </summary>
        public void PutOnClothes(CharacterSave character)
        {
            if (_inventory == null)
            {
                _inventory = GetComponent<Inventory>();
            }
            if (_inventory == null || character?.Wearing == null)
            {
                return;
            }
            foreach (string itemId in character.Wearing.ToArray())
            {
                if (ItemRegistry.TryGet(itemId, out ItemDefinition definition) && definition.IsWearable)
                {
                    _inventory.EquipItemId(itemId, gameObject);
                }
            }
            // And the primary weapon back in the hands (CharacterSave.PrimaryWeapon), if nothing is there already.
            if (!string.IsNullOrEmpty(character.PrimaryWeapon) && string.IsNullOrEmpty(_inventory.EquippedItemId(Inventory.PrimarySlot))
                && ItemRegistry.TryGet(character.PrimaryWeapon, out ItemDefinition weapon) && Inventory.IsWeaponSlot(weapon.EquipSlot))
            {
                _inventory.EquipItemId(character.PrimaryWeapon, gameObject);
            }
        }

        private void SaveWhatIsWorn()
        {
            if (_character != null && (!_inventory.WornClothingIds().SequenceEqual(_character.Wearing)
                || _inventory.EquippedItemId(Inventory.PrimarySlot) != (_character.PrimaryWeapon ?? "")))
            {
                Write();
            }
        }

        /// <summary>
        /// Pushes a save's appearance and earned stats onto this body. Public so
        /// a test can drive it without a menu having run.
        /// </summary>
        public void Apply(CharacterSave character)
        {
            if (character == null)
            {
                return;
            }
            _character = character;

            if (_vitals == null)
            {
                _vitals = GetComponent<PlayerVitals>();
            }
            if (_vitals != null)
            {
                _vitals.Level = character.Level;
                _vitals.Experience = character.Experience;
                _vitals.StatPoints = character.StatPoints;
                CharacterStats.Load(_vitals, character);
                _vitals.SkillPoints = character.SkillPoints;
                // The stat points move the ceilings, so the bars have to be
                // refilled against the NEW maximums or a character with points
                // in Vitality spawns on a fraction of their health.
                _vitals.Revive();
            }

            // What they have learnt and what is on their bar (a save from
            // before the skill tree keeps everything it could already use).
            PlayerSkills.On(gameObject).Load(character);

            ApplyLook(character);
        }

        /// <summary>
        /// A save's appearance alone put on this body: hair, and paint or skin colour. What a redraw changes. Nothing the character has
        /// earned is touched and nothing is refilled.
        /// </summary>
        public void ApplyLook(CharacterSave character)
        {
            if (character == null)
            {
                return;
            }
            var hair = GetComponentInChildren<CharacterHair>(includeInactive: true);
            if (hair != null)
            {
                hair.SetStyle(character.HairStyle);
                hair.SetColour(character.HairColour);
                hair.SetBeard(string.IsNullOrEmpty(character.Beard) ? CharacterHair.None : character.Beard);
            }

            // PAINT WINS OVER THE FLAT COLOUR when there is any, because paint
            // is authored on top of a base and replacing it with a tint would
            // throw the player's work away. A character with none -- typed a
            // name and pressed enter -- still gets their chosen skin.
            byte[] painted = character.GetPaint();
            if (painted != null)
            {
                var paint = new BodyPaint(character.Skin);
                if (paint.Decode(painted))
                {
                    paint.ShrinkForWearing();
                    ApplyPaint(paint.Atlas);
                    LookApplied?.Invoke(this);
                    return;
                }
                Debug.LogWarning($"CityX: {character.Name}'s paint would not load -- falling " +
                                 "back to their skin colour.");
            }

            Recolour(character.Skin);
            LookApplied?.Invoke(this);
        }

        /// <summary>
        /// Raised when a whole appearance (hair, colour, paint) has just been put on a body. Whatever
        /// keeps a picture of the character -- the HUD portrait -- retakes it here, since it would
        /// otherwise show the character as they were before the change.
        /// </summary>
        public static event System.Action<CharacterIdentity> LookApplied;

        /// <summary>Puts a painted atlas on the body's own surface (the skin and the skull, <see cref="BodySurface"/>).</summary>
        public void ApplyPaint(Texture texture)
        {
            foreach (Renderer renderer in BodySurface.Renderers(transform))
            {
                Material instance = renderer.material;
                if (instance == null)
                {
                    continue;
                }
                if (instance.HasProperty("_BaseMap"))
                {
                    instance.SetTexture("_BaseMap", texture);
                }
                instance.mainTexture = texture;
                // White underneath, or the paint comes out tinted by whatever
                // skin colour the material was left on.
                if (instance.HasProperty("_BaseColor"))
                {
                    instance.SetColor("_BaseColor", Color.white);
                }
                if (instance.HasProperty("_Color"))
                {
                    instance.SetColor("_Color", Color.white);
                }
            }
        }

        /// <summary>
        /// Tints the body's own surface (the skin and the skull, <see cref="BodySurface"/>).
        ///
        /// Writes to `material`, NOT `sharedMaterial`. sharedMaterial is the
        /// asset on disk: setting a colour through it recolours every character
        /// in the scene at once and leaves the change in the repo as a modified
        /// .mat file. `material` instances it for this renderer, which is
        /// exactly the intent.
        /// </summary>
        public void Recolour(Color skin)
        {
            foreach (Renderer renderer in BodySurface.Renderers(transform))
            {
                Material instance = renderer.material;
                if (instance == null)
                {
                    continue;
                }
                // URP's Lit shader reads _BaseColor; the built-in one reads
                // _Color. Setting whichever exists means this does not silently
                // do nothing if the pipeline changes underneath it.
                if (instance.HasProperty("_BaseColor"))
                {
                    instance.SetColor("_BaseColor", skin);
                }
                if (instance.HasProperty("_Color"))
                {
                    instance.SetColor("_Color", skin);
                }
            }
        }

        private void Update()
        {
            if (_character == null)
            {
                return;
            }

            _character.PlaytimeSeconds += Time.deltaTime;

            _sinceSave += Time.deltaTime;
            if (_sinceSave >= SaveIntervalSeconds)
            {
                _sinceSave = 0.0f;
                Write();
            }
        }

        /// <summary>
        /// Copies what the body has become back onto the save and stores it on the server played on.
        /// </summary>
        public bool Write()
        {
            if (_character == null)
            {
                return false;
            }

            if (_vitals != null)
            {
                _character.Level = _vitals.Level;
                _character.Experience = _vitals.Experience;
                _character.StatPoints = _vitals.StatPoints;
                CharacterStats.Save(_vitals, _character);
                _character.SkillPoints = _vitals.SkillPoints;
            }
            // Only once the saved bag is on the body (Dress): before, the body's empty hands would be written over what the character carries.
            if (_inventory != null && _dressed)
            {
                _character.Wearing = _inventory.WornClothingIds();
                _character.PrimaryWeapon = _inventory.EquippedItemId(Inventory.PrimarySlot);
                _character.Bag = _inventory.ToBag();
                _character.BagVersion = CharacterSave.CurrentBagVersion;
                _character.WeaponInHand = _inventory.ActiveWeaponSlot;
            }
            PlayerSkills skills = GetComponent<PlayerSkills>();
            if (skills != null)
            {
                skills.Save(_character);
            }
            _character.LastPlayedUnixSeconds =
                System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            _wroteOnce = true;
            CharacterSources.Current.Save(_character, PlaySession.AdminEdited);
            PlaySession.AdminEdited = false;
            return true;
        }

        // Both, and deliberately: OnApplicationQuit does not run when the
        // editor leaves play mode by some routes, and OnDestroy does not run
        // when a standalone build is closed by the window's X on every
        // platform. Writing twice is harmless -- it is the same record.
        private void OnApplicationQuit()
        {
            Write();
        }

        private void OnDestroy()
        {
            if (_character != null && !_wroteOnce)
            {
                Write();
            }
            if (Ours == this)
            {
                Ours = null;
            }
        }
    }
}
