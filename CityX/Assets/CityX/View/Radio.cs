using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.View
{
    /// <summary>
    /// A boombox you can switch on. Ported from radio/radio.gd.
    ///
    /// Starts off; press E nearby to power it on and off. It works through its
    /// playlist, advancing when a track ends, and PAUSES rather than restarts
    /// -- switching it off and on again picks the song up where it was, which
    /// is what a radio does and what a naive stop/play would get wrong.
    ///
    /// IT OWNS ITS OWN PROXIMITY, like the building door does. There is no
    /// "nearby interactable" registry anywhere in this project; every
    /// interactable watches for itself. Two of them is a coincidence; a third
    /// would be a reason to build the registry.
    ///
    /// REAL POSITIONAL AUDIO, not a hand-rolled distance fade. The engine's own
    /// inverse-distance rolloff already makes it loud up close and silent past
    /// its range, and a hand-rolled one would be a worse copy of it.
    /// </summary>
    public sealed class Radio : MonoBehaviour
    {
        public const float InteractRange = 2.5f;

        /// <summary>
        /// Audible within a few metres and silent past eighteen. A radio in a
        /// doorway, not something that carries across a city block.
        /// </summary>
        public const float MaxDistance = 18.0f;

        public const float MinDistance = 4.0f;

        [Tooltip("The stations, in order. It works through them and wraps.")]
        public AudioClip[] Playlist;

        private AudioSource _source;
        private int _track;
        private Transform _nearby;

        public bool IsPoweredOn { get; private set; }

        public int Track => _track;

        private void Awake()
        {
            Bind();
        }

        /// <summary>
        /// Builds the AudioSource and the trigger. PUBLIC AND SEPARATE FROM
        /// AWAKE because an EditMode test's AddComponent never runs Awake --
        /// there is no scene load and nothing pumps the player loop -- so a
        /// test has to call this by hand to get an object as real as the one
        /// the game actually plays with.
        /// </summary>
        public void Bind()
        {
            if (_source != null)
            {
                return;
            }

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 1.0f;              // fully positional
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = MinDistance;
            _source.maxDistance = MaxDistance;

            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = InteractRange;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<CharacterController>() != null)
            {
                _nearby = other.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_nearby != null && other.transform.IsChildOf(_nearby.root))
            {
                _nearby = null;
            }
        }

        private void Update()
        {
            if (_nearby != null && Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                Toggle();
            }

            // A track running out advances to the next one, but only while the
            // power is on -- a paused radio has a clip loaded and not playing,
            // which looks exactly the same from here.
            if (IsPoweredOn && _source.clip != null && !_source.isPlaying)
            {
                Next();
            }
        }

        public void Toggle()
        {
            IsPoweredOn = !IsPoweredOn;

            if (!IsPoweredOn)
            {
                // PAUSE, NOT STOP. Stopping loses the position, so switching a
                // radio off and on again would restart the song from the top --
                // which reads as a bug rather than as a power switch.
                _source.Pause();
                return;
            }

            if (_source.clip != null)
            {
                _source.UnPause();
                if (!_source.isPlaying)
                {
                    _source.Play();
                }
                return;
            }
            Play(_track);
        }

        /// <summary>
        /// The playlist wraps rather than stopping at the end -- pulled out as
        /// a pure function because "does it wrap" is exactly the kind of thing
        /// that is easy to get subtly wrong (off-by-one at the last track) and
        /// a test can check without any audio hardware behind it.
        /// </summary>
        public static int NextTrack(int current, int count)
        {
            return count <= 0 ? 0 : (current + 1) % count;
        }

        private void Next()
        {
            if (Playlist == null || Playlist.Length == 0)
            {
                return;
            }
            Play(NextTrack(_track, Playlist.Length));
        }

        private void Play(int track)
        {
            if (Playlist == null || Playlist.Length == 0)
            {
                return;
            }
            _track = Mathf.Clamp(track, 0, Playlist.Length - 1);
            _source.clip = Playlist[_track];
            _source.Play();
        }
    }
}
