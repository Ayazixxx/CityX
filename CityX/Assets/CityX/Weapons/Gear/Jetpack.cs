using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE JETPACK (docs/FLIGHT.md): flight gear (<see cref="FlightGear"/>) with a flame and an engine. Space in the air takes off; from then
    /// on it hovers with nothing held, climbs on Space (faster on Shift with nothing else held), sinks on Ctrl, flies about on WASD with
    /// inertia and follows the camera up and down going forward, until it lands (2026-10-28: a hover and a Ctrl descent replaced "let go to
    /// fall", with the wings; owner: "the jetpack movement could use some work as well"). Infinite fuel, on purpose (the owner's own ask).
    /// The flame burns low in a hover and full in a climb; the engine loops while flying; a wind loop fades in on a boost and quickens with
    /// speed. The body leans: upright in a hover, and flying forward along the camera's pitch from standing (looking up) through horizontal
    /// (level: Superman) to a headstand (looking down), banking into a strafe.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Jetpack : FlightGear
    {
        public override string ItemId => ItemRegistry.Jetpack;

        /// <summary>Space: the climb, metres a second; Shift with nothing else held, twice it.</summary>
        public const float ThrustSpeed = 11.0f;

        public const float BoostThrustSpeed = ThrustSpeed * 2.0f;

        /// <summary>Ctrl: the descent.</summary>
        public const float DescendSpeed = 12.0f;

        /// <summary>The cruise is the body's own sprint; Shift flies this many times faster (the owner's "twice as fast").</summary>
        public const float DiveSpeedMultiplier = 2.0f;

        /// <summary>The cruise when the body has no PlayerLocomotion to ask (a flying NPC, a bare test).</summary>
        public const float DefaultCruiseSpeed = 7.5f;

        /// <summary>How quickly the horizontal speed is reached and shed, metres a second each second: a jetpack is snappier than wings.</summary>
        public const float Acceleration = 30.0f;

        /// <summary>The flame in a hover and in a descent, as a share of a climb's.</summary>
        public const float HoverFlameShare = 0.4f;

        public const float DescendFlameShare = 0.25f;

        public const float HoverEngineVolume = 0.5f;

        public const float JetpackMinPitchDegrees = MinPitchDegrees;

        public const float JetpackMaxPitchDegrees = MaxPitchDegrees;

        /// <summary>Flush against the spine between the shoulder blades: past the torso's own radius (-0.20 sank the tanks into the back).</summary>
        public static readonly Vector3 BackPosition = new Vector3(0.0f, 0.02f, -0.34f);

        public static readonly Vector3 BackRotationEuler = Vector3.zero;

        /// <summary>How far the body banks into a strafe, degrees: slight; further boosting; a decisive plane's roll in a boosted forward dive.</summary>
        public const float TiltMaxDegrees = 12.0f;

        public const float BoostTiltMaxDegrees = 20.0f;

        public const float SupermanRollMaxDegrees = 35.0f;

        /// <summary>
        /// Flying forward the body's pitch is the camera's plus this: camera -90 (straight up) stands it upright, 0 lays it horizontal
        /// (Superman), +90 puts it head down. A hover stays upright wherever the camera looks (the owner's sixth correction of this).
        /// </summary>
        public const float SupermanPitchOffsetDegrees = 90.0f;

        /// <summary>Seconds for the wind loop to fade in and out across its whole range (a rapid tap of Shift never sounds slower than a hold).</summary>
        public const float WindFadeSeconds = 0.5f;

        [Tooltip("Set by JetpackBuilder -- burns while flying, low in a hover.")]
        public ParticleSystem FlameEffect;

        [Tooltip("Set by JetpackBuilder (jetpack_thrust_loop.wav) -- loops while flying.")]
        public AudioSource ThrustAudio;

        [Tooltip("Set by JetpackBuilder (jetpack_wind_loop.wav) -- fades in on a boost, quickens with speed.")]
        public AudioSource WindAudio;

        private ParticleSystem[] _flames;
        private float[] _flameRates;

        protected override Vector3 MountPosition => BackPosition;

        protected override Vector3 MountRotationEuler => BackRotationEuler;

        protected override FlightTuning Tuning
        {
            get
            {
                float cruise = Locomotion != null ? Locomotion.SprintSpeed : DefaultCruiseSpeed;
                return new FlightTuning(ThrustSpeed, BoostThrustSpeed, DescendSpeed, cruise, cruise * DiveSpeedMultiplier, Acceleration);
            }
        }

        protected override Vector2 LeanTarget(in FlightInput input)
        {
            bool amplify = input.BoostHeld && input.Move.sqrMagnitude > 0.0001f;
            return TiltTarget(input.Move, input.CameraPitchDegrees, amplify, input.Move.y > 0.0f);
        }

        /// <summary>
        /// Where the lean is headed, degrees: x pitch, y roll. Flying forward the pitch follows the camera through the standing / horizontal /
        /// headstand sweep; a hover is upright. The roll banks into the strafe (a right strafe, positive x, needs a negative angle to drop the
        /// right side), as far as a plain, a boosted, or a boosted forward flight allows. Pure and static for the tests.
        /// </summary>
        public static Vector2 TiltTarget(Vector2 moveInput, float cameraPitchDegrees, bool boosting = false, bool flyingForward = false)
        {
            float maxDegrees = (boosting && flyingForward) ? SupermanRollMaxDegrees
                : boosting ? BoostTiltMaxDegrees : TiltMaxDegrees;
            float roll = -moveInput.x * maxDegrees;
            float pitch = flyingForward ? cameraPitchDegrees + SupermanPitchOffsetDegrees : 0.0f;
            return new Vector2(pitch, roll);
        }

        protected override void OnWorn(GameObject player)
        {
            ShowFlame(0.0f);
        }

        protected override void OnMounted(GameObject body)
        {
            ShowFlame(0.0f);
        }

        public override void SetCosmeticActive(bool active)
        {
            ShowFlame(active ? 1.0f : 0.0f);
        }

        protected override void OnFlightTick(in FlightInput input, float deltaTime)
        {
            FlightModel model = Model;
            bool flying = model.Flying;
            float flame = !flying ? 0.0f : model.Climbing || model.Boosting ? 1.0f : model.Descending ? DescendFlameShare : HoverFlameShare;
            ShowFlame(flame);

            if (ThrustAudio != null)
            {
                if (flying && !ThrustAudio.isPlaying)
                {
                    ThrustAudio.Play();
                }
                else if (!flying && ThrustAudio.isPlaying)
                {
                    ThrustAudio.Stop();
                }
                ThrustAudio.volume = model.Climbing || model.Boosting ? 1.0f : HoverEngineVolume;
            }

            // The wind: Shift only (plain flight keeps the engine alone), from the top each time (Play, never UnPause), faded by volume.
            bool boosting = flying && model.Boosting;
            if (WindAudio != null)
            {
                float fadeStep = deltaTime / WindFadeSeconds;
                if (boosting)
                {
                    if (!WindAudio.isPlaying)
                    {
                        WindAudio.volume = 0.0f;
                        WindAudio.Play();
                    }
                    WindAudio.volume = Mathf.MoveTowards(WindAudio.volume, 1.0f, fadeStep);
                }
                else if (WindAudio.isPlaying)
                {
                    WindAudio.volume = Mathf.MoveTowards(WindAudio.volume, 0.0f, fadeStep);
                    if (WindAudio.volume <= 0.0f)
                    {
                        WindAudio.Stop();
                    }
                }
            }
        }

        protected override void OnFlightFeel(float speedFraction)
        {
            if (WindAudio != null)
            {
                WindAudio.pitch = FlightFeel.WindPitch(speedFraction);
            }
        }

        /// <summary>A crash or a ragdoll: the flame and the engine stop at once, however Space is held.</summary>
        protected override void OnEffectsStopped()
        {
            ShowFlame(0.0f);
            if (ThrustAudio != null && ThrustAudio.isPlaying)
            {
                ThrustAudio.Stop();
            }
            if (WindAudio != null && WindAudio.isPlaying)
            {
                WindAudio.Stop();
            }
        }

        /// <summary>The flame at <paramref name="share"/> of the builder's rate (0 puts it out): every system under FlameEffect, the core with the outer.</summary>
        private void ShowFlame(float share)
        {
            if (FlameEffect == null)
            {
                return;
            }
            if (_flames == null)
            {
                _flames = FlameEffect.GetComponentsInChildren<ParticleSystem>(true);
                _flameRates = new float[_flames.Length];
                for (int i = 0; i < _flames.Length; i++)
                {
                    _flameRates[i] = _flames[i].emission.rateOverTimeMultiplier;
                }
            }
            if (share <= 0.0f)
            {
                if (FlameEffect.isEmitting)
                {
                    FlameEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                return;
            }
            for (int i = 0; i < _flames.Length; i++)
            {
                ParticleSystem.EmissionModule emission = _flames[i].emission;
                emission.rateOverTimeMultiplier = _flameRates[i] * share;
            }
            if (!FlameEffect.isEmitting)
            {
                FlameEffect.Play();
            }
        }
    }
}
