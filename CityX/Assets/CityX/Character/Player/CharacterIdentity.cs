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

        /// <summary>The character being played, or null when there is none.</summary>
        public CharacterSave Character => _character;

        private void Awake()
        {
            _vitals = GetComponent<PlayerVitals>();
            _character = PlaySession.Character;
            if (_character == null)
            {
                return;
            }

            Apply(_character);
        }

        /// <summary>
        /// The clothes the character had on are put back on, after Awake has painted the body (painting recolours every renderer under it,
        /// clothes too); from then on every change to what is worn is saved at once.
        /// </summary>
        private void Start()
        {
            if (_character == null)
            {
                return;
            }
            _inventory = GetComponent<Inventory>();
            if (_inventory == null)
            {
                return;
            }
            PutOnClothes(_character);
            _inventory.EquipmentChanged += SaveWhatIsWorn;
        }

        /// <summary>Puts each piece of <paramref name="character"/>'s saved clothing on this body. Public so a test can drive it.</summary>
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
        }

        private void SaveWhatIsWorn()
        {
            if (_character != null && !_inventory.WornClothingIds().SequenceEqual(_character.Wearing))
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

            var hair = GetComponentInChildren<CharacterHair>(includeInactive: true);
            if (hair != null)
            {
                hair.SetStyle(character.HairStyle);
                hair.SetColour(character.HairColour);
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

        /// <summary>Puts a painted atlas on the body.</summary>
        public void ApplyPaint(Texture texture)
        {
            // EVERY RENDERER, NOT JUST THE SKINNED ONES. The skull is a plain
            // mesh hung off the head bone rather than part of the skinned body
            // -- see HeadMesh -- so looking only for SkinnedMeshRenderers left
            // every character with a default-coloured, unpainted head.
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
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
        /// Tints every skinned renderer under this body.
        ///
        /// Writes to `material`, NOT `sharedMaterial`. sharedMaterial is the
        /// asset on disk: setting a colour through it recolours every character
        /// in the scene at once and leaves the change in the repo as a modified
        /// .mat file. `material` instances it for this renderer, which is
        /// exactly the intent.
        /// </summary>
        public void Recolour(Color skin)
        {
            // EVERY RENDERER, NOT JUST THE SKINNED ONES. The skull is a plain
            // mesh hung off the head bone rather than part of the skinned body
            // -- see HeadMesh -- so looking only for SkinnedMeshRenderers left
            // every character with a default-coloured, unpainted head.
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
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
        /// Copies what the body has become back onto the save and stores it.
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
            if (_inventory != null)
            {
                _character.Wearing = _inventory.WornClothingIds();
            }
            PlayerSkills skills = GetComponent<PlayerSkills>();
            if (skills != null)
            {
                skills.Save(_character);
            }
            _character.LastPlayedUnixSeconds =
                System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            _wroteOnce = true;
            return CharacterStore.Update(_character);
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
        }
    }
}
