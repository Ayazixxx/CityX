using CityX.Core;
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
                _vitals.Vitality = character.Vitality;
                _vitals.Endurance = character.Endurance;
                _vitals.Strength = character.Strength;
                // The stat points move the ceilings, so the bars have to be
                // refilled against the NEW maximums or a character with points
                // in Vitality spawns on a fraction of their health.
                _vitals.Revive();
            }

            Recolour(character.Skin);
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
            foreach (SkinnedMeshRenderer renderer in
                GetComponentsInChildren<SkinnedMeshRenderer>())
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
                _character.Vitality = _vitals.Vitality;
                _character.Endurance = _vitals.Endurance;
                _character.Strength = _vitals.Strength;
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
