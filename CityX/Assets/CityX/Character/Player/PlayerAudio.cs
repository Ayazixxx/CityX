using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Everything the player makes a noise about: footsteps, landing hard, and
    /// the city itself going quiet when you step inside a building.
    ///
    /// Ported from the pile of AudioStreamPlayers player.tscn hangs off the
    /// player -- WalkSoundPlayer, RunSoundPlayer, FallImpactPlayer, DashPlayer,
    /// CityAmbiencePlayer -- and the handful of player.gd functions that drive
    /// them. Gathered into one component here rather than five, because in
    /// Unity a source is a component on the same object and five of them on the
    /// player is five inspector entries that have to be wired individually and
    /// can each be forgotten separately.
    ///
    /// THE AMBIENCE LIVES ON THE PLAYER, which looks odd for a sound belonging
    /// to the city, and is Godot's arrangement for a good reason: it is not
    /// positional. It is the sound of being outdoors, so it follows the ears.
    /// </summary>
    public sealed class PlayerAudio : MonoBehaviour, IIndoorAware
    {
        [Tooltip("walk_sound.mp3 -- loops while walking on the ground.")]
        public AudioClip WalkLoop;

        [Tooltip("run_sound.mp3 -- loops while sprinting on the ground.")]
        public AudioClip RunLoop;

        [Tooltip("fall_impact.mp3 -- a landing hard enough to hurt.")]
        public AudioClip FallImpact;

        [Tooltip("dash_1/2/3.mp3 -- one at random per dash.")]
        public AudioClip[] DashSounds;

        [Tooltip("city_ambience.mp3 -- the street, looping, ducked indoors.")]
        public AudioClip CityAmbience;

        /// <summary>
        /// NOT unity gain. Godot's own -3.1 dB, which is 70% of the original
        /// amplitude -- a real explicit ask to bring the ambience down by 30%,
        /// and 0.7 is what that actually means. Taking 30% off a decibel figure
        /// instead would be a far smaller change, because decibels are already
        /// logarithmic and percentages of them do not subtract. Then 60% quieter again, by the same reading (owner, 2026-11-24: "make the wind
        /// noise ambience that plays when outside 60% quieter"): 0.7 x 0.4.
        /// </summary>
        public const float AmbienceVolume = 0.28f;

        /// <summary>
        /// How long the ambience takes to fade out on stepping inside, and to
        /// come up the very first time it starts at all.
        /// </summary>
        public const float SlowFadeSeconds = 1.5f;

        /// <summary>
        /// Coming back out is noticeably quicker than going in. Godot's, from a
        /// real explicit ask: stepping back onto the street should feel
        /// immediate in a way that stepping indoors should not.
        /// </summary>
        public const float FastFadeSeconds = 0.6f;

        private AudioSource _walk;
        private AudioSource _run;
        private AudioSource _oneShots;
        private AudioSource _ambience;

        /// <summary>
        /// The buildings this body is inside, as a STACK rather than a count.
        ///
        /// Two reasons, both Godot's. Footprints overlap -- walking out of the
        /// outer of two should leave the inner one as the building you are in,
        /// which a count cannot express. And "which building am I in" needs an
        /// answer for anything that wants to act on it.
        /// </summary>
        private readonly List<Transform> _insideOf = new List<Transform>();

        private float _ambienceTarget;
        private float _ambienceRate;
        private bool _somebodyElses;

        public bool IsIndoors => _insideOf.Count > 0;

        /// <summary>The building this body is in, or null out on the street.</summary>
        public Transform CurrentBuilding =>
            _insideOf.Count > 0 ? _insideOf[_insideOf.Count - 1] : null;

        private void Awake()
        {
            _walk = AddLoop(WalkLoop);
            _run = AddLoop(RunLoop);

            _oneShots = gameObject.AddComponent<AudioSource>();
            _oneShots.playOnAwake = false;
            _oneShots.spatialBlend = 0.0f;

            // Enemies and display bodies are built from the player's parts, so they carry this too. Being flat, each
            // would add another whole layer of the city wherever it stood; only a body someone plays as may sound it.
            if (!_somebodyElses && GetComponent<PlayerInputReader>() != null)
            {
                StartAmbience();
            }
        }

        /// <summary>Told by the network which body is this machine's, possibly before this component's Awake: somebody else's copy has the same parts, and must not add its own layer of the city.</summary>
        public void SetOwnBody(bool own)
        {
            _somebodyElses = !own;
            if (own)
            {
                StartAmbience();
            }
            else if (_ambience != null)
            {
                _ambience.Stop();
            }
        }

        private void StartAmbience()
        {
            if (CityAmbience == null)
            {
                return;
            }
            if (_ambience == null)
            {
                _ambience = AddLoop(CityAmbience);
                _ambience.volume = 0.0f;
            }
            if (!_ambience.isPlaying)
            {
                _ambience.Play();
            }
            FadeAmbience(!IsIndoors, SlowFadeSeconds);
        }

        private AudioSource AddLoop(AudioClip clip)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            // Flat, not positional: these are the sounds of being this body,
            // heard from inside it, and 3D falloff on your own footsteps makes
            // them drift as the camera swings round you.
            source.spatialBlend = 0.0f;
            return source;
        }

        /// <summary>Which footstep loop should be running, if any.</summary>
        public enum Step
        {
            None = 0,
            Walk = 1,
            Run = 2,
        }

        /// <summary>
        /// Which loop the last <see cref="UpdateFootsteps"/> asked for.
        ///
        /// SEPARATE FROM PLAYING IT so the rule can be checked without a scene
        /// and without ears -- the same split SkyCycle and DayNightCycle use,
        /// and for the same reason. What can actually go wrong here is the
        /// rule, not the PlayOneShot.
        /// </summary>
        public Step Stepping { get; private set; }

        /// <summary>
        /// Decides which footstep loop belongs to a body in this state.
        ///
        /// GATED ON BEING GROUNDED, not just on moving -- a sprint jump carries
        /// its momentum through the air, and without this the footsteps carry
        /// with it and you hear yourself walking across the sky. That is
        /// Godot's own stated reason for checking is_on_floor rather than the
        /// speed alone.
        /// </summary>
        public static Step StepFor(bool moving, bool grounded, bool sprinting)
        {
            if (!moving || !grounded)
            {
                return Step.None;
            }
            return sprinting ? Step.Run : Step.Walk;
        }

        /// <summary>Runs the matching loop while actually moving on the ground.</summary>
        public void UpdateFootsteps(bool moving, bool grounded, bool sprinting)
        {
            Stepping = StepFor(moving, grounded, sprinting);
            Toggle(_run, Stepping == Step.Run);
            Toggle(_walk, Stepping == Step.Walk);
        }

        private static void Toggle(AudioSource source, bool wanted)
        {
            if (source == null || source.clip == null)
            {
                return;
            }
            if (wanted && !source.isPlaying)
            {
                source.Play();
            }
            else if (!wanted && source.isPlaying)
            {
                source.Stop();
            }
        }

        /// <summary>Both loops off at once, for dying or being put in a cutscene.</summary>
        public void SilenceFootsteps()
        {
            Stepping = Step.None;
            Toggle(_walk, false);
            Toggle(_run, false);
        }

        public void PlayFallImpact()
        {
            if (FallImpact != null)
            {
                _oneShots.PlayOneShot(FallImpact);
            }
        }

        public void PlayDash()
        {
            if (DashSounds == null || DashSounds.Length == 0)
            {
                return;
            }
            AudioClip clip = DashSounds[Random.Range(0, DashSounds.Length)];
            if (clip != null)
            {
                _oneShots.PlayOneShot(clip);
            }
        }

        public void EnterBuilding(Transform volume)
        {
            _insideOf.Add(volume);
            if (_insideOf.Count == 1)
            {
                FadeAmbience(false, SlowFadeSeconds);
            }
        }

        public void ExitBuilding(Transform volume)
        {
            // Removes THIS building rather than just popping the end, so
            // leaving the outer of two overlapping footprints still leaves the
            // inner one current. Searched from the back so a duplicate entry --
            // two triggers for one building -- pops the most recent, which is
            // what makes it a stack.
            int index = _insideOf.LastIndexOf(volume);
            if (index >= 0)
            {
                _insideOf.RemoveAt(index);
            }
            else if (_insideOf.Count > 0)
            {
                // A volume nobody told us we entered -- streamed in around a
                // body already standing in it, say. Still has to come off, or
                // the count sticks above zero and the player never reads as
                // outdoors again.
                _insideOf.RemoveAt(_insideOf.Count - 1);
            }

            if (_insideOf.Count == 0)
            {
                FadeAmbience(true, FastFadeSeconds);
            }
        }

        private void FadeAmbience(bool audible, float seconds)
        {
            if (_ambience == null)
            {
                return;
            }
            _ambienceTarget = audible ? AmbienceVolume : 0.0f;
            _ambienceRate = AmbienceVolume / Mathf.Max(seconds, 0.0001f);
        }

        private void Update()
        {
            if (_ambience == null)
            {
                return;
            }
            _ambience.volume = Mathf.MoveTowards(
                _ambience.volume, _ambienceTarget, _ambienceRate * Time.deltaTime);
        }
    }
}
