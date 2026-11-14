using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// What the title screen's rain sounds like -- and whether it is drawn -- for
    /// whichever panel is up.
    ///
    /// The rain is one looping recording that used to play at full volume under every
    /// panel. The menu is standing in it; the character list and the creator are on the
    /// conveyor belt in the sky, with no weather at all (the sound fades out, and the
    /// streaks over the screen go with it). Under cover (quieter, and dull, the way rain
    /// sounds through a wall) was the list's before it moved onto the belt. TitleScreen.Show says which, through
    /// <see cref="MoodFor"/>, and this eases there.
    ///
    /// The rain recording carries the wind with it, so silencing it silences both.
    /// </summary>
    public sealed class TitleAmbience : MonoBehaviour
    {
        public enum Mood
        {
            /// <summary>Out in it: the menu, settings, multiplayer.</summary>
            Outdoors,

            /// <summary>Under cover: quieter and muffled. No panel since the character list moved onto the belt (2026-11-11).</summary>
            Indoors,

            /// <summary>Nothing: no sound, no streaks. The creator.</summary>
            Silent,
        }

        public const float OutdoorsVolume = 1.0f;
        public const float IndoorsVolume = 0.3f;

        /// <summary>Where the rain is cut off outdoors: 22kHz is unfiltered.</summary>
        public const float OutdoorsCutoffHz = 22000.0f;

        /// <summary>Rain through a wall: mostly the low rush, none of the patter.</summary>
        public const float IndoorsCutoffHz = 700.0f;

        /// <summary>How long stepping in or out of cover takes.</summary>
        public const float CoverFadeSeconds = 1.0f;

        /// <summary>How long the sound takes to die away on entering the creator.</summary>
        public const float SilenceFadeSeconds = 1.6f;

        [Tooltip("The looping rain recording.")]
        public AudioSource Sound;

        [Tooltip("Muffles the recording. Added next to it by TitleScreenBuilder.")]
        public AudioLowPassFilter Filter;

        [Tooltip("The streaks drawn over the screen. Hidden with the sound.")]
        public TitleRainLayer Rain;

        private Mood _mood = Mood.Outdoors;
        private float _fromVolume = OutdoorsVolume;
        private float _fromLogCutoff;
        private float _elapsed;
        private float _duration = CoverFadeSeconds;

        public Mood Current => _mood;

        /// <summary>Which mood a panel gets. Pure, so a test can read it without a scene.</summary>
        public static Mood MoodFor(TitleScreen.Panel panel)
        {
            switch (panel)
            {
                case TitleScreen.Panel.CharacterSelect: return Mood.Silent;   // on the belt in the sky with the creator
                case TitleScreen.Panel.Creator: return Mood.Silent;
                default: return Mood.Outdoors;
            }
        }

        public static float VolumeFor(Mood mood)
        {
            switch (mood)
            {
                case Mood.Indoors: return IndoorsVolume;
                case Mood.Silent: return 0.0f;
                default: return OutdoorsVolume;
            }
        }

        public static float CutoffFor(Mood mood)
        {
            return mood == Mood.Indoors ? IndoorsCutoffHz : OutdoorsCutoffHz;
        }

        /// <summary>
        /// Eases towards a mood. The streaks are hidden at once and shown at once (a picture
        /// has nothing to fade that would not look like a glitch); the sound eases.
        /// </summary>
        public void SetMood(Mood mood)
        {
            if (Rain != null)
            {
                Rain.enabled = mood != Mood.Silent;
            }
            if (mood == _mood && _elapsed >= _duration)
            {
                return;
            }

            _mood = mood;
            _fromVolume = Sound != null ? Sound.volume : VolumeFor(mood);
            _fromLogCutoff = Mathf.Log(Filter != null ? Mathf.Max(Filter.cutoffFrequency, 10.0f) : CutoffFor(mood));
            _elapsed = 0.0f;
            _duration = mood == Mood.Silent ? SilenceFadeSeconds : CoverFadeSeconds;
        }

        private void Awake()
        {
            // Begin AT the current mood: a screen that opens on the character list should not
            // start at full volume and duck.
            _fromLogCutoff = Mathf.Log(CutoffFor(_mood));
            _elapsed = _duration;
            Apply(1.0f);
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        /// <summary>Moves the fade on by some seconds.</summary>
        public void Advance(float seconds)
        {
            if (_elapsed >= _duration)
            {
                return;
            }
            _elapsed = Mathf.Min(_elapsed + Mathf.Max(seconds, 0.0f), _duration);
            Apply(Mathf.SmoothStep(0.0f, 1.0f, _elapsed / _duration));
        }

        private void Apply(float k)
        {
            if (Sound != null)
            {
                Sound.volume = Mathf.Lerp(_fromVolume, VolumeFor(_mood), k);
            }
            if (Filter != null)
            {
                // A cutoff is heard in octaves, so it is eased in them: a straight line in hertz
                // would spend nearly the whole fade above where anything is audibly muffled.
                Filter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(_fromLogCutoff, Mathf.Log(CutoffFor(_mood)), k));
            }
        }
    }
}
